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
