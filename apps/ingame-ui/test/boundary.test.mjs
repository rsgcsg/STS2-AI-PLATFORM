import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");
const mod = fs.readFileSync(path.join(root, "PlatformLiveUiMod.cs"), "utf8");
const client = fs.readFileSync(path.join(root, "PlatformLiveStatusClient.cs"), "utf8");
const contracts = fs.readFileSync(path.join(root, "PlatformLiveContracts.cs"), "utf8");
const feed = fs.readFileSync(path.join(root, "PlatformLiveActionFeed.cs"), "utf8");
const presentation = fs.readFileSync(path.join(root, "PlatformLiveUiPresentation.cs"), "utf8");

test("Live UI is a non-authorizing hidden overlay", () => {
  assert.match(mod, /internal Control Root.*Visible = true/su);
  assert.match(mod, /Root.*MouseFilterEnum\.Ignore/su);
  assert.match(mod, /_workspace\.Visible = !_workspace\.Visible/u);
  assert.match(mod, /Key\.K/u);
  assert.match(mod, /Key\.Escape/u);
  assert.match(mod, /tree\.ProcessFrame \+= _processFrameHandler/u);
  assert.doesNotMatch(`${mod}\n${client}`, /player-environment\/actions/u);
  assert.doesNotMatch(`${mod}\n${client}`, /bound_action_id/u);
  assert.doesNotMatch(`${mod}\n${client}`, /RecorderRuntime|HumanActionScope|AppendDecision/u);
  assert.doesNotMatch(mod, /override void _(Ready|Process|Input)/u);
});

test("Product navigation exposes exactly Agent Run and Human Recorder", () => {
  assert.match(mod, /new\[\] \{ "Agent Run", "Human Recorder" \}/u);
  assert.match(mod, /BuildAgentRunPage\(_surfaceViewport\)/u);
  assert.match(mod, /BuildRecorderPage\(_surfaceViewport\)/u);
  assert.match(mod, /_surfaces\.Add\(_agentRunPage\)/u);
  assert.match(mod, /_surfaces\.Add\(_recorderPage\)/u);
  assert.doesNotMatch(mod, /Overview|Environment|Human Data|Diagnostics|AddPage/u);
  assert.doesNotMatch(`${mod}\n${presentation}`, /"(Overview|Environment|Human Data|Diagnostics)"|BodyCollapsed|ActiveTab|ToggleActiveTabBody/u);
});

test("Current layout is a fail-soft two-surface state", () => {
  assert.match(presentation, /CurrentVersion = 4/u);
  assert.match(presentation, /string ActiveSurface/u);
  assert.match(presentation, /"agent_run"/u);
  assert.match(presentation, /"human_recorder"/u);
  assert.match(presentation, /live-ui-layout-v4\.json/u);
  assert.match(presentation, /new Vector2\(760, 500\)/u);
  assert.match(mod, /new Vector2\(640, 420\)/u);
  assert.match(presentation, /fail-soft/u);
  assert.doesNotMatch(presentation, /BodyCollapsed|ActiveTab|CollapsedWorkspaceHeight/u);
});

