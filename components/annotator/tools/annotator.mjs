import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { spawn, spawnSync } from "node:child_process";
import {
  commandMatchesExecutable,
  loadHeadlessWorkstationApi,
  normalizeExactModsetCanary,
  normalizeInstalledProvenance,
  prepareExactWindowsModSettings,
  resolveConnectorCanaryEnvironment,
  resolveWorkstationInstallation
} from "./workstation-platform.mjs";

const root = path.resolve(import.meta.dirname, "..");
const local = path.join(root, ".local");
const connectorRoot = path.resolve(root, "..", "STS2-Connector");
const connectorArtifact = path.join(connectorRoot, "host", "out", "STS2_MCP", "STS2_MCP.dll");
const connectorBuildIdentity = path.join(
  connectorRoot,
  "host",
  "out",
  "STS2_MCP",
  "build-identity.json"
);
const connectorCompatibility = path.join(connectorRoot, "contracts", "host-compatibility.json");
const modOutput = path.join(root, "src", "STS2HumanAnnotator.Mod", "bin", "Release", "net9.0");
const toolDll = path.join(root, "src", "STS2HumanAnnotator.Tool", "bin", "Release", "net9.0", "sts2-human-annotator.dll");
const headlessApi = await loadHeadlessWorkstationApi(root);
const installation = resolveWorkstationInstallation({ headlessApi });
const gameDir = installation.game_dir;
const dataDir = installation.data_dir;
const modsDir = installation.mods_dir;
const runtimeStatus = path.join(local, "runtime-status.json");
const canaryPath = path.join(local, "exact-modset-canary.json");
const provenancePath = path.join(local, "build-provenance.json");
const manifestSource = path.join(root, "src", "STS2HumanAnnotator.Mod", "mod_manifest.json");
const steamAppId = "2868840";
const windowsSettingsSchema = 8;
const exactObserverModIds = ["STS2_MCP", "STS2_HUMAN_ANNOTATOR"];
const exactConnectorOnlyModIds = ["STS2_MCP"];

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
  return JSON.parse(fs.readFileSync(file, "utf8").replace(/^\uFEFF/u, ""));
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

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function sameIdentity(left, right) {
  return left?.sha256 === right?.sha256
    && left?.module_version_id === right?.module_version_id;
}

function archiveLocalState(destination) {
  const entries = [runtimeStatus, canaryPath];
  const state = [];
  for (const file of entries) {
    const name = path.basename(file);
    const existed = fs.existsSync(file);
    state.push({ name, existed });
    if (existed) fs.copyFileSync(file, path.join(destination, name));
    fs.rmSync(file, { force: true });
  }
  return state;
}

function gameRunning() {
  if (headlessApi) {
    return headlessApi.listGameProcesses(process.platform, { failClosed: true }).length > 0;
  }
  if (process.platform === "win32") {
    throw new Error(
      "Strict Windows process detection requires the canonical sibling STS2-headless checkout."
    );
  }
  const result = spawnSync("pgrep", ["-f", "SlayTheSpire2"], { encoding: "utf8" });
  if (result.error || ![0, 1].includes(result.status)) {
    throw new Error(`Could not enumerate STS2 processes: ${result.error?.message ?? result.stderr}`);
  }
  return result.status === 0;
}

function requireInstallation() {
  const required = [
    installation.executable,
    installation.data_dir,
    installation.release_info,
    path.join(installation.data_dir, "sts2.dll"),
    path.join(installation.data_dir, "GodotSharp.dll"),
    path.join(installation.data_dir, "0Harmony.dll")
  ];
  const missing = required.filter((entry) => !fs.existsSync(entry));
  if (missing.length) {
    throw new Error(`STS2 installation is incomplete: ${missing.join(", ")}`);
  }
}

function connectorProcessCanary(provenance) {
  if (!fs.existsSync(connectorBuildIdentity) || !fs.existsSync(connectorCompatibility)) {
    throw new Error("Build the exact Connector before launching STS2.");
  }
  return resolveConnectorCanaryEnvironment({
    compatibility: readJson(connectorCompatibility),
    connectorBuild: readJson(connectorBuildIdentity),
    gameRelease: provenance.game.release,
    gameIdentity: provenance.game.sts2_identity
  });
}

