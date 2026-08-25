#!/usr/bin/env node

import { spawn, spawnSync } from "node:child_process";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import process from "node:process";

import {
  loadHostRuntimeWorkstationApi,
  resolveWorkstationInstallation
} from "../../components/annotator/tools/workstation-platform.mjs";
import { sourceSetIdentity, sourceSetMatches } from "./source-identity.mjs";

const appRoot = import.meta.dirname;
const platformRoot = path.resolve(appRoot, "../..");
const annotatorRoot = path.join(platformRoot, "components/annotator");
const outputRoot = path.join(appRoot, "bin/Release/net9.0");
const builtDll = path.join(outputRoot, "STS2_PLATFORM.dll");
const buildProvenance = path.join(outputRoot, "build-provenance.json");
const manifestSource = path.join(appRoot, "mod_manifest.json");
const localRoot = path.join(appRoot, ".local");
const installedProvenance = path.join(localRoot, "installed-provenance.json");
const runtimeStatus = path.join(annotatorRoot, ".local/runtime-status.json");
const identityTool = path.join(
  annotatorRoot,
  "src/STS2HumanAnnotator.Tool/bin/Release/net9.0/sts2-human-annotator.dll"
);
const hostApi = await loadHostRuntimeWorkstationApi(annotatorRoot);
const installation = resolveWorkstationInstallation({ headlessApi: hostApi });
const installedDll = path.join(installation.mods_dir, "STS2_PLATFORM.dll");
const installedManifest = path.join(installation.mods_dir, "STS2_PLATFORM.json");
const retiredProductionFiles = [
  "STS2_MCP.dll",
  "STS2_MCP.json",
  "STS2_HUMAN_ANNOTATOR.dll",
  "STS2_HUMAN_ANNOTATOR.json",
  "STS2_PLATFORM_LIVE_UI.dll",
  "STS2_PLATFORM_LIVE_UI.json"
];
const managedModFiles = [
  "STS2_PLATFORM.dll",
  "STS2_PLATFORM.json",
  ...retiredProductionFiles
];
const managedConfigFiles = ["STS2_MCP.conf", "STS2_HUMAN_ANNOTATOR.conf"];
const command = process.argv[2] ?? "doctor";

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
  if (!fs.existsSync(identityTool)) throw new Error("Run npm run game-mod:build first.");
  const result = spawnSync("dotnet", [identityTool, "identity", file], {
    cwd: platformRoot,
    encoding: "utf8"
  });
  if (result.status !== 0) throw new Error(result.stderr || `identity tool exited with ${result.status}`);
  return JSON.parse(result.stdout);
}

function sameIdentity(left, right) {
  return left?.sha256 === right?.sha256
    && left?.module_version_id === right?.module_version_id;
}

function sameHostIdentity(left, right) {
  return left?.artifact_sha256 === right?.sha256
    && left?.module_version_id === right?.module_version_id;
}

function gameProcesses() {
  if (hostApi) return hostApi.listGameProcesses(process.platform, { failClosed: true });
  if (process.platform === "win32") throw new Error("Strict Windows process detection requires Platform Host Runtime.");
  const result = spawnSync("pgrep", ["-f", "SlayTheSpire2"], { encoding: "utf8" });
  if (result.error || ![0, 1].includes(result.status)) throw new Error("Could not enumerate STS2 processes.");
  return result.status === 0 ? result.stdout.trim().split(/\s+/u).filter(Boolean) : [];
}

function gameRunning() {
  return gameProcesses().length > 0;
}

function requireBuild() {
  if (!fs.existsSync(buildProvenance) || !fs.existsSync(builtDll)) {
    throw new Error("Run npm run game-mod:build first.");
  }
  const provenance = readJson(buildProvenance);
  if (provenance.schema !== "sts2.platform/game-mod-build-provenance-1") {
    throw new Error("Game Mod build provenance schema is unsupported.");
  }
  const current = sourceSetIdentity(platformRoot);
  if (current.platform.workspace_worktree_status !== "clean") {
    throw new Error("Commit the exact Platform source before production deployment.");
  }
  if (!sourceSetMatches(provenance.source, current)) {
    throw new Error("Game Mod build provenance differs from current exact source.");
  }
  const built = exactIdentity(builtDll);
  if (!sameIdentity(built, provenance.artifact)) {
    throw new Error("Built Game Mod differs from build provenance.");
  }
  return { provenance, built };
}

