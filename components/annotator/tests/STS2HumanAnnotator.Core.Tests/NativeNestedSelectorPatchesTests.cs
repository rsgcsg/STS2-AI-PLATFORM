using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class NativeNestedSelectorPatchesTests
{
    private sealed record NativeCallbackFixture(string Name, bool IsTerminal);

    [Fact]
    public void ManualPreviewRouteAdmitsOnlyThePreviewTerminalCallback()
    {
        string acceptedPatch = AcceptedPatchSource();
        NativeCallbackFixture[] clawsManualPreview =
        {
            new("ConfirmSelection", IsTerminal: false),
            new("CompleteSelection", IsTerminal: true)
        };

        Assert.False(clawsManualPreview[0].IsTerminal);
        Assert.True(clawsManualPreview[1].IsTerminal);
        Assert.Contains(
            "Required(typeof(NDeckTransformSelectScreen), \"CompleteSelection\", typeof(NButton))",
            acceptedPatch);
        Assert.DoesNotContain(
            "Required(typeof(NDeckTransformSelectScreen), \"ConfirmSelection\", typeof(NButton))",
            acceptedPatch);
    }

    [Fact]
    public void CompletedEmptySelectionRemainsAcceptedAndEmptyOperandsValidate()
    {
        string acceptedPatch = AcceptedPatchSource();

        // An exact terminal task with an empty result is a valid native result
        // for min-0 selectors; only an unavailable read is fail-closed.
        Assert.Contains("out object[] selected", acceptedPatch);
        Assert.Contains("selected.FirstOrDefault()", acceptedPatch);
        Assert.DoesNotContain("selected.Length == 0", acceptedPatch);
        Assert.Contains("unavailable != null", acceptedPatch);
        Assert.Contains("completion_result_unavailable", acceptedPatch);

        var occurrence = new HumanActionOccurrenceEvidence(
            "occurrence-empty-selector",
            "native_nested_selector",
            "generic_simple_card_selector",
            "select",
            null,
            new Dictionary<string, string>(),
            "selector-owner",
            "parent-action",
            "GameAction",
            "paused",
            "CardSelectCmd.FromSimpleGridForRewards",
            "failed_closed");

        Assert.Empty(HumanActionOccurrenceEvidenceValidator.Validate(occurrence));
    }

    private static string AcceptedPatchSource()
    {
        string sourcePath = FindSourcePath();
        string source = File.ReadAllText(sourcePath);
        const string start = "internal static class NativeNestedSelectorAcceptedPatch";
        const string end = "internal static class NativeNestedSelectorExitPatch";
        int begin = source.IndexOf(start, StringComparison.Ordinal);
        int finish = source.IndexOf(end, begin, StringComparison.Ordinal);
        Assert.True(begin >= 0, $"Missing source section: {start}");
        Assert.True(finish > begin, $"Missing source section end: {end}");
        return source[begin..finish];
    }

    private static string FindSourcePath()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current != null;
             current = current.Parent)
        {
            string candidate = Path.Combine(
                current.FullName,
                "components",
                "annotator",
                "src",
                "STS2HumanAnnotator.Mod",
                "NativeNestedSelectorPatches.cs");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            "Could not locate NativeNestedSelectorPatches.cs from the test output directory.");
    }
}
