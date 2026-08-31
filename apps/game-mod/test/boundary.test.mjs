import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

import {
  compiledSourceDigest,
  isGameModCompiledSource
} from "../source-identity.mjs";

const root = path.resolve(import.meta.dirname, "../../..");
const read = (relative) => fs.readFileSync(path.join(root, relative), "utf8");

test("game Mod test discovery is shell-neutral", () => {
  const packageJson = JSON.parse(read("apps/game-mod/package.json"));

  assert.equal(packageJson.scripts.check, "node --test");
});

test("production game Mod has one manifest, assembly and explicit initializer", () => {
  const manifest = JSON.parse(read("apps/game-mod/mod_manifest.json"));
  const project = read("apps/game-mod/STS2Platform.GameMod.csproj");
  const initializer = read("apps/game-mod/UnifiedPlatformMod.cs");

  assert.equal(manifest.id, "STS2_PLATFORM");
  assert.deepEqual(manifest.dependencies, []);
  assert.match(project, /<AssemblyName>STS2_PLATFORM<\/AssemblyName>/u);
  assert.match(project, /STS2_PLATFORM_UNIFIED/u);
  assert.match(project, /<SourceRevisionId>\$\(PlatformSourceRevision\)<\/SourceRevisionId>/u);
  assert.match(initializer, /ConnectorMod\.Initialize\(\);[\s\S]*RecorderMod\.Initialize\(\);[\s\S]*PlatformLiveUiMod\.Initialize\(\);/u);
});

test("compiled identity ignores repository-only provenance", () => {
  const component = {
    source_revision: "a".repeat(40),
    source_digest_sha256: "b".repeat(64),
    component_tree_revision: "c".repeat(40),
    component_worktree_status: "clean",
    file_count: 3
  };
  const baseline = compiledSourceDigest({ game_mod: component });
  const repositoryOnlyDrift = compiledSourceDigest({
    game_mod: {
      ...component,
      component_tree_revision: "d".repeat(40),
      component_worktree_status: "dirty",
      file_count: 99
    }
  });
  const nativeDrift = compiledSourceDigest({
    game_mod: { ...component, source_digest_sha256: "e".repeat(64) }
  });

  assert.equal(repositoryOnlyDrift, baseline);
  assert.notEqual(nativeDrift, baseline);
});

test("game-Mod provenance covers every compiled composition source", () => {
  assert.equal(isGameModCompiledSource("UnifiedPlatformMod.cs"), true);
  assert.equal(isGameModCompiledSource("NativeFoundationOwnerPatches.cs"), true);
  assert.equal(isGameModCompiledSource("STS2Platform.GameMod.csproj"), true);
  assert.equal(isGameModCompiledSource("mod_manifest.json"), true);
  assert.equal(isGameModCompiledSource("test/boundary.test.mjs"), false);
});

