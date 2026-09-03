namespace STS2HumanAnnotator.Core;

public sealed record RecorderFrameWorkPlan(bool RefreshStatus)
{
    public bool HasWork => RefreshStatus;
}

public static class RecorderFrameWorkPlanner
{
    public static readonly TimeSpan StatusRefreshInterval = TimeSpan.FromSeconds(1);

    public static RecorderFrameWorkPlan Plan(
        DateTimeOffset now,
        DateTimeOffset lastStatusRefreshAt,
        bool statusRefreshRequested)
    {
        bool refreshStatus = statusRefreshRequested
            && now - lastStatusRefreshAt >= StatusRefreshInterval;
        return new RecorderFrameWorkPlan(refreshStatus);
    }
}
