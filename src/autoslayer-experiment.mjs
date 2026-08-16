import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import {
  copyFileSync,
  createWriteStream,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  statSync,
  writeFileSync
} from "node:fs";
import path from "node:path";
import { finished } from "node:stream/promises";
import { performance } from "node:perf_hooks";
import { evaluateRuntimeCompatibility } from "./compatibility.mjs";
import {
  compareFilesystemSnapshots,
  sharedGameUserDataRoot,
  snapshotFilesystemTree
} from "./filesystem-sentinel.mjs";
import { readDiskIdentity, sha256File } from "./game-installation.mjs";
import { ProcessResourceSampler } from "./process-resource-sampler.mjs";
import { isolatedProfileLaunch } from "./profile-isolation.mjs";
import { instantiateProfileTemplate } from "./profile-template.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { listGameProcesses, shippedRuntimeLaunch, stopChild } from "./runtime-probe.mjs";
import { readSystemIdentity } from "./system-identity.mjs";

const EXPECTED_STS2_SHA256 =
  "0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9";
const CONNECTOR_FILES = Object.freeze(["STS2_MCP.conf", "STS2_MCP.dll", "STS2_MCP.json"]);
const PROBE_ID = "STS2_AutoSlayerUpperBound";
const PROBE_FILES = Object.freeze([`${PROBE_ID}.dll`, `${PROBE_ID}.json`]);
const SEED_PATTERN = /^[A-Z0-9]{1,32}$/u;

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function fileSha(file) {
  return createHash("sha256").update(readFileSync(file)).digest("hex");
}

export function modDirectoryInventory(modsDirectory) {
  if (!existsSync(modsDirectory)) return [];
  return readdirSync(modsDirectory, { withFileTypes: true })
    .map((entry) => {
      if (!entry.isFile()) throw new Error(`Experimental Modset refuses non-file entry: ${entry.name}`);
      const file = path.join(modsDirectory, entry.name);
      return { name: entry.name, size: statSync(file).size, sha256: fileSha(file) };
    })
    .sort((left, right) => left.name.localeCompare(right.name));
}

export function requireConnectorOnlyModset(inventory) {
  const actual = inventory.map((entry) => entry.name).sort();
  const expected = [...CONNECTOR_FILES].sort();
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error(
      `AutoSlayer experiment requires exact Connector-only disk Modset; found: ${actual.join(", ")}`
    );
  }
  return inventory;
}

export function parseAutoSlayerLog(content) {
  const lines = String(content).split(/\r?\n/u).filter(Boolean);
  const rooms = lines.flatMap((line) => {
    const match = line.match(/\[AutoSlay\] Entering (\S+) room \(Act (\d+), Floor (\d+)\)/u);
    return match == null ? [] : [{ type: match[1], act: Number(match[2]), floor: Number(match[3]) }];
  });
  const actions = lines.filter((line) => /\[AutoSlay\] Action:/u.test(line));
  const errors = lines.filter((line) => /\[(?:ERROR|WARN)\]/u.test(line));
  return {
    started: lines.some((line) => /\[AutoSlay\] Starting run with seed=/u.test(line)),
    completed: lines.some((line) => /\[AutoSlay\] Run completed successfully with seed=/u.test(line)),
    failed: lines.some((line) => /\[AutoSlay\] Run failed with seed=/u.test(line)),
    room_entries: rooms.length,
    max_act_floor_observed: rooms.length === 0 ? null : Math.max(...rooms.map((room) => room.floor)),
    act_count_observed: new Set(rooms.map((room) => room.act)).size,
    native_action_log_entries: actions.length,
    warning_or_error_log_entries: errors.length,
    room_type_counts: Object.fromEntries(
      [...new Set(rooms.map((room) => room.type))].sort().map((type) => [
        type,
        rooms.filter((room) => room.type === type).length
      ])
    )
  };
}