function exactCurrentGame() {
  requireInstallation();
  const release = readJson(installation.release_info);
  const sts2Identity = exactIdentity(path.join(dataDir, "sts2.dll"));
  return {
    release,
    executable: {
      path: installation.executable,
      sha256: sha256(installation.executable)
    },
    sts2_identity: sts2Identity,
    godotsharp_sha256: sha256(path.join(dataDir, "GodotSharp.dll"))
  };
}

function requireInstalledProvenance() {
  const file = path.join(local, "installed-provenance.json");
  if (!fs.existsSync(file)) throw new Error("Deploy the exact Annotator before launching STS2.");
  const storedProvenance = readJson(file);
  const currentGame = exactCurrentGame();
  const connectorBuild = readJson(connectorBuildIdentity);
  const normalized = normalizeInstalledProvenance({
    provenance: storedProvenance,
    currentGame,
    connectorBuild
  });
  const provenance = normalized.provenance;
  if (provenance.game.executable.sha256 !== currentGame.executable.sha256
      || !sameIdentity(provenance.game.sts2_identity, currentGame.sts2_identity)
      || JSON.stringify(provenance.game.release) !== JSON.stringify(currentGame.release))
    throw new Error("Installed provenance no longer matches the exact STS2 installation.");
  const installedAnnotator = exactIdentity(path.join(modsDir, "STS2_HUMAN_ANNOTATOR.dll"));
  const installedConnector = exactIdentity(path.join(modsDir, "STS2_MCP.dll"));
  if (!sameIdentity(installedAnnotator, provenance.installed_artifact))
    throw new Error("Installed Annotator no longer matches installed provenance.");
  if (!sameIdentity(installedConnector, provenance.connector_artifact))
    throw new Error("Installed Connector no longer matches Annotator provenance.");
  return {
    provenance,
    provenanceCompatibility: normalized.compatibility,
    currentGame,
    installedAnnotator,
    installedConnector
  };
}

function windowsSettings() {
  if (process.platform !== "win32") return null;
  const roaming = process.env.APPDATA;
  if (!roaming) throw new Error("APPDATA is unavailable; cannot resolve Windows STS2 settings.");
  const steamRoot = path.join(roaming, "SlayTheSpire2", "steam");
  if (!fs.existsSync(steamRoot)) throw new Error(`Windows STS2 Steam settings root is absent: ${steamRoot}`);
  const candidates = fs.readdirSync(steamRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => path.join(steamRoot, entry.name, "settings.save"))
    .filter(fs.existsSync);
  if (candidates.length !== 1) {
    throw new Error(`Expected exactly one Windows Steam settings.save, observed ${candidates.length}.`);
  }
  const file = candidates[0];
  const value = readJson(file);
  if (value.schema_version !== windowsSettingsSchema)
    throw new Error(`Windows settings schema drift: expected ${windowsSettingsSchema}, observed ${value.schema_version}.`);
  if (value.mod_settings == null
      || typeof value.mod_settings !== "object"
      || Array.isArray(value.mod_settings)
      || !Array.isArray(value.mod_settings.mod_list))
    throw new Error("Windows settings have an unexpected mod_settings shape.");
  return { file, value };
}

function requireExactObserverModSettings() {
  const resolved = windowsSettings();
  if (resolved == null) return null;
  const { value } = resolved;
  const entries = value.mod_settings.mod_list;
  const enabledUnexpected = entries
    .filter((entry) => entry?.is_enabled === true && !exactObserverModIds.includes(entry.id))
    .map((entry) => entry.id ?? "unidentified");
  const exact = exactObserverModIds.every((id) => entries.some((entry) =>
    entry?.id === id
      && entry?.source === "mods_directory"
      && entry?.is_enabled === true
  ));
  if (value.mod_settings.mods_enabled !== true || !exact || enabledUnexpected.length) {
    throw new Error(
      `Windows exact observer Mod settings are not admitted; unexpected enabled Mods: ${enabledUnexpected.join(", ") || "none"}. `
      + "Run npm run prepare:mods while STS2 is closed."
    );
  }
  return resolved;
}

