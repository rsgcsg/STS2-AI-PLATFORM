#!/usr/bin/env node
import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import os from "node:os";
import path from "node:path";
import {
  clientDependenciesAvailable,
  configurePlayerEnvironmentEvidenceProfile,
  evaluateEnvironmentReadiness,
  evaluateLoadedArtifact,
  inspectModInstallation,
  resolveModsDir,
  sourceProtocols,
  windowsTaskListHasGame
} from "./connector.mjs";
import {
  canonicalSourceBytes,
  readInstalledProvenance,
  sourceRevisionForFiles
} from "./connector-provenance.mjs";

assert.deepEqual(sourceProtocols(), { csharp: "1.0.0", client: "1.0.0" });
assert.equal(clientDependenciesAvailable(), true);
assert.equal(resolveModsDir("C:\\Game", "win32"), "C:\\Game\\mods");
assert.equal(windowsTaskListHasGame('"SlayTheSpire2.exe","123"'), true);
assert.equal(windowsTaskListHasGame('"steam.exe","123"'), false);
assert.equal(canonicalSourceBytes(Buffer.from("first\r\nsecond\n")).toString("utf8"), "first\nsecond\n");

const provenanceRepository = mkdtempSync(path.join(os.tmpdir(), "sts2-connector-provenance-"));
try {
  const connectorRoot = path.join(provenanceRepository, "components", "connector");
  mkdirSync(path.join(connectorRoot, "host"), { recursive: true });
  mkdirSync(path.join(connectorRoot, "tools"), { recursive: true });
  execFileSync("git", ["init", "-q"], { cwd: provenanceRepository });
  execFileSync("git", ["config", "user.name", "Connector Test"], { cwd: provenanceRepository });
  execFileSync("git", ["config", "user.email", "connector-test@example.invalid"], { cwd: provenanceRepository });
  writeFileSync(path.join(connectorRoot, "host", "Native.cs"), "internal sealed class Native {}\n");
  execFileSync("git", ["add", "."], { cwd: provenanceRepository });
  execFileSync("git", ["commit", "-qm", "native"], { cwd: provenanceRepository });
  const nativeRevision = execFileSync("git", ["rev-parse", "HEAD"], {
    cwd: provenanceRepository,
    encoding: "utf8"
  }).trim();
  writeFileSync(path.join(connectorRoot, "tools", "doctor.mjs"), "export const doctor = true;\n");
  execFileSync("git", ["add", "."], { cwd: provenanceRepository });
  execFileSync("git", ["commit", "-qm", "tooling"], { cwd: provenanceRepository });
  assert.equal(sourceRevisionForFiles(connectorRoot, ["host/Native.cs"]), nativeRevision);
} finally {
  rmSync(provenanceRepository, { recursive: true, force: true });
}

const capabilities = {
  protocol_version: "1.0.0",
  execution_available: true,
  host: {
    runtime_instance_id: "runtime-1",
    implementation: {
      artifact_sha256: "a".repeat(64),
      module_version_id: "mvid-1",
      source_revision: "b".repeat(40)
    }
  },
  game: {
    compatibility: { observation_allowed: true },
    modset: { status: "exact_player_environment_only" }
  }
};
assert.equal(evaluateLoadedArtifact({
  csharpProtocol: "1.0.0",
  clientProtocol: "1.0.0",
  builtSha: "a".repeat(64),
  installedSha: "a".repeat(64),
  builtMvid: "mvid-1",
  installedMvid: "mvid-1",
  builtSourceRevision: "b".repeat(40),
  capabilities
}).ok, true);
assert.deepEqual(evaluateEnvironmentReadiness(capabilities, "1.0.0").blockers, []);
assert.deepEqual(
  evaluateEnvironmentReadiness(capabilities, "1.0-rc.2").blockers,
  [
    "unsupported_player_environment_protocol",
    "player_snapshot_disabled",
    "player_input_delivery_disabled"
  ]
);

const temporary = mkdtempSync(path.join(os.tmpdir(), "sts2-connector-cli-"));
try {
  mkdirSync(path.join(temporary, "backup"));
  writeFileSync(path.join(temporary, "STS2_MCP.json"), JSON.stringify({ id: "STS2_MCP", version: "1" }));
  writeFileSync(path.join(temporary, "backup", "duplicate.json"), JSON.stringify({ id: "STS2_MCP", version: "old" }));
  const installation = inspectModInstallation(temporary);
  assert.equal(installation.duplicate_installation_blocker, true);
  assert.equal(installation.duplicate_manifests.length, 1);
  const configPath = path.join(temporary, "STS2_MCP.conf");
  writeFileSync(configPath, JSON.stringify({
    port: 15526,
    permission_mode: "retired",
    qualification_store: "retired.json",
    human_equivalence_enabled: true
  }));
  configurePlayerEnvironmentEvidenceProfile(configPath, true);
  assert.deepEqual(JSON.parse(readFileSync(configPath, "utf8")), {
    port: 15526,
    player_environment_native_page_evidence_enabled: true
  });

  const sidecarPath = path.join(temporary, "STS2_MCP.identity");
  const workspaceRecordPath = path.join(temporary, "workspace-installation.json");
  writeFileSync(workspaceRecordPath, JSON.stringify({ source_revision: "stale-workspace" }));
  assert.deepEqual(readInstalledProvenance(sidecarPath, workspaceRecordPath), {
    metadata: { source_revision: "stale-workspace" },
    location: "workspace_record"
  });
  writeFileSync(sidecarPath, JSON.stringify({ source_revision: "release-sidecar" }));
  assert.deepEqual(readInstalledProvenance(sidecarPath, workspaceRecordPath), {
    metadata: { source_revision: "release-sidecar" },
    location: "installed_sidecar"
  });
} finally {
  rmSync(temporary, { recursive: true, force: true });
}

console.log("connector CLI checks passed");