function archiveTarget(backup, location, file) {
  const existed = fs.existsSync(file);
  const archiveName = `${location}--${path.basename(file)}`;
  if (existed) fs.copyFileSync(file, path.join(backup, archiveName));
  return { location, name: path.basename(file), archive_name: archiveName, existed };
}

function restoreTarget(entry) {
  if (entry.location === "mods") return path.join(installation.mods_dir, entry.name);
  if (entry.location === "local" && entry.name === path.basename(installedProvenance)) {
    return installedProvenance;
  }
  throw new Error(`Unsupported Game Mod rollback target: ${entry.location}/${entry.name}`);
}

function validateRollbackManifest(manifest) {
  if (manifest.schema !== "sts2.platform/game-mod-rollback-1" || !Array.isArray(manifest.files)) {
    throw new Error("Game Mod rollback manifest is unsupported.");
  }
  const allowedMods = new Set([...managedModFiles, ...managedConfigFiles]);
  for (const entry of manifest.files) {
    const validLocation = entry?.location === "mods" || entry?.location === "local";
    const validName = typeof entry?.name === "string"
      && path.basename(entry.name) === entry.name
      && entry.name !== "."
      && entry.name !== "..";
    const allowedName = entry?.location === "mods"
      ? allowedMods.has(entry.name)
      : entry?.name === path.basename(installedProvenance);
    const expectedArchive = validLocation && validName
      ? `${entry.location}--${entry.name}`
      : null;
    if (!validLocation
        || !validName
        || !allowedName
        || entry.archive_name !== expectedArchive
        || typeof entry.existed !== "boolean") {
      throw new Error("Game Mod rollback manifest contains an invalid target.");
    }
  }
  return manifest;
}

function restoreEntries(backup, entries) {
  for (const entry of entries) {
    const target = restoreTarget(entry);
    if (entry.existed) fs.copyFileSync(path.join(backup, entry.archive_name), target);
    else fs.rmSync(target, { force: true });
  }
}

function rollbackDirectory(value) {
  const resolved = path.resolve(value ?? "");
  const root = path.resolve(localRoot, "deployments");
  if (!resolved.startsWith(`${root}${path.sep}`)) {
    throw new Error("Game Mod rollback directory is outside the local deployment archive.");
  }
  return resolved;
}

function doctor() {
  const report = {
    status: fs.existsSync(installation.executable) ? "ok" : "action_required",
    platform: process.platform,
    architecture: process.arch,
    game_running: gameRunning(),
    installation,
    source: (() => { try { return sourceSetIdentity(platformRoot); } catch { return null; } })(),
    built: fs.existsSync(builtDll) ? exactIdentity(builtDll) : null,
    installed: fs.existsSync(installedDll) ? exactIdentity(installedDll) : null,
    legacy_production_files_present: retiredProductionFiles.filter((name) =>
      fs.existsSync(path.join(installation.mods_dir, name))),
    build_provenance: fs.existsSync(buildProvenance) ? readJson(buildProvenance) : null,
    installed_provenance: fs.existsSync(installedProvenance) ? readJson(installedProvenance) : null,
    non_claims: ["doctor_is_read_only", "installed_is_not_loaded", "loaded_is_not_human_or_policy_evidence"]
  };
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
}