function prepareModSettings() {
  if (process.platform !== "win32") {
    console.log(JSON.stringify({ status: "not_required", platform: process.platform }, null, 2));
    return;
  }
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before changing Mod settings.");
  const resolved = windowsSettings();
  const entries = resolved.value.mod_settings.mod_list;
  const enabledUnexpected = entries
    .filter((entry) => entry?.is_enabled === true && !exactObserverModIds.includes(entry.id))
    .map((entry) => entry.id ?? "unidentified");
  if (enabledUnexpected.length) {
    throw new Error(`Refusing to preserve an enabled non-observer Modset: ${enabledUnexpected.join(", ")}`);
  }
  const retained = entries.filter((entry) => !exactObserverModIds.includes(entry?.id));
  const updated = {
    ...resolved.value,
    mod_settings: {
      ...resolved.value.mod_settings,
      mod_list: [
        ...exactObserverModIds.map((id) => ({ id, is_enabled: true, source: "mods_directory" })),
        ...retained
      ],
      mods_enabled: true
    }
  };
  const backup = path.join(local, "settings-backups", safeTimestamp());
  fs.mkdirSync(backup, { recursive: true });
  fs.copyFileSync(resolved.file, path.join(backup, "settings.save"));
  writeJson(path.join(backup, "backup-provenance.json"), {
    schema_version: 1,
    backed_up_at: new Date().toISOString(),
    source_path: resolved.file,
    source_sha256: sha256(path.join(backup, "settings.save")),
    expected_settings_schema: windowsSettingsSchema
  });
  writeJson(resolved.file, updated);
  requireExactObserverModSettings();
  console.log(JSON.stringify({
    status: "exact_observer_mods_enabled_cold_start_required",
    settings_file: resolved.file,
    backup,
    enabled_mods: exactObserverModIds
  }, null, 2));
}

function requireExactConnectorOnlyModSettings() {
  const resolved = windowsSettings();
  if (resolved == null) throw new Error("Connector-only live launch currently requires Windows.");
  const entries = resolved.value.mod_settings.mod_list;
  const enabled = entries.filter((entry) => entry?.is_enabled === true).map((entry) => entry.id);
  const exact = exactConnectorOnlyModIds.every((id) => entries.some((entry) =>
    entry?.id === id && entry?.source === "mods_directory" && entry?.is_enabled === true
  ));
  if (resolved.value.mod_settings.mods_enabled !== true
      || !exact
      || enabled.length !== exactConnectorOnlyModIds.length) {
    throw new Error(`Windows Connector-only Mod settings drifted: ${enabled.join(", ")}`);
  }
  return resolved;
}

function prepareConnectorOnlyModSettings() {
  if (process.platform !== "win32")
    throw new Error("Connector-only live settings currently require Windows.");
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before changing Mod settings.");
  const resolved = windowsSettings();
  const updated = prepareExactWindowsModSettings({
    settings: resolved.value,
    enabledModIds: exactConnectorOnlyModIds,
    allowedPreviouslyEnabledModIds: exactObserverModIds
  });
  const backup = path.join(local, "live-settings-backups", safeTimestamp());
  fs.mkdirSync(backup, { recursive: true });
  fs.copyFileSync(resolved.file, path.join(backup, "settings.save"));
  writeJson(path.join(backup, "backup-provenance.json"), {
    schema_version: 1,
    backed_up_at: new Date().toISOString(),
    source_path: resolved.file,
    source_sha256: sha256(path.join(backup, "settings.save")),
    expected_settings_schema: windowsSettingsSchema,
    purpose: "temporary_exact_connector_only_live_s1"
  });
  writeJson(resolved.file, updated);
  requireExactConnectorOnlyModSettings();
  console.log(JSON.stringify({
    status: "exact_connector_only_enabled_cold_start_required",
    settings_file: resolved.file,
    backup,
    enabled_mods: exactConnectorOnlyModIds,
    disabled_but_preserved_mods: ["STS2_HUMAN_ANNOTATOR"]
  }, null, 2));
}

