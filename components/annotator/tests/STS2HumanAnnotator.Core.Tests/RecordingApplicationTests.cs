using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class RecordingApplicationTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public void MultipleSessionsFollowTheBoundedLifecycle()
    {
        RecordingLifecycleSnapshot initial = RecordingLifecycleSnapshot.Ready(T0);
        RecordingCommandResult first = RecordingLifecycleStateMachine.Apply(
            initial,
            RecordingCommandKind.StartNewSession,
            "session-1",
            T0.AddSeconds(1),
            pendingDecision: false);
        RecordingCommandResult paused = RecordingLifecycleStateMachine.Apply(
            first.Lifecycle,
            RecordingCommandKind.Pause,
            null,
            T0.AddSeconds(2),
            pendingDecision: false);
        RecordingCommandResult resumed = RecordingLifecycleStateMachine.Apply(
            paused.Lifecycle,
            RecordingCommandKind.Resume,
            null,
            T0.AddSeconds(3),
            pendingDecision: false);
        RecordingCommandResult closing = RecordingLifecycleStateMachine.Apply(
            resumed.Lifecycle,
            RecordingCommandKind.Close,
            null,
            T0.AddSeconds(4),
            pendingDecision: false);
        RecordingLifecycleSnapshot closed = RecordingLifecycleStateMachine.MarkClosed(
            closing.Lifecycle,
            T0.AddSeconds(5));
        RecordingCommandResult second = RecordingLifecycleStateMachine.Apply(
            closed,
            RecordingCommandKind.StartNewSession,
            "session-2",
            T0.AddSeconds(6),
            pendingDecision: false);

        Assert.True(first.Accepted);
        Assert.True(paused.Accepted);
        Assert.True(resumed.Accepted);
        Assert.True(closing.Accepted);
        Assert.False(closing.Pending);
        Assert.Equal(RecordingLifecycleState.Closed, closed.State);
        Assert.True(second.Accepted);
        Assert.Equal("session-2", second.Lifecycle.SessionId);
    }

    [Fact]
    public void PauseAndClosePreserveAnAdmittedPendingDecision()
    {
        RecordingLifecycleSnapshot recording = new(
            RecordingLifecycleState.Recording,
            "session-1",
            T0,
            "recording");

        RecordingCommandResult paused = RecordingLifecycleStateMachine.Apply(
            recording,
            RecordingCommandKind.Pause,
            null,
            T0.AddSeconds(1),
            pendingDecision: true);
        RecordingCommandResult closing = RecordingLifecycleStateMachine.Apply(
            paused.Lifecycle,
            RecordingCommandKind.Close,
            null,
            T0.AddSeconds(2),
            pendingDecision: true);

        Assert.True(paused.Accepted);
        Assert.Contains("still settle", paused.Detail, StringComparison.Ordinal);
        Assert.True(closing.Accepted);
        Assert.True(closing.Pending);
        Assert.Equal(RecordingLifecycleState.Closing, closing.Lifecycle.State);
    }

    [Fact]
    public void InvalidTransitionsDoNotMutateState()
    {
        RecordingLifecycleSnapshot initial = RecordingLifecycleSnapshot.Ready(T0);
        RecordingCommandResult paused = RecordingLifecycleStateMachine.Apply(
            initial,
            RecordingCommandKind.Pause,
            null,
            T0.AddSeconds(1),
            pendingDecision: false);

        Assert.False(paused.Accepted);
        Assert.Equal("invalid_transition", paused.Code);
        Assert.Equal(initial, paused.Lifecycle);
    }

    [Fact]
    public void EventStreamIsOrderedAndReportsReconnectGaps()
    {
        var stream = new RecordingEventStream(capacity: 2);
        RecordingEvent one = stream.Publish(RecordingEventKind.RuntimeReady, T0);
        RecordingEvent two = stream.Publish(RecordingEventKind.SessionStarted, T0.AddSeconds(1), "session-1");
        RecordingEvent three = stream.Publish(RecordingEventKind.SessionPaused, T0.AddSeconds(2), "session-1");

        RecordingEventBatch current = stream.ReadAfter(two.Sequence);
        RecordingEventBatch gap = stream.ReadAfter(0);

        Assert.Equal(1, one.Sequence);
        Assert.Equal(2, two.Sequence);
        Assert.Equal(3, three.Sequence);
        Assert.False(current.Gap);
        Assert.Single(current.Events);
        Assert.Equal(three, current.Events[0]);
        Assert.True(gap.Gap);
        Assert.Empty(gap.Events);
        Assert.Equal(2, gap.OldestAvailableSequence);
    }

    [Fact]
    public void CommandLedgerReturnsTheOriginalResultForDuplicateRequests()
    {
        var ledger = new RecordingCommandLedger(capacity: 2);
        RecordingLifecycleSnapshot lifecycle = RecordingLifecycleSnapshot.Ready(T0);
        var original = new RecordingCommandResult(
            true,
            false,
            "session_started",
            "started",
            lifecycle);
        var conflicting = original with { Code = "different" };

        ledger.Remember("command-1", original);
        ledger.Remember("command-1", conflicting);

        Assert.True(ledger.TryGet("command-1", out RecordingCommandResult? remembered));
        Assert.Equal(original, remembered);
    }

    [Fact]
    public void StagedCardPlayAllowsTheExpectedTransientSnapshotAdvance()
    {
        bool continuous = StagedCardPlayGuard.IsContinuous(
            "runtime-1",
            "environment-1",
            "combat-1",
            stagedSequence: 10,
            stagedAt: T0,
            "runtime-1",
            "environment-1",
            "combat-1",
            currentSequence: 11,
            observedAt: T0.AddMilliseconds(20),
            externalControllerActive: false,
            maximumAge: TimeSpan.FromSeconds(30));

        Assert.True(continuous);
    }

    [Theory]
    [InlineData("runtime-2", "environment-1", "combat-1", 11, false)]
    [InlineData("runtime-1", "environment-2", "combat-1", 11, false)]
    [InlineData("runtime-1", "environment-1", "combat-2", 11, false)]
    [InlineData("runtime-1", "environment-1", "combat-1", 9, false)]
    [InlineData("runtime-1", "environment-1", "combat-1", 11, true)]
    public void StagedCardPlayRejectsAuthorityOrContextDrift(
        string runtime,
        string environment,
        string interaction,
        long sequence,
        bool externalController)
    {
        bool continuous = StagedCardPlayGuard.IsContinuous(
            "runtime-1",
            "environment-1",
            "combat-1",
            stagedSequence: 10,
            stagedAt: T0,
            runtime,
            environment,
            interaction,
            sequence,
            observedAt: T0.AddMilliseconds(20),
            externalController,
            maximumAge: TimeSpan.FromSeconds(30));

        Assert.False(continuous);
    }
}
