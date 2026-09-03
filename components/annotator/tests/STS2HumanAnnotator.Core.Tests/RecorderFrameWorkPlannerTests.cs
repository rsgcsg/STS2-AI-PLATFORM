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
            Now.AddMilliseconds(-100),
            statusRefreshRequested: false);

        Assert.False(plan.HasWork);
    }

    [Fact]
    public void ExplicitStatusRefreshSchedulesOneCapture()
    {
        RecorderFrameWorkPlan plan = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddSeconds(-1),
            statusRefreshRequested: true);

        Assert.True(plan.RefreshStatus);
    }

    [Fact]
    public void RequestedStatusRefreshIsThrottledWithoutBecomingPeriodicPolling()
    {
        RecorderFrameWorkPlan throttled = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddMilliseconds(-999),
            statusRefreshRequested: true);
        RecorderFrameWorkPlan due = RecorderFrameWorkPlanner.Plan(
            Now,
            Now.AddSeconds(-1),
            statusRefreshRequested: true);

        Assert.False(throttled.HasWork);
        Assert.True(due.RefreshStatus);
    }
}
