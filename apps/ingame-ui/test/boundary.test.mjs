import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");
const mod = fs.readFileSync(path.join(root, "PlatformLiveUiMod.cs"), "utf8");
const client = fs.readFileSync(path.join(root, "PlatformLiveStatusClient.cs"), "utf8");
const contracts = fs.readFileSync(path.join(root, "PlatformLiveContracts.cs"), "utf8");
const deploy = fs.readFileSync(path.join(root, "deploy.mjs"), "utf8");
const build = fs.readFileSync(path.join(root, "build.mjs"), "utf8");
const manifest = JSON.parse(fs.readFileSync(path.join(root, "mod_manifest.json"), "utf8"));

test("Live UI remains a non-authorizing hidden overlay", () => {
  assert.match(mod, /Visible = false/u);
  assert.match(mod, /Key\.F10/u);
  assert.match(mod, /Key\.Escape/u);
  assert.doesNotMatch(`${mod}\n${client}`, /player-environment\/actions/u);
  assert.doesNotMatch(`${mod}\n${client}`, /bound_action_id/u);
  assert.doesNotMatch(`${mod}\n${client}`, /Input\.ParseInputEvent|InputEventMouseMotion/u);
  assert.equal(manifest.affects_gameplay, false);
});

test("Policy view uses only the typed Policy Runtime status", () => {
  assert.match(client, /sts2\.policy-runtime\/http-1/u);
  assert.match(contracts, /PolicyRuntime/u);
  assert.doesNotMatch(contracts, /ReadScoreNodes|Contains\("score"/u);
});

test("Connector capabilities, snapshot, and controller merge only after coherence checks", () => {
  assert.match(contracts, /EnsureConnectorCoherence\(/u);
  assert.match(contracts, /capabilities\.Host\.RuntimeInstanceId/u);
  assert.match(contracts, /snapshot\.Session\.RuntimeInstanceId/u);
  assert.match(contracts, /controller\.RuntimeInstanceId/u);
  assert.match(contracts, /capabilities\.EnvironmentFingerprint/u);
  assert.match(contracts, /snapshot\.Session\.EnvironmentFingerprint/u);
  assert.match(contracts, /complete coherent response set/u);
  assert.match(client, /PlatformLiveStatusProjection\.EnsureConnectorCoherence/u);
  assert.match(client, /capabilities = null;/u);
  assert.match(client, /snapshot = null;/u);
  assert.match(client, /controller = null;/u);
});

test("environment Reads and Annotator identity do not depend on a running policy", () => {
  assert.match(contracts, /snapshot\.Reads/u);
  assert.match(contracts, /recording\.Environment\?\.Annotator/u);
  assert.match(contracts, /advertised by Connector Snapshot/u);
});

test("deployment is source-bound, reversible, and keeps loaded identity distinct", () => {
  assert.match(deploy, /Fully close Slay the Spire 2 before deployment/u);
  assert.match(deploy, /build provenance differs from current exact source/u);
  assert.match(deploy, /rollback-manifest\.json/u);
  assert.match(deploy, /location: "local"/u);
  assert.match(deploy, /path\.basename\(installedProvenance\)/u);
  assert.match(deploy, /rollbackTarget\(/u);
  assert.match(deploy, /rollback directory is outside the local deployment archive/u);
  assert.match(deploy, /installed\.schema !== "sts2\.platform\/live-ui-installed-provenance-1"/u);
  assert.match(deploy, /fs\.rmSync\(target, \{ force: true \}\)/u);
  assert.match(deploy, /loaded_installed_sha_mismatch/u);
  assert.match(deploy, /loaded_installed_mvid_mismatch/u);
  assert.match(deploy, /loaded_identity_precedes_install/u);
});

test("Live UI builds dependencies by default and rejects stale ui-only builds", () => {
  assert.match(build, /--ui-only is disabled/u);
  assert.match(build, /runComponentBuild\(connectorRoot\);/u);
  assert.match(build, /runComponentBuild\(annotatorRoot\);/u);
  assert.match(build, /Component-owned build entrypoints are the only provenance authorities/u);
  assert.doesNotMatch(build, /run\(connectorProject\)|run\(annotatorProject/u);
  assert.doesNotMatch(build, /if \(!uiOnly\)/u);
});
