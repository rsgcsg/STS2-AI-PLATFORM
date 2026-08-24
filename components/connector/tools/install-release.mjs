#!/usr/bin/env node
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  realpathSync,
  rmSync,
  writeFileSync
} from "node:fs";
import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { resolveGameDir, resolveModsDir } from "./steam-paths.mjs";

const FILES = [
  { payload: "STS2_MCP.dll", installed: "STS2_MCP.dll" },
  { payload: "STS2_MCP.json", installed: "STS2_MCP.json" },
  { payload: "build-identity.json", installed: "STS2_MCP.identity" }
];

function sha256(file) {
  return existsSync(file)
    ? createHash("sha256").update(readFileSync(file)).digest("hex")
    : null;
}

export function gameProcessRunning(platform = process.platform) {
  const result = platform === "win32"
    ? spawnSync("tasklist", ["/FO", "CSV", "/NH"], { encoding: "utf8", stdio: "pipe" })
    : spawnSync("ps", ["-Ao", "comm="], { encoding: "utf8", stdio: "pipe" });
  if (result.status !== 0) return true;
  return processListContainsGame(result.stdout, platform);
}

export function processListContainsGame(output, platform = process.platform) {
  return output.split(/\r?\n/u).some((line) => {
    const executable = platform === "win32"
      ? line.split("\",")[0]?.replace(/^"|"$/gu, "").trim()
      : path.basename(line.trim());
    return /^(?:SlayTheSpire2|Slay the Spire 2)(?:\.exe)?$/iu.test(executable ?? "");
  });
}

export function installRelease({
  releaseRoot,
  gameDir,
  platform = process.platform,
  backupRoot = path.join(os.homedir(), ".sts2-connector", "backups"),
  now = () => new Date(),
  processRunning = () => gameProcessRunning(platform)
}) {
  if (processRunning()) throw new Error("Close Slay the Spire 2 before installing the Connector.");
  const payload = path.join(releaseRoot, "payload");
  for (const { payload: name } of FILES) {
    if (!existsSync(path.join(payload, name))) throw new Error(`Release payload is missing ${name}.`);
  }
  const modsDir = resolveModsDir(gameDir, platform);
  mkdirSync(modsDir, { recursive: true });
  const stamp = now().toISOString().replace(/[:.]/gu, "-");
  const backup = path.join(backupRoot, stamp);
  mkdirSync(backup, { recursive: true });
  const previous = {};
  for (const file of FILES) {
    const destination = path.join(modsDir, file.installed);
    previous[file.installed] = existsSync(destination);
    if (previous[file.installed]) {
      copyFileSync(destination, path.join(backup, file.installed));
    }
    copyFileSync(path.join(payload, file.payload), destination);
  }
  const identity = JSON.parse(readFileSync(path.join(payload, "build-identity.json"), "utf8"));
  const deployment = {
    schema_version: 1,
    installed_at: now().toISOString(),
    game_dir: gameDir,
    mods_dir: modsDir,
    previous,
    installed_sha256: sha256(path.join(modsDir, "STS2_MCP.dll")),
    source_revision: identity.source_revision,
    artifact_mvid: identity.artifact_mvid,
    protocol: identity.source_protocol
  };
  writeFileSync(path.join(backup, "deployment.json"), `${JSON.stringify(deployment, null, 2)}\n`);
  return {
    status: "installed_game_must_be_cold_started",
    ...deployment,
    rollback_backup: backup,
    loaded: "non_claim"
  };
}

export function rollbackRelease({ backup, processRunning = () => gameProcessRunning() }) {
  if (processRunning()) throw new Error("Close Slay the Spire 2 before rolling back the Connector.");
  const deploymentFile = path.join(backup, "deployment.json");
  if (!existsSync(deploymentFile)) throw new Error("Rollback backup is missing deployment.json.");
  const deployment = JSON.parse(readFileSync(deploymentFile, "utf8"));
  for (const file of FILES) {
    const destination = path.join(deployment.mods_dir, file.installed);
    if (deployment.previous?.[file.installed]) {
      copyFileSync(path.join(backup, file.installed), destination);
    }
    else rmSync(destination, { force: true });
  }
  return {
    status: "rollback_restored_game_must_be_cold_started",
    backup,
    installed_sha256: sha256(path.join(deployment.mods_dir, "STS2_MCP.dll")),
    loaded: "non_claim"
  };
}

function option(args, name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : null;
}

async function main() {
  const args = process.argv.slice(2);
  if (args.includes("--help")) {
    console.log("Usage: node tools/install-release.mjs [--game-dir DIR] [--rollback BACKUP_DIR]");
    return;
  }
  const rollback = option(args, "--rollback");
  const releaseRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  const result = rollback
    ? rollbackRelease({ backup: path.resolve(rollback) })
    : installRelease({
        releaseRoot,
        gameDir: resolveGameDir(
          option(args, "--game-dir")
            ? { ...process.env, STS2_GAME_DIR: option(args, "--game-dir") }
            : process.env
        )
      });
  console.log(JSON.stringify(result, null, 2));
}

if (process.argv[1]
    && realpathSync(fileURLToPath(import.meta.url)) === realpathSync(process.argv[1])) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  });
}