function deploy() {
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before deployment.");
  const exact = requireBuild();
  fs.mkdirSync(installation.mods_dir, { recursive: true });
  const backup = path.join(localRoot, "deployments", new Date().toISOString().replaceAll(":", "-"));
  fs.mkdirSync(backup, { recursive: true });
  const entries = [
    ...managedModFiles.map((name) => archiveTarget(
      backup,
      "mods",
      path.join(installation.mods_dir, name))),
    ...managedConfigFiles.map((name) => archiveTarget(
      backup,
      "mods",
      path.join(installation.mods_dir, name))),
    archiveTarget(backup, "local", installedProvenance)
  ];
  writeJson(path.join(backup, "rollback-manifest.json"), {
    schema: "sts2.platform/game-mod-rollback-1",
    files: entries
  });

  try {
    for (const name of managedModFiles) {
      fs.rmSync(path.join(installation.mods_dir, name), { force: true });
    }
    fs.copyFileSync(builtDll, installedDll);
    fs.copyFileSync(manifestSource, installedManifest);
    const connectorConfig = path.join(installation.mods_dir, "STS2_MCP.conf");
    if (!fs.existsSync(connectorConfig)) {
      writeJson(connectorConfig, {
        port: 15526,
        player_environment_native_page_evidence_enabled: false
      });
    }
    const annotatorConfig = path.join(installation.mods_dir, "STS2_HUMAN_ANNOTATOR.conf");
    if (!fs.existsSync(annotatorConfig)) {
      writeJson(annotatorConfig, {
        recording_root: path.join(annotatorRoot, ".local/recordings"),
        runtime_status_path: runtimeStatus,
        successor_timeout_ms: 20000
      });
    }
    const installed = {
      schema: "sts2.platform/game-mod-installed-provenance-1",
      installed_at: new Date().toISOString(),
      source: exact.provenance.source,
      game: exact.provenance.game,
      artifact: exactIdentity(installedDll),
      manifest: readJson(installedManifest),
      retired_production_files: retiredProductionFiles,
      rollback: backup
    };
    if (!sameIdentity(installed.artifact, exact.built)) {
      throw new Error("Installed Game Mod identity differs from the exact build.");
    }
    writeJson(installedProvenance, installed);
    process.stdout.write(`${JSON.stringify(installed, null, 2)}\n`);
  } catch (error) {
    restoreEntries(backup, entries);
    throw error;
  }
}

function rollback() {
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before rollback.");
  if (!fs.existsSync(installedProvenance)) throw new Error("Game Mod installed provenance is unavailable.");
  const installed = readJson(installedProvenance);
  const rollback = rollbackDirectory(installed.rollback);
  const manifest = validateRollbackManifest(readJson(path.join(rollback, "rollback-manifest.json")));
  restoreEntries(rollback, manifest.files);
  process.stdout.write(`${JSON.stringify({ status: "restored", rollback }, null, 2)}\n`);
}

function launch() {
  if (gameRunning()) throw new Error("Slay the Spire 2 is already running; cold-load requires a closed process.");
  if (!fs.existsSync(installedProvenance)) throw new Error("Deploy the unified Game Mod first.");
  if (!["darwin", "win32"].includes(process.platform)) {
    throw new Error(`Automated cold launch is unsupported on ${process.platform}.`);
  }
  const installed = readJson(installedProvenance);
  const currentInstalled = exactIdentity(installedDll);
  if (!sameIdentity(currentInstalled, installed.artifact)) {
    throw new Error("Installed Game Mod drifted from installed provenance.");
  }
  const env = {
    ...process.env,
    STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION:
      installed.source.components.connector.source_revision
  };
  env.SteamAppId ??= "2868840";
  env.SteamGameId ??= "2868840";
  const child = spawn(installation.executable, [], {
    cwd: installation.executable_cwd,
    env,
    detached: true,
    stdio: "ignore",
    windowsHide: false
  });
  child.unref();
  const record = {
    schema: "sts2.platform/game-mod-launch-1",
    status: "launched",
    launched_at: new Date().toISOString(),
    pid: child.pid,
    executable: installation.executable,
    source_revision_canary: installed.source.components.connector.source_revision,
    expected_artifact: installed.artifact,
    expected_modset_status: "exact_platform_modset"
  };
  writeJson(path.join(localRoot, "last-launch.json"), record);
  process.stdout.write(`${JSON.stringify(record, null, 2)}\n`);
}

async function fetchJson(route) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 3000);
  try {
    const response = await fetch(`http://127.0.0.1:15526/${route}`, { signal: controller.signal });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return await response.json();
  } finally {
    clearTimeout(timeout);
  }
}

function latestIdentity(log, prefix) {
  return log.split(/\r?\n/u).flatMap((line) => {
    const index = line.indexOf(prefix);
    if (index < 0) return [];
    try { return [JSON.parse(line.slice(index + prefix.length))]; } catch { return []; }
  }).at(-1);
}

