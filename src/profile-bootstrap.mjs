import { createWriteStream, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { finished } from "node:stream/promises";
import { readDiskIdentity } from "./game-installation.mjs";
import { listGameProcesses, shippedRuntimeLaunch, stopChild } from "./runtime-probe.mjs";
import { publicProfileDescriptor, resolveLaunchProfile } from "./profile-isolation.mjs";
import { readProjectIdentity } from "./project-identity.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function settingsFile(profile) {
  return path.join(profile.expected_user_data_root, "default", "1", "settings.save");
}

export function readNativeSettingsSchema(file) {
  if (!existsSync(file)) return null;
  try {
    const value = JSON.parse(readFileSync(file, "utf8"));
    return Number.isSafeInteger(value.schema_version) && value.schema_version > 0
      ? value.schema_version
      : null;
  } catch {
    return null;
  }
}

async function waitForNativeSettings(file, timeoutMs, child) {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const schema = readNativeSettingsSchema(file);
    if (schema != null) return schema;
    if (child.exitCode != null || child.signalCode != null) break;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  return null;
}

export async function bootstrapIsolatedProfile({
  installation,
  localRoot,
  profileId,
  timeoutMs = 60_000,
  evidenceRoot
}) {
  const running = listGameProcesses();
  if (running.length > 0) {
    throw new Error(`Refusing to bootstrap beside an existing STS2 process:\n${running.join("\n")}`);
  }
  const profile = resolveLaunchProfile({
    localRoot,
    isolatedProfileId: profileId,
    sharedProfileAcknowledged: false
  });
  const nativeSettings = settingsFile(profile);
  const existingSchema = readNativeSettingsSchema(nativeSettings);
  if (existingSchema != null) {
    return {
      status: "already_bootstrapped",
      profile: publicProfileDescriptor(profile),
      settings_file: nativeSettings,
      settings_schema: existingSchema,
      loaded_connector: "non_claim"
    };
  }

  const evidenceDirectory = path.join(evidenceRoot, `profile-bootstrap-${safeTimestamp()}`);
  mkdirSync(evidenceDirectory, { recursive: true });
  const stdoutFile = path.join(evidenceDirectory, "stdout.log");
  const stderrFile = path.join(evidenceDirectory, "stderr.log");
  const reportFile = path.join(evidenceDirectory, "report.json");
  const { child, args } = shippedRuntimeLaunch(installation, { launchProfile: profile });
  const stdout = createWriteStream(stdoutFile);
  const stderr = createWriteStream(stderrFile);
  let stdoutTail = "";
  child.stdout.on("data", (chunk) => {
    stdoutTail = (stdoutTail + chunk.toString()).slice(-256_000);
  });
  child.stdout.pipe(stdout);
  child.stderr.pipe(stderr);

  let schema = null;
  let exit = null;
  try {
    schema = await waitForNativeSettings(nativeSettings, timeoutMs, child);
    if (schema == null) {
      throw new Error("The shipped runtime did not create a valid native settings.save before timeout.");
    }
    await new Promise((resolve) => setTimeout(resolve, 750));
  } finally {
    exit = await stopChild(child);
    stdout.end();
    stderr.end();
    await Promise.allSettled([finished(stdout), finished(stderr)]);
  }

  const profilePathLogged = stdoutTail.includes(profile.expected_user_data_root.replaceAll("\\", "/"))
    || stdoutTail.includes(profile.expected_user_data_root);
  const steamDisabledObserved = /Steam not initialized|Skipping Steam initialization/iu.test(stdoutTail);
  const report = {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    status: schema != null && steamDisabledObserved
      ? "native_profile_bootstrap_pass"
      : "native_profile_bootstrap_incomplete",
    headless: readProjectIdentity(),
    route: "shipped_godot_headless_profile_bootstrap",
    command: { executable: installation.executable, args },
    profile: publicProfileDescriptor(profile),
    disk_identity: readDiskIdentity(installation),
    settings_file: nativeSettings,
    settings_schema: schema,
    runtime_checks: {
      expected_profile_settings_created: schema != null,
      expected_profile_path_logged: profilePathLogged,
      steam_disabled_observed: steamDisabledObserved,
      process_exit: exit
    },
    loaded_connector: "non_claim"
  };
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  return { ...report, report_file: reportFile };
}
