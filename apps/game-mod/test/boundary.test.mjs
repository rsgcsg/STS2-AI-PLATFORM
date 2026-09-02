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
const sourceBetween = (source, start, end) => {
  const startAt = source.indexOf(start);
  const endAt = source.indexOf(end, startAt + start.length);
  if (startAt < 0 || endAt < 0)
    throw new Error(`Could not isolate source span: ${start} -> ${end}`);
  return source.slice(startAt, endAt);
};
const harmonyPatchContaining = (source, target, from = 0) => {
  const targetAt = source.indexOf(target, from);
  const patchStart = source.lastIndexOf("[HarmonyPatch]", targetAt);
  const nextPatch = source.indexOf("[HarmonyPatch]", targetAt + target.length);
  if (from < 0 || targetAt < 0 || patchStart < 0 || nextPatch < 0)
    throw new Error(`Could not isolate Harmony patch containing: ${target}`);
  return source.slice(patchStart, nextPatch);
};

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
  const projection = sourceBetween(
    runtime,
    "private static void PersistDerivedTransitionProjection",
    "private static bool IsTerminalWithoutNativeCompletion"
  );

  assert.match(runtime, /RecordingEnvironmentAdmission\.IsExactModset/u);
  assert.match(validator, /RecordingEnvironmentAdmission\.IsExactModset/u);
  assert.match(projection, /semantic_projection_persistence_unknown/u);
  assert.match(projection, /evidence_commit_unknown/u);
  assert.match(projection, /Quarantine\([\s\S]*semantic_projection_persistence_unknown/u);
  assert.doesNotMatch(projection, /\b(?:retry|backfill)\b/iu);
});

test("native card staging reuses one exact evidence frame without a second capture guard", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");

  assert.match(runtime, /StageCardPlay\(CardModel card\)[\s\S]*CaptureReadRichFrame\(\)[\s\S]*new StagedCardFrame\([\s\S]*new ExactDecisionFrame\(frame, environment\)/u);
  assert.match(runtime, /ReferenceEquals\(staged\.Card, stagedCard\)[\s\S]*IsExact\(staged\.Decision\.Frame\.Resolve\(expectedAction\)\)[\s\S]*selected = staged\.Decision\.Frame/u);
  assert.doesNotMatch(runtime, /StagedCardPlayGuard/u);
  assert.doesNotMatch(runtime, /cached\.Frame\.Snapshot\.SnapshotId,[\s\S]*current\.Snapshot\.SnapshotId/u);
});

test("native lifecycle observation does not materialize a semantic frame", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const discriminator = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeSemanticDiscriminatorRuntime.cs");

  assert.equal(
    (runtime.match(/NativeSemanticDiscriminatorRuntime\.ObserveLifecycleOnly\(/gu) ?? []).length,
    1
  );
  assert.match(
    discriminator,
    /capture: false,[\s\S]*NativeSemanticDiscriminatorContract\.LifecycleOnlyDetail/u
  );
  assert.doesNotMatch(
    runtime,
    /NativeSemanticDiscriminatorRuntime\.Observe\([\s\S]*capture: kind is NativeActionLifecycleKinds\./u
  );
});

test("native discriminator profiles fallback snapshots separately from frame projections", () => {
  const discriminator = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeSemanticDiscriminatorRuntime.cs");

  assert.match(discriminator, /native_semantic_discriminator_snapshot_capture/u);
  assert.match(discriminator, /native_semantic_discriminator_frame_projection/u);
  assert.match(discriminator, /uiFrame == null/u);
});

test("execution semantic action space is captured once and preserved without becoming public authority", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const beforeExecution = sourceBetween(
    runtime,
    "private static void ObserveBeforeActionExecution",
    "private static void ObserveSemanticDecisionBoundary"
  );
  const snapshot = read("components/connector/host/PlayerEnvironment/Observation/SnapshotBuilder.cs");
  const projection = read("components/annotator/src/STS2HumanAnnotator.Core/SemanticTransitionProjection.cs");

  assert.match(beforeExecution, /ProcessLocalNativeWitnessFrame frame = CaptureSemanticFrame\(\)/u);
  assert.match(beforeExecution, /PlayerEnvironmentNativeSemanticWitness\.Capture\(phase, action, frame\)/u);
  assert.match(beforeExecution, /capturedValue: semanticCapture/u);
  assert.match(beforeExecution, /ToExecutionSemanticActionSpace[\s\S]*executionSemanticActionSpace: actionSpace/u);
  assert.match(projection, /ExecutionSemanticActionSpaceValidator\.Validate/u);
  assert.match(projection, /native_semantic_execution/u);
  assert.match(snapshot, /CanPublishMutationAuthority\(draft\.Readiness\)[\s\S]*Array\.Empty<NativeUiBoundAction>/u);
  assert.doesNotMatch(snapshot, /ExecutionSemanticActionSpace|HumanAnnotator/u);
  assert.doesNotMatch(runtime, /PendingDecision|AcceptedHumanActionLedger|SerializedEvidenceAdmission/u);
});

