using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using STS2Connector.Authority;
using STS2Connector.NativeUi;
using STS2Connector.PlayerEnvironment.Protocol;

namespace STS2Connector.PlayerEnvironment.Witness;

/// <summary>
/// A native action already accepted through the shipped UI. Native references
/// enter this process-local API only and are never serialized or made executable.
/// </summary>
public sealed record ProcessLocalObservedAction(
    string Verb,
    object? Subject,
    IReadOnlyDictionary<string, object> Arguments);

public sealed record ProcessLocalNativeMatch(
    string Status,
    int MatchCount,
    string? BoundActionId,
    PlayerEnvironmentBoundAction? BoundAction,
    string Evidence,
    string? Detail);

public sealed record ProcessLocalReadCapture(
    string Kind,
    string Status,
    PlayerEnvironmentReadResponse? Read,
    string? ErrorCode,
    string? Detail);

/// <summary>
/// One immutable public Snapshot plus the exact Host-local bindings from the
/// same observation. It can correlate an observed native action but cannot
/// authorize or deliver one.
/// </summary>
public sealed class ProcessLocalNativeWitnessFrame
{
    private readonly IReadOnlyDictionary<string, object> _exactEntities;
    private readonly IReadOnlySet<string> _exactBindingIds;

    internal ProcessLocalNativeWitnessFrame(
        PlayerEnvironmentSnapshot snapshot,
        PlayerEnvironmentCapabilitiesResponse capabilities,
        string sourceDigest,
        bool externalControllerActive,
        IReadOnlyDictionary<string, object> exactEntities,
        IReadOnlySet<string> exactBindingIds,
        IReadOnlyDictionary<string, ProcessLocalReadCapture>? reads = null)
    {
        Snapshot = snapshot;
        Capabilities = capabilities;
        SourceDigest = sourceDigest;
        ExternalControllerActive = externalControllerActive;
        _exactEntities = exactEntities;
        _exactBindingIds = exactBindingIds;
        Reads = reads ?? new Dictionary<string, ProcessLocalReadCapture>(StringComparer.Ordinal);
    }

    public PlayerEnvironmentSnapshot Snapshot { get; }

    public PlayerEnvironmentCapabilitiesResponse Capabilities { get; }

    public string SourceDigest { get; }

    public bool ExternalControllerActive { get; }

    public IReadOnlyDictionary<string, ProcessLocalReadCapture> Reads { get; }

    public ProcessLocalNativeMatch Resolve(ProcessLocalObservedAction observed)
    {
        if (!string.Equals(Snapshot.Status, "interactive", StringComparison.Ordinal)
            || !string.Equals(Snapshot.BoundActions.Status, "complete", StringComparison.Ordinal)
            || Snapshot.BoundActions.Actions.Count == 0)
        {
            return new ProcessLocalNativeMatch(
                "frame_not_authoritative",
                0,
                null,
                null,
                "no_match_attempted",
                "Only a complete interactive frozen catalog can be correlated.");
        }

        if (string.IsNullOrWhiteSpace(observed.Verb)
            || observed.Arguments == null
            || observed.Arguments.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null))
        {
            return new ProcessLocalNativeMatch(
                "invalid_observed_action",
                0,
                null,
                null,
                "no_match_attempted",
                "The observed native action must have a verb and non-null named operands.");
        }

        PlayerEnvironmentBoundAction[] matches = Snapshot.BoundActions.Actions
            .Where(action => _exactBindingIds.Contains(action.BoundActionId))
            .Where(action => Matches(action, observed))
            .ToArray();
        if (matches.Length != 1)
        {
            return new ProcessLocalNativeMatch(
                matches.Length == 0 ? "zero" : "ambiguous",
                matches.Length,
                null,
                null,
                "reference_equality_to_frozen_host_binding",
                matches.Length == 0
                    ? "No frozen BoundAction matched the exact observed native references."
                    : "More than one frozen BoundAction matched; correlation is quarantined.");
        }

        return new ProcessLocalNativeMatch(
            "exact_unique",
            1,
            matches[0].BoundActionId,
            matches[0],
            "reference_equality_to_frozen_host_binding",
            null);
    }

    private bool Matches(
        PlayerEnvironmentBoundAction action,
        ProcessLocalObservedAction observed)
    {
        if (!string.Equals(action.Verb, observed.Verb, StringComparison.Ordinal))
            return false;
        if (action.SubjectReferentId == null)
        {
            if (observed.Subject != null)
                return false;
        }
        else if (observed.Subject == null
                 || !IsSameEntity(action.SubjectReferentId, observed.Subject))
        {
            return false;
        }

        if (action.Arguments.Count != observed.Arguments.Count)
            return false;
        foreach (PlayerEnvironmentBoundActionArgument argument in action.Arguments)
        {
            if (!observed.Arguments.TryGetValue(argument.Role, out object? native)
                || !IsSameEntity(argument.ReferentId, native))
                return false;
        }
        return true;
    }

    private bool IsSameEntity(string referentId, object entity) =>
        _exactEntities.TryGetValue(referentId, out object? frozen)
        && ReferenceEquals(frozen, entity);
}