export function evaluateAutoSlayerUpperBound({
  processExit,
  parsedLog,
  timedOut,
  rollbackVerified,
  sharedProfileUnchanged
}) {
  const errors = [];
  if (timedOut) errors.push("experiment_timeout");
  if (processExit?.code !== 0) errors.push("nonzero_process_exit");
  if (parsedLog?.started !== true) errors.push("autoslayer_not_started");
  if (parsedLog?.completed !== true || parsedLog?.failed === true) {
    errors.push("autoslayer_did_not_complete");
  }
  if (!(parsedLog?.room_entries > 0)) errors.push("no_room_progress_observed");
  if (!rollbackVerified) errors.push("modset_rollback_not_verified");
  if (!sharedProfileUnchanged) errors.push("shared_profile_changed");
  return {
    verdict: errors.length === 0 ? "autoslayer_upper_bound_pass" : "autoslayer_upper_bound_failed",
    errors,
    normalized_semantic_decisions: null,
    qualification: "not_qualified",
    route_role: "exact_build_official_automation_upper_bound_only"
  };
}

function buildProbe({ installation, projectRoot, sourceRevision }) {
  const project = path.join(
    projectRoot,
    "experiments",
    "autoslayer-upper-bound",
    "STS2.AutoSlayerUpperBound.csproj"
  );
  const result = spawnSync("dotnet", [
    "build",
    project,
    "--configuration",
    "Release",
    `-p:STS2GameDir=${installation.game_dir}`,
    `-p:SourceRevision=${sourceRevision ?? "unavailable"}`
  ], { encoding: "utf8", windowsHide: true, maxBuffer: 10 * 1024 * 1024 });
  if (result.status !== 0) {
    throw new Error(`AutoSlayer probe build failed:\n${result.stdout}\n${result.stderr}`);
  }
  const output = path.join(
    projectRoot,
    "experiments",
    "autoslayer-upper-bound",
    "bin",
    "Release",
    "net9.0",
    `${PROBE_ID}.dll`
  );
  if (!existsSync(output)) throw new Error(`AutoSlayer probe artifact missing: ${output}`);
  return {
    dll: output,
    manifest: path.join(projectRoot, "experiments", "autoslayer-upper-bound", "mod_manifest.json"),
    dll_sha256: sha256File(output),
    build_stdout: result.stdout.trim()
  };
}

function installExperimentalModset({ installation, localRoot, artifact }) {
  const modsDirectory = path.join(installation.game_dir, "mods");
  const before = requireConnectorOnlyModset(modDirectoryInventory(modsDirectory));
  const backup = path.join(localRoot, "research", `autoslayer-modset-backup-${safeTimestamp()}`);
  mkdirSync(backup, { recursive: true });
  for (const entry of before) copyFileSync(path.join(modsDirectory, entry.name), path.join(backup, entry.name));
  try {
    for (const entry of before) rmSync(path.join(modsDirectory, entry.name));
    copyFileSync(artifact.dll, path.join(modsDirectory, `${PROBE_ID}.dll`));
    copyFileSync(artifact.manifest, path.join(modsDirectory, `${PROBE_ID}.json`));
    const installed = modDirectoryInventory(modsDirectory);
    if (JSON.stringify(installed.map((entry) => entry.name)) !== JSON.stringify([...PROBE_FILES].sort())) {
      throw new Error("Experimental AutoSlayer Modset installation was not exact.");
    }
    return { modsDirectory, backup, before, installed };
  } catch (error) {
    for (const entry of modDirectoryInventory(modsDirectory)) {
      rmSync(path.join(modsDirectory, entry.name));
    }
    for (const entry of before) {
      copyFileSync(path.join(backup, entry.name), path.join(modsDirectory, entry.name));
    }
    throw error;
  }
}

function restoreModset(transaction) {
  for (const entry of modDirectoryInventory(transaction.modsDirectory)) {
    rmSync(path.join(transaction.modsDirectory, entry.name));
  }
  for (const entry of transaction.before) {
    copyFileSync(path.join(transaction.backup, entry.name), path.join(transaction.modsDirectory, entry.name));
  }
  const after = modDirectoryInventory(transaction.modsDirectory);
  return {
    verified: JSON.stringify(after) === JSON.stringify(transaction.before),
    before: transaction.before,
    experimental: transaction.installed,
    after,
    backup: transaction.backup
  };
}

