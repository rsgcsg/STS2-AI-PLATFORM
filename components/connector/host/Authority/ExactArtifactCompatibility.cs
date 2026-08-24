using System;
using System.Linq;

namespace STS2Connector.Authority;

internal sealed record ExactArtifactPermission(
    string Status,
    bool ActionExecutionAllowed,
    string Detail);

internal static class ExactArtifactCompatibility
{
    internal const string CanarySourceRevisionEnvironmentVariable =
        "STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION";
    internal const string SealedSourceRevision =
        "c38d4ad2e9d6eb029f8853ed852cce1152bc6d50";
    internal const string SealedArtifactSha256 =
        "5014224ce8a1f5a61455f21d6873a87052eac533acffce04ac3fb75195bff185";
    internal const string SealedArtifactMvid =
        "68f7a9aa-c293-4897-94cd-1e59ab6dd180";

    internal static ExactArtifactPermission Evaluate(
        string? sourceRevision,
        string? artifactSha256,
        string? artifactMvid,
        string? requestedCanarySourceRevision)
    {
        if (!IsExactRevision(sourceRevision)
            || string.IsNullOrWhiteSpace(artifactSha256)
            || string.IsNullOrWhiteSpace(artifactMvid))
        {
            return new ExactArtifactPermission(
                "artifact_identity_incomplete",
                false,
                "Host mutation is disabled because loaded artifact identity is incomplete.");
        }

        if (string.Equals(sourceRevision, SealedSourceRevision, StringComparison.Ordinal)
            && string.Equals(artifactSha256, SealedArtifactSha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(artifactMvid, SealedArtifactMvid, StringComparison.OrdinalIgnoreCase))
        {
            return new ExactArtifactPermission(
                "supported_exact",
                true,
                "The loaded Host artifact matches the sealed v1.0.0 tuple.");
        }

        bool canaryEnabled = IsExactRevision(requestedCanarySourceRevision)
            && string.Equals(
                requestedCanarySourceRevision,
                sourceRevision,
                StringComparison.Ordinal);
        return canaryEnabled
            ? new ExactArtifactPermission(
                "canary_exact",
                true,
                $"The loaded Host source {sourceRevision} was explicitly enabled for this process-local canary.")
            : new ExactArtifactPermission(
                "artifact_unqualified",
                false,
                "The loaded Host artifact is not sealed and its exact source revision was not explicitly enabled for this process.");
    }

    private static bool IsExactRevision(string? value) =>
        value?.Length == 40
        && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
