namespace STS2HumanAnnotator.Core;

public static class RecordingEnvironmentAdmission
{
    public static bool IsExactModset(string status) =>
        status is "exact_platform_modset" or "canary_exact_observer_modset";
}