function waitForExit(child, timeoutMs) {
  return new Promise((resolve) => {
    if (child.exitCode != null || child.signalCode != null) {
      resolve({ code: child.exitCode, signal: child.signalCode, timed_out: false });
      return;
    }
    const timer = setTimeout(() => {
      cleanup();
      resolve({ code: child.exitCode, signal: child.signalCode, timed_out: true });
    }, timeoutMs);
    const onExit = (code, signal) => {
      cleanup();
      resolve({ code, signal, timed_out: false });
    };
    const onError = () => {
      cleanup();
      resolve({ code: child.exitCode, signal: child.signalCode, timed_out: false });
    };
    function cleanup() {
      clearTimeout(timer);
      child.off("exit", onExit);
      child.off("error", onError);
    }
    child.once("exit", onExit);
    child.once("error", onError);
  });
}

function summarizeResources(samples, elapsedSeconds) {
  if (samples.length === 0) return { samples: 0, elapsed_seconds: elapsedSeconds };
  const cpuSeconds = samples.length < 2
    ? null
    : Math.max(0, samples.at(-1).cpu_seconds_total - samples[0].cpu_seconds_total);
  return {
    samples: samples.length,
    elapsed_seconds: elapsedSeconds,
    cpu_seconds: cpuSeconds,
    average_cores: cpuSeconds == null || elapsedSeconds <= 0 ? null : cpuSeconds / elapsedSeconds,
    peak_rss_bytes: Math.max(...samples.map((sample) => sample.rss_bytes)),
    peak_private_bytes: samples.some((sample) => sample.private_bytes != null)
      ? Math.max(...samples.flatMap((sample) => sample.private_bytes == null ? [] : [sample.private_bytes]))
      : null
  };
}

