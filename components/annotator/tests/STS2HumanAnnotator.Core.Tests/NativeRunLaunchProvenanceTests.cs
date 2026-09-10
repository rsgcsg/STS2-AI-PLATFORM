using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class NativeRunLaunchProvenanceTests
{
    [Fact]
    public void SavedAndUnknownLaunchCannotClaimNewRunEvenAfterNewSetup()
    {
        var provenance = new NativeRunLaunchProvenance<object>();
        var fresh = new object();
        var saved = new object();
        provenance.ObserveSetup(fresh, true);
        provenance.ObserveSetup(saved, false);
        Assert.Equal("run_started_native", provenance.JournalKind(fresh));
        Assert.Equal("run_resumed_native", provenance.JournalKind(saved));
        Assert.Equal("run_launched_native_origin_unknown", provenance.JournalKind(new object()));
        Assert.Equal("run_started_native", provenance.JournalKind(fresh));
    }
    [Fact]
    public void ConflictingSetupCannotBeUpgradedByAnotherObservation()
    {
        var provenance = new NativeRunLaunchProvenance<object>();
        var state = new object();
        provenance.ObserveSetup(state, true);
        provenance.ObserveSetup(state, true);
        Assert.Equal("run_started_native", provenance.JournalKind(state));
        provenance.ObserveSetup(state, false);
        provenance.ObserveSetup(state, true);
        Assert.Equal("run_launched_native_origin_unknown", provenance.JournalKind(state));
    }
}
