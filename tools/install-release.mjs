#!/usr/bin/env node
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync
} from "node:fs";
import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import os from "node:os";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { resolveGameDir, resolveModsDir } from "./steam-paths.mjs";

const FILES = ["STS2_MCP.dll", "STS2_MCP.json"];

function sha256(file) {
  return existsSync(file)
    ? createHash("sha256").update(readFileSync(file)).digest("hex")
    : null;
}

export function gameProcessRunning(platform = process.platform) {
  const result = platform === "win32"
    ? spawnSync("tasklist", ["/FO", "CSV", "/NH"], { encoding: "utf8", stdio: "pipe" })
    : spawnSync("ps", ["-Ao", "command="], { encoding: "utf8", stdio: "pipe" });
  if (result.status !== 0) return true;
  return /Slay(?:The| the )Spire2|Slay the Spire 2/iu.test(result.stdout);
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
  for (const name of [...FILES, "build-identity.json"]) {
    if (!existsSync(path.join(payload, name))) throw new Error(`Release payload is missing ${name}.`);
  }
  const modsDir = resolveModsDir(gameDir, platform);
  mkdirSync(modsDir, { recursive: true });
  const stamp = now().toISOString().replace(/[:.]/gu, "-");
  const backup = path.join(backupRoot, stamp);
  mkdirSync(backup, { recursive: true });
  const previous = {};
  for (const name of FILES) {
    const destination = path.join(modsDir, name);
    previous[name] = existsSync(destination);
    if (previous[name]) copyFileSync(destination, path.join(backup, name));
    copyFileSync(path.join(payload, name), destination);
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
  for (const name of FILES) {
    const destination = path.join(deployment.mods_dir, name);
    if (deployment.previous?.[name]) copyFileSync(path.join(backup, name), destination);
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

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  });
}
