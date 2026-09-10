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
