using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class HumanRecorderV2Contract
{
    public const int SchemaVersion = 2;
    public const string RecordSchema = "sts2.human-annotator/decision-record-2";
    public const string ManifestSchema = "sts2.human-annotator/recording-manifest-2";
    public const string InvalidationSchema = "sts2.human-annotator/invalidation-2";
    public const string CoverageSchema = "sts2.human-annotator/coverage-2";
    public const string CaptureProfileSchema = "sts2.ai-platform/human-capture-profile-2";
    public const string ReadEvidenceSchema = "sts2.human-annotator/read-evidence-2";
    public const string RunJournalSchema = "sts2.human-annotator/run-journal-event-2";
    public const string SessionBundleSchema = "sts2.human-annotator/session-bundle-2";
    public const string SessionBundleAuditSchema = "sts2.human-annotator/session-bundle-audit-2";
}

public sealed record CaptureReadRequirement(
    string Phase,
    string Kind,
    bool Required,
    string? InteractionKind = null);

public sealed record HumanCaptureProfile(
    int SchemaVersion,
    string Schema,
    string ProfileId,
    string RecordSchema,
    IReadOnlyList<string> SupportedActionFamilies,
    IReadOnlyList<CaptureReadRequirement> Reads,
    IReadOnlyList<string> NonClaims);

public sealed record ReadEvidence(
    int SchemaVersion,
    string Schema,
    string ReadEvidenceId,
    string ReadId,
    string Kind,
    string SnapshotId,
    string RuntimeInstanceId,
    string EnvironmentFingerprint,
    string Status,
    string? ContentSchema,
    JsonNode? Completeness,
    string? PayloadRef,
    string? PayloadSha256,
    DateTimeOffset CapturedAt,
    string? ErrorCode,
    string? Detail);

public sealed record CapturedReadPayload(
    string ReadId,
    string Kind,
    string SnapshotId,
    string RuntimeInstanceId,
    string EnvironmentFingerprint,
    string Status,
    string? ContentSchema,
    JsonNode? Content,
    JsonNode? Completeness,
    DateTimeOffset CapturedAt,
    string? ErrorCode,
    string? Detail);

public sealed record FrozenDecisionFrameV2(
    string SnapshotId,
    string InteractionId,
    string InteractionKind,
    string SurfaceSchema,
    string CatalogDigest,
    int CatalogCount,
    JsonNode Snapshot,
    IReadOnlyList<ReadEvidence> Reads);

public sealed record StableSuccessorV2(
    string SnapshotId,
    string Status,
    string InteractionId,
    string InteractionKind,
    DateTimeOffset ObservedAt,
    JsonNode Snapshot,
    IReadOnlyList<ReadEvidence> Reads);

public sealed record HumanDecisionRecordV2(
    int SchemaVersion,
    string Schema,
    string RecordId,
    string SessionId,
    string RunId,
    string TimelineId,
    long Sequence,
    DateTimeOffset RecordedAt,
    RecorderEnvironmentIdentity Environment,
    string CaptureProfileId,
    FrozenDecisionFrameV2 Pre,
    NativeWitnessEvidence NativeWitness,
    ExactMappingEvidence Mapping,
    RecordedBoundAction Action,
    StableSuccessorV2 Successor,
    string DecisionFamily,
    string Surface,
    RecordEligibility Eligibility);

public sealed record RunJournalEvent(
    int SchemaVersion,
    string Schema,
    string EventId,
    string SessionId,
    string RunId,
    string TimelineId,
    long Sequence,
    DateTimeOffset RecordedAt,
    string Kind,
    string? RecordId,
    string? SnapshotId,
    string? Detail);

public sealed record RecordingManifestV2(
    int SchemaVersion,
    string Schema,
    string SessionId,
    string TimelineId,
    DateTimeOffset CreatedAt,
    string RecorderVersion,
    string RecorderSourceRevision,
    string Platform,
    string CaptureProfileId,
    string CaptureProfileSha256,
    IReadOnlyList<string> SupportedFamilies,
    IReadOnlyList<string> NonClaims);

public sealed record CoverageSummaryV2(
    int SchemaVersion,
    string Schema,
    string SessionId,
    long AdmittedRecords,
    long Invalidations,
    long ReadMaterialized,
    long ReadFailed,
    IReadOnlyDictionary<string, long> Families,
    IReadOnlyDictionary<string, long> ReadsByKind,
    IReadOnlyDictionary<string, long> InvalidationsByReason,
    DateTimeOffset UpdatedAt);
