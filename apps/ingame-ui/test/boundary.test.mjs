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
  assert.match(mod, /CustomMinimumSize = new Vector2\(640, 420\)/u);
  assert.match(mod, /_workspace.*ClipContents = true/su);
  assert.match(mod, /_workspaceSurface\.SetAnchorsAndOffsetsPreset\(LayoutPreset\.FullRect\)/u);
  assert.match(mod, /ClampWorkspace/u);
  assert.match(mod, /ResetLayout/u);
  assert.match(mod, /PlatformLiveLayout\.Load\(\)/u);
  assert.match(mod, /PlatformLiveLayout\.Save\(/u);
  assert.match(presentation, /LocalApplicationData/u);
  assert.match(presentation, /fail-soft/u);
});

test("responsive layout has bounded small, default, expanded and collapsed geometry", () => {
  assert.match(presentation, /CurrentVersion = 3/u);
  assert.match(presentation, /new Vector2\(760, 500\)/u);
  assert.match(presentation, /new Vector2\(640, 420\)/u);
  assert.match(presentation, /CollapsedWorkspaceHeight = 220/u);
  assert.match(presentation, /viewport\.X - 32/u);
  assert.match(presentation, /viewport\.Y - 48/u);
  assert.doesNotMatch(mod, /_workspaceBody\.(Position|Size)\s*=/u);
  assert.doesNotMatch(mod, /_toastStack\.(Position|Size)\s*=/u);
  assert.doesNotMatch(mod, /CustomMinimumSize = new Vector2\(360, 220\)/u);
  assert.match(mod, /_layout = _defaultLayout/u);
  assert.match(mod, /scroll\.ScrollVertical = 0/u);
  assert.doesNotMatch(mod, /RecordingApplicationService\.Instance\.Execute.*ResetLayout/su);
});

test("one presentation owner prevents legacy and workspace shells from overlapping", () => {
  assert.equal((mod.match(/new CanvasLayer/g) ?? []).length, 1);
  assert.equal((mod.match(/BuildRecorderPage\(_bodyViewport\);/g) ?? []).length, 1);
  assert.match(mod, /bodyViewport\.AddChild\(_recorderPage\)/u);
  assert.match(mod, /_toastViewport\.AddChild\(_toastStack\)/u);
  assert.match(mod, /_workspaceContent\.AddChild\(_toastViewport\)/u);
  assert.match(mod, /_workspaceSurface\.GuiInput \+= OnWorkspaceInput/u);
  assert.doesNotMatch(mod, /_recorderPage\.Position/u);
  assert.doesNotMatch(mod, /Root\.AddChild\(_hud\)/u);
  assert.doesNotMatch(mod, /_workspaceSurface\.AddChild\(_recorderPage\)/u);
  assert.match(mod, /_toastViewport\.Visible = workspaceVisible && !_layout\.BodyCollapsed && _toasts\.Count > 0/u);
  assert.match(mod, /ApplyPresentationVisibility\(\)/u);
});

