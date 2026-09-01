import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");
const mod = fs.readFileSync(path.join(root, "PlatformLiveUiMod.cs"), "utf8");
const client = fs.readFileSync(path.join(root, "PlatformLiveStatusClient.cs"), "utf8");
const contracts = fs.readFileSync(path.join(root, "PlatformLiveContracts.cs"), "utf8");

test("Live UI remains a non-authorizing hidden overlay", () => {
  assert.match(mod, /Visible = true/u);
  assert.match(mod, /MouseFilterEnum\.Ignore/u);
  assert.match(mod, /_workspace\.Visible = !_workspace\.Visible/u);
  assert.match(mod, /Key\.K/u);
  assert.match(mod, /tree\.ProcessFrame \+= _processFrameHandler/u);
  assert.match(mod, /Input\.IsPhysicalKeyPressed/u);
  assert.match(mod, /Key\.Escape/u);
  assert.doesNotMatch(`${mod}\n${client}`, /player-environment\/actions/u);
  assert.doesNotMatch(`${mod}\n${client}`, /bound_action_id/u);
  assert.match(mod, /InputEventMouseMotion/u);
  assert.match(mod, /OnWorkspaceInput/u);
  assert.match(mod, /OnRecorderInput/u);
  assert.doesNotMatch(mod, /override void _(Ready|Process|Input)/u);
  assert.doesNotMatch(`${mod}\n${client}`, /Key\.F\d+/u);
});

test("Workspace is bounded, click-through outside controls, and locally persistent", () => {
  assert.match(mod, /Root.*MouseFilterEnum\.Ignore/su);
  assert.match(mod, /CustomMinimumSize = new Vector2\(560, 360\)/u);
  assert.match(mod, /ClampWorkspace/u);
  assert.match(mod, /ClampRecorder/u);
  assert.match(mod, /ResetLayout/u);
  assert.match(mod, /PlatformLiveLayout\.Load\(\)/u);
  assert.match(mod, /PlatformLiveLayout\.Save\(/u);
  assert.match(fs.readFileSync(path.join(root, "PlatformLiveUiPresentation.cs"), "utf8"), /LocalApplicationData/u);
  assert.match(fs.readFileSync(path.join(root, "PlatformLiveUiPresentation.cs"), "utf8"), /fail-soft/u);
});

test("one presentation owner prevents legacy and workspace shells from overlapping", () => {
  assert.equal((mod.match(/new CanvasLayer/g) ?? []).length, 1);
  assert.equal((mod.match(/BuildRecorderCard\(\);/g) ?? []).length, 1);
  assert.match(mod, /_workspaceSurface\.AddChild\(_recorderCard\)/u);
  assert.match(mod, /_workspaceSurface\.AddChild\(_toastStack\)/u);
  assert.match(mod, /_workspaceSurface\.GuiInput \+= OnWorkspaceInput/u);
  assert.doesNotMatch(mod, /Root\.AddChild\(_recorderCard\)/u);
  assert.match(mod, /_hud\.Visible = !workspaceVisible/u);
  assert.match(mod, /_recorderCard\.Visible = workspaceVisible/u);
  assert.match(mod, /_toastStack\.Visible = workspaceVisible/u);
  assert.match(mod, /ApplyPresentationVisibility\(\)/u);
});

test("Recorder and policy controls expose typed state and fail-closed reasons", () => {
  assert.match(mod, /ApplyRecordingAvailability/u);
  assert.match(mod, /RecordingLifecycleState\.Recording/u);
  assert.match(mod, /PolicyUnavailableReason/u);
  assert.match(mod, /PushToast\("policy\.error"/u);
  assert.match(mod, /PushToast\(/u);
  assert.match(mod, /Human is the safe default/u);
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
  assert.match(mod, /QueryStatus\(\)/u);
  assert.match(mod, /Native-accepted but failed closed \(not recorded\)/u);
  assert.match(mod, /Declared out of scope/u);
});

test("Live UI has no standalone packaging or deployment authority", () => {
  for (const file of ["build.mjs", "deploy.mjs", "mod_manifest.json", "STS2PlatformLiveUi.csproj"]) {
    assert.equal(fs.existsSync(path.join(root, file)), false);
  }
});
