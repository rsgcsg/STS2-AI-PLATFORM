using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class TwoSignalCommitGateTests
{
    [Fact]
    public void InlineCompletionBeforeAcceptanceCommitsOnce()
    {
        var gate = new TwoSignalCommitGate();

        Assert.False(gate.ObserveSecond());
        Assert.True(gate.ObserveFirst());
        Assert.True(gate.TryReserveCommit());
        Assert.False(gate.TryReserveCommit());
    }

    [Fact]
    public void AcceptanceBeforeAsyncCompletionCommitsOnce()
    {
        var gate = new TwoSignalCommitGate();

        Assert.False(gate.ObserveFirst());
        Assert.True(gate.ObserveSecond());
        Assert.True(gate.TryReserveCommit());
        Assert.False(gate.TryReserveCommit());
    }

    [Fact]
    public void FailedPersistenceReleasesReservationWithoutInventingSignal()
    {
        var gate = new TwoSignalCommitGate();
        gate.ObserveFirst();
        gate.ObserveSecond();

        Assert.True(gate.TryReserveCommit());
        Assert.True(gate.TryReleaseCommit());
        Assert.True(gate.TryReserveCommit());
    }

    [Fact]
    public async Task ConcurrentTerminalCallbacksCanReserveOnlyOneCommit()
    {
        var gate = new TwoSignalCommitGate();
        gate.ObserveFirst();
        gate.ObserveSecond();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> Compete()
        {
            await start.Task;
            return gate.TryReserveCommit();
        }

        Task<bool>[] competitors = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(Compete))
            .ToArray();
        start.SetResult();

        bool[] results = await Task.WhenAll(competitors);
        Assert.Single(results, value => value);
    }
}
