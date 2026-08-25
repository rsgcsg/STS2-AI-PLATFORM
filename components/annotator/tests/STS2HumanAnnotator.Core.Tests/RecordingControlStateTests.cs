using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class RecordingControlStateTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public void StartPauseResumeCloseFollowTheBoundedLifecycle()
    {
        RecordingControlSnapshot initial = RecordingControlSnapshot.Initial(T0);

        RecordingControlResult started = RecordingControlStateMachine.Apply(
            initial,
            RecordingControlCommand.Start,
            "session-1",
            T0.AddSeconds(1));
        RecordingControlResult paused = RecordingControlStateMachine.Apply(
            started.Snapshot,
            RecordingControlCommand.Pause,
            "session-1",
            T0.AddSeconds(2));
        RecordingControlResult resumed = RecordingControlStateMachine.Apply(
            paused.Snapshot,
            RecordingControlCommand.Resume,
            "session-1",
            T0.AddSeconds(3));
        RecordingControlResult closed = RecordingControlStateMachine.Apply(
            resumed.Snapshot,
            RecordingControlCommand.Close,
            "session-1",
            T0.AddSeconds(4));

        Assert.True(started.Accepted);
        Assert.True(paused.Accepted);
        Assert.True(resumed.Accepted);
        Assert.True(closed.Accepted);
        Assert.Equal(RecordingControlState.Closed, closed.Snapshot.State);
    }

    [Fact]
    public void InvalidTransitionsDoNotMutateState()
    {
        RecordingControlSnapshot initial = RecordingControlSnapshot.Initial(T0);

        RecordingControlResult paused = RecordingControlStateMachine.Apply(
            initial,
            RecordingControlCommand.Pause,
            "session-1",
            T0.AddSeconds(1));

        Assert.False(paused.Accepted);
        Assert.Equal("invalid_transition", paused.Code);
        Assert.Equal(initial, paused.Snapshot);
    }

    [Fact]
    public void ClosedStateIsTerminal()
    {
        RecordingControlSnapshot closed = new(
            RecordingControlState.Closed,
            "session-1",
            T0,
            "closed");

        RecordingControlResult resumed = RecordingControlStateMachine.Apply(
            closed,
            RecordingControlCommand.Resume,
            "session-1",
            T0.AddSeconds(1));

        Assert.False(resumed.Accepted);
        Assert.Equal(RecordingControlState.Closed, resumed.Snapshot.State);
    }
}
