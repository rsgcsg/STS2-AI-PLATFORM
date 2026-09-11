using Xunit;
namespace STS2HumanAnnotator.Core.Tests;

public sealed class CarrierIngressSourceTests
{
    private static string Source(string name)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "src", "STS2HumanAnnotator.Mod", name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(name);
    }

    [Fact]
    public void RestProceedNativeNoOpDoesNotOpenHumanScope()
    {
        string source = Source("NativeUiPatches.cs");
        int start = source.IndexOf("internal static class NativeRestSiteProceedPatch");
        int end = source.IndexOf("internal static class NativeShopPurchasePatch", start);
        string patch = source[start..end];
        int guard = patch.IndexOf("NMapScreen.Instance?.IsOpen == true");
        int capture = patch.IndexOf("RecorderRuntime.TryEnterSemanticScope");
        Assert.True(guard >= 0 && capture > guard);
        Assert.Contains("__state = default;", patch[guard..capture]);
        Assert.Contains("return;", patch[guard..capture]);
    }

    [Fact]
    public void RewardFactoryLineageReachesExistingAtomicTrackerMutation()
    {
        string patch = Source("NativeRewardDecisionLineage.cs");
        Assert.Contains("nameof(NRewardsScreen.ShowScreen)", patch);
        Assert.Contains("RegisterOptionalInputOwner(__result, __originalMethod)", patch);
        Assert.Contains("binding.RecordingSessionId != SessionId", patch);
        Assert.Contains("BoundaryTracker.DecisionIdentity(binding.ActionWitnessId)", patch);
        Assert.Contains("tracker.ObserveNestedInputBoundary", patch);
        string runtime = Source("RecorderRuntime.cs");
        Assert.Contains("nestedInput: acceptedContext.NestedInput", runtime);
        int begin = runtime.IndexOf("private static bool StartSemanticUiAction(");
        int handoff = runtime.IndexOf("result.AddRange(ObserveNestedUiInputBoundary", begin);
        int accept = runtime.IndexOf("result.AddRange(tracker.Accept", begin);
        Assert.True(handoff > begin && accept > handoff);
        string patches = Source("NativeUiPatches.cs");
        Assert.Contains("nestedInputOwner: NOverlayStack.Instance?.Peek() as NRewardsScreen", patches);
        Assert.Contains("nestedInputOwner: __instance", patches);
    }

    [Fact]
    public void DeferredInputIsBoundBeforeRequestAndReusedByTrueAcceptedCallback()
    {
        string patches = Source("NativeUiPatches.cs");
        int start = patches.IndexOf("internal static class NativeSubmittedHumanInputPatch");
        int end = patches.IndexOf("internal static class AcceptedGameActionPatch", start);
        Assert.Contains("private static void Prefix", patches[start..end]);
        Assert.Contains("BindSubmittedHumanInput(action)", patches[start..end]);
        string runtime = Source("RecorderRuntime.cs");
        Assert.Contains("submitted != null ? submitted.Context : HumanActionScope.Current", runtime);
        Assert.Contains("submitted.SessionId != SessionId || submitted.TimelineId != TimelineId", runtime);
        Assert.Contains("hasMapping, submittedFailure", runtime);
        int duplicate = runtime.IndexOf("if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.Duplicate)");
        int noScope = runtime.IndexOf("if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.NoScope)", duplicate);
        Assert.Contains("return;", runtime[duplicate..noScope]);
        Assert.DoesNotContain("TryQuarantine", runtime[duplicate..noScope]);
        Assert.Contains("submittedFailure: submittedFailure", runtime);
        Assert.Contains("NativePotionUseDecisionProvider.ResolveTarget(use, potion)", runtime);
        Assert.Contains("semanticDecision, nativeInputBinding: nativeInput", runtime);
        string binding = Source("NativeSubmittedInputBindings.cs");
        Assert.DoesNotContain("ObserveAcceptedAction", binding); // no speculative acceptance at request
        Assert.DoesNotContain("CurrentRoot", binding);
        Assert.Contains("if (SubmittedInputs.TryGet(action, out _)) return", binding);
    }

    [Fact]
    public void AcceptedMappingFailuresUseTheEffectBarrierNotReasonWhitelist()
    {
        string source = Source("RecorderRuntime.cs");
        int start = source.IndexOf("internal static void ObserveAcceptedAction(");
        int end = source.IndexOf("internal static void ObservePlayCardExecutionAborted", start);
        string ingress = source[start..end];
        Assert.Contains("QuarantineAcceptedHumanEffect(", ingress);
        Assert.DoesNotContain("\n                Quarantine(", ingress);
        Assert.Contains("acceptedHumanEffect: true", source);
        Assert.Contains("!diagnostic && (acceptedHumanEffect", source);
    }

    [Fact]
    public void ExactCarrierIsSubscribedBeforeTheNativeRequestCanDeferOrCancel()
    {
        string patches = Source("NativeUiPatches.cs");
        int start = patches.IndexOf("internal static class NativeRewardPotionDiscardEnqueuePatch");
        int end = patches.IndexOf("internal static class NativeRewardPotionDiscardCommitPatch", start);
        Assert.Contains("RecorderRuntime.ObserveSubmittedUiCarrier(action)", patches[start..end]);
        string runtime = Source("RecorderRuntime.cs");
        Assert.Contains("if (lifecycleAction == null)", runtime);
        Assert.Contains("? exactSubscription.ActionWitnessId", runtime);
    }
}
