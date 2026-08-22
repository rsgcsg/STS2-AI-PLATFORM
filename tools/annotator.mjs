import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { spawn, spawnSync } from "node:child_process";

const root = path.resolve(import.meta.dirname, "..");
const local = path.join(root, ".local");
const connectorRoot = path.resolve(root, "..", "STS2-Connector");
const connectorArtifact = path.join(connectorRoot, "host", "out", "STS2_MCP", "STS2_MCP.dll");
const modOutput = path.join(root, "src", "STS2HumanAnnotator.Mod", "bin", "Release", "net9.0");
const toolDll = path.join(root, "src", "STS2HumanAnnotator.Tool", "bin", "Release", "net9.0", "sts2-human-annotator.dll");
const defaultMacGame = path.join(os.homedir(), "Library", "Application Support", "Steam", "steamapps", "common", "Slay the Spire 2");
const gameDir = path.resolve(process.env.STS2_GAME_DIR || defaultMacGame);
const dataDir = process.platform === "darwin"
  ? path.join(gameDir, "SlayTheSpire2.app", "Contents", "Resources", "data_sts2_macos_arm64")
  : process.platform === "win32"
    ? path.join(gameDir, "data_sts2_windows_x86_64")
    : path.join(gameDir, "data_sts2_linuxbsd_x86_64");
const modsDir = process.platform === "darwin"
  ? path.join(gameDir, "SlayTheSpire2.app", "Contents", "MacOS", "mods")
  : path.join(gameDir, "mods");
const runtimeStatus = path.join(local, "runtime-status.json");
const canaryPath = path.join(local, "exact-modset-canary.json");
const provenancePath = path.join(local, "build-provenance.json");
const manifestSource = path.join(root, "src", "STS2HumanAnnotator.Mod", "mod_manifest.json");

const command = process.argv[2] || "doctor";
const args = process.argv.slice(3);

function run(executable, commandArgs, options = {}) {
  const result = spawnSync(executable, commandArgs, {
    cwd: options.cwd || root,
    env: options.env || process.env,
    encoding: "utf8",
    stdio: options.capture ? "pipe" : "inherit"
  });
  if (result.status !== 0) {
    if (options.capture) process.stderr.write(result.stderr || result.stdout || "");
    throw new Error(`${executable} exited with ${result.status}`);
  }
  return options.capture ? result.stdout : "";
}

function git(...gitArgs) {
  return run("git", gitArgs, { cwd: root, capture: true }).trim();
}

function sha256(file) {
  return crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
}

function sourceDigest() {
  const files = git("ls-files").split("\n").filter(Boolean).sort();
  const hash = crypto.createHash("sha256");
  for (const relative of files) {
    hash.update(relative);
    hash.update("\0");
    hash.update(fs.readFileSync(path.join(root, relative)));
    hash.update("\0");
  }
  return hash.digest("hex");
}

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8"));
}