test("Recorder is the default Workspace tab and never a floating overlay", () => {
  assert.match(mod, /Name = "Recorder"/u);
  assert.match(mod, /BuildRecorderPage\(_bodyViewport\)/u);
  assert.match(mod, /new\[\] \{ "Recorder", "Overview", "Environment", "Policy", "Human Data", "Diagnostics" \}/u);
  assert.match(mod, /_layout = _layout with \{ ActiveTab = 0, BodyCollapsed = false \}/u);
  assert.match(mod, /_tabBar\.CurrentTab = activeTab/u);
  assert.match(mod, /_recorderPage\.AddChild\(/u);
  assert.doesNotMatch(mod, /_workspaceSurface\.AddChild\(_recorderPage\)/u);
  assert.doesNotMatch(mod, /_recorderPage\.GuiInput/u);
  assert.doesNotMatch(`${mod}\n${presentation}`, /RecorderCollapsed|ToggleRecorderCollapse/u);
});

test("Recorder owns a bounded canonical Action Feed with explicit unavailable fields", () => {
  assert.match(mod, /QueryEvents\(/u);
  assert.match(mod, /RefreshActionFeed\(status\.Recording\)/u);
  assert.match(mod, /_recorderPage = new ScrollContainer/u);
  assert.match(mod, /_recorderScroll = _recorderPage/u);
  assert.match(mod, /_recorderDetails\.AddChild\(_actionFeedList\)/u);
  assert.match(mod, /bool feedChanged = false/u);
  assert.match(mod, /if \(feedChanged\)\s+RenderActionFeed\(\)/u);
  assert.match(mod, /SizeFlagsHorizontal = SizeFlags\.ExpandFill/u);
  assert.match(mod, /_actionFeed\.Recent\(PlatformLiveActionFeed\.MaxEntries\)/u);
  assert.match(mod, /feedChanged \|= _actionFeed\.Apply\(value\)/u);
  assert.match(mod, /value\.SessionId, sessionId/u);
  assert.match(mod, /sessionId == null/u);
  assert.match(feed, /DecisionPending => "… Observed"/u);
  assert.match(feed, /DecisionRecorded => "✓ Recorded"/u);
  assert.match(feed, /DecisionInvalidated => "✕ Invalidated"/u);
  assert.match(feed, /record:\{value\.RecordId\}/u);
  assert.match(feed, /RecordId action root unavailable/u);
  assert.doesNotMatch(feed, /return \(`bound-action:/u);
  assert.match(feed, /SubjectReferentId/u);
  assert.match(feed, /Arguments/u);
  assert.match(feed, /EffectSummary/u);
  assert.match(feed, /unavailable \(not present in canonical evidence\)/u);
  assert.match(client, /RecordingApplicationService\.Instance\.QueryStatus\(\)/u);
  assert.doesNotMatch(feed, /InputEventMouseButton|InputEventMouseMotion|frame.*delay|AppendDecision|Execute\(/isu);
});

test("header controls and all presentation regions stay inside the Workspace hierarchy", () => {
  assert.match(mod, /_workspace\.AddChild\(_workspaceSurface\)/u);
  assert.match(mod, /_workspaceSurface\.AddChild\(workspaceMargin\)/u);
  assert.match(mod, /workspaceMargin\.AddChild\(_workspaceBody\)/u);
  assert.match(mod, /_workspaceBody\.AddChild\(titleRow\)/u);
  assert.match(mod, /_workspaceBody\.AddChild\(_workspaceContent\)/u);
  assert.equal((mod.match(/titleRow\.AddChild\(BuildHeaderButton/g) ?? []).length, 2);
  assert.match(mod, /_workspaceCollapse = BuildHeaderButton/u);
  assert.match(mod, /button\.CustomMinimumSize = new Vector2\(88, 34\)/u);
  assert.match(mod, /button\.ClipText = true/u);
  assert.doesNotMatch(mod, /Root\.AddChild\((_toast|_recorder|_body|_tab)/u);
});

test("all tabs share one clipped viewport and overflow vertically", () => {
  assert.match(mod, /_workspaceContent\.AddChild\(_tabBar\)/u);
  assert.match(mod, /_workspaceContent\.AddChild\(_bodyViewport\)/u);
  assert.match(mod, /_bodyViewport.*ClipContents = true/su);
  assert.match(mod, /_recorderPage\.SetAnchorsAndOffsetsPreset\(LayoutPreset\.FullRect\)/u);
  assert.match(mod, /scroll\.SetAnchorsAndOffsetsPreset\(LayoutPreset\.FullRect\)/u);
  assert.match(mod, /HorizontalScrollMode = ScrollContainer\.ScrollMode\.Disabled/u);
  assert.match(mod, /VerticalScrollMode = ScrollContainer\.ScrollMode\.Auto/u);
  assert.match(mod, /_pages\.Add\(_recorderPage\)/u);
  assert.match(mod, /_pages\.Add\(scroll\)/u);
  assert.doesNotMatch(mod, /AddPage\(TabContainer|BuildRecorderPage\(TabContainer/u);
});

test("active-tab clicks use one collapse state and other tabs expand", () => {
  assert.match(mod, /_tabBar\.TabClicked \+= OnTabClicked/u);
  assert.match(presentation, /selectedTab == state\.ActiveTab/u);
  assert.match(presentation, /BodyCollapsed = !state\.BodyCollapsed/u);
  assert.match(presentation, /ActiveTab = selectedTab, BodyCollapsed = false/u);
  assert.match(mod, /_bodyViewport\.Visible = !_layout\.BodyCollapsed/u);
  assert.match(mod, /ToggleActiveTabBody\(\) => SelectTab\(_layout\.ActiveTab\)/u);
  assert.doesNotMatch(`${mod}\n${presentation}`, /WorkspaceCollapsed|RecorderCollapsed/u);
});

test("canonical action fixtures cover lifecycle aggregation and explicit evidence fields", () => {
  assert.match(feed, /FormatEntry\(PlatformLiveActionItem value\)/u);
  assert.match(feed, /FormatDetail\(PlatformLiveActionItem value\)/u);
  assert.match(feed, /Subject\/card ID/u);
  assert.match(feed, /Target IDs/u);
  assert.match(feed, /Action ID/u);
  assert.match(feed, /waiting for canonical settlement/u);
  assert.match(mod, /Records \{recording\.Counters\.Records\}/u);
  assert.match(mod, /Pending \{pending\}/u);
  assert.match(mod, /Invalidated \{invalidated\}/u);
  assert.match(mod, /Recent Actions also includes Pending \/ Invalidated/u);
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

test("lifecycle-aware Action Feed fixtures pass without a shell-specific entrypoint", () => {
  const result = spawnSync(
    "dotnet",
    [
      "test",
      path.join(root, "test", "STS2PlatformLiveUi.Tests.csproj"),
      "--configuration",
      "Release",
      "--nologo",
    ],
    {
      cwd: root,
      encoding: "utf8",
      shell: false,
    },
  );
  assert.equal(
    result.status,
    0,
    `Action Feed lifecycle fixtures failed.\n${result.stdout}\n${result.stderr}`,
  );
});