test("combat roots carry the Native Foundation decision from admission through execution", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const uiPatches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");
  const witness = read("components/connector/host/PlayerEnvironment/Witness/ProcessLocalNativeSemanticWitness.cs");

  assert.match(runtime, /TryEnterCardScope[\s\S]*semanticSelection: observed/u);
  assert.match(runtime, /before_native_action_admission[\s\S]*semanticNativeActionType: nameof\(UsePotionAction\)/u);
  assert.match(uiPatches, /native_end_turn_ui[\s\S]*semanticSelection:[\s\S]*end_turn/u);
  assert.match(witness, /semanticObserved.Subject == null[\s\S]*DescribeWithoutSubject/u);
});

test("typed non-combat native decisions bind exact Human actions without Annotator legality", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const witness = read("components/connector/host/PlayerEnvironment/Witness/ProcessLocalNativeSemanticWitness.cs");
  const catalog = read("components/native-foundation/src/NativeDecisionContracts.cs");

  assert.match(runtime, /before_native_action_admission/u);
  assert.match(runtime, /subscription\?\.NativeSemanticDecision/u);
  assert.match(witness, /DescribeDomainSelection/u);
  assert.match(witness, /NativeSemanticActionCatalog\.DescribeByIdentity/u);
  assert.match(catalog, /mechanical identity join, not legality/u);
  assert.doesNotMatch(runtime, /outside_direct_native_catalog/u);
});

test("legacy Decision V2 compatibility cannot gate durable modern canonical evidence", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const projection = sourceBetween(
    runtime,
    "private static void PersistDerivedTransitionProjection",
    "private static bool IsTerminalWithoutNativeCompletion"
  );

  assert.ok(
    projection.indexOf("store.AppendCanonicalTransition(canonical)")
      < projection.indexOf("SemanticTransitionProjection.CreateDecision")
  );
  assert.match(projection, /decision_v2_compatibility_omitted/u);
  assert.doesNotMatch(projection, /semantic_projection_not_eligible/u);
});

