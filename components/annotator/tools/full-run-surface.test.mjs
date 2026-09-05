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
const nestedSelectors = fs.readFileSync(
  path.join(root, "src", "STS2HumanAnnotator.Mod", "NativeNestedSelectorPatches.cs"),
  "utf8"
);
const exactAsyncBindings = fs.readFileSync(
  path.join(root, "src", "STS2HumanAnnotator.Core", "ExactAsyncOwnerBindingScope.cs"),
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
  assert.match(commit, /NativeUiCompletionRootBindings\.Take\(__instance\)/u);
  assert.match(commit, /NativeUiCompletionRootBindings\.Remember\(RunManager\.Instance, __state\)/u);
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
  assert.match(commit, /NativeUiCompletionRootBindings\.Take\(parent\)/u);
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

test("nested selectors bind exact parent scope to exact screen without ambient guesses", () => {
  assert.match(nestedSelectors, /ExactAsyncOwnerBindingScope<object, Parent, Binding>/u);
  assert.match(nestedSelectors, /Screens\.TryBindCurrent\(/u);
  assert.match(exactAsyncBindings, /AsyncLocal<Frame\?>/u);
  assert.match(exactAsyncBindings, /ConditionalWeakTable<TKey, Holder>/u);
  assert.match(nestedSelectors, /NativePlayerChoiceLineage\.Capture\(\)/u);
  assert.match(nestedSelectors, /NativeUiCompletionRootBindings\.TryGet\(action/u);
  assert.match(runtime, /StartSemanticNativeAction[\s\S]*?NativeUiCompletionRootBindings\.Remember\(action, actionWitnessId\)/u);
  assert.match(nestedSelectors, /ObserveAcceptedNestedHumanContinuation\(/u);
  assert.match(nestedSelectors, /TryReadCompletedSelection\(/u);
  assert.doesNotMatch(nestedSelectors, /MoveNext|FIFO|latest[-_ ]frame|NOverlayStack\.Instance\?\.Peek|Task\.Delay|Timer/u);
  assert.doesNotMatch(exactAsyncBindings, /FIFO|latest[-_ ]|Task\.Delay|Timer/u);
});

test("card reward alternatives and removal use exact owner carriers", () => {
  assert.match(nestedSelectors, /CardReward\.OnSelect/u);
  assert.match(nestedSelectors, /NCardRewardSelectionScreen[\s\S]*?ShowScreen/u);
  assert.match(nestedSelectors, /ConditionalWeakTable<NCardRewardSelectionScreen, ScreenBinding>/u);
  assert.match(nestedSelectors, /OnAlternateRewardSelected/u);
  assert.match(nestedSelectors, /TryGetAlternative\(/u);
  assert.match(nestedSelectors, /CardReward\.Reroll/u);
  assert.match(nestedSelectors, /Reward\.SelectUnsynchronized/u);
  assert.match(nestedSelectors, /CardRemovalReward\.OnSelect/u);
  assert.match(nestedSelectors, /reward_card_removal\.nested_selector/u);
  assert.doesNotMatch(nestedSelectors, /CardRewardAlternative\.Generate\(__instance\)/u);
});

test("merchant removal uses exact shipped three-argument outer carrier", () => {
  const purchase = section(patches, "internal static class NativeShopPurchasePatch", "internal static class NativeShopRoomOpenPatch");
  assert.match(purchase, /typeof\(MerchantCardRemovalEntry\)/u);
  assert.match(purchase, /typeof\(MerchantInventory\), typeof\(bool\), typeof\(bool\)/u);
  assert.match(purchase, /MerchantCardRemovalEntry\.OnTryPurchaseWrapper/u);
  assert.match(purchase, /NativeNestedSelectorBindings\.EnterParent\(/u);
});

test("event and rest outer owners carry selector lineage while parent Task retains disposition", () => {
  const event = section(patches, "internal static class NativeEventOptionPatch", "internal static class NativeEventOptionCompletionPatch");
  const eventCompletion = section(patches, "internal static class NativeEventOptionCompletionPatch", "internal static class NativeRestSiteOptionPatch");
  const rest = section(patches, "internal static class NativeRestSiteOptionPatch", "internal static class NativeRestSiteButtonPatch");
  assert.match(rest, /RestSiteSynchronizer\.ChooseLocalOption/u);
  assert.match(rest, /NativeNestedSelectorBindings\.EnterParent\(/u);
  assert.match(rest, /QueueNativePostCommitBoundary\(/u);
  const eventParent = section(nestedSelectors, "internal static class NativeEventNestedSelectorParentPatch", "internal static class NativeNestedSelectorFactoryPatch");
  assert.match(eventParent, /typeof\(EventOption\), nameof\(EventOption\.Chosen\)/u);
  assert.match(eventParent, /event_option\.nested_selector/u);
  assert.doesNotMatch(eventParent, /ObserveAcceptedSemanticUiAction\(/u);
  assert.match(eventCompletion, /ConditionalWeakTable<EventOption, TaskCarrier>/u);
  assert.match(eventCompletion, /Tasks\.Add\(__instance, new TaskCarrier\(__result\)\)/u);
  assert.match(event, /TryTakeTask\(option/u);
  assert.match(event, /QueueNativePostCommitBoundary\(/u);
});

test("only terminal selector callbacks create one child continuation", () => {
  const accepted = section(nestedSelectors, "internal static class NativeNestedSelectorAcceptedPatch", "internal static class NativeNestedSelectorExitPatch");
  assert.match(accepted, /CompleteSelection/u);
  assert.match(accepted, /ConfirmSelection/u);
  assert.match(accepted, /CloseSelection/u);
  assert.doesNotMatch(accepted, /CancelSelection/u);
  assert.match(accepted, /NativeNestedSelectorBindings\.TryTake/u);
  assert.match(accepted, /task\.IsCompleted/u);
});