test("component initializers are disabled only in the unified build", () => {
  for (const file of [
    "components/connector/host/ConnectorMod.cs",
    "components/annotator/src/STS2HumanAnnotator.Mod/RecorderMod.cs",
    "apps/ingame-ui/PlatformLiveUiMod.cs"
  ]) {
    const source = read(file);
    assert.match(source, /#if !STS2_PLATFORM_UNIFIED\s+\[ModInitializer\("Initialize"\)\]\s+#endif/u);
  }
});

test("Live UI uses K from the SceneTree signal and logs readiness", () => {
  const source = read("apps/ingame-ui/PlatformLiveUiMod.cs");
  assert.match(source, /internal sealed class PlatformLivePanel : IDisposable/u);
  assert.match(source, /tree\.ProcessFrame \+= _processFrameHandler/u);
  assert.match(source, /Input\.IsKeyPressed\(Key\.K\) \|\| Input\.IsPhysicalKeyPressed\(Key\.K\)/u);
  assert.doesNotMatch(source, /class PlatformLivePanel : Control/u);
  assert.doesNotMatch(source, /override void _(Ready|Process|Input)/u);
  assert.match(source, /adding layer to SceneTree root/u);
  assert.match(source, /panel mount failed/u);
  assert.match(source, /panel ready; input=K/u);
  assert.doesNotMatch(source, /Key\.F\d+/u);
});

test("single-Mod deploy retires every legacy production manifest and DLL", () => {
  const lifecycle = read("apps/game-mod/lifecycle.mjs");
  for (const name of [
    "STS2_MCP.dll",
    "STS2_MCP.json",
    "STS2_HUMAN_ANNOTATOR.dll",
    "STS2_HUMAN_ANNOTATOR.json",
    "STS2_PLATFORM_LIVE_UI.dll",
    "STS2_PLATFORM_LIVE_UI.json"
  ]) assert.match(lifecycle, new RegExp(name.replace(".", "\\."), "u"));
});

test("single-Mod deploy replaces archived predecessor component configuration", () => {
  const lifecycle = read("apps/game-mod/lifecycle.mjs");
  assert.match(lifecycle, /managedConfigFiles\.map\([\s\S]*archiveTarget/u);
  assert.match(lifecycle, /writeJson\(connectorConfig,/u);
  assert.match(lifecycle, /writeJson\(annotatorConfig,/u);
  assert.doesNotMatch(lifecycle, /if \(!fs\.existsSync\((?:connector|annotator)Config\)\)/u);
});

test("single-Mod deploy and rollback transact native Windows Mod settings", () => {
  const lifecycle = read("apps/game-mod/lifecycle.mjs");

  assert.match(lifecycle, /resolveWindowsSteamSettings/u);
  assert.match(lifecycle, /prepareSoleWindowsModSettings/u);
  assert.match(lifecycle, /archiveTarget\(backup, "settings", settings\.file\)/u);
  assert.match(lifecycle, /entry\.location === "settings"/u);
  assert.match(lifecycle, /enabled_mod_ids: \[platformModId\]/u);
});

test("cold launch derives exact Connector canaries from compatibility authority", () => {
  const lifecycle = read("apps/game-mod/lifecycle.mjs");
  assert.match(lifecycle, /resolveConnectorCanaryEnvironment/u);
  assert.match(lifecycle, /components\/connector\/contracts\/host-compatibility\.json/u);
  assert.match(lifecycle, /\.\.\.connectorCanary\.environment/u);
  assert.doesNotMatch(
    lifecycle,
    /STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION:\s*installed\.source\.components\.connector/u
  );
});

test("exact build uses shared Host Runtime installation discovery", () => {
  const build = read("apps/game-mod/build.mjs");

  assert.match(build, /loadHostRuntimeWorkstationApi/u);
  assert.match(build, /resolveWorkstationInstallation/u);
  assert.match(build, /installation\.data_dir/u);
  assert.match(build, /installation\.release_info/u);
  assert.doesNotMatch(build, /defaultGameDirectory/u);
});

test("loaded verification never promotes an input canary to owner evidence", () => {
  const lifecycle = read("apps/game-mod/lifecycle.mjs");
  assert.match(lifecycle, /ui_toggle_runtime_canary/u);
  assert.match(lifecycle, /owner_ui_visibility: "pending human runtime evidence"/u);
  assert.match(lifecycle, /input_canary_is_not_owner_visibility_evidence/u);
  assert.doesNotMatch(lifecycle, /owner_ui_toggle/u);
});

test("record settlement accepts only shared exact Modsets and never retries an unknown evidence commit", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const validator = read("components/annotator/src/STS2HumanAnnotator.Core/RecordValidation.cs");

  assert.match(runtime, /RecordingEnvironmentAdmission\.IsExactModset/u);
  assert.match(validator, /RecordingEnvironmentAdmission\.IsExactModset/u);
  assert.match(runtime, /decision_persistence_unknown/u);
  assert.match(runtime, /evidence_commit_unknown/u);
  assert.match(runtime, /ClearPendingWithInvalidation\([\s\S]*decision_persistence_unknown/u);
});

test("native card staging reuses one exact evidence frame without a second capture guard", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");

  assert.match(runtime, /StageCardPlay\(CardModel card\)[\s\S]*TryPrepareSerializedEvidence\(out ProcessLocalNativeWitnessFrame\? preparedFrame\)[\s\S]*new ExactDecisionFrame\(frame, environment\)/u);
  assert.match(runtime, /ReferenceEquals\(staged\.Card, stagedCard\)[\s\S]*IsExact\(staged\.Decision\.Frame\.Resolve\(expectedAction\)\)[\s\S]*selected = staged\.Decision\.Frame/u);
  assert.doesNotMatch(runtime, /StagedCardPlayGuard/u);
  assert.doesNotMatch(runtime, /cached\.Frame\.Snapshot\.SnapshotId,[\s\S]*current\.Snapshot\.SnapshotId/u);
});

test("annotator evidence prefixes never become generic STS2 gameplay gates", () => {
  const patches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");

  assert.doesNotMatch(patches, /(?:private|internal|public)\s+static\s+bool\s+Prefix\s*\(/u);
  assert.doesNotMatch(patches, /BlockMutation|AllowMutation/u);
});

test("capture failures become invalidations only after the native action is accepted", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const scope = read("components/annotator/src/STS2HumanAnnotator.Mod/HumanActionScope.cs");
  const patches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");

  assert.match(runtime, /HumanActionScope\.EnterDeferredFailure/u);
  assert.match(runtime, /TryQuarantineDeferredAcceptedAction\(action\.GetType\(\)\.Name\)/u);
  assert.match(runtime, /TryQuarantineDeferredAcceptedAction\(nativeActionType\)/u);
  assert.match(scope, /AcceptedRootActionGate/u);
  assert.match(patches, /RecorderRuntime\.ExitNativeUiScope/u);
  assert.doesNotMatch(runtime, /if \(selected == null\)[\s\S]{0,500}Quarantine\(/u);
});

test("rapid accepted actions use one exact lifecycle ledger and never fabricate V2 successors", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const patches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");
  const ledger = read("components/annotator/src/STS2HumanAnnotator.Core/NativeActionLedger.cs");

  assert.match(patches, /HarmonyPatch\(typeof\(GameAction\), nameof\(GameAction\.OnEnqueued\)\)/u);
  assert.match(runtime, /NativeActionLedger\.CanAdmitStrictTransition/u);
  assert.match(runtime, /displaced != null && displaced\.NativeActionWitnessId == null/u);
  assert.match(runtime, /rapid_input_transition_unproven/u);
  assert.match(runtime, /NativeActionLifecycleKinds\.StrictTransitionAdmitted/u);
  assert.doesNotMatch(runtime, /overlapping_action_before_successor/u);
  assert.match(ledger, /AcceptedHumanActionLedger/u);
  assert.match(ledger, /ObserveRecoveryBoundary/u);
});

test("semantic direct UI commits share one execution boundary and scoped selector path", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const patches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");

  assert.match(runtime, /SemanticBoundaryWitnessKinds\.BeforeHumanActionExecution/u);
  assert.doesNotMatch(runtime, /before_direct_ui_commit/u);
  assert.match(runtime, /HumanActionScope\.Current != null/u);
  assert.match(patches, /class NativeCombatHandSelectPatch/u);
  assert.match(patches, /SelectCardInSimpleMode/u);
  assert.match(patches, /SelectCardInUpgradeMode/u);
  assert.match(patches, /class NativeCombatHandDeselectPatch/u);
  assert.match(patches, /DeselectHolder/u);
  assert.match(patches, /class NativeCombatHandConfirmPatch/u);
  assert.match(patches, /OnSelectModeConfirmButtonPressed/u);
  assert.match(patches, /RecorderRuntime\.TryEnterSemanticScope/u);
});

test("Native Foundation is the single combat semantic and lifecycle seam", () => {
  const foundation = read("components/native-foundation/src/NativeCombatDecisionProvider.cs");
  const combatSurface = read("components/connector/host/LiveHost/CombatTurnSurfaceReader.cs");
  const witness = read("components/connector/host/PlayerEnvironment/Witness/ProcessLocalNativeSemanticWitness.cs");
  const lifecycle = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeActionLifecycleSubscription.cs");

  assert.match(foundation, /PlayerCombatState\.Hand\.Cards/u);
  assert.match(foundation, /CardModel\.CanPlayTargeting/u);
  assert.match(foundation, /PotionModel\.IsValidTarget/u);
  assert.match(combatSurface, /NativeCombatDecisionProvider\.Capture/u);
  assert.match(combatSurface, /NativeDecisionProjection[\s\S]*?\.VisibleSubjects/u);
  assert.match(witness, /NativeCombatDecisionProvider\.Capture/u);
  assert.doesNotMatch(witness, /private static IReadOnlyList<ProcessLocalSemanticAction> BuildActions/u);
  assert.doesNotMatch(witness, /CanUsePotionSemantically/u);
  assert.match(lifecycle, /NativeActionLifecycleObserver/u);
  assert.doesNotMatch(lifecycle, /\.BeforeExecuted \+=/u);
});

test("non-combat native owners are observed by explicit read-only seams", () => {
  const initializer = read("apps/game-mod/UnifiedPlatformMod.cs");
  const patches = read("apps/game-mod/NativeFoundationOwnerPatches.cs");

  assert.match(
    initializer,
    /NativeFoundationOwnerPatches\.Initialize\(\);[\s\S]*ConnectorMod\.Initialize\(\);[\s\S]*RecorderMod\.Initialize\(\);/u
  );
  assert.match(patches, /typeof\(NRewardsScreen\), nameof\(NRewardsScreen\.ShowScreen\)/u);
  assert.match(patches, /typeof\(NCardRewardSelectionScreen\),[\s\S]*nameof\(NCardRewardSelectionScreen\.ShowScreen\)/u);
  assert.match(patches, /typeof\(NCardRewardSelectionScreen\),[\s\S]*nameof\(NCardRewardSelectionScreen\.RefreshOptions\)/u);
  assert.match(patches, /typeof\(NTreasureRoom\),[\s\S]*nameof\(NTreasureRoom\.Create\)/u);
  assert.match(patches, /typeof\(NTreasureRoom\),[\s\S]*"OnChestButtonReleased"/u);
  assert.equal((patches.match(/harmony\.Patch\(original, postfix:/gu) ?? []).length, 1);
  assert.doesNotMatch(patches, /PatchAll|prefix:|finalizer:|transpiler:|static\s+bool\s+Prefix/u);
});

test("treasure semantic stages come from Native Foundation rather than UI publication", () => {
  const reader = read("components/connector/host/LiveHost/TreasureRoomSurfaceReader.cs");
  const witness = read("components/connector/host/PlayerEnvironment/Witness/ProcessLocalNativeSemanticWitness.cs");
  const provider = read("components/native-foundation/src/NativeTreasureDecisionProvider.cs");

  assert.match(reader, /NativeTreasureDecisionProvider\.Capture/u);
  assert.match(reader, /NativeSemanticActionCatalog\.ContainsExactlyOnce/u);
  assert.doesNotMatch(provider, /public static bool Contains/u);
  assert.doesNotMatch(reader, /ChestOpenedField|CollectionOpenField|TreasureLifecycleFacts/u);
  assert.match(witness, /NativeTreasureDecisionProvider\.Capture/u);
});

test("Native Foundation remains semantic-only and Ritsu-free", () => {
  const foundationFiles = [
    "components/native-foundation/src/NativeDecisionContracts.cs",
    "components/native-foundation/src/NativeCombatDecisionProvider.cs",
    "components/native-foundation/src/NativeDomainOwnerProbe.cs",
    "components/native-foundation/src/NativeTreasureDecisionProvider.cs",
    "components/native-foundation/src/NativeActionLifecycleObserver.cs"
  ];
  const source = foundationFiles.map(read).join("\n");
  const project = read("apps/game-mod/STS2Platform.GameMod.csproj");

  assert.doesNotMatch(source, /Harmony|Http|JsonSerializer|File\.|Directory\.|Receipt|EvidenceStore/u);
  assert.doesNotMatch(`${source}\n${project}`, /RitsuLib|STS2RitsuLib/u);
  assert.match(project, /components\/native-foundation\/src\/\*\.cs/u);
});

test("semantic witness preserves Native Foundation capture failures", () => {
  const witness = read("components/connector/host/PlayerEnvironment/Witness/ProcessLocalNativeSemanticWitness.cs");
  assert.match(witness, /Schema,\s*decision\.Status,/u);
  assert.match(witness, /decision\.Detail\);/u);
  assert.doesNotMatch(witness, /Schema,\s*"captured",\s*scope,/u);
});

test("delivery receipt cannot claim causal settlement", () => {
  const submission = read("components/connector/host/PlayerEnvironment/Execution/ActionSubmission.cs");
  const protocol = read("components/connector/host/PlayerEnvironment/Protocol/PlayerEnvironmentContracts.cs");

  assert.match(submission, /postDeliveryObservation/u);
  assert.match(submission, /not causal settlement/u);
  assert.match(protocol, /not business completion or a canonical[\s\S]*causal next-decision state/u);
  assert.doesNotMatch(submission, /WaitFor.*Successor|CompletionProbe|BusinessOutcome/u);
});