test("Workspace stays bounded and click-through outside its controls", () => {
  assert.match(mod, /CustomMinimumSize = new Vector2\(640, 420\)/u);
  assert.match(mod, /_workspace.*ClipContents = true/su);
  assert.match(mod, /_workspaceSurface\.SetAnchorsAndOffsetsPreset\(LayoutPreset\.FullRect\)/u);
  assert.match(mod, /ClampWorkspace/u);
  assert.match(mod, /PlatformLiveLayout\.Load\(\)/u);
  assert.match(mod, /PlatformLiveLayout\.Save\(/u);
  assert.match(mod, /_workspaceSurface\.GuiInput \+= OnWorkspaceInput/u);
  assert.match(presentation, /viewport\.X - 32/u);
  assert.match(presentation, /viewport\.Y - 48/u);
});

test("Drag and resize use stable global pointer coordinates and persist on release", () => {
  assert.match(mod, /_dragStartPointerGlobal = mouseButton\.GlobalPosition/u);
  assert.match(mod, /_dragStartWorkspaceGlobal = _workspace\.GlobalPosition/u);
  assert.match(mod, /motion\.GlobalPosition - _dragStartPointerGlobal/u);
  assert.match(mod, /_resizeStartPointerGlobal = mouseButton\.GlobalPosition/u);
  assert.match(mod, /motion\.GlobalPosition - _resizeStartPointerGlobal/u);
  assert.match(mod, /if \(_resizingWorkspace \|\| _draggingWorkspace\)\s+PersistLayout\(\)/u);
  assert.doesNotMatch(mod, /motion[\s\S]{0,180}PersistLayout\(\)/u);
});

test("Recorder feed is read-only, RecordId-rooted, readable, and scroll-stable", () => {
  assert.match(mod, /QueryEvents\(/u);
  assert.match(mod, /RefreshActionFeed\(status\.Recording\)/u);
  assert.match(mod, /_actionFeed\.Recent\(PlatformLiveActionFeed\.MaxEntries\)/u);
  assert.match(mod, /feedChanged \|= _actionFeed\.Apply\(value\)/u);
  assert.match(mod, /Text = PlatformLiveActionFeed\.FormatEntry\(value\)/u);
  assert.match(mod, /CustomMinimumSize = new Vector2\(0, 24\)/u);
  assert.match(mod, /VerticalAlignment = VerticalAlignment\.Center/u);
  assert.match(mod, /if \(feedChanged\)\s*\{[\s\S]*?RenderActionFeed\(\);\s*\}/u);
  assert.doesNotMatch(mod, /RenderActionFeed\(\)[\s\S]{0,120}_recorderScroll\.ScrollVertical = 0/u);
  assert.match(feed, /record:\{value\.RecordId\}/u);
  assert.match(feed, /RecordId action root unavailable/u);
  assert.doesNotMatch(feed, /return \(`bound-action:/u);
  assert.match(feed, /RootPending => "… Observed"/u);
  assert.match(feed, /DecisionRecorded => "✓ Recorded"/u);
  assert.match(feed, /DecisionInvalidated => "✕ Invalidated"/u);
  assert.match(feed, /Action unavailable/u);
  assert.match(feed, /SubjectReferentId/u);
  assert.match(feed, /Target IDs/u);
  assert.doesNotMatch(feed, /InputEventMouseButton|InputEventMouseMotion|AppendDecision|Execute\(/isu);
});

test("Session changes may reset scroll, normal feed updates do not", () => {
  assert.match(mod, /_actionFeedSessionId, sessionId/u);
  assert.match(mod, /_actionFeed\.Reset\(\)/u);
  assert.match(mod, /scroll\.ScrollVertical = 0/u);
  assert.match(mod, /ResetLayout[\s\S]*scroll\.ScrollVertical = 0/u);
});

test("Agent Run uses only existing typed Policy Runtime status and controls", () => {
  assert.match(mod, /FormatAgentRun\(status\)/u);
  assert.match(mod, /Policy Runtime: \{status\.PolicyRuntimeTransportStatus\}/u);
  assert.match(mod, /SetRuntimeModeAsync\(mode\)/u);
  assert.match(mod, /TickRuntimeAsync\(\)/u);
  assert.match(mod, /Human is the safe default/u);
  assert.match(mod, /PolicyUnavailableReason/u);
  assert.match(client, /sts2\.policy-runtime\/http-1/u);
  assert.match(contracts, /PolicyRuntime/u);
  assert.doesNotMatch(contracts, /ReadScoreNodes|Contains\("score"/u);
});

test("Recorder controls use the typed application boundary", () => {
  assert.match(mod, /RecordingApplicationService\.Instance\.Execute\(command\)/u);
  assert.match(mod, /RecordingApplicationService\.Instance\.QueryStatus\(\)/u);
  assert.match(mod, /RecordingCommandKind\.StartNewSession/u);
  assert.match(mod, /RecordingLifecycleState\.Recording/u);
  assert.match(mod, /Records \{recording\.Counters\.Records\}/u);
  assert.match(mod, /Pending \{pending\}/u);
  assert.match(mod, /Invalidated \{invalidated\}/u);
});

test("Connector status is merged only after runtime/environment coherence", () => {
  assert.match(contracts, /EnsureConnectorCoherence\(/u);
  assert.match(contracts, /capabilities\.Host\.RuntimeInstanceId/u);
  assert.match(contracts, /snapshot\.Session\.RuntimeInstanceId/u);
  assert.match(contracts, /controller\.RuntimeInstanceId/u);
  assert.match(client, /PlatformLiveStatusProjection\.EnsureConnectorCoherence/u);
  assert.match(client, /capabilities = null;/u);
  assert.match(client, /snapshot = null;/u);
  assert.match(client, /controller = null;/u);
});

test("No standalone packaging/deployment authority exists in Live UI", () => {
  for (const file of ["build.mjs", "deploy.mjs", "mod_manifest.json", "STS2PlatformLiveUi.csproj"]) {
    assert.equal(fs.existsSync(path.join(root, file)), false);
  }
});

test("Current Action Feed lifecycle fixtures pass", () => {
  const result = spawnSync(
    "dotnet",
    ["test", path.join(root, "test", "STS2PlatformLiveUi.Tests.csproj"), "--configuration", "Release", "--nologo"],
    { cwd: root, encoding: "utf8", shell: false },
  );
  assert.equal(result.status, 0, `Action Feed fixtures failed.\n${result.stdout}\n${result.stderr}`);
});
