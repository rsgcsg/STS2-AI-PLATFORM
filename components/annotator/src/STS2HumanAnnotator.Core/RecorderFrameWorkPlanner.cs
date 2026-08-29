namespace STS2HumanAnnotator.Core;

public sealed record RecorderFrameWorkPlan(
    bool RefreshStatus,
    bool ProbeRecoveryBoundary)
{
    public bool HasWork => RefreshStatus || ProbeRecoveryBoundary;
}

public static class RecorderFrameWorkPlanner
{
    public static readonly TimeSpan RecoveryProbeInterval = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan StatusRefreshInterval = TimeSpan.FromSeconds(1);

    public static RecorderFrameWorkPlan Plan(
        DateTimeOffset now,
        DateTimeOffset lastRecoveryProbeAt,
        DateTimeOffset lastStatusRefreshAt,
        bool statusRefreshRequested,
        bool recoveryBoundaryRequired)
    {
        bool refreshStatus = statusRefreshRequested
            && now - lastStatusRefreshAt >= StatusRefreshInterval;
        bool probeRecovery = recoveryBoundaryRequired
            && (refreshStatus || now - lastRecoveryProbeAt >= RecoveryProbeInterval);
        return new RecorderFrameWorkPlan(refreshStatus, probeRecovery);
    }
}
