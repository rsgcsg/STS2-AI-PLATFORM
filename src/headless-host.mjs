import { spawnSync } from "node:child_process";
import {
  createWriteStream,
  existsSync,
  mkdirSync,
  readFileSync,
  unlinkSync,
  writeFileSync
} from "node:fs";
import path from "node:path";
import { finished } from "node:stream/promises";
import {
  readJson,
  shippedRuntimeLaunch,
  snapshotIsInteractive,
  stopChild,
  waitForEndpoint,
  waitForInteractiveSnapshot
} from "./runtime-probe.mjs";
import { readDiskIdentity } from "./game-installation.mjs";
import { requireSupportedRuntime } from "./compatibility.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { publicProfileDescriptor, resolveLaunchProfile } from "./profile-isolation.mjs";

const DEFAULT_ENDPOINT = "http://127.0.0.1:15526";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function writeJson(file, value) {
  mkdirSync(path.dirname(file), { recursive: true });
  writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
}

export function readHostRecord(file) {
  if (!existsSync(file)) return null;
  return JSON.parse(readFileSync(file, "utf8"));
}

export function processCommand(pid, platform = process.platform) {
  if (!Number.isSafeInteger(pid) || pid <= 0) return null;
  if (platform === "win32") {
    const script = [
      `$p = Get-CimInstance Win32_Process -Filter \"ProcessId = ${pid}\" -ErrorAction SilentlyContinue`,
      "if ($null -ne $p) { $p.CommandLine }"
    ].join("; ");
    const result = spawnSync(
      "powershell.exe",
      ["-NoProfile", "-NonInteractive", "-Command", script],
      { encoding: "utf8", windowsHide: true }
    );
    return result.status === 0 ? result.stdout.trim() || null : null;
  }
  const result = spawnSync("ps", ["-p", String(pid), "-o", "command="], {
    encoding: "utf8"
  });
  return result.status === 0 ? result.stdout.trim() || null : null;
}

export function commandOwnsHeadlessRuntime(command, executable) {
  if (typeof command !== "string" || typeof executable !== "string") return false;
  const normalizedCommand = command.replaceAll("/", "\\").toLowerCase();
  const normalizedExecutable = executable.replaceAll("/", "\\").toLowerCase();
  return normalizedCommand.includes(normalizedExecutable)
    && /(?:^|\s)--headless(?:\s|$)/iu.test(command);
}

export function evaluateHeadlessCapabilities(capabilities, expectedProtocol = "1.0.0") {
  const errors = [];
  if (capabilities?.protocol_version !== expectedProtocol) errors.push("protocol_mismatch");
  if (capabilities?.host?.host_kind !== "headless") errors.push("host_kind_not_headless");
  if (capabilities?.game?.modset?.status !== "exact_player_environment_only") {
    errors.push("unsupported_modset");
  }
  if (capabilities?.execution_available !== true) errors.push("execution_unavailable");
  return { ok: errors.length === 0, errors };
}

function hostFiles(localRoot) {
  const runtimeRoot = path.join(localRoot, "runtime");
  return {
    runtimeRoot,
    current: path.join(runtimeRoot, "current.json")
  };
}

function childReference(child) {
  return {
    get exitCode() {
      return child.exitCode;
    },
    get signalCode() {
      return child.signalCode;
    }
  };
}

export async function queryHeadlessStatus({ localRoot, endpoint = DEFAULT_ENDPOINT }) {
  const files = hostFiles(localRoot);
  const record = readHostRecord(files.current);
  const command = record?.pid ? processCommand(record.pid) : null;
  const endpointResult = await readJson(endpoint, "/api/player-environment/capabilities", 1000);
  return {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    lifecycle: record,
    process: record?.pid
      ? {
          pid: record.pid,
          running: command != null,
          command_matches_record: commandOwnsHeadlessRuntime(command, record.executable),
          command
        }
      : null,
    endpoint: endpointResult.ok
      ? {
          reachable: true,
          protocol: endpointResult.value.protocol_version,
          host: endpointResult.value.host,
          game: endpointResult.value.game
        }
      : { reachable: false, error: endpointResult.error }
  };
}

