using STS2Connector.NativeUi;

namespace STS2Connector.Tests;

public sealed class SnapshotIdentityTrackerTests
{
    [Fact]
    public void StableSignatureKeepsStateIdentity()
    {
        var tracker = new SnapshotIdentityTracker("testsession");

        var first = tracker.Observe("signature-a");
        var second = tracker.Observe("signature-a");

        Assert.Equal("state_testsession_1", first.StateId);
        Assert.Equal(first, second);
    }

    [Fact]
    public void SemanticChangeAdvancesStateIdentity()
    {
        var tracker = new SnapshotIdentityTracker("testsession");

        var first = tracker.Observe("signature-a");
        var second = tracker.Observe("signature-b");

        Assert.Equal(1, first.Sequence);
        Assert.Equal(2, second.Sequence);
        Assert.NotEqual(first.StateI