#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { spawnSync } from "node:child_process";

import {
  loadHostRuntimeWorkstationApi,
  resolveWorkstationInstallation
} from "../../components/annotator/tools/workstation-platform.mjs";
import { componentGitFiles, componentGitState } from "../../tools/component-git.mjs";

const appRoot = import.meta.dirname;
const platformRoot = path.resolve(appRoot, "../..");
const annotatorRoot = path.join(platformRoot, "components/annotator");
const localRoot = path.join(appRoot, ".local");
const outputRoot = path.join(appRoot, "bin/Release/net9.0");
const builtDll = path.join(outputRoot, "STS2_PLATFORM_LIVE_UI.dll");
const buildProvenance = path.join(outputRoot, "build-provenance.json");
const manifestSource = path.join(appRoot, "mod_manifest.json");
const identityTool = path.join(
  annotatorRoot,
  "src/STS2HumanAnnotator.Tool/bin/Release/net9.0/sts2-human-annotator.dll"
);
const hostApi = await loadHostRuntimeWorkstationApi(annotatorRoot);
const installation = resolveWorkstationInstallation({ headlessApi: hostApi });
const installedDll = path.join(installation.mods_dir, "STS2_PLATFORM_LIVE_UI.dll");
const installedManifest = path.join(installation.mods_dir, "STS2_PLATFORM_LIVE_UI.json");
const installedProvenance = path.join(localRoot, "installed-provenance.json");
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
  if (!fs.existsSync(identityTool)) throw new Error("Build the Annotator identity tool first.");
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

function rollbackTarget(entry) {
  if (entry.location === "mods") return path.join(installation.mods_dir, entry.name);
  if (entry.location === "local" && entry.name === path.basename(installedProvenance)) {
    return installedProvenance;
  }
  throw new Error(`Unsupported Live UI rollback target: ${entry.location}/${entry.name}`);
}

function rollbackDirectory(value) {
  if (typeof value !== "string" || value.length === 0) {
    throw new Error("Live UI rollback directory is invalid.");
  }
  const resolved = path.resolve(value);
  const deploymentsRoot = path.resolve(localRoot, "deployments");
  if (resolved !== deploymentsRoot && !resolved.startsWith(`${deploymentsRoot}${path.sep}`)) {
    throw new Error("Live UI rollback directory is outside the local deployment archive.");
  }
  return resolved;
}

function readRollbackManifest(file) {
  const manifest = readJson(file);
  if (manifest.schema !== "sts2.platform/live-ui-rollback-1" || !Array.isArray(manifest.files)) {
    throw new Error("Live UI rollback manifest is unsupported.");
  }
  for (const entry of manifest.files) {
    if (entry === null || typeof entry !== "object"
        || !["mods", "local"].includes(entry.location ?? "mods")
        || typeof entry.name !== "string"
        || entry.name.length === 0
        || entry.name === "."
        || entry.name === ".."
        || path.basename(entry.name) !== entry.name
        || typeof entry.existed !== "boolean") {
      throw new Error("Live UI rollback manifest contains an invalid target.");
    }
    rollbackTarget({ ...entry, location: entry.location ?? "mods" });
  }
  return manifest;
}

function sourceDigest() {
  const digest = crypto.createHash("sha256");
  for (const relative of componentGitFiles(appRoot)) {
    digest.update(relative).update("\0");
    digest.update(fs.readFileSync(path.join(appRoot, relative))).update("\0");
  }
  return digest.digest("hex");
}

function gameRunning() {
  if (hostApi) return hostApi.listGameProcesses(process.platform, { failClosed: true }).length > 0;
  if (process.platform === "win32") throw new Error("Strict Windows process detection requires Platform Host Runtime.");
  const result = spawnSync("pgrep", ["-f", "SlayTheSpire2"], { encoding: "utf8" });
  if (result.error || ![0, 1].includes(result.status)) throw new Error("Could not enumerate STS2 processes.");
  return result.status === 0;
}

function requireBuiltProvenance() {
  if (!fs.existsSync(buildProvenance) || !fs.existsSync(builtDll)) throw new Error("Run npm run live-ui:build first.");
  const provenance = readJson(buildProvenance);
  const source = componentGitState(appRoot);
  if (source.componentWorktreeStatus !== "clean") throw new Error("Commit Platform Live UI source before deployment.");
  if (provenance.source.source_revision !== source.componentSourceRevision
      || provenance.source.source_digest_sha256 !== sourceDigest()) {
    throw new Error("Live UI build provenance differs from current exact source.");
  }
  const built = exactIdentity(builtDll);
  if (!sameIdentity(built, provenance.artifact)) throw new Error("Built Live UI differs from build provenance.");
  const installedConnector = exactIdentity(path.join(installation.mods_dir, "STS2_MCP.dll"));
  const installedAnnotator = exactIdentity(path.join(installation.mods_dir, "STS2_HUMAN_ANNOTATOR.dll"));
  if (!sameIdentity(installedConnector, provenance.dependencies.connector)) throw new Error("Installed Connector differs from the Live UI build dependency.");
  if (!sameIdentity(installedAnnotator, provenance.dependencies.annotator)) throw new Error("Installed Annotator differs from the Live UI build dependency.");
  return { provenance, built, installedConnector, installedAnnotator };
}

