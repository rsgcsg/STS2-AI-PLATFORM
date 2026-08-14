#!/usr/bin/env node
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import {
  mkdtempSync,
  mkdirSync,
  readFileSync,
  rmSync,
  symlinkSync,
  writeFileSync
} from "node:fs";
import { spawnSync } from "node:child_process";
import os from "node:os";
import path from "node:path";
import { installRelease, rollbackRelease } from "./install-release.mjs";
import { evaluateReleaseIdentity } from "./verify-release.mjs";

const root = mkdtempSync(path.join(os.tmpdir(), "sts2-connector-release-"));
try {
  const releaseRoot = path.join(root, "release");
  const payload = path.join(releaseRoot, "payload");
  const gameDir = path.join(root, "game");
  const modsDir = path.join(gameDir, "mods");
  const backups = path.join(root, "backups");
  mkdirSync(payload, { recursive: true });
  mkdirSync(modsDir, { recursive: true });
  writeFileSync(path.join(payload, "STS2_MCP.dll"), "new-host");
  writeFileSync(path.join(payload, "STS2_MCP.json"), "{\"id\":\"STS2_MCP\"}\n");
  const artifactSha = createHash("sha256").update("new-host").digest("hex");
  const identity = {
    source_revision: "a".repeat(40),
    source_protocol: "1.0.0",
    artifact_sha256: artifactSha,
    artifact_mvid: "11111111-2222-3333-4444-555555555555"
  };
  writeFileSync(path.join(payload, "build-identity.json"), JSON.stringify(identity));
  writeFileSync(path.join(modsDir, "STS2_MCP.dll"), "old-host");
  writeFileSync(path.join(modsDir, "STS2_MCP.json"), "{\"id\":\"STS2_MCP\",\"version\":\"old\"}\n");

  const installed = installRelease({
    releaseRoot,
    gameDir,
    platform: "linux",
    backupRoot: backups,
    now: () => new Date("2026-08-14T00:00:00.000Z"),
    processRunning: () => false
  });
  assert.equal(readFileSync(path.join(modsDir, "STS2_MCP.dll"), "utf8"), "new-host");
  assert.equal(installed.installed_sha256, artifactSha);

  const capabilities = {
    protocol_version: identity.source_protocol,
    host: {
      runtime_instance_id: "runtime",
      implementation: {
        artifact_sha256: artifactSha,
        module_version_id: identity.artifact_mvid,
        source_revision: identity.source_revision
      }
    },
    game: { version: "test" }
  };
  assert.equal(evaluateReleaseIdentity({ identity, installedSha: artifactSha, capabilities }).ok, true);
  assert.deepEqual(
    evaluateReleaseIdentity({ identity, installedSha: "wrong", capabilities }).errors,
    ["built_installed_sha_mismatch"]
  );

  const rolledBack = rollbackRelease({
    backup: installed.rollback_backup,
    processRunning: () => false
  });
  assert.equal(rolledBack.status, "rollback_restored_game_must_be_cold_started");
  assert.equal(readFileSync(path.join(modsDir, "STS2_MCP.dll"), "utf8"), "old-host");

  const linkedVerifier = path.join(root, "verify-link.mjs");
  symlinkSync(path.resolve("tools/verify-release.mjs"), linkedVerifier);
  const help = spawnSync(process.execPath, [linkedVerifier, "--help"], { encoding: "utf8" });
  assert.equal(help.status, 0);
  assert.match(help.stdout, /Usage: node tools\/verify-release\.mjs/u);
} finally {
  rmSync(root, { recursive: true, force: true });
}

console.log("release install, rollback and identity checks passed");