test("combat owner-ready uses the exact post-input-owner native seam and a typed fail-closed capture", () => {
  const patches = read("apps/game-mod/NativeFoundationOwnerPatches.cs");
  const provider = read("components/native-foundation/src/NativeDecisionOwnerReadyProvider.cs");
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const trace = read("components/annotator/src/STS2HumanAnnotator.Core/SemanticBoundaryTrace.cs");

  assert.match(patches, /typeof\(NEndTurnButton\)[\s\S]*"OnTurnStarted"[\s\S]*typeof\(CombatState\)/u);
  assert.match(patches, /NativeDecisionOwnerReadyProvider\.ObservePlayerCombatTurnReady\(state\)/u);
  assert.match(provider, /ReferenceEquals\(CombatManager\.Instance\.DebugOnlyGetState\(\), state\)/u);
  assert.match(provider, /state\.CurrentSide != CombatSide\.Player/u);
  assert.match(provider, /NativeCombatDecisionProvider\.IsSemanticPlayPhase\(player, combat\)/u);
  assert.match(runtime, /NativeDecisionOwnerReadyProvider\.Observed \+= ObserveNativeDecisionOwnerReady/u);
  assert.match(runtime, /CaptureSemanticFrame\(\)[\s\S]*frame\.Snapshot\.Interaction\.Kind[\s\S]*observation\.Domain/u);
  assert.match(runtime, /SemanticBoundaryWitnessKinds\.NativeDecisionOwnerReady/u);
  assert.match(trace, /NativeDecisionOwnerReadyEvidence/u);
  assert.match(trace, /NativeDecisionOwnerReady\.Domain[\s\S]*InteractionKind/u);
  assert.doesNotMatch(provider, /Timer|Delay|ProcessFrame|poll/iu);
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
  const trace = read("components/annotator/src/STS2HumanAnnotator.Core/SemanticBoundaryTrace.cs");
  const ledger = read("components/annotator/src/STS2HumanAnnotator.Core/NativeActionLedger.cs");

  assert.match(runtime, /CanOpenSemanticEvidenceWindow[\s\S]*BoundaryTracker\.CanOpenNextRoot/u);
  assert.match(runtime, /semantic_causal_overlap/u);
  assert.match(runtime, /NativeActionLifecycleKinds\.Finished/u);
  assert.match(trace, /DisposeUnknown\([\s\S]*intervening_human_action_before_boundary/u);
  assert.match(ledger, /Historical durable native-lifecycle evidence contract/u);
  assert.match(ledger, /NativeActionLedgerValidator/u);
  assert.doesNotMatch(
    `${runtime}\n${ledger}`,
    /AcceptedHumanActionLedger|NativeActionLedger\.CanAdmitStrictTransition|ObserveRecoveryBoundary/u
  );
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

test("non-combat Human witnesses use public bindings and exact native completion operands", () => {
  const patches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");

  assert.match(
    patches,
    /native_map_choice_ui[\s\S]*new ProcessLocalObservedAction\([\s\S]*"activate",\s*point\.Point/u
  );
  assert.match(
    patches,
    /native_reward_claim_ui[\s\S]*new ProcessLocalObservedAction\([\s\S]*"activate",\s*__instance\.Reward/u
  );
  assert.match(
    patches,
    /native_treasure_chest_ui[\s\S]*new ProcessLocalObservedAction\([\s\S]*"open",\s*null/u
  );
  assert.match(
    patches,
    /native_treasure_relic_ui[\s\S]*new ProcessLocalObservedAction\([\s\S]*"activate",\s*relic/u
  );
  assert.match(
    patches,
    /class NativeTreasureProceedPatch[\s\S]*string verb = isGameAction \? "skip" : "activate"/u
  );
  assert.match(
    patches,
    /class NativeTreasureNormalRewardsPatch[\s\S]*"OneOffSynchronizer\.DoLocalTreasureRoomRewards"[\s\S]*nativeOperand: NativeTreasureUiContext\.CurrentRoom\(\)/u
  );
  assert.match(patches, /ProceedFromTerminalRewardsScreen[\s\S]*QueueNativePostCommitBoundary/u);
});

test("terminal rewards completion is family-neutral at the shared native seam", () => {
  const patches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");
  const completionMarker = "NativeTreasureProceedCompletionPatch";
  const sharedCompletionPatch = harmonyPatchContaining(
    patches,
    "ProceedFromTerminalRewardsScreen",
    patches.indexOf(completionMarker)
  );

  assert.ok(sharedCompletionPatch.length > 0);
  assert.match(sharedCompletionPatch, /QueueNativePostCommitBoundary\(\s*__result,/u);
  assert.doesNotMatch(sharedCompletionPatch, /"(?:reward|treasure)_proceed"/u);
});

test("task completion correlation does not consult HumanActionScope.Current", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const queueMethod = sourceBetween(
    runtime,
    "private static void QueueNativePostCommitBoundary<TTask>",
    "private static void PersistSemanticBoundaryDrafts"
  );
  const completionMethod = sourceBetween(
    runtime,
    "private static void ObserveNativePostCommitCompletion",
    "private static NativeCompletionEvidence ToCompletionEvidence"
  );

  assert.match(queueMethod, /NativeTaskCompletion signal = new/u);
  assert.doesNotMatch(queueMethod, /HumanActionScope\.Current/u);
  assert.match(completionMethod, /NativePostCommitCompletions\.CompleteTask\(taskCompletion\)/u);
  assert.doesNotMatch(completionMethod, /HumanActionScope\.Current/u);
});

test("GameAction Finished and task completion remain evidence-only before boundary handling", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const lifecycleMethod = sourceBetween(
    runtime,
    "private static void ObserveSemanticOnlyNativeActionLifecycle",
    "private static void ObserveNativeCommit"
  );
  const completionMethod = sourceBetween(
    runtime,
    "private static void ObserveNativePostCommitCompletion",
    "private static NativeCompletionEvidence ToCompletionEvidence"
  );

  assert.match(lifecycleMethod, /NativeActionLifecycleKinds\.Finished/u);
  assert.doesNotMatch(
    lifecycleMethod,
    /ObserveNativePostCommitBoundary|CaptureSemanticFrame|TrySettle/u
  );
  assert.doesNotMatch(
    completionMethod,
    /ObserveNativePostCommitBoundary|CaptureSemanticFrame|TrySettle/u
  );
});

test("a committed unresolved root admits the next exact root for execution handoff", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const admission = sourceBetween(
    runtime,
    "private static bool CanOpenSemanticEvidenceWindow",
    "internal static void StageCardPlay"
  );
  const trace = read("components/annotator/src/STS2HumanAnnotator.Core/SemanticBoundaryTrace.cs");
  const nextRoot = sourceBetween(
    trace,
    "public bool CanOpenNextRoot",
    "public IReadOnlyList<SemanticBoundaryTraceDraft> Accept"
  );

  assert.match(
    admission,
    /!BoundaryTracker\.HasUnresolvedActions\s*[\s\S]*BoundaryTracker\.CanOpenNextRoot/u
  );
  assert.match(nextRoot, /IsWaitingForBoundary\(entry\)[\s\S]*!entry\.Action\.RequiresNativePostCommit[\s\S]*entry\.NativeCommit != null/u);
  assert.doesNotMatch(admission, /Task\.Delay|Thread\.Sleep|Stopwatch|\bTimer\b/u);
});

test("native completion proof has no FIFO, count, timer, or polling fallback", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const queueMethod = sourceBetween(
    runtime,
    "private static void QueueNativePostCommitBoundary<TTask>",
    "private static void PersistSemanticBoundaryDrafts"
  );
  const completionMethod = sourceBetween(
    runtime,
    "private static void ObserveNativePostCommitCompletion",
    "private static NativeCompletionEvidence ToCompletionEvidence"
  );
  const proofFallback = /(?:\bFIFO\b|\.Count\b|FirstOrDefault|LastOrDefault|TryDequeue|\bDequeue\(|Task\.Delay|Task\.Wait|WaitAsync|Task\.WhenAny|Thread\.Sleep|Stopwatch|System\.Timers|\bTimer\b|\bPoll(?:ing)?\b|TrySettle)/u;

  // The queue is only cross-thread transport; proof stays in the exact ledger.
  assert.match(queueMethod, /QueuedNativePostCommitCompletions\.Enqueue\(signal\)/u);
  assert.doesNotMatch(queueMethod, proofFallback);
  assert.doesNotMatch(completionMethod, proofFallback);
});

test("treasure semantic stages come from Native Foundation rather than UI publication", () => {
  const reader = read("components/connector/host/LiveHost/TreasureRoomSurfaceReader.cs");
  const witness = read("components/connector/host/PlayerEnvironment/Witness/ProcessLocalNativeSemanticWitness.cs");
  const provider = read("components/native-foundation/src/NativeTreasureDecisionProvider.cs");

  assert.match(reader, /NativeTreasureDecisionProvider\.Capture/u);
  assert.match(reader, /NativeSemanticActionCatalog\.ContainsExactlyOnce/u);
  assert.match(provider, /ObserveRelicPickCommitted/u);
  assert.match(provider, /LocalRelicVoteCommitted/u);
  assert.doesNotMatch(provider, /GetPlayerVote/u);
  assert.doesNotMatch(provider, /public static bool Contains/u);
  assert.doesNotMatch(reader, /ChestOpenedField|CollectionOpenField|TreasureLifecycleFacts/u);
  assert.match(witness, /NativeTreasureDecisionProvider\.Capture/u);
});

test("treasure skip keeps its exact bound family when sharing PickRelicAction", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");

  assert.match(
    runtime,
    /SupportedFamilyForSemanticAction\(draft\.Action\)/u
  );
  assert.match(
    runtime,
    /action\.NativeActionType == nameof\(PickRelicAction\)[\s\S]*?action\.BoundAction\?\.Verb[\s\S]*?treasure_room\.skip/u
  );
  assert.match(runtime, /"NTreasureRoom\.OnProceedButtonPressed" => "treasure_room\.proceed"/u);
});

test("card reward owner creation commits the parent claim before the child decision", () => {
  const patches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");

  assert.match(
    patches,
    /reward is CardReward[\s\S]*?NCardRewardSelectionScreen\.ShowScreen/u
  );
  assert.match(
    patches,
    /ObserveSemanticUiNativeCommit\([\s\S]*?"reward_claim"[\s\S]*?"NCardRewardSelectionScreen\.ShowScreen"/u
  );
  assert.match(
    patches,
    /if \(reward is CardReward\)[\s\S]*?return;[\s\S]*?QueueNativePostCommitBoundary/u
  );
});

test("reward proceed observes actual native commit routes and never treats act-ready enqueue as Commit", () => {
  const patches = read("components/annotator/src/STS2HumanAnnotator.Mod/NativeUiPatches.cs");

  assert.match(patches, /RewardsSetSynchronizer\.SkipLocalRewardsSet[\s\S]*ObserveSemanticUiNativeCommit/u);
  assert.match(patches, /RunManager\.ProceedFromTerminalRewardsScreen/u);
  assert.doesNotMatch(patches, /ActChangeSynchronizer\.SetLocalPlayerReady[\s\S]*ObserveSemanticUiNativeCommit/u);
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