export async function stopHeadlessHost({ localRoot, endpoint = DEFAULT_ENDPOINT }) {
  const files = hostFiles(localRoot);
  const record = readHostRecord(files.current);
  if (!record?.pid || !record.executable) {
    return { status: "not_running", detail: "No Headless lifecycle record exists." };
  }
  const command = processCommand(record.pid);
  if (!command) {
    unlinkSync(files.current);
    return { status: "not_running", detail: "The recorded process no longer exists." };
  }
  if (!commandOwnsHeadlessRuntime(command, record.executable)) {
    throw new Error(`Refusing to signal PID ${record.pid}; it is not the recorded Headless runtime.`);
  }
  process.kill(record.pid, "SIGINT");
  const started = Date.now();
  while (Date.now() - started < 10_000 && processCommand(record.pid)) {
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  const stopped = processCommand(record.pid) == null;
  if (stopped && existsSync(files.current)) unlinkSync(files.current);
  const endpointAfter = await readJson(endpoint, "/api/player-environment/capabilities", 1000);
  return {
    status: stopped ? "stopped" : "still_running",
    pid: record.pid,
    endpoint_released: !endpointAfter.ok
  };
}

export async function runHeadlessHost({
  installation,
  localRoot,
  endpoint = DEFAULT_ENDPOINT,
  timeoutMs = 90_000,
  mirrorLogs = false,
  sharedProfileAcknowledged = false,
  isolatedProfileId = null
}) {
  const launchProfile = resolveLaunchProfile({
    localRoot,
    isolatedProfileId,
    sharedProfileAcknowledged
  });
  const files = hostFiles(localRoot);
  const diskIdentity = readDiskIdentity(installation);
  const compatibility = requireSupportedRuntime(diskIdentity);
  const existing = await queryHeadlessStatus({ localRoot, endpoint });
  if (existing.process?.running || existing.endpoint.reachable) {
    throw new Error("A game process or Connector endpoint is already active; inspect it with `npm run status`.");
  }

  const sessionDirectory = path.join(files.runtimeRoot, `session-${safeTimestamp()}`);
  mkdirSync(sessionDirectory, { recursive: true });
  const stdoutFile = path.join(sessionDirectory, "stdout.log");
  const stderrFile = path.join(sessionDirectory, "stderr.log");
  const launch = shippedRuntimeLaunch(installation, { launchProfile });
  const { child, args } = launch;
  const stdoutStream = createWriteStream(stdoutFile);
  const stderrStream = createWriteStream(stderrFile);
  child.stdout.pipe(stdoutStream);
  child.stderr.pipe(stderrStream);
  if (mirrorLogs) {
    child.stdout.pipe(process.stdout);
    child.stderr.pipe(process.stderr);
  }

  let record = {
    schema_version: 1,
    status: "starting",
    started_at: new Date().toISOString(),
    pid: child.pid,
    executable: installation.executable,
    args,
    endpoint,
    profile: publicProfileDescriptor(launchProfile),
    headless: readProjectIdentity(),
    session_directory: sessionDirectory,
    stdout_file: stdoutFile,
    stderr_file: stderrFile,
    disk_identity: diskIdentity,
    compatibility,
    loaded_identity: null
  };
  writeJson(files.current, record);
  writeJson(path.join(sessionDirectory, "lifecycle.json"), record);

  try {
    const capabilitiesResult = await waitForEndpoint(endpoint, timeoutMs, childReference(child));
    if (!capabilitiesResult.ok) {
      throw new Error(`Connector endpoint did not become ready: ${capabilitiesResult.error}`);
    }
    const capabilityGate = evaluateHeadlessCapabilities(capabilitiesResult.value);
    if (!capabilityGate.ok) {
      throw new Error(`Loaded environment is not an admitted Headless runtime: ${capabilityGate.errors.join(", ")}`);
    }
    const snapshots = await waitForInteractiveSnapshot(endpoint, timeoutMs, childReference(child));
    const snapshot = snapshots.at(-1);
    if (!snapshotIsInteractive(snapshot)) {
      throw new Error("The real runtime loaded but did not mount an interactive Player Environment decision.");
    }
    record = {
      ...record,
      status: "ready",
      ready_at: new Date().toISOString(),
      loaded_identity: {
        protocol: capabilitiesResult.value.protocol_version,
        host: capabilitiesResult.value.host,
        game: capabilitiesResult.value.game
      },
      initial_snapshot: {
        snapshot_id: snapshot.value.snapshot_id,
        status: snapshot.value.status,
        interaction_kind: snapshot.value.interaction.kind,
        bound_action_count: snapshot.value.bound_actions.actions.length
      }
    };
    writeJson(files.current, record);
    writeJson(path.join(sessionDirectory, "lifecycle.json"), record);
    console.log(JSON.stringify({ status: "ready", ...record.loaded_identity, initial_snapshot: record.initial_snapshot }, null, 2));

    let stopping = false;
    const forwardSignal = () => {
      if (stopping) return;
      stopping = true;
      child.kill("SIGINT");
    };
    process.once("SIGINT", forwardSignal);
    process.once("SIGTERM", forwardSignal);
    const exit = await new Promise((resolve) => child.once("exit", (code, signal) => resolve({ code, signal })));
    process.off("SIGINT", forwardSignal);
    process.off("SIGTERM", forwardSignal);
    record = { ...record, status: "exited", exited_at: new Date().toISOString(), exit };
    writeJson(path.join(sessionDirectory, "lifecycle.json"), record);
    if (existsSync(files.current) && readHostRecord(files.current)?.pid === child.pid) {
      unlinkSync(files.current);
    }
    return record;
  } catch (error) {
    const exit = await stopChild(child);
    record = {
      ...record,
      status: "failed",
      failed_at: new Date().toISOString(),
      error: error instanceof Error ? error.message : String(error),
      exit
    };
    writeJson(path.join(sessionDirectory, "lifecycle.json"), record);
    if (existsSync(files.current) && readHostRecord(files.current)?.pid === child.pid) {
      unlinkSync(files.current);
    }
    throw error;
  } finally {
    await Promise.allSettled([finished(stdoutStream), finished(stderrStream)]);
  }
}