async function verifyLoaded() {
  if (!gameRunning()) throw new Error("Slay the Spire 2 is not running.");
  if (!fs.existsSync(installedProvenance)) throw new Error("Game Mod installed provenance is unavailable.");
  if (!fs.existsSync(runtimeStatus)) throw new Error("Annotator runtime status is unavailable.");
  if (!installation.log_file || !fs.existsSync(installation.log_file)) throw new Error("STS2 runtime log is unavailable.");
  const installed = readJson(installedProvenance);
  const status = readJson(runtimeStatus);
  const capabilities = await fetchJson("api/player-environment/capabilities");
  const log = fs.readFileSync(installation.log_file, "utf8");
  const platformIdentity = latestIdentity(log, "[STS2 Platform] identity ");
  const liveUiIdentity = latestIdentity(log, "[STS2 Platform Live UI] identity ");
  const errors = [];
  const expected = installed.artifact;
  const ageMs = Date.now() - Date.parse(status.observed_at);
  if (ageMs > 5000) errors.push("runtime_status_not_fresh");
  if (!sameIdentity(status.environment?.connector, expected)) errors.push("connector_not_loaded_from_unified_artifact");
  if (!sameIdentity(status.environment?.annotator, expected)) errors.push("annotator_not_loaded_from_unified_artifact");
  if (status.environment?.connector?.source_revision !== installed.source.components.connector.source_revision) errors.push("connector_source_revision_mismatch");
  if (status.environment?.connector?.source_digest_sha256 !== installed.source.components.connector.source_digest_sha256) errors.push("connector_source_digest_mismatch");
  if (status.environment?.annotator?.source_revision !== installed.source.components.annotator.source_revision) errors.push("annotator_source_revision_mismatch");
  if (status.environment?.annotator?.source_digest_sha256 !== installed.source.components.annotator.source_digest_sha256) errors.push("annotator_source_digest_mismatch");
  if (status.environment?.modset_status !== "exact_platform_modset") errors.push("unified_modset_not_exact");
  if (!sameHostIdentity(capabilities.host?.implementation, expected)) errors.push("connector_capabilities_artifact_mismatch");
  if (capabilities.execution_available !== true) errors.push("connector_execution_not_available");
  if (!platformIdentity) errors.push("platform_loaded_identity_absent");
  if (platformIdentity?.artifact_sha256 !== expected.sha256) errors.push("platform_loaded_sha_mismatch");
  if (platformIdentity?.module_version_id !== expected.module_version_id) errors.push("platform_loaded_mvid_mismatch");
  if (platformIdentity?.platform_source_revision !== installed.source.platform.source_revision) errors.push("platform_loaded_source_revision_mismatch");
  if (!liveUiIdentity) errors.push("live_ui_loaded_identity_absent");
  if (liveUiIdentity?.artifact_sha256 !== expected.sha256) errors.push("live_ui_loaded_sha_mismatch");
  if (liveUiIdentity?.module_version_id !== expected.module_version_id) errors.push("live_ui_loaded_mvid_mismatch");
  if (liveUiIdentity?.source_revision !== installed.source.components.live_ui.source_revision) errors.push("live_ui_source_revision_mismatch");
  const latestUiIdentityIndex = log.lastIndexOf("[STS2 Platform Live UI] identity ");
  if (latestUiIdentityIndex < 0 || !log.slice(latestUiIdentityIndex).includes("[STS2 Platform Live UI] panel ready; input=K")) {
    errors.push("live_ui_panel_ready_absent");
  }
  const result = {
    status: errors.length ? "fail" : "pass",
    errors,
    installed,
    platform_loaded_identity: platformIdentity,
    live_ui_loaded_identity: liveUiIdentity,
    runtime: status,
    connector_capabilities: capabilities,
    owner_ui_toggle: log.slice(Math.max(0, latestUiIdentityIndex)).includes(
      "[STS2 Platform Live UI] toggle; input=K; visible=true")
      ? "exercised"
      : "pending human runtime evidence",
    non_claims: ["loaded_is_not_human_action_evidence", "ui_ready_is_not_ui_visible_evidence"]
  };
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
  if (errors.length) process.exitCode = 1;
}

try {
  if (command === "doctor") doctor();
  else if (command === "deploy") deploy();
  else if (command === "rollback") rollback();
  else if (command === "launch") launch();
  else if (command === "verify-loaded") await verifyLoaded();
  else throw new Error(`Unknown command: ${command}`);
} catch (error) {
  process.stderr.write(`${JSON.stringify({
    status: "error",
    command,
    error: error instanceof Error ? error.message : String(error)
  }, null, 2)}\n`);
  process.exitCode = 1;
}
