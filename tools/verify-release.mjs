#!/usr/bin/env node
import { existsSync, readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { resolveGameDir, resolveModsDir } from "./steam-paths.mjs";

function sha256(file) {
  return existsSync(file)
    ? createHash("sha256").update(readFileSync(file)).digest("hex")
    : null;
}

export function evaluateReleaseIdentity({ identity, installedSha, capabilities }) {
  const host = capabilities?.host;
  const loaded = host?.implementation;
  const errors = [];
  if (!installedSha) errors.push("installed_artifact_missing");
  if (installedSha && installedSha !== identity.artifact_sha256) errors.push("built_installed_sha_mismatch");
  if (loaded?.artifact_sha256 !== identity.artifact_sha256) errors.push("built_loaded_sha_mismatch");
  if (loaded?.module_version_id !== identity.artifact_mvid) errors.push("built_loaded_mvid_mismatch");
  if (loaded?.source_revision !== identity.source_revision) errors.push("source_loaded_revision_mismatch");
  if (capabilities?.protocol_version !== identity.source_protocol) errors.push("source_loaded_protocol_mismatch");
  return {
    ok: errors.length === 0,
    errors,
    source_revision: identity.source_revision,
    protocol: identity.source_protocol,
    built_sha256: identity.artifact_sha256,
    installed_sha256: installedSha,
    loaded_sha256: loaded?.artifact_sha256 ?? null,
    built_mvid: identity.artifact_mvid,
    loaded_mvid: loaded?.module_version_id ?? null,
    runtime_instance_id: host?.runtime_instance_id ?? null,
    game: capabilities?.game ?? null,
    loaded: errors.length === 0
  };
}

function option(args, name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : null;
}

async function main() {
  const args = process.argv.slice(2);
  if (args.includes("--help")) {
    console.log("Usage: node tools/verify-release.mjs [--game-dir DIR] [--endpoint URL]");
    return;
  }
  const releaseRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  const payload = path.join(releaseRoot, "payload");
  const identity = JSON.parse(readFileSync(path.join(payload, "build-identity.json"), "utf8"));
  const gameDir = resolveGameDir(
    option(args, "--game-dir")
      ? { ...process.env, STS2_GAME_DIR: option(args, "--game-dir") }
      : process.env
  );
  const endpoint = option(args, "--endpoint") ?? process.env.STS2_API_URL ?? "http://127.0.0.1:15526";
  const response = await fetch(`${endpoint}/api/player-environment/capabilities`);
  if (!response.ok) throw new Error(`Connector capabilities returned HTTP ${response.status}.`);
  const body = await response.json();
  const result = evaluateReleaseIdentity({
    identity,
    installedSha: sha256(path.join(resolveModsDir(gameDir), "STS2_MCP.dll")),
    capabilities: body.data ?? body
  });
  console.log(JSON.stringify(result, null, 2));
  if (!result.ok) process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  });
}
