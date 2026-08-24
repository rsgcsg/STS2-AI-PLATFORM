using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class HumanRecorderContract
{
    public const string ProductVersion = "0.2.0-rc.2";
    public const int SchemaVersion = 1;
    public const string RecordSchema = "sts2.human-annotator/decision-record-1";
    public const string InvalidationSchema = "sts2.human-annotator/invalidation-1";
    public const string ManifestSchema = "sts2.human-annotator/recording-manifest-1";
    public const string RuntimeStatusSchema = "sts2.human-annotator/runtime-status-1";
    public const string SessionBundleSchema = "sts2.human-annotator/session-bundle-1";
    public const string SessionBundleAuditSchema = "sts2.human-annotator/session-bundle-audit-1";
}

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

public sealed record FrozenDecisionFrame(
    string SnapshotId,
    string InteractionId,
    string InteractionKind,
    string SurfaceSchema,
    string CatalogDigest,
    int CatalogCount,
    JsonNode Snapshot);

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

public sealed record StableSuccessor(
    string SnapshotId,
    string Status,
    string InteractionId,
    string InteractionKind,
    DateTimeOffset ObservedAt,
    JsonNode Snapshot);

public sealed record RecordEligibility(
    string Status,
    IReadOnlyList<string> PassedGates,
    IReadOnlyList<string> NonClaims);

public sealed record HumanDecisionRecord(
    int SchemaVersion,
    string Schema,
    string RecordId,
    string SessionId,
    string RunId,
    long Sequence,
    DateTimeOffset RecordedAt,
    RecorderEnvironmentIdentity Environment,
    FrozenDecisionFrame Pre,
    NativeWitnessEvidence NativeWitness,
    ExactMappingEvidence Mapping,
    RecordedBoundAction Action,
    StableSuccessor Successor,
    string DecisionFamily,
    string Surface,
    RecordEligibility Eligibility);

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
    string EvidenceLevel);

public sealed record RecordingManifest(
    int SchemaVersion,
    string Schema,
    string SessionId,
    DateTimeOffset CreatedAt,
    string RecorderVersion,
    string RecorderSourceRevision,
    string Platform,
    IReadOnlyList<string> SupportedFamilies,
    IReadOnlyList<string> NonClaims);

public sealed record CoverageSummary(
    int SchemaVersion,
    string SessionId,
    long AdmittedRecords,
    long Invalidations,
    IReadOnlyDictionary<string, long> Families,
    IReadOnlyDictionary<string, long> InvalidationsByReason,
    DateTimeOffset UpdatedAt);

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
