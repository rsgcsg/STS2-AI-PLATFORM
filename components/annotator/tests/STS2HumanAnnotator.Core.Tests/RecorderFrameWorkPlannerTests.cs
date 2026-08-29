using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class RecorderFrameWorkPlannerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddSeconds(10);

    [Fact]
    public void IdleRecordingDoesNoFrameWorkBetweenStatusRefreshes()
    {
        RecorderFrameWorkPlan plan = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddSeconds(-5),
            Now.AddMilliseconds(-100),
            statusRefreshRequested: false,
            recoveryBoundaryRequired: false);

        Assert.False(plan.HasWork);
    }

    [Fact]
    public void RecoveryDebtAloneSchedulesOnlyTheBoundedProbe()
    {
        RecorderFrameWorkPlan plan = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddMilliseconds(-50),
            Now.AddMilliseconds(-100),
            statusRefreshRequested: false,
            recoveryBoundaryRequired: true);

        Assert.False(plan.RefreshStatus);
        Assert.True(plan.ProbeRecoveryBoundary);
    }

    [Fact]
    public void StatusRefreshReusesOneCaptureForConcurrentRecoveryWork()
    {
        RecorderFrameWorkPlan plan = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddMilliseconds(-1),
            Now.AddSeconds(-1),
            statusRefreshRequested: true,
            recoveryBoundaryRequired: true);

        Assert.True(plan.RefreshStatus);
        Assert.True(plan.ProbeRecoveryBoundary);
    }

    [Fact]
    public void RecoveryProbeRemainsThrottled()
    {
        RecorderFrameWorkPlan plan = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddMilliseconds(-49),
            Now.AddMilliseconds(-100),
            statusRefreshRequested: false,
            recoveryBoundaryRequired: true);

        Assert.False(plan.HasWork);
    }

    [Fact]
    public void RequestedStatusRefreshIsThrottledWithoutBecomingPeriodicPolling()
    {
        RecorderFrameWorkPlan throttled = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddSeconds(-5),
            Now.AddMilliseconds(-999),
            statusRefreshRequested: true,
            recoveryBoundaryRequired: false);
        RecorderFrameWorkPlan due = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddSeconds(-5),
            Now.AddSeconds(-1),
            statusRefreshRequested: true,
            recoveryBoundaryRequired: false);

        Assert.False(throttled.HasWork);
        Assert.True(due.RefreshStatus);
        Assert.False(due.ProbeRecoveryBoundary);
    }
}
