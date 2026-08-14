import { spawn, spawnSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { createWriteStream, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { finished } from "node:stream/promises";
import {
  EnvironmentControllerSession,
  PlayerEnvironmentRestClient
} from "@rsgcsg/sts2-connector-client";
import { evaluateMenuControlGate, evaluateShippedProbe } from "./probe-verdict.mjs";
import { readDiskIdentity, STS2_APP_ID } from "./game-installation.mjs";
import { evaluateRuntimeCompatibility } from "./compatibility.mjs";
import { readProjectIdentity } from "./project-identity.mjs";

export function listGameProcesses(platform = process.platform) {
  if (platform === "win32") {
    const result = spawnSync("tasklist", ["/FO", "CSV", "/NH"], { encoding: "utf8" });
    if (result.status !== 0) return [];
    return result.stdout.split("\n")
      .filter((line) => /^"?SlayTheSpire2\.exe"?(?:,|$)/iu.test(line.trim()));
  }
  const result = spawnSync("ps", ["-Ao", "pid=,command="], { encoding: "utf8" });
  if (result.status !== 0) return [];
  return result.stdout.split("\n")
    .map((line) => line.trim())
    .filter((line) => /(?:Slay the Spire 2|SlayTheSpire2)(?:\s|$)/u.test(line))
    .filter((line) => !line.includes("tools/headless.mjs"));
}

export async function readJson(endpoint, route, timeoutMs = 2500) {
  try {
    const response = await fetch(`${endpoint.replace(/\/$/u, "")}${route}`, {
      signal: AbortSignal.timeout(timeoutMs)
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return { ok: true, value: await response.json(), error: null };
  } catch (error) {
    return { ok: false, value: null, error: error instanceof Error ? error.message : String(error) };
  }
}

export async function waitForEndpoint(endpoint, timeoutMs, child) {
  const started = Date.now();
  let last = null;
  while (Date.now() - started < timeoutMs) {
    last = await readJson(endpoint, "/api/player-environment/capabilities");
    if (last.ok) return last;
    if (child.exitCode != null || child.signalCode != null) return last;
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  return last ?? { ok: false, value: null, error: "timeout" };
}

export function snapshotIsInteractive(result) {
  return result.ok
    && result.value?.status === "interactive"
    && result.value?.bound_actions?.status === "complete"
    && result.value?.bound_actions?.actions?.length > 0;
}

export async function waitForInteractiveSnapshot(endpoint, timeoutMs, child) {
  const started = Date.now();
  const observed = [];
  let lastFingerprint = null;
  while (Date.now() - started < timeoutMs) {
    const current = await readJson(endpoint, "/api/player-environment/snapshot");
    const fingerprint = current.ok
      ? `${current.value?.snapshot_id}:${current.value?.status}:${current.value?.interaction?.kind}`
      : `error:${current.error}`;
    if (fingerprint !== lastFingerprint) {
      observed.push(current);
      lastFingerprint = fingerprint;
    }
    if (snapshotIsInteractive(current)) return observed;
    if (child.exitCode != null || child.signalCode != null) return observed;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  return observed;
}

async function waitForSuccessor(client, previousInteractionId, timeoutMs, child) {
  const started = Date.now();
  let latest = null;
  while (Date.now() - started < timeoutMs) {
    latest = (await client.observe()).data;
    if (latest.status === "interactive"
        && latest.interaction.interaction_id !== previousInteractionId) {
      return latest;
    }
    if (child.exitCode != null || child.signalCode != null) return latest;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  return latest;
}

async function exerciseMenuControl(endpoint, child, timeoutMs, productVersion) {
  const client = new PlayerEnvironmentRestClient(endpoint, 5_000);
  const capabilities = (await client.capabilities()).data;
  const initialSnapshot = (await client.observe()).data;
  if (initialSnapshot.interaction.kind !== "main_menu") {
    throw new Error(`Expected main_menu, observed ${initialSnapshot.interaction.kind}.`);
  }
  if (initialSnapshot.bound_actions.status !== "complete"
      || initialSnapshot.bound_actions.actions.length !== 1) {
    throw new Error("The main-menu control gate requires one complete advertised action.");
  }

  const action = initialSnapshot.bound_actions.actions[0];
  const session = new EnvironmentControllerSession(client, {
    productId: "sts2-headless-probe",
    productName: "STS2 Headless Probe",
    productVersion
  });
  await session.register(capabilities.host, capabilities.control);
  try {
    const credentials = await session.credentials();
    const request = {
      requestId: `headless-menu-${randomUUID()}`,
      expectedSnapshotId: initialSnapshot.snapshot_id,
      boundActionId: action.bound_action_id,
      clientSessionId: credentials.clientSessionId,
      controllerLeaseId: credentials.controllerLeaseId,
      controllerGeneration: credentials.controllerGeneration
    };
    const receipt = (await client.submit(request)).data;
    const duplicateReceipt = (await client.submit(request)).data;
    const successorSnapshot = await waitForSuccessor(
      client,
      initialSnapshot.interaction.interaction_id,
      timeoutMs,
      child
    );
    const staleReceipt = (await client.submit({
      ...request,
      requestId: `headless-stale-${randomUUID()}`
    })).data;
    return {
      strict_sdk_protocol: capabilities.protocol_version,
      action,
      initial_snapshot: initialSnapshot,
      receipt,
      duplicate_receipt: duplicateReceipt,
      stale_receipt: staleReceipt,
      successor_snapshot: successorSnapshot,
      verdict: evaluateMenuControlGate({
        initialSnapshot,
        receipt,
        duplicateReceipt,
        staleReceipt,
        successorSnapshot
      })
    };
  } finally {
    await session.close();
  }
}

export async function waitForExit(child, timeoutMs) {
  if (child.exitCode != null || child.signalCode != null) {
    return { code: child.exitCode, signal: child.signalCode };
  }
  return await Promise.race([
    new Promise((resolve) => child.once("exit", (code, signal) => resolve({ code, signal }))),
    new Promise((resolve) => setTimeout(() => resolve(null), timeoutMs))
  ]);
}

export async function stopChild(child) {
  child.kill("SIGINT");
  let exit = await waitForExit(child, 5_000);
  if (exit != null) return exit;
  child.kill("SIGTERM");
  exit = await waitForExit(child, 5_000);
  if (exit != null) return exit;
  child.kill("SIGKILL");
  return await waitForExit(child, 3_000);
}

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

export function shippedRuntimeLaunch(installation, {
  stdout = "pipe",
  stderr = "pipe",
  extraEnvironment = {}
} = {}) {
  const args = ["--headless", "--verbose"];
  const environment = {
    ...process.env,
    SteamAppId: process.env.SteamAppId ?? STS2_APP_ID,
    SteamGameId: process.env.SteamGameId ?? STS2_APP_ID,
    ...extraEnvironment
  };
  const child = spawn(installation.executable, args, {
    cwd: installation.executable_cwd,
    env: environment,
    stdio: ["ignore", stdout, stderr]
  });
  return { child, args, environment };
}

export async function runShippedProbe({
  installation,
  endpoint = "http://127.0.0.1:15526",
  timeoutMs = 90_000,
  evidenceRoot,
  exerciseMenu = false,
  sharedProfileAcknowledged = false,
  experimentalBuildAcknowledged = false
}) {
  if (!sharedProfileAcknowledged) {
    throw new Error(
      "The shipped-runtime probe reaches the active Steam profile during startup. Pass --shared-profile to acknowledge that isolation is not proven."
    );
  }
  const existing = listGameProcesses();
  if (existing.length > 0) {
    throw new Error(`Refusing to launch beside an existing STS2 process:\n${existing.join("\n")}`);
  }
  if (!existsSync(installation.executable)) {
    throw new Error(`Game executable not found: ${installation.executable}`);
  }

  const diskIdentityBefore = readDiskIdentity(installation);
  const compatibility = evaluateRuntimeCompatibility(diskIdentityBefore);
  const headlessIdentity = readProjectIdentity();
  if (compatibility.status !== "supported_exact" && !experimentalBuildAcknowledged) {
    throw new Error(
      `Unsupported STS2 runtime (${compatibility.mismatches.join(", ")}); `
      + "pass --experimental-build only to collect non-support evidence."
    );
  }

  const evidenceDir = path.join(evidenceRoot, `shipped-h0-${safeTimestamp()}`);
  mkdirSync(evidenceDir, { recursive: true });
  const stdoutFile = path.join(evidenceDir, "stdout.log");
  const stderrFile = path.join(evidenceDir, "stderr.log");
  const reportFile = path.join(evidenceDir, "report.json");
  const beforeLog = installation.log_file && existsSync(installation.log_file)
    ? readFileSync(installation.log_file, "utf8")
    : null;
  if (beforeLog != null) writeFileSync(path.join(evidenceDir, "godot.before.log"), beforeLog);

  const endpointBefore = await readJson(endpoint, "/api/player-environment/capabilities", 1000);
  const { child, args } = shippedRuntimeLaunch(installation);
  const stdoutStream = createWriteStream(stdoutFile);
  const stderrStream = createWriteStream(stderrFile);
  child.stdout.pipe(stdoutStream);
  child.stderr.pipe(stderrStream);
  const capabilitiesResult = await waitForEndpoint(endpoint, timeoutMs, child);
  let snapshots = [];
  if (capabilitiesResult.ok) {
    snapshots = await waitForInteractiveSnapshot(endpoint, timeoutMs, child);
  }
  let controlGate = null;
  if (exerciseMenu && snapshotIsInteractive(snapshots.at(-1))) {
    controlGate = await exerciseMenuControl(endpoint, child, timeoutMs, headlessIdentity.version);
  }

  const processExit = await stopChild(child);
  await Promise.allSettled([finished(stdoutStream), finished(stderrStream)]);

  const stdout = existsSync(stdoutFile) ? readFileSync(stdoutFile, "utf8") : "";
  const stderr = existsSync(stderrFile) ? readFileSync(stderrFile, "utf8") : "";
  if (installation.log_file && existsSync(installation.log_file)) {
    writeFileSync(path.join(evidenceDir, "godot.after.log"), readFileSync(installation.log_file, "utf8"));
  }
  const verdict = evaluateShippedProbe({
    endpointWasClear: !endpointBefore.ok,
    processStarted: child.pid != null,
    processExit,
    capabilities: capabilitiesResult.ok ? capabilitiesResult.value : null,
    snapshots,
    stdout,
    stderr
  });
  const report = {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    headless: headlessIdentity,
    command: {
      executable: installation.executable,
      args,
      environment: { SteamAppId: STS2_APP_ID, SteamGameId: STS2_APP_ID }
    },
    disk_identity_before: diskIdentityBefore,
    disk_identity_after: readDiskIdentity(installation),
    compatibility,
    evidence_mode: compatibility.status === "supported_exact" ? "supported" : "experimental",
    endpoint_before: endpointBefore,
    endpoint_after_launch: capabilitiesResult,
    snapshots,
    control_gate: controlGate,
    verdict
  };
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  return { report, evidenceDir, reportFile };
}
