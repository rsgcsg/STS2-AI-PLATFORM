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
