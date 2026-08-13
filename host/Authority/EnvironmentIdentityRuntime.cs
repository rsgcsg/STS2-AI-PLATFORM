using System;
using System.Linq;
using MegaCrit.Sts2.Core.Debug;
using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.Authority;

/// <summary>
/// Exact process, artifact, game and Modset identity for Player Environment.
/// Compatibility identity does not grant UI action authority; current native
/// actionability is rediscovered and revalidated elsewhere.
/// </summary>
internal static class EnvironmentIdentityRuntime
{
    private static readonly string RuntimeInstanceId = Guid.NewGuid().ToString("N");

    internal static GameBuildIdentity ReadGame()
    {
        ReleaseInfo? release = null;
        try
        {
            release = ReleaseInfoManager.Instance.ReleaseInfo;
        }
        catch
        {
            // Missing release metadata remains explicit in the public identity.
        }

        int? assemblyHash = null;
        try
        {
            assemblyHash = AssemblyHasher.GetMainAssemblyHash();
        }
        catch
        {
            // Missing game identity disables mutation but not fair observation.
        }

        ModsetIdentity modset = LiveModsetIdentity.Read();
        bool executionIdentityComplete = !string.IsNullOrWhiteSpace(release?.Version)
                                         && !string.IsNullOrWhiteSpace(release?.Commit)
                                         && assemblyHash.HasValue
                                         && !string.IsNullOrWhiteSpace(
                                             HostArtifactIdentity.LoadedAssemblySha256)
                                         && IsExactSourceRevision(
                                             HostArtifactIdentity.SourceRevision)
                                         && IsExactSupportedModset(modset);
        var compatibility = new CompatibilityAssessment(
            executionIdentityComplete ? "identified" : "identity_incomplete",
            ActionExecutionAllowed: executionIdentityComplete,
            StateObservationAllowed: true,
            ReadAllowed: true,
            executionIdentityComplete
                ? "Exact runtime, game, artifact and Modset identity recorded; current native UI mechanics determine actionability."
                : "Fair observation remains available, but mutation is disabled until exact game and loaded-artifact identity are complete.");
        return new GameBuildIdentity(
            release?.Version,
            release?.Commit,
            release?.Branch,
            assemblyHash,
            compatibility,
            modset)
        {
            ReleaseDeclaredMainAssemblyHash = release?.MainAssemblyHash
        };
    }

    internal static LiveHostIdentity HostIdentity() => new(
        "sts2_live_player_environment_host",
        "STS2 Live Player Environment Host",
        ConnectorMod.Version,
        HostArtifactIdentity.SourceRevision ?? "unavailable",
        typeof(ConnectorMod).Assembly.ManifestModule.ModuleVersionId.ToString("D"),
        RuntimeInstanceId)
    {
        ArtifactSha256 = HostArtifactIdentity.LoadedAssemblySha256 ?? string.Empty
    };

    internal static InformationPolicyInfo InformationPolicy() => new(
        "player_visible_v1",
        "Information currently presented by, or normally inspectable through, the local player's game UI.",
        IncludesHiddenInformation: false,
        UnknownFieldBehavior: "omit_and_mark_incomplete");

    internal static bool ExecutionAvailable(GameBuildIdentity game) =>
        ExecutionAvailable(
            game,
            HostArtifactIdentity.LoadedAssemblySha256,
            HostArtifactIdentity.SourceRevision);

    internal static bool ExecutionAvailable(
        GameBuildIdentity game,
        string? loadedAssemblySha256,
        string? sourceRevision) =>
        game.Compatibility.ActionExecutionAllowed
        && game.Compatibility.StateObservationAllowed
        && !string.IsNullOrWhiteSpace(game.Version)
        && !string.IsNullOrWhiteSpace(game.Commit)
        && game.MainAssemblyHash.HasValue
        && !string.IsNullOrWhiteSpace(loadedAssemblySha256)
        && IsExactSourceRevision(sourceRevision)
        && IsExactSupportedModset(game.Modset);

    private static bool IsExactSupportedModset(ModsetIdentity? modset) =>
        string.Equals(
            modset?.Status,
            "exact_player_environment_only",
            StringComparison.Ordinal);

    private static bool IsExactSourceRevision(string? sourceRevision) =>
        sourceRevision?.Length == 40
        && sourceRe