export async function runAutoSlayerUpperBound({
  installation,
  projectRoot,
  localRoot,
  evidenceRoot,
  templateId = "vanilla-clean",
  profileId = "autoslayer-upper-bound",
  seed = "H1AUTOSLAYER01",
  timeoutMs = 30 * 60 * 1000,
  experimentalBuildAcknowledged = false
}) {
  if (!SEED_PATTERN.test(seed)) throw new Error("AutoSlayer seed must be 1-32 uppercase letters or digits.");
  if (!Number.isSafeInteger(timeoutMs) || timeoutMs < 60_000) {
    throw new Error("AutoSlayer timeout must be an integer of at least 60000ms.");
  }
  const running = listGameProcesses();
  if (running.length > 0) throw new Error(`Refusing to run beside STS2:\n${running.join("\n")}`);

  const diskIdentity = readDiskIdentity(installation);
  const compatibility = evaluateRuntimeCompatibility(diskIdentity);
  if (diskIdentity?.sts2_assembly?.sha256 !== EXPECTED_STS2_SHA256) {
    throw new Error("AutoSlayer upper-bound source contract is not valid for this sts2.dll.");
  }
  if (compatibility.status !== "supported_exact" && !experimentalBuildAcknowledged) {
    throw new Error("Pass --experimental-build to collect non-support AutoSlayer evidence.");
  }

  const headless = readProjectIdentity(projectRoot);
  const evidenceDirectory = path.join(evidenceRoot, `autoslayer-upper-bound-${safeTimestamp()}`);
  mkdirSync(evidenceDirectory, { recursive: true });
  const stdoutFile = path.join(evidenceDirectory, "stdout.log");
  const stderrFile = path.join(evidenceDirectory, "stderr.log");
  const autoSlayerLogFile = path.join(evidenceDirectory, "autoslayer.log");
  const reportFile = path.join(evidenceDirectory, "report.json");
  const artifact = buildProbe({ installation, projectRoot, sourceRevision: headless.source_revision });
  const sharedRoot = sharedGameUserDataRoot();
  const sharedBefore = snapshotFilesystemTree(sharedRoot);
  const profile = instantiateProfileTemplate({
    localRoot,
    templateId,
    profileId,
    expectedGameIdentity: diskIdentity
  });
  const launchProfile = isolatedProfileLaunch(localRoot, profileId);
  let transaction = null;
  let child = null;
  let sampler = null;
  let samples = [];
  let sampleErrors = [];
  let processExit = null;
  let elapsedSeconds = null;
  let launchArgs = null;
  let runError = null;
  let rollback = { verified: false };

  try {
    transaction = installExperimentalModset({ installation, localRoot, artifact });
    const launch = shippedRuntimeLaunch(installation, {
      launchProfile,
      extraEnvironment: {
        STS2_HEADLESS_AUTOSLAYER_EXPERIMENT: "1",
        STS2_HEADLESS_AUTOSLAYER_SEED: seed,
        STS2_HEADLESS_AUTOSLAYER_LOG: autoSlayerLogFile
      }
    });
    child = launch.child;
    launchArgs = launch.args;
    const stdout = createWriteStream(stdoutFile);
    const stderr = createWriteStream(stderrFile);
    child.stdout.pipe(stdout);
    child.stderr.pipe(stderr);
    sampler = new ProcessResourceSampler(child.pid, { intervalMs: 500 });
    const started = performance.now();
    await sampler.start();
    processExit = await waitForExit(child, timeoutMs);
    if (processExit.timed_out) {
      const forced = await stopChild(child);
      processExit = { ...processExit, forced_cleanup: forced };
    }
    elapsedSeconds = (performance.now() - started) / 1000;
    ({ samples, errors: sampleErrors } = await sampler.stop());
    sampler = null;
    stdout.end();
    stderr.end();
    await Promise.all([finished(stdout), finished(stderr)]);
  } catch (error) {
    runError = error instanceof Error ? error.message : String(error);
    if (child != null && child.exitCode == null && child.signalCode == null) {
      processExit = { ...(processExit ?? {}), forced_cleanup: await stopChild(child) };
    }
    if (sampler != null) {
      ({ samples, errors: sampleErrors } = await sampler.stop());
      sampler = null;
    }
  } finally {
    if (transaction != null) rollback = restoreModset(transaction);
  }

  const sharedProfile = compareFilesystemSnapshots(sharedBefore, snapshotFilesystemTree(sharedRoot));
  const parsedLog = parseAutoSlayerLog(
    existsSync(autoSlayerLogFile) ? readFileSync(autoSlayerLogFile, "utf8") : ""
  );
  const verdict = evaluateAutoSlayerUpperBound({
    processExit,
    parsedLog,
    timedOut: processExit?.timed_out === true,
    rollbackVerified: rollback.verified,
    sharedProfileUnchanged: sharedProfile.unchanged
  });
  if (runError != null) verdict.errors.push(`runtime_error:${runError}`);
  if (verdict.errors.length > 0) verdict.verdict = "autoslayer_upper_bound_failed";
  const report = {
    schema_version: 1,
    experiment: "official_autoslayer_exact_build_upper_bound",
    generated_at: new Date().toISOString(),
    status: verdict.verdict,
    headless,
    system: readSystemIdentity(),
    disk_identity: diskIdentity,
    compatibility,
    experiment_artifact: {
      source_contract_sts2_sha256: EXPECTED_STS2_SHA256,
      dll_sha256: artifact.dll_sha256,
      manifest: JSON.parse(readFileSync(artifact.manifest, "utf8"))
    },
    profile,
    seed,
    launch: { args: launchArgs, steam: launchProfile.steam },
    process_exit: processExit,
    elapsed_seconds: elapsedSeconds,
    autoslayer: parsedLog,
    resources: { ...summarizeResources(samples, elapsedSeconds ?? 0), sample_errors: sampleErrors },
    modset_transaction: rollback,
    shared_profile_sentinel: sharedProfile,
    verdict,
    non_claims: [
      "AutoSlayer native action and room logs are not normalized semantic decisions.",
      "This patched-Modset experiment is not a Connector Host or gameplay contract.",
      "A successful run does not grant Reference, Host, Connector, Windows, or training qualification.",
      "The official fixed AutoSlayer policy is not an RL or Agent policy.",
      "An unchanged local profile tree does not prove Steam Cloud server isolation."
    ]
  };
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  return { report, reportFile };
}
