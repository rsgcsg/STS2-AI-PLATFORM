using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;
using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

[HarmonyPatch]
internal static class NativeRewardInputOwnerPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NRewardsScreen), nameof(NRewardsScreen.ShowScreen))
        ?? throw new MissingMethodException(typeof(NRewardsScreen).FullName, nameof(NRewardsScreen.ShowScreen));

    private static void Postfix(NRewardsScreen __result, MethodBase __originalMethod) =>
        NativeNestedCallbackSafety.Run("reward_screen.exact_parent", () =>
            NativeNestedSelectorBindings.RegisterOptionalInputOwner(__result, __originalMethod));
}

internal static partial class RecorderRuntime
{
    internal sealed record NestedUiInput(object Owner,
        NativeNestedSelectorBindings.Binding Binding, DecisionOccurrenceIdentity Parent);

    private static NestedUiInput? ResolveNestedUiInput(object? owner)
    {
        if (owner == null || !NativeNestedSelectorBindings.TryGet(owner, out var binding)
            || binding == null) return null;
        if (binding.RecordingSessionId != SessionId || binding.ActionWitnessId == "unavailable")
            throw new InvalidOperationException("Reward input owner has no current exact Human parent binding.");
        DecisionOccurrenceIdentity? parent = binding.ParentDecision
            ?? BoundaryTracker.DecisionIdentity(binding.ActionWitnessId);
        if (parent == null)
            throw new InvalidOperationException("Reward input owner has no exact Human parent decision.");
        binding.ParentDecision = parent;
        return new(owner, binding, parent);
    }

    private static IReadOnlyList<SemanticBoundaryTraceDraft> ObserveNestedUiInputBoundary(
        SemanticBoundaryTracker tracker, NestedUiInput input,
        ProcessLocalNativeWitnessFrame frame, CurrentDecisionFrame pre, string mechanism)
    {
        string parentActionId = input.Binding.DecisionHeadActionId ?? input.Binding.ActionWitnessId;
        if (!tracker.Contains(parentActionId)) return Array.Empty<SemanticBoundaryTraceDraft>();
        string ownerId = NativeWitnessIdentity.Get(input.Owner, "selector_owner");
        var boundary = CreateSemanticBoundaryObservation(frame,
            SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady, null, pre) with {
            NativeDecisionOwnerReady = new NativeDecisionOwnerReadyEvidence(
                pre.InteractionKind, ownerId, input.Owner.GetType().FullName!, mechanism) };
        return tracker.ObserveNestedInputBoundary(parentActionId, boundary,
            new NativeContinuationEvidence($"reward-input-boundary-{Guid.NewGuid():N}",
                "exact_selector_input_owner", parentActionId, ownerId,
                NativeWitnessIdentity.Get(input.Binding.ParentOwner, "native_owner"), true));
    }
}