function launchConnectorOnly() {
  if (process.platform !== "win32")
    throw new Error("Connector-only live launch currently requires Windows.");
  if (gameRunning()) throw new Error("Slay the Spire 2 is already running; cold-load requires a fully closed process.");
  requireInstallation();
  requireExactConnectorOnlyModSettings();
  const currentGame = exactCurrentGame();
  const connectorBuild = readJson(connectorBuildIdentity);
  const installedConnector = exactIdentity(path.join(modsDir, "STS2_MCP.dll"));
  if (installedConnector.sha256 !== connectorBuild.artifact_sha256
      || installedConnector.module_version_id !== connectorBuild.artifact_mvid) {
    throw new Error("Installed Connector differs from the exact built artifact.");
  }
  const connectorCanary = connectorProcessCanary({
    game: { release: currentGame.release, sts2_identity: currentGame.sts2_identity }
  });
  const env = { ...process.env, ...connectorCanary.environment };
  env.SteamAppId ??= steamAppId;
  env.SteamGameId ??= steamAppId;
  delete env.STS2_CONNECTOR_EXPERIMENTAL_MODSET_FINGERPRINT;
  const child = spawn(installation.executable, [], {
    cwd: installation.executable_cwd,
    env,
    detached: true,
    stdio: "ignore",
    windowsHide: false
  });
  child.unref();
  const record = {
    status: "launched_exact_connector_only",
    launched_at: new Date().toISOString(),
    pid: child.pid,
    executable: installation.executable,
    connector_runtime: connectorCanary.runtime,
    connector_environment: connectorCanary.environment,
    installed_connector: installedConnector,
    enabled_mods: exactConnectorOnlyModIds
  };
  writeJson(path.join(local, "last-live-connector-launch.json"), record);
  console.log(JSON.stringify(record, null, 2));
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
  requireInstallation();
  const source = sourceState();
  const digest = sourceDigest();
  run("dotnet", [
    "build", "STS2HumanAnnotator.sln", "-c", "Release",
    `-p:STS2GameDir=${gameDir}`,
    `-p:ConnectorAssembly=${connectorArtifact}`,
    `-p:SourceRevision=${source.head}`,
    `-p:AnnotatorSourceDigest=${digest}`
  ]);
  const artifact = exactIdentity(path.join(modOutput, "STS2_HUMAN_ANNOTATOR.dll"));
  const connector = exactIdentity(connectorArtifact);
  const connectorBuild = readJson(connectorBuildIdentity);
  if (connector.sha256 !== connectorBuild.artifact_sha256
      || connector.module_version_id !== connectorBuild.artifact_mvid)
    throw new Error("Connector build identity does not match the exact build artifact.");
  const game = exactCurrentGame();
  writeJson(provenancePath, {
    schema_version: 2,
    built_at: new Date().toISOString(),
    platform: process.platform,
    architecture: process.arch,
    source_revision: source.head,
    source_digest_sha256: digest,
    source_worktree: source.worktree,
    artifact,
    connector_artifact: connector,
    connector_build: connectorBuild,
    installation: {
      discovery_method: installation.discovery_method,
      game_dir: gameDir,
      executable: installation.executable,
      executable_cwd: installation.executable_cwd,
      data_dir: dataDir,
      mods_dir: modsDir
    },
    game
  });
  console.log(JSON.stringify(readJson(provenancePath), null, 2));
}

function test() {
  run("dotnet", ["test", "tests/STS2HumanAnnotator.Core.Tests/STS2HumanAnnotator.Core.Tests.csproj", "-c", "Release"]);
  run("node", ["--test", path.join(root, "tools", "workstation-platform.test.mjs")]);
  run("node", [path.join(root, "tools", "check-boundary.mjs")]);
  run("node", [path.join(root, "tools", "check-docs.mjs")]);
}

