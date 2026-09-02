namespace STS2HumanAnnotator.Core;

/// <summary>
/// Identity and binding evidence shared by the current recorder and explicit
/// archival readers. These shapes are facts, not a recording-format authority.
/// The current store/audit gates their schema-bearing containers separately.
/// </summary>
public sealed record ExactArtifactIdentity(
    string Product,
    string Version,
    string SourceRevision,
    string SourceDigestSha256,
    string Sha256,
    string ModuleVersionId);

public sealed record ExactGameIdentity(
    string? Version,
    string? Commit,
    string MainAssemblySha256,
    string MainAssemblyModuleVersionId);

public sealed record RecorderEnvironmentIdentity(
    ExactGameIdentity Game,
    ExactArtifactIdentity Connector,
    ExactArtifactIdentity Annotator,
    string PlayerEnvironmentProtocol,
    string RuntimeInstanceId,
    string EnvironmentFingerprint,
    string ModsetStatus,
    string ModsetFingerprint);

public sealed record NativeWitnessEvidence(
    string Origin,
    string NativeActionType,
    string? SubjectWitnessId,
    IReadOnlyDictionary<string, string> ArgumentWitnessIds,
    DateTimeOffset AcceptedAt);

public sealed record ExactMappingEvidence(
    string Status,
    int MatchCount,
    string Basis,
    string? Detail);

public sealed record RecordedBoundAction(
    string BoundActionId,
    string Verb,
    string? SubjectReferentId,
    IReadOnlyDictionary<string, string> Arguments,
    string Label);

public sealed record RecordEligibility(
    string Status,
    IReadOnlyList<string> PassedGates,
    IReadOnlyList<string> NonClaims);

/// <summary>
/// Operational lifecycle/status evidence. The current runtime writes only the
/// current status schema; the archival reader can still inspect predecessor
/// status rows without making them current recording authority.
/// </summary>
public sealed record RecorderRuntimeStatus(
    int SchemaVersion,
    string Schema,
    string Status,
    DateTimeOffset ObservedAt,
    int ProcessId,
    string SessionId,
    string RecordingDirectory,
    RecorderEnvironmentIdentity? Environment,
    string? CurrentSnapshotId,
    string? PendingRecordId,
    string? Detail,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> NonClaims);

/// <summary>
/// Invalidation evidence has one shape across current and archival readers;
/// the enclosing contract/version determines which product may emit it.
/// </summary>
public sealed record HumanActionOccurrenceEvidence(
    string OccurrenceId,
    string NativeActionType,
    string Family,
    string Verb,
    string? NativeSubjectWitnessId,
    IReadOnlyDictionary<string, string> NativeOperands,
    string? NativeOwnerWitnessId,
    string? PausedParentActionWitnessId,
    string? PausedParentActionType,
    string? PausedParentState,
    string NativeMechanism,
    string Disposition);

/// <summary>
/// Validates immutable Human occurrence metadata carried by a fail-closed
/// invalidation. It is an auditable disposition, never an admission ledger or
/// replacement for canonical transition proof.
/// </summary>
public static class HumanActionOccurrenceEvidenceValidator
{
    public static IReadOnlyList<string> Validate(HumanActionOccurrenceEvidence? value)
    {
        if (value == null)
            return Array.Empty<string>();
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(value.OccurrenceId)
            || string.IsNullOrWhiteSpace(value.NativeActionType)
            || string.IsNullOrWhiteSpace(value.Family)
            || string.IsNullOrWhiteSpace(value.Verb)
            || string.IsNullOrWhiteSpace(value.NativeMechanism))
        {
            errors.Add("human_occurrence_identity_invalid");
        }
        if (!string.Equals(value.Disposition, "failed_closed", StringComparison.Ordinal))
            errors.Add("human_occurrence_disposition_invalid");
        if (value.NativeOperands == null
            || value.NativeOperands.Any(pair => string.IsNullOrWhiteSpace(pair.Key)
                                                || string.IsNullOrWhiteSpace(pair.Value)))
        {
            errors.Add("human_occurrence_operands_invalid");
        }
        return errors;
    }
}

public sealed record InvalidationRecord(
    int SchemaVersion,
    string Schema,
    string InvalidationId,
    string SessionId,
    string RunId,
    DateTimeOffset RecordedAt,
    string ReasonCode,
    string Detail,
    string? PreSnapshotId,
    string? NativeActionType,
    string EvidenceLevel)
{
    public HumanActionOccurrenceEvidence? HumanOccurrence { get; init; }
}