/// <summary>
/// Generic process-local observation seam for recorders and conformance tools.
/// It is deliberately absent from REST/MCP and has no mutation methods.
/// </summary>
public static class PlayerEnvironmentNativeWitness
{
    public static ProcessLocalNativeWitnessFrame Capture(
        IReadOnlyCollection<string>? requiredReadKinds = null)
    {
        SnapshotBuildResult frame =
            STS2Connector.PlayerEnvironment.PlayerEnvironmentService.BuildSnapshot(
                requiredReadKinds: requiredReadKinds);
        return CreateFrame(frame, requiredReadKinds);
    }

    public static ProcessLocalNativeWitnessFrame Capture(
        Func<string, IReadOnlyCollection<string>> requiredReadKindsForInteraction)
    {
        ArgumentNullException.ThrowIfNull(requiredReadKindsForInteraction);
        SnapshotBuildResult frame =
            STS2Connector.PlayerEnvironment.PlayerEnvironmentService.BuildSnapshot(
                requiredReadKindsForInteraction: requiredReadKindsForInteraction);
        return CreateFrame(frame, frame.ReadBuilds.Keys.ToArray());
    }

    private static ProcessLocalNativeWitnessFrame CreateFrame(
        SnapshotBuildResult frame,
        IReadOnlyCollection<string>? requiredReadKinds)
    {
        HashSet<string> referentIds = frame.Snapshot.BoundActions.Actions
            .SelectMany(action => action.Arguments.Select(argument => argument.ReferentId)
                .Append(action.SubjectReferentId))
            .Where(referentId => referentId != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        IReadOnlyDictionary<string, ProcessLocalReadCapture> reads =
            MaterializeReads(frame, requiredReadKinds);
        return new ProcessLocalNativeWitnessFrame(
            frame.Snapshot,
            STS2Connector.PlayerEnvironment.PlayerEnvironmentService.GetCapabilities(),
            ReadAssemblyMetadata("ConnectorPlayerEnvironmentSourceDigest", "PlayerEnvironmentSourceDigest"),
            MutationControlRuntime.Snapshot().Controller != null,
            NativeUiRuntime.Entities.CaptureExactReferences(referentIds),
            frame.Bindings.Keys.ToHashSet(StringComparer.Ordinal),
            reads);
    }

    private static IReadOnlyDictionary<string, ProcessLocalReadCapture> MaterializeReads(
        SnapshotBuildResult frame,
        IReadOnlyCollection<string>? requiredReadKinds)
    {
        if (requiredReadKinds == null || requiredReadKinds.Count == 0)
            return new Dictionary<string, ProcessLocalReadCapture>(StringComparer.Ordinal);
        var result = new Dictionary<string, ProcessLocalReadCapture>(StringComparer.Ordinal);
        foreach (string kind in requiredReadKinds
                     .Where(kind => !string.IsNullOrWhiteSpace(kind))
                     .Distinct(StringComparer.Ordinal))
        {
            string readId = $"read:{kind}";
            PlayerEnvironmentReadResolution resolution =
                STS2Connector.PlayerEnvironment.PlayerEnvironmentService.ResolveReadMaterialization(
                    frame.Snapshot,
                    readId,
                    frame.Snapshot.SnapshotId,
                    frame.ReadBuilds);
            if (resolution.ErrorCode != null)
            {
                result[kind] = new ProcessLocalReadCapture(
                    kind,
                    resolution.ErrorCode == "read_not_available" ? "not_available" : "failed",
                    null,
                    resolution.ErrorCode,
                    resolution.Detail);
                continue;
            }
            PlayerEnvironmentReadResult built =
                STS2Connector.PlayerEnvironment.PlayerEnvironmentService.BuildResolvedRead(
                    frame,
                    resolution,
                    frame.Snapshot.SnapshotId);
            result[kind] = built.Read == null
                ? new ProcessLocalReadCapture(
                    kind,
                    "failed",
                    null,
                    built.ErrorCode ?? "read_materialization_failed",
                    built.Detail)
                : new ProcessLocalReadCapture(kind, "materialized", built.Read, null, null);
        }
        return result;
    }

    private static string ReadAssemblyMetadata(params string[] keys)
    {
        AssemblyMetadataAttribute[] metadata = typeof(PlayerEnvironmentNativeWitness).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToArray();
        foreach (string key in keys)
        {
            string? value = metadata.FirstOrDefault(attribute =>
                    string.Equals(attribute.Key, key, StringComparison.Ordinal))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return "unavailable";
    }
}
