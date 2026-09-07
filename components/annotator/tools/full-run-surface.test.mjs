import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");
const patches = fs.readFileSync(
  path.join(root, "src", "STS2HumanAnnotator.Mod", "NativeUiPatches.cs"),
  "utf8"
);
const runtime = fs.readFileSync(
  path.join(root, "src", "STS2HumanAnnotator.Mod", "RecorderRuntime.cs"),
  "utf8"
);
const foundation = fs.readFileSync(
  path.resolve(root, "..", "native-foundation", "src", "NativeBossRelicDecisionProvider.cs"),
  "utf8"
);
const gameMod = fs.readFileSync(
  path.resolve(root, "..", "..", "apps", "game-mod", "NativeFoundationOwnerPatches.cs"),
  "utf8"
);

function section(source, start, end) {
  const begin = source.indexOf(start);
  assert.notEqual(begin, -1, `missing source section: ${start}`);
  const finish = end == null ? source.length : source.indexOf(end, begin);
  assert.notEqual(finish, -1, `missing source section end: ${end}`);
  return source.slice(begin, finish);
}

test("act-ready binds one exact queued action and skips generic duplicate ingress", () => {
  const enqueue = section(patches, "internal static class NativeActChangeVoteEnqueuePatch", "internal static class NativeActChangeVoteCommitPatch");
  const commit = section(patches, "internal static class NativeActChangeVoteCommitPatch", "[HarmonyPatch(typeof(RewardsSetSynchronizer)");

  assert.match(enqueue, /private static void Prefix\(/u);
  assert.doesNotMatch(enqueue, /private static void Postfix\(/u);
  assert.match(enqueue, /NativeUiCompletionRootBindings\.Remember\(action, actionWitnessId\)/u);
  assert.match(runtime, /if \(NativeUiCompletionRootBindings\.Contains\(action\)\)\s*return;/u);
  assert.match(commit, /NativeUiCompletionRootBindings\.TryGet\(__instance, out __state\)/u);
  assert.match(commit, /RememberOrFailClosed\([\s\S]*?RunManager\.Instance/u);
  assert.match(commit, /TakeIfMatches\(__instance, __state\)/u);
  assert.match(commit, /ObserveNativeActChangeOwnerReady\(__state, __instance\)/u);
  assert.equal((commit.match(/ObserveSemanticUiNativeCommit\(/gu) ?? []).length, 1);
});

test("boss relic uses the registered native parent exactly once", () => {
  const selection = section(patches, "internal static class NativeBossRelicSelectionPatch", "internal static class NativeBossRelicCommitPatch");
  const commit = section(patches, "internal static class NativeBossRelicCommitPatch", "internal static class NativeCombatHandSelectPatch");

  assert.doesNotMatch(patches, /class NativeBossRelicCommandPatch/u);
  assert.match(selection, /TryGetRegisteredChoiceCarrier\(/u);
  assert.match(selection, /carrier\.ParentLineage\.ParentAction/u);
  assert.match(selection, /NativeUiCompletionRootBindings\.Remember\(/u);
  assert.doesNotMatch(selection, /NativePlayerChoiceLineage\.Capture\(/u);
  assert.match(commit, /TryGetRegisteredCurrentChoiceCarrier\(/u);
  assert.match(commit, /NativeUiCompletionRootBindings\.TryGet\(parent/u);
  assert.match(commit, /NativeUiCompletionRootBindings\.TakeIfMatches\(parent, actionWitnessId\)/u);
  assert.doesNotMatch(commit, /NativePlayerChoiceLineage\.Capture\(/u);
  assert.equal((foundation.match(/RegisterFromChooseARelicScreen\(/gu) ?? []).length, 1);
  assert.match(gameMod, /PatchBefore\([\s\S]*?NativeBossRelicCommandPatch/u);
});

test("boss relic carrier failures are one durable failed-closed occurrence", () => {
  const selection = section(patches, "internal static class NativeBossRelicSelectionPatch", "internal static class NativeBossRelicCommitPatch");
  const commit = section(patches, "internal static class NativeBossRelicCommitPatch", "internal static class NativeCombatHandSelectPatch");

  assert.match(selection, /ObserveAcceptedSemanticUiFailure\(/u);
  assert.match(selection, /boss_relic_accepted_carrier_unavailable/u);
  assert.match(commit, /ObserveSemanticUiNativeCommitBindingFailure\(/u);
  assert.match(commit, /ConsumeRegisteredChoice\(/u);
  assert.match(runtime, /ObserveAcceptedSemanticUiFailure\(/u);
  assert.match(runtime, /AcceptedDecisionObserver\.Observe\([\s\S]*hasMapping:\s*false/u);
  assert.match(runtime, /OutcomeKind\.Duplicate/u);
});

test("bound potion and act actions retain lifecycle without duplicate root ingress", () => {
  const enqueue = section(patches, "internal static class NativeRewardPotionDiscardEnqueuePatch", "internal static class NativeRewardPotionDiscardCommitPatch");
  const actEnqueue = section(patches, "internal static class NativeActChangeVoteEnqueuePatch", "internal static class NativeActChangeVoteCommitPatch");
  const subscription = fs.readFileSync(
    path.join(root, "src", "STS2HumanAnnotator.Mod", "NativeActionLifecycleSubscription.cs"),
    "utf8"
  );

  assert.match(enqueue, /NativeUiCompletionRootBindings\.Remember\(action, actionWitnessId\)/u);
  assert.match(actEnqueue, /NativeUiCompletionRootBindings\.Remember\(action, actionWitnessId\)/u);
  assert.match(runtime, /TryGetAction\(actionWitnessId/u);
  assert.match(runtime, /finishIsNativeCommit:\s*completionExpectation == null/u);
  assert.match(runtime, /NativeActionLifecycleKinds\.Cancelled/u);
  assert.match(subscription, /FinishIsNativeCommit/u);
});

test("boss carrier registration is one-shot and lifecycle-cleaned", () => {
  assert.match(foundation, /ConsumeRegisteredChoice\(GameAction parentAction\)/u);
  assert.match(foundation, /ObserveParentLifecycle\(/u);
  assert.match(foundation, /NativeActionLifecyclePhase\.Cancelled/u);
  assert.match(foundation, /NativeActionLifecyclePhase\.Finished/u);
  assert.match(foundation, /Observer\?\.Dispose\(\)/u);
});

test("native observation callbacks contain exception barriers", () => {
  const callbacks = [
    ["NativeBossRelicSelectionPatch", "internal static class NativeBossRelicCommitPatch"],
    ["NativeBossRelicCommitPatch", "internal static class NativeCombatHandSelectPatch"],
    ["NativeRewardProceedPatch", "internal static class NativeRewardPotionDiscardPatch"],
    ["NativeRewardPotionDiscardPatch", "internal static class NativeRewardPotionDiscardEnqueuePatch"],
    ["NativeRewardPotionDiscardEnqueuePatch", "internal static class NativeRewardPotionDiscardCommitPatch"],
    ["NativeRewardPotionDiscardCommitPatch", "internal static class NativeActChangeVoteEnqueuePatch"],
    ["NativeActChangeVoteEnqueuePatch", "internal static class NativeActChangeVoteCommitPatch"],
    ["NativeActChangeVoteCommitPatch", "[HarmonyPatch(typeof(RewardsSetSynchronizer)"],
  ];
  for (const [name, end] of callbacks) {
    const body = section(patches, `internal static class ${name}`, end);
    assert.match(body, /try\s*\{/u, `${name} has no callback try barrier`);
    assert.match(body, /catch \(Exception/u, `${name} has no callback catch barrier`);
  }
  const safety = section(patches, "internal static class NativeUiObservationSafety", "[HarmonyPatch]");
  assert.match(safety, /catch\s*\{/u);
});

test("failed native carrier installation has a durable unknown disposition", () => {
  assert.match(runtime, /ObserveSemanticUiCarrierBindingFailure\(/u);
  assert.match(runtime, /PreviewUnknown\(/u);
  assert.match(runtime, /CommitUnknown\(/u);
  assert.match(runtime, /semantic_ui_action_start_persistence_failed/u);
  assert.match(runtime, /semantic_native_action_start_persistence_failed/u);
  assert.match(runtime, /CleanupUnstartedSemanticUiAction\(/u);
  assert.doesNotMatch(patches, /CurrentParentForCleanup\(/u);
});

test("all owner and witness collisions fail closed without replacement", () => {
  assert.match(patches, /ExactOwnerWitnessBindingTable<object>/u);
  assert.match(patches, /RememberOrFailClosed\(/u);
  assert.match(patches, /already bound to a different child action/u);
  assert.match(patches, /ObserveSemanticUiCarrierBindingFailure\(/u);
});

test("semantic mutations and task completions commit only after authoritative append", () => {
  assert.match(runtime, /PersistTrackerMutationOrUnknown\(/u);
  assert.match(runtime, /BeginDurableMutation\(/u);
  assert.match(runtime, /pending\.MarkAuthoritativeAppend\(\)/u);
  assert.match(runtime, /PreviewTaskCompletion\(taskCompletion\)/u);
  assert.match(runtime, /CommitTaskCompletion\(taskCompletion\)/u);
  assert.doesNotMatch(runtime, /resolution = NativePostCommitCompletions\.CompleteTask\(taskCompletion\)/u);
});

test("boss same-parent registration is idempotent or explicitly ambiguous", () => {
  assert.match(foundation, /sameParent/u);
  assert.match(foundation, /IsAmbiguous = true/u);
  assert.match(foundation, /Multiple RelicSelectCmd option registrations share the exact PlayerChoice parent/u);
});
