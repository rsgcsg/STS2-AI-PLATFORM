import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");
const mod = fs.readFileSync(path.join(root, "PlatformLiveUiMod.cs"), "utf8");
const client = fs.readFileSync(path.join(root, "PlatformLiveStatusClient.cs"), "utf8");
const contracts = fs.readFileSync(path.join(root, "PlatformLiveContracts.cs"), "utf8");
const feed = fs.readFileSync(path.join(root, "PlatformLiveActionFeed.cs"), "utf8");

test("Live UI remains a non-authorizing hidden overlay", () => {
  assert.match(mod, /internal Control Root.*Visible = true/su);
  assert.doesNotMatch(mod, /_hud|_hudText|HUD=visible/u);
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
  assert.doesNotMatch(mod, /override void _(Ready|Process|Input)/u);
  assert.doesNotMatch(`${mod}\n${client}`, /Key\.F\d+/u);
});

test("Workspace is bounded, click-through outside controls, and locally persistent", () => {
  assert.match(mod, /Root.*MouseFilterEnum\.Ignore/su);
  assert.match(mod, /CustomMinimumSize = new Vector2\(560, 360\)/u);
  assert.match(mod, /ClampWorkspace/u);
  assert.match(mod, /ResetLayout/u);
  assert.match(mod, /PlatformLiveLayout\.Load\(\)/u);
  assert.match(mod, /PlatformLiveLayout\.Save\(/u);
  assert.match(fs.readFileSync(path.join(root, "PlatformLiveUiPresentation.cs"), "utf8"), /LocalApplicationData/u);
  assert.match(fs.readFileSync(path.join(root, "PlatformLiveUiPresentation.cs"), "utf8"), /fail-soft/u);
});

test("compact layout keeps content width and reset presentation-only", () => {
  const presentation = fs.readFileSync(path.join(root, "PlatformLiveUiPresentation.cs"), "utf8");
  assert.match(presentation, /CurrentVersion = 2/u);
  assert.match(presentation, /new Vector2\(660, 440\)/u);
  assert.match(mod, /Math\.Max\(360, _workspace\.Size\.X - 32\)/u);
  assert.match(mod, /CustomMinimumSize = new Vector2\(360, 220\)/u);
  assert.match(mod, /_layout = _defaultLayout/u);
  assert.doesNotMatch(mod, /RecordingApplicationService\.Instance\.Execute.*ResetLayout/su);
});

test("one presentation owner prevents legacy and workspace shells from overlapping", () => {
  assert.equal((mod.match(/new CanvasLayer/g) ?? []).length, 1);
  assert.equal((mod.match(/BuildRecorderPage\(tabs\);/g) ?? []).length, 1);
  assert.match(mod, /tabs\.AddChild\(_recorderPage\)/u);
  assert.match(mod, /_workspaceSurface\.AddChild\(_toastStack\)/u);
  assert.match(mod, /_workspaceSurface\.GuiInput \+= OnWorkspaceInput/u);
  assert.doesNotMatch(mod, /_recorderPage\.Position/u);
  assert.doesNotMatch(mod, /Root\.AddChild\(_hud\)/u);
  assert.match(mod, /_toastStack\.Visible = workspaceVisible/u);
  assert.match(mod, /ApplyPresentationVisibility\(\)/u);
});

test("Recorder is the default Workspace tab and never a floating overlay", () => {
  assert.match(mod, /Name = "Recorder"/u);
  assert.match(mod, /BuildRecorderPage\(tabs\)/u);
  assert.match(mod, /_tabs\.CurrentTab = 0/u);
  assert.match(mod, /tabs\.CurrentTab = Math\.Clamp\(_layout\.LastPage, 0, 5\)/u);
  assert.match(mod, /_recorderPage\.AddChild\(/u);
  assert.doesNotMatch(mod, /_workspaceSurface\.AddChild\(_recorderPage\)/u);
  assert.doesNotMatch(mod, /_recorderPage\.GuiInput/u);
});

test("Recorder owns a bounded canonical Action Feed with explicit unavailable fields", () => {
  assert.match(mod, /QueryEvents\(/u);
  assert.match(mod, /RefreshActionFeed\(status\.Recording\)/u);
  assert.match(mod, /_recorderDetails\.AddChild\(_actionFeedScroll\)/u);
  assert.match(mod, /_actionFeedScroll = new ScrollContainer/u);
  assert.match(mod, /SizeFlagsHorizontal = SizeFlags\.ExpandFill/u);
  assert.match(mod, /while \(_actionFeed\.Count > PlatformLiveActionFeed\.MaxEntries\)/u);
  assert.match(mod, /value\.SessionId, sessionId/u);
  assert.match(mod, /sessionId == null/u);
  assert.match(feed, /DecisionPending => "Observed"/u);
  assert.match(feed, /DecisionRecorded => "Recorded"/u);
  assert.match(feed, /DecisionInvalidated => "Invalidated"/u);
  assert.match(feed, /SubjectReferentId/u);
  assert.match(feed, /Arguments/u);
  assert.match(feed, /EffectSummary/u);
  assert.match(feed, /unavailable \(not present in canonical evidence\)/u);
  assert.match(client, /RecordingApplicationService\.Instance\.QueryStatus\(\)/u);
  assert.doesNotMatch(feed, /InputEventMouseButton|InputEventMouseMotion|frame.*delay/isu);
});

test("canonical action fixtures cover ordinary, targeted, choice and end-turn shapes", () => {
  assert.match(feed, /FormatEntry\(RecordingEvent value\)/u);
  assert.match(feed, /FormatDetail\(RecordingEvent value\)/u);
  assert.match(feed, /Stable subject\/card ID/u);
  assert.match(feed, /Target IDs/u);
  assert.match(feed, /Card \/ choice display/u);
  assert.match(feed, /Action kind/u);
  assert.match(feed, /RecordingEventKind\.DecisionPending/u);
  assert.match(feed, /RecordingEventKind\.DecisionRecorded/u);
  assert.match(feed, /RecordingEventKind\.DecisionInvalidated/u);
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
  assert.match(mod, /Scope:/u);
});

test("Live UI has no standalone packaging or deployment authority", () => {
  for (const file of ["build.mjs", "deploy.mjs", "mod_manifest.json", "STS2PlatformLiveUi.csproj"]) {
    assert.equal(fs.existsSync(path.join(root, file)), false);
  }
});
