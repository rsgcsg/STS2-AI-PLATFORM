using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class RecordingRunLifecycleTests
{
    [Fact]
    public void PollBeforeLaunchRetainsRunIdentityAndNativeStartWitness()
    {
        RecordingRunLifecycle lifecycle = new();
        Assert.Equal(RecordingRunObservation.ObservedInProgress, lifecycle.ObserveInProgress(true));
        string runId = lifecycle.RunId;
        Assert.Equal(RecordingRunObservation.StartedNative, lifecycle.ObserveNativeStarted());
        Assert.Equal(runId, lifecycle.RunId);
        Assert.Equal(RecordingRunObservation.None, lifecycle.ObserveNativeStarted());
        Assert.Equal(RecordingRunObservation.None, lifecycle.ObserveInProgress(true));
    }

    [Fact]
    public void LaunchBeforePollEmitsOneNativeStartAndOneNativeEnd()
    {
        RecordingRunLifecycle lifecycle = new();
        Assert.Equal(RecordingRunObservation.StartedNative, lifecycle.ObserveNativeStarted());
        Assert.Equal("run-0001", lifecycle.RunId);
        Assert.Equal(RecordingRunObservation.None, lifecycle.ObserveInProgress(true));
        Assert.Equal(RecordingRunObservation.EndedNative, lifecycle.ObserveNativeEnded());
        Assert.Equal(RecordingRunObservation.None, lifecycle.ObserveNativeEnded());
        // Native terminal precedes removal of the old RunState.
        Assert.Equal(RecordingRunObservation.None, lifecycle.ObserveInProgress(true));
        Assert.Equal(RecordingRunObservation.None, lifecycle.ObserveInProgress(false));
        Assert.Equal("run-0001", lifecycle.RunId);
        Assert.Equal(RecordingRunObservation.StartedNative, lifecycle.ObserveNativeStarted());
        Assert.Equal("run-0002", lifecycle.RunId);
    }

    [Fact]
    public void NativeNewRunDoesNotRequireAFalsePollAfterPreviousNativeEnd()
    {
        RecordingRunLifecycle lifecycle = new();
        lifecycle.ObserveNativeStarted();
        lifecycle.ObserveNativeEnded();
        Assert.Equal(RecordingRunObservation.StartedNative, lifecycle.ObserveNativeStarted());
        Assert.Equal("run-0002", lifecycle.RunId);
        Assert.Equal(RecordingRunObservation.EndedNative, lifecycle.ObserveNativeEnded());
    }

    [Fact]
    public void LateJoinNeverInventsANativeStartOrTerminalFromPolling()
    {
        RecordingRunLifecycle lifecycle = new();
        Assert.Equal(RecordingRunObservation.ObservedInProgress, lifecycle.ObserveInProgress(true));
        Assert.Equal(RecordingRunObservation.None, lifecycle.ObserveInProgress(true));
        Assert.Equal(RecordingRunObservation.EndedUnproved, lifecycle.ObserveInProgress(false));
        Assert.Equal(RecordingRunObservation.None, lifecycle.ObserveInProgress(false));
        Assert.Equal(RecordingRunObservation.ObservedInProgress, lifecycle.ObserveInProgress(true));
        Assert.Equal("run-0002", lifecycle.RunId);
        Assert.Equal(RecordingRunObservation.StartedNative, lifecycle.ObserveNativeStarted());
        Assert.Equal("run-0002", lifecycle.RunId);
    }

    [Fact]
    public void LateJoinCanRetainAnExactNativeEndWithoutClaimingNativeStart()
    {
        RecordingRunLifecycle lifecycle = new();
        lifecycle.ObserveInProgress(true);
        Assert.Equal(RecordingRunObservation.EndedNative, lifecycle.ObserveNativeEnded());
        Assert.Equal("run-0001", lifecycle.RunId);
    }

    [Fact]
    public void SessionResetDoesNotTransferLifecycleWitnessesOrRunNumber()
    {
        RecordingRunLifecycle lifecycle = new();
        lifecycle.ObserveNativeStarted();
        lifecycle.ObserveNativeEnded();
        lifecycle.Reset();
        Assert.Equal("run-unassigned", lifecycle.RunId);
        Assert.Equal(RecordingRunObservation.ObservedInProgress, lifecycle.ObserveInProgress(true));
        Assert.Equal("run-0001", lifecycle.RunId);
        Assert.Equal(RecordingRunObservation.StartedNative, lifecycle.ObserveNativeStarted());
        Assert.Equal("run-0001", lifecycle.RunId);
    }
}