function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temporary = `${file}.tmp-${crypto.randomUUID()}`;
  fs.writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`);
  fs.renameSync(temporary, file);
}

function exactIdentity(file) {
  const output = run("dotnet", [toolDll, "identity", file], { capture: true });
  return JSON.parse(output);
}

function gameRunning() {
  if (process.platform === "win32") return false;
  const result = spawnSync("pgrep", ["-f", "SlayTheSpire2"], { encoding: "utf8" });
  return result.status === 0;
}

function processAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

function sourceState() {
  return {
    branch: git("branch", "--show-current"),
    head: git("rev-parse", "HEAD"),
    worktree: git("status", "--porcelain").length === 0 ? "clean" : "dirty"
  };
}

function build() {
  const source = sourceState();
  run("dotnet", [
    "build", "STS2HumanAnnotator.sln", "-c", "Release",
    `-p:STS2GameDir=${gameDir}`,
    `-p:ConnectorAssembly=${connectorArtifact}`,
    `-p:SourceRevision=${source.head}`,
    `-p:AnnotatorSourceDigest=${sourceDigest()}`
  ]);
  const artifact = exactIdentity(path.join(modOutput, "STS2_HUMAN_ANNOTATOR.dll"));
  const connector = exactIdentity(connectorArtifact);
  writeJson(provenancePath, {
    schema_version: 1,
    built_at: new Date().toISOString(),
    source_revision: source.head,
    source_digest_sha256: sourceDigest(),
    source_worktree: source.worktree,
    artifact,
    connector_artifact: connector,
    game: {
      directory: gameDir,
      sts2_sha256: sha256(path.join(dataDir, "sts2.dll")),
      sts2_identity: exactIdentity(path.join(dataDir, "sts2.dll"))
    }
  });
  console.log(JSON.stringify(readJson(provenancePath), null, 2));
}

function test() {
  run("dotnet", ["test", "tests/STS2HumanAnnotator.Core.Tests/STS2HumanAnnotator.Core.Tests.csproj", "-c", "Release"]);
  run("node", [path.join(root, "tools", "check-boundary.mjs")]);
  run("node", [path.join(root, "tools", "check-docs.mjs")]);
}

function doctor() {
  const built = path.join(modOutput, "STS2_HUMAN_ANNOTATOR.dll");
  const installed = path.join(modsDir, "STS2_HUMAN_ANNOTATOR.dll");
  const status = fs.existsSync(runtimeStatus) ? readJson(runtimeStatus) : null;
  const report = {
    status: fs.existsSync(gameDir) && fs.existsSync(connectorRoot) ? "ok" : "action_required",
    repository: sourceState(),
    game_dir: gameDir,
    game_exists: fs.existsSync(gameDir),
    game_running: gameRunning(),
    connector_repository: connectorRoot,
    connector_exists: fs.existsSync(connectorRoot),
    built: fs.existsSync(built) ? exactIdentity(built) : null,
    installed: fs.existsSync(installed) ? exactIdentity(installed) : null,
    runtime_status: status,
    exact_modset_canary: fs.existsSync(canaryPath) ? readJson(canaryPath) : null,
    non_claims: ["doctor_is_read_only", "installed_is_not_loaded", "loaded_is_not_human_action_evidence"]
  };
  console.log(JSON.stringify(report, null, 2));
}

function deploy() {
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before deployment.");
  const allowDirty = args.includes("--allow-dirty");
  const source = sourceState();
  if (source.worktree !== "clean" && !allowDirty)
    throw new Error("Refusing to deploy a dirty source; commit first or use --allow-dirty for local development only.");
  if (!fs.existsSync(provenancePath)) throw new Error("Run npm run build first.");
  const provenance = readJson(provenancePath);
  if (provenance.source_revision !== source.head)
    throw new Error("Build provenance does not match current HEAD.");
  const builtArtifact = exactIdentity(path.join(modOutput, "STS2_HUMAN_ANNOTATOR.dll"));
  const currentConnector = exactIdentity(connectorArtifact);
  if (builtArtifact.sha256 !== provenance.artifact.sha256
      || builtArtifact.module_version_id !== provenance.artifact.module_version_id)
    throw new Error("Built Annotator artifact no longer matches build provenance.");
  if (currentConnector.sha256 !== provenance.connector_artifact.sha256
      || currentConnector.module_version_id !== provenance.connector_artifact.module_version_id)
    throw new Error("Connector artifact no longer matches Annotator build provenance.");
  const installedConnectorPath = path.join(modsDir, "STS2_MCP.dll");
  if (!fs.existsSync(installedConnectorPath))
    throw new Error("Deploy the exact Connector before deploying the Annotator.");
  const installedConnector = exactIdentity(installedConnectorPath);
  if (installedConnector.sha256 !== currentConnector.sha256
      || installedConnector.module_version_id !== currentConnector.module_version_id)
    throw new Error("Installed Connector does not match the Annotator build dependency.");
  fs.mkdirSync(modsDir, { recursive: true });
  const timestamp = new Date().toISOString().replaceAll(":", "-");
  const backup = path.join(local, "deployments", timestamp);
  fs.mkdirSync(backup, { recursive: true });
  const names = [
    "STS2_HUMAN_ANNOTATOR.dll",
    "STS2HumanAnnotator.Core.dll",
    "STS2_HUMAN_ANNOTATOR.json",
    "STS2_HUMAN_ANNOTATOR.conf"
  ];
  const backupState = [];
  for (const name of names) {
    const installed = path.join(modsDir, name);
    const existed = fs.existsSync(installed);
    backupState.push({ name, existed });
    if (existed) fs.copyFileSync(installed, path.join(backup, name));
  }
  writeJson(path.join(backup, "rollback-manifest.json"), { schema_version: 1, files: backupState });
  fs.copyFileSync(path.join(modOutput, "STS2_HUMAN_ANNOTATOR.dll"), path.join(modsDir, "STS2_HUMAN_ANNOTATOR.dll"));
  fs.copyFileSync(path.join(modOutput, "STS2HumanAnnotator.Core.dll"), path.join(modsDir, "STS2HumanAnnotator.Core.dll"));
  fs.copyFileSync(manifestSource, path.join(modsDir, "STS2_HUMAN_ANNOTATOR.json"));
  writeJson(path.join(modsDir, "STS2_HUMAN_ANNOTATOR.conf"), {
    recording_root: path.join(local, "recordings"),
    runtime_status_path: runtimeStatus,
    successor_timeout_ms: 20000
  });
  writeJson(path.join(local, "installed-provenance.json"), {
    ...provenance,
    installed_at: new Date().toISOString(),
    installed_artifact: exactIdentity(path.join(modsDir, "STS2_HUMAN_ANNOTATOR.dll")),
    rollback: backup
  });
  console.log(JSON.stringify(readJson(path.join(local, "installed-provenance.json")), null, 2));
}

function admitCurrentModset() {
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before admitting the observed fingerprint.");
  if (!fs.existsSync(runtimeStatus)) throw new Error("No runtime status. Cold-load once without canary first.");
  const status = readJson(runtimeStatus);
  if (!status.environment?.modset_fingerprint) throw new Error("Runtime status lacks a Modset fingerprint.");
  const loadedAnnotator = status.environment?.annotator;
  const loadedConnector = status.environment?.connector;
  const installedAnnotator = exactIdentity(path.join(modsDir, "STS2_HUMAN_ANNOTATOR.dll"));
  const installedConnector = exactIdentity(path.join(modsDir, "STS2_MCP.dll"));
  if (loadedAnnotator?.sha256 !== installedAnnotator.sha256
      || loadedAnnotator?.module_version_id !== installedAnnotator.module_version_id
      || loadedConnector?.sha256 !== installedConnector.sha256
      || loadedConnector?.module_version_id !== installedConnector.module_version_id)
    throw new Error("The observed Modset fingerprint is not from the currently installed artifacts.");
  const connectorSource = status.environment.connector.source_revision;
  if (!/^[0-9a-f]{40}$/.test(connectorSource)) throw new Error("Connector source revision is not exact.");
  writeJson(canaryPath, {
    admitted_at: new Date().toISOString(),
    modset_fingerprint: status.environment.modset_fingerprint,
    connector_source_revision: connectorSource,
    observed_runtime_instance_id: status.environment.runtime_instance_id,
    note: "Process-local canary input only; this is not qualification or human validation."
  });
  console.log(JSON.stringify(readJson(canaryPath), null, 2));
}

function launch() {
  if (gameRunning()) throw new Error("Slay the Spire 2 is already running; cold-load requires a fully closed process.");
  if (process.platform !== "darwin") throw new Error("Automated launch is currently implemented only for macOS.");
  const executable = path.join(gameDir, "SlayTheSpire2.app", "Contents", "MacOS", "SlayTheSpire2");
  const env = { ...process.env };
  if (fs.existsSync(canaryPath)) {
    const canary = readJson(canaryPath);
    env.STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION = canary.connector_source_revision;
    env.STS2_CONNECTOR_EXPERIMENTAL_MODSET_FINGERPRINT = canary.modset_fingerprint;
  }
  const child = spawn(executable, [], { cwd: path.dirname(executable), env, detached: true, stdio: "ignore" });
  child.unref();
  console.log(JSON.stringify({ status: "launched", pid: child.pid, canary_applied: fs.existsSync(canaryPath) }, null, 2));
}

function verifyLoaded() {
  if (!fs.existsSync(runtimeStatus)) throw new Error("Runtime status is absent.");
  const status = readJson(runtimeStatus);
  const provenance = readJson(path.join(local, "installed-provenance.json"));
  const ageMs = Date.now() - Date.parse(status.observed_at);
  const installedAnnotator = exactIdentity(path.join(modsDir, "STS2_HUMAN_ANNOTATOR.dll"));
  const installedConnector = exactIdentity(path.join(modsDir, "STS2_MCP.dll"));
  const errors = [];
  if (!gameRunning() || !processAlive(status.process_id)) errors.push("runtime_process_not_running");
  if (ageMs > 5000) errors.push("runtime_status_not_fresh");
  if (status.process_id <= 0) errors.push("runtime_process_id_missing");
  if (status.environment?.annotator?.sha256 !== installedAnnotator.sha256) errors.push("annotator_loaded_installed_sha_mismatch");
  if (status.environment?.annotator?.module_version_id !== installedAnnotator.module_version_id) errors.push("annotator_loaded_installed_mvid_mismatch");
  if (status.environment?.connector?.sha256 !== installedConnector.sha256) errors.push("connector_loaded_installed_sha_mismatch");
  if (status.environment?.connector?.module_version_id !== installedConnector.module_version_id) errors.push("connector_loaded_installed_mvid_mismatch");
  if (status.environment?.annotator?.source_revision !== provenance.source_revision) errors.push("annotator_loaded_source_revision_mismatch");
  if (status.environment?.annotator?.source_digest_sha256 !== provenance.source_digest_sha256) errors.push("annotator_loaded_source_digest_mismatch");
  if (status.environment?.game?.main_assembly_sha256 !== provenance.game.sts2_sha256) errors.push("loaded_game_sha_mismatch");
  if (status.environment?.game?.main_assembly_module_version_id !== provenance.game.sts2_identity.module_version_id) errors.push("loaded_game_mvid_mismatch");
  if (status.environment?.modset_status !== "canary_exact_observer_modset") errors.push("exact_observer_modset_canary_not_active");
  console.log(JSON.stringify({ status: errors.length ? "fail" : "pass", errors, runtime: status, installed_annotator: installedAnnotator, installed_connector: installedConnector }, null, 2));
  if (errors.length) process.exitCode = 1;
}

function audit() {
  const directory = args[0] || readJson(runtimeStatus).recording_directory;
  run("dotnet", [toolDll, "audit", directory]);
}

function exportRecords() {
  const directory = args[0] || readJson(runtimeStatus).recording_directory;
  const output = args[1] || path.join(local, "exports", `${path.basename(directory)}.jsonl`);
  run("dotnet", [toolDll, "export", directory, output]);
}

function rollback() {
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before rollback.");
  const installation = readJson(path.join(local, "installed-provenance.json"));
  const backup = installation.rollback;
  if (!backup || !fs.existsSync(backup)) throw new Error("Rollback snapshot is unavailable.");
  const manifest = readJson(path.join(backup, "rollback-manifest.json"));
  for (const entry of manifest.files) {
    const installed = path.join(modsDir, entry.name);
    if (entry.existed) fs.copyFileSync(path.join(backup, entry.name), installed);
    else fs.rmSync(installed, { force: true });
  }
  console.log(JSON.stringify({ status: "restored", rollback: backup }, null, 2));
}

function check() {
  test();
  run("dotnet", ["build", "STS2HumanAnnotator.sln", "-c", "Release", `-p:STS2GameDir=${gameDir}`, `-p:ConnectorAssembly=${connectorArtifact}`]);
}

try {
  if (command === "doctor") doctor();
  else if (command === "build") build();
  else if (command === "test") test();
  else if (command === "check") check();
  else if (command === "deploy") deploy();
  else if (command === "admit-current-modset") admitCurrentModset();
  else if (command === "launch") launch();
  else if (command === "verify-loaded") verifyLoaded();
  else if (command === "audit") audit();
  else if (command === "export") exportRecords();
  else if (command === "rollback") rollback();
  else throw new Error(`Unknown command: ${command}`);
} catch (error) {
  console.error(JSON.stringify({ status: "error", command, error: error.message }, null, 2));
  process.exitCode = 1;
}