function doctor() {
  const built = path.join(modOutput, "STS2_HUMAN_ANNOTATOR.dll");
  const installed = path.join(modsDir, "STS2_HUMAN_ANNOTATOR.dll");
  const status = fs.existsSync(runtimeStatus) ? readJson(runtimeStatus) : null;
  const report = {
    status: fs.existsSync(installation.executable) && fs.existsSync(connectorRoot) ? "ok" : "action_required",
    repository: sourceState(),
    platform: process.platform,
    architecture: process.arch,
    installation,
    game_exists: fs.existsSync(installation.executable) && fs.existsSync(dataDir),
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
  requireInstallation();
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before deployment.");
  const allowDirty = args.includes("--allow-dirty");
  const source = sourceState();
  if (source.worktree !== "clean" && !allowDirty)
    throw new Error("Refusing to deploy a dirty source; commit first or use --allow-dirty for local development only.");
  if (!fs.existsSync(provenancePath)) throw new Error("Run npm run build first.");
  const provenance = readJson(provenancePath);
  if (provenance.source_revision !== source.head || provenance.source_digest_sha256 !== sourceDigest())
    throw new Error("Build provenance does not match the current exact source.");
  const currentGame = exactCurrentGame();
  if (provenance.platform !== process.platform || provenance.architecture !== process.arch
      || provenance.game.executable.sha256 !== currentGame.executable.sha256
      || !sameIdentity(provenance.game.sts2_identity, currentGame.sts2_identity)
      || JSON.stringify(provenance.game.release) !== JSON.stringify(currentGame.release))
    throw new Error("Build provenance does not match the exact STS2 runtime.");
  const builtArtifact = exactIdentity(path.join(modOutput, "STS2_HUMAN_ANNOTATOR.dll"));
  const currentConnector = exactIdentity(connectorArtifact);
  if (!sameIdentity(builtArtifact, provenance.artifact))
    throw new Error("Built Annotator artifact no longer matches build provenance.");
  if (!sameIdentity(currentConnector, provenance.connector_artifact))
    throw new Error("Connector artifact no longer matches Annotator build provenance.");
  const installedConnectorPath = path.join(modsDir, "STS2_MCP.dll");
  if (!fs.existsSync(installedConnectorPath))
    throw new Error("Deploy the exact Connector before deploying the Annotator.");
  const installedConnector = exactIdentity(installedConnectorPath);
  if (!sameIdentity(installedConnector, currentConnector))
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
  const localState = archiveLocalState(backup);
  writeJson(path.join(backup, "rollback-manifest.json"), {
    schema_version: 2,
    files: backupState,
    local_state: localState
  });
  fs.rmSync(path.join(modsDir, "STS2HumanAnnotator.Core.dll"), { force: true });
  fs.copyFileSync(path.join(modOutput, "STS2_HUMAN_ANNOTATOR.dll"), path.join(modsDir, "STS2_HUMAN_ANNOTATOR.dll"));
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
  const installed = requireInstalledProvenance();
  if (!status.environment?.modset_fingerprint) throw new Error("Runtime status lacks a Modset fingerprint.");
  const loadedAnnotator = status.environment?.annotator;
  const loadedConnector = status.environment?.connector;
  const installedAnnotator = installed.installedAnnotator;
  const installedConnector = installed.installedConnector;
  if (!sameIdentity(loadedAnnotator, installedAnnotator)
      || !sameIdentity(loadedConnector, installedConnector))
    throw new Error("The observed Modset fingerprint is not from the currently installed artifacts.");
  const connectorSource = status.environment.connector.source_revision;
  if (!/^[0-9a-f]{40}$/.test(connectorSource)) throw new Error("Connector source revision is not exact.");
  const connectorCanary = connectorProcessCanary(installed.provenance);
  if (connectorSource !== installed.provenance.connector_build.source_revision
      || loadedConnector.source_digest_sha256
        !== installed.provenance.connector_build.player_environment_source_digest)
    throw new Error("Observed Connector source provenance does not match the deployed exact build.");
  if (loadedAnnotator.source_revision !== installed.provenance.source_revision
      || loadedAnnotator.source_digest_sha256 !== installed.provenance.source_digest_sha256)
    throw new Error("Observed Annotator source provenance does not match the deployed exact build.");
  if (status.environment.game.main_assembly_sha256 !== installed.currentGame.sts2_identity.sha256
      || status.environment.game.main_assembly_module_version_id
        !== installed.currentGame.sts2_identity.module_version_id)
    throw new Error("Observed game identity does not match the deployed exact runtime.");
  writeJson(canaryPath, {
    schema_version: 2,
    admitted_at: new Date().toISOString(),
    modset_fingerprint: status.environment.modset_fingerprint,
    connector_source_revision: connectorSource,
    connector_game_id: connectorCanary.runtime.status === "candidate_exact"
      ? connectorCanary.runtime.id
      : null,
    connector_artifact: installedConnector,
    annotator_source_revision: installed.provenance.source_revision,
    annotator_source_digest_sha256: installed.provenance.source_digest_sha256,
    annotator_artifact: installedAnnotator,
    game_release: installed.currentGame.release,
    game_executable_sha256: installed.currentGame.executable.sha256,
    game_sts2_identity: installed.currentGame.sts2_identity,
    observed_runtime_instance_id: status.environment.runtime_instance_id,
    note: "Process-local canary input only; this is not qualification or human validation."
  });
  console.log(JSON.stringify(readJson(canaryPath), null, 2));
}

function launch() {
  if (gameRunning()) throw new Error("Slay the Spire 2 is already running; cold-load requires a fully closed process.");
  if (!["darwin", "win32"].includes(process.platform))
    throw new Error(`Automated cold launch is unsupported on ${process.platform}.`);
  const installed = requireInstalledProvenance();
  requireExactObserverModSettings();
  const executable = installation.executable;
  const connectorCanary = connectorProcessCanary(installed.provenance);
  const env = { ...process.env };
  env.SteamAppId ??= steamAppId;
  env.SteamGameId ??= steamAppId;
  Object.assign(env, connectorCanary.environment);
  let canary = null;
  if (fs.existsSync(canaryPath)) {
    canary = normalizeExactModsetCanary({
      canary: readJson(canaryPath),
      installed,
      connectorRuntime: connectorCanary.runtime
    });
    if (canary.connector_source_revision !== installed.provenance.connector_build.source_revision
        || canary.annotator_source_revision !== installed.provenance.source_revision
        || canary.game_executable_sha256 !== installed.currentGame.executable.sha256
        || !sameIdentity(canary.connector_artifact, installed.installedConnector)
        || !sameIdentity(canary.annotator_artifact, installed.installedAnnotator)
        || !sameIdentity(canary.game_sts2_identity, installed.currentGame.sts2_identity)
        || canary.connector_game_id !== (connectorCanary.runtime.status === "candidate_exact"
          ? connectorCanary.runtime.id
          : null))
      throw new Error("Exact Modset canary has drifted from the installed process envelope.");
    env.STS2_CONNECTOR_EXPERIMENTAL_MODSET_FINGERPRINT = canary.modset_fingerprint;
  }
  const child = spawn(executable, [], {
    cwd: installation.executable_cwd,
    env,
    detached: true,
    stdio: "ignore",
    windowsHide: false
  });
  child.unref();
  const launchRecord = {
    status: "launched",
    launched_at: new Date().toISOString(),
    pid: child.pid,
    executable,
    connector_runtime: connectorCanary.runtime,
    connector_environment: connectorCanary.environment,
    modset_canary_applied: canary != null,
    modset_fingerprint: canary?.modset_fingerprint ?? null
  };
  writeJson(path.join(local, "last-launch.json"), launchRecord);
  console.log(JSON.stringify(launchRecord, null, 2));
}

function verifyLoaded() {
  if (!fs.existsSync(runtimeStatus)) throw new Error("Runtime status is absent.");
  const status = readJson(runtimeStatus);
  const installed = requireInstalledProvenance();
  const provenance = installed.provenance;
  if (!fs.existsSync(canaryPath)) throw new Error("Exact Modset canary is absent.");
  const canary = readJson(canaryPath);
  const ageMs = Date.now() - Date.parse(status.observed_at);
  const installedAnnotator = installed.installedAnnotator;
  const installedConnector = installed.installedConnector;
  const errors = [];
  if (!gameRunning() || !processAlive(status.process_id)) errors.push("runtime_process_not_running");
  const runtimeCommand = headlessApi?.processCommand(status.process_id, process.platform) ?? null;
  if (!commandMatchesExecutable(runtimeCommand, installation.executable))
    errors.push("runtime_process_executable_mismatch");
  if (ageMs > 5000) errors.push("runtime_status_not_fresh");
  if (status.process_id <= 0) errors.push("runtime_process_id_missing");
  if (status.environment?.annotator?.sha256 !== installedAnnotator.sha256) errors.push("annotator_loaded_installed_sha_mismatch");
  if (status.environment?.annotator?.module_version_id !== installedAnnotator.module_version_id) errors.push("annotator_loaded_installed_mvid_mismatch");
  if (status.environment?.connector?.sha256 !== installedConnector.sha256) errors.push("connector_loaded_installed_sha_mismatch");
  if (status.environment?.connector?.module_version_id !== installedConnector.module_version_id) errors.push("connector_loaded_installed_mvid_mismatch");
  if (status.environment?.annotator?.source_revision !== provenance.source_revision) errors.push("annotator_loaded_source_revision_mismatch");
  if (status.environment?.annotator?.source_digest_sha256 !== provenance.source_digest_sha256) errors.push("annotator_loaded_source_digest_mismatch");
  if (status.environment?.connector?.source_revision !== provenance.connector_build.source_revision) errors.push("connector_loaded_source_revision_mismatch");
  if (status.environment?.connector?.source_digest_sha256 !== provenance.connector_build.player_environment_source_digest) errors.push("connector_loaded_source_digest_mismatch");
  if (status.environment?.game?.version !== provenance.game.release.version
      || status.environment?.game?.commit !== provenance.game.release.commit) errors.push("loaded_game_release_mismatch");
  if (status.environment?.game?.main_assembly_sha256 !== provenance.game.sts2_identity.sha256) errors.push("loaded_game_sha_mismatch");
  if (status.environment?.game?.main_assembly_module_version_id !== provenance.game.sts2_identity.module_version_id) errors.push("loaded_game_mvid_mismatch");
  if (status.environment?.modset_status !== "canary_exact_observer_modset") errors.push("exact_observer_modset_canary_not_active");
  if (status.environment?.modset_fingerprint !== canary.modset_fingerprint) errors.push("loaded_modset_fingerprint_mismatch");
  console.log(JSON.stringify({ status: errors.length ? "fail" : "pass", errors, provenance_compatibility: installed.provenanceCompatibility, runtime_command: runtimeCommand, runtime: status, canary, installed_annotator: installedAnnotator, installed_connector: installedConnector }, null, 2));
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

function option(name) {
  const index = args.indexOf(name);
  if (index < 0 || index + 1 >= args.length) throw new Error(`Missing required option: ${name}`);
  return args[index + 1];
}

function packSession() {
  if (git("status", "--porcelain").trim())
    throw new Error("Commit or remove Annotator worktree changes before evidence packing.");
  const directory = path.resolve(args[0] || readJson(runtimeStatus).recording_directory);
  const profile = path.resolve(option("--profile"));
  const worker = option("--worker");
  const campaign = option("--campaign");
  if (!args.includes("--attest-human-origin"))
    throw new Error("Packing requires explicit --attest-human-origin.");
  const outputIndex = args.indexOf("--output");
  const output = outputIndex >= 0
    ? path.resolve(args[outputIndex + 1])
    : path.join(local, "bundles", path.basename(directory));
  if (gameRunning() && fs.existsSync(runtimeStatus)) {
    const status = readJson(runtimeStatus);
    if (path.resolve(status.recording_directory) === directory && processAlive(status.process_id))
      throw new Error("The active recording session must be closed before packing.");
  }
  run("dotnet", [
    toolDll,
    "pack-session",
    directory,
    profile,
    worker,
    campaign,
    output,
    git("rev-parse", "HEAD"),
    "human_origin_attested"
  ]);
}

function rollback() {
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before rollback.");
  const installation = readJson(path.join(local, "installed-provenance.json"));
  const backup = installation.rollback;
  if (!backup || !fs.existsSync(backup)) throw new Error("Rollback snapshot is unavailable.");
  const manifest = readJson(path.join(backup, "rollback-manifest.json"));
  const rollbackArchive = path.join(local, "rollback-state", safeTimestamp());
  fs.mkdirSync(rollbackArchive, { recursive: true });
  archiveLocalState(rollbackArchive);
  for (const entry of manifest.files) {
    const installed = path.join(modsDir, entry.name);
    if (entry.existed) fs.copyFileSync(path.join(backup, entry.name), installed);
    else fs.rmSync(installed, { force: true });
  }
  for (const entry of manifest.local_state ?? []) {
    const target = path.join(local, entry.name);
    if (entry.existed) fs.copyFileSync(path.join(backup, entry.name), target);
    else fs.rmSync(target, { force: true });
  }
  console.log(JSON.stringify({ status: "restored", rollback: backup }, null, 2));
}

function check() {
  requireInstallation();
  test();
  run("dotnet", ["build", "STS2HumanAnnotator.sln", "-c", "Release", `-p:STS2GameDir=${gameDir}`, `-p:ConnectorAssembly=${connectorArtifact}`]);
}

try {
  if (command === "doctor") doctor();
  else if (command === "build") build();
  else if (command === "test") test();
  else if (command === "check") check();
  else if (command === "deploy") deploy();
  else if (command === "prepare-mod-settings") prepareModSettings();
  else if (command === "prepare-live-connector-settings") prepareConnectorOnlyModSettings();
  else if (command === "admit-current-modset") admitCurrentModset();
  else if (command === "launch") launch();
  else if (command === "launch-live-connector") launchConnectorOnly();
  else if (command === "verify-loaded") verifyLoaded();
  else if (command === "audit") audit();
  else if (command === "export") exportRecords();
  else if (command === "pack-session") packSession();
  else if (command === "rollback") rollback();
  else throw new Error(`Unknown command: ${command}`);
} catch (error) {
  console.error(JSON.stringify({ status: "error", command, error: error.message }, null, 2));
  process.exitCode = 1;
}
