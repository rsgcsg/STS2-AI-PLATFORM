import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");
const mod = fs.readFileSync(path.join(root, "PlatformLiveUiMod.cs"), "utf8");
const client = fs.readFileSync(path.join(root, "PlatformLiveStatusClient.cs"), "utf8");
const contracts = fs.readFileSync(path.join(root, "PlatformLiveContracts.cs"), "utf8");

test("Live UI remains a non-authorizing hidden overlay", () => {
  assert.match(mod, /Visible = false/u);
  assert.match(mod, /Key\.K/u);
  assert.match(mod, /tree\.ProcessFrame \+= _processFrameHandler/u);
  assert.match(mod, /Input\.IsPhysicalKeyPressed/u);
  assert.match(mod, /Key\.Escape/u);
  assert.doesNotMatch(`${mod}\n${client}`, /player-environment\/actions/u);
  assert.doesNotMatch(`${mod}\n${client}`, /bound_action_id/u);
  assert.doesNotMatch(`${mod}\n${client}`, /Input\.ParseInputEvent|InputEventMouseMotion/u);
  assert.doesNotMatch(mod, /override void _(Ready|Process|Input)/u);
  assert.doesNotMatch(`${mod}\n${client}`, /Key\.F\d+/u);
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

test("Human Data controls use only the typed RecordingService application boundary", () => {
  assert.match(client, /RecordingApplicationService\.Instance\.QueryStatus\(\)/u);
  assert.match(mod, /RecordingApplicationService\.Instance\.Execute\(command\)/u);
  assert.match(mod, /RecordingCommandKind\.StartNewSession/u);
  assert.doesNotMatch(`${mod}\n${client}`, /RecorderRuntime|HumanActionScope|AppendDecision/u);
});

test("Live UI has no standalone packaging or deployment authority", () => {
  for (const file of ["build.mjs", "deploy.mjs", "mod_manifest.json", "STS2PlatformLiveUi.csproj"]) {
    assert.equal(fs.existsSync(path.join(root, file)), false);
  }
});