function doctor() {
  const report = {
    status: fs.existsSync(installation.executable) ? "ok" : "action_required",
    platform: process.platform,
    architecture: process.arch,
    game_running: gameRunning(),
    installation,
    source: (() => { try { return componentGitState(appRoot); } catch { return null; } })(),
    built: fs.existsSync(builtDll) ? exactIdentity(builtDll) : null,
    installed: fs.existsSync(installedDll) ? exactIdentity(installedDll) : null,
    build_provenance: fs.existsSync(buildProvenance) ? readJson(buildProvenance) : null,
    installed_provenance: fs.existsSync(installedProvenance) ? readJson(installedProvenance) : null,
    non_claims: ["doctor_is_read_only", "installed_is_not_loaded", "loaded_is_not_policy_or_human_evidence"]
  };
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
}

function deploy() {
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before deployment.");
  const exact = requireBuiltProvenance();
  fs.mkdirSync(installation.mods_dir, { recursive: true });
  const backup = path.join(localRoot, "deployments", new Date().toISOString().replaceAll(":", "-"));
  fs.mkdirSync(backup, { recursive: true });
  const files = [
    { location: "mods", source: installedDll, name: path.basename(installedDll) },
    { location: "mods", source: installedManifest, name: path.basename(installedManifest) },
    { location: "local", source: installedProvenance, name: path.basename(installedProvenance) }
  ];
  const state = files.map(({ location, source, name }) => {
    const existed = fs.existsSync(source);
    if (existed) fs.copyFileSync(source, path.join(backup, name));
    return { location, name, existed };
  });
  writeJson(path.join(backup, "rollback-manifest.json"), { schema: "sts2.platform/live-ui-rollback-1", files: state });
  fs.copyFileSync(builtDll, installedDll);
  fs.copyFileSync(manifestSource, installedManifest);
  const installed = {
    schema: "sts2.platform/live-ui-installed-provenance-1",
    installed_at: new Date().toISOString(),
    source: exact.provenance.source,
    game: exact.provenance.game,
    dependencies: exact.provenance.dependencies,
    artifact: exactIdentity(installedDll),
    rollback: backup
  };
  writeJson(installedProvenance, installed);
  process.stdout.write(`${JSON.stringify(installed, null, 2)}\n`);
}

function rollback() {
  if (gameRunning()) throw new Error("Fully close Slay the Spire 2 before rollback.");
  if (!fs.existsSync(installedProvenance)) throw new Error("Live UI installed provenance is unavailable.");
  const installed = readJson(installedProvenance);
  if (installed.schema !== "sts2.platform/live-ui-installed-provenance-1") {
    throw new Error("Live UI installed provenance schema is unsupported.");
  }
  const rollback = rollbackDirectory(installed.rollback);
  const manifest = readRollbackManifest(path.join(rollback, "rollback-manifest.json"));
  for (const entry of manifest.files) {
    const normalizedEntry = { ...entry, location: entry.location ?? "mods" };
    const target = rollbackTarget(normalizedEntry);
    if (normalizedEntry.existed) {
      fs.copyFileSync(path.join(rollback, normalizedEntry.name), target);
    } else {
      fs.rmSync(target, { force: true });
    }
  }
  process.stdout.write(`${JSON.stringify({ status: "restored", rollback }, null, 2)}\n`);
}

function verifyLoaded() {
  if (!gameRunning()) throw new Error("Slay the Spire 2 is not running.");
  if (!fs.existsSync(installedProvenance)) throw new Error("Live UI installed provenance is unavailable.");
  if (!installation.log_file || !fs.existsSync(installation.log_file)) throw new Error("STS2 runtime log is unavailable.");
  const installed = readJson(installedProvenance);
  const prefix = "[STS2 Platform Live UI] identity ";
  const lines = fs.readFileSync(installation.log_file, "utf8").split(/\r?\n/u);
  const identities = lines.flatMap((line) => {
    const index = line.indexOf(prefix);
    if (index < 0) return [];
    try { return [JSON.parse(line.slice(index + prefix.length))]; } catch { return []; }
  });
  const loaded = identities.at(-1);
  const errors = [];
  if (!loaded) errors.push("loaded_identity_log_absent");
  if (loaded?.schema !== "sts2.platform/live-ui-loaded-identity-1") errors.push("loaded_identity_schema_mismatch");
  if (loaded?.artifact_sha256 !== installed.artifact.sha256) errors.push("loaded_installed_sha_mismatch");
  if (loaded?.module_version_id !== installed.artifact.module_version_id) errors.push("loaded_installed_mvid_mismatch");
  if (loaded?.source_revision !== installed.source.source_revision) errors.push("loaded_source_revision_mismatch");
  if (loaded?.source_digest_sha256 !== installed.source.source_digest_sha256) errors.push("loaded_source_digest_mismatch");
  if (!loaded?.loaded_at || Date.parse(loaded.loaded_at) < Date.parse(installed.installed_at)) errors.push("loaded_identity_precedes_install");
  process.stdout.write(`${JSON.stringify({ status: errors.length ? "fail" : "pass", errors, loaded, installed }, null, 2)}\n`);
  if (errors.length) process.exitCode = 1;
}

try {
  if (command === "doctor") doctor();
  else if (command === "deploy") deploy();
  else if (command === "rollback") rollback();
  else if (command === "verify-loaded") verifyLoaded();
  else throw new Error(`Unknown command: ${command}`);
} catch (error) {
  process.stderr.write(`${JSON.stringify({ status: "error", command, error: error instanceof Error ? error.message : String(error) }, null, 2)}\n`);
  process.exitCode = 1;
}
