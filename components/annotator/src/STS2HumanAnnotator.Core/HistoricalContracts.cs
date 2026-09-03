using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class HistoricalRecordingContract
{
    public const string ProductVersion = "0.3.0-rc.1";
    public const int SchemaVersion = 1;
    public const string RecordSchema = "sts2.human-annotator/decision-record-1";
    public const string InvalidationSchema = "sts2.human-annotator/invalidation-1";
    public const string ManifestSchema = "sts2.human-annotator/recording-manifest-1";
    public const string RuntimeStatusSchema = "sts2.human-annotator/runtime-status-1";
    public const string SessionBundleSchema = "sts2.human-annotator/session-bundle-1";
    public const string SessionBundleAuditSchema = "sts2.human-annotator/session-bundle-audit-1";
}

public sealed record HistoricalDecisionFrame(
    string SnapshotId,
    string InteractionId,
    string InteractionKind,
    string SurfaceSchema,
    string CatalogDigest,
    int CatalogCount,
    JsonNode Snapshot);

public sealed record HistoricalSuccessor(
    string SnapshotId,
    string Status,
    string InteractionId,
    string InteractionKind,
    DateTimeOffset ObservedAt,
    JsonNode Snapshot);

public sealed record HistoricalDecisionRecord(
    int SchemaVersion,
    string Schema,
    string RecordId,
    string SessionId,
    string RunId,
    long Sequence,
    DateTimeOffset RecordedAt,
    RecorderEnvironmentIdentity Environment,
    HistoricalDecisionFrame Pre,
    NativeWitnessEvidence NativeWitness,
    ExactMappingEvidence Mapping,
    RecordedBoundAction Action,
    HistoricalSuccessor Successor,
    string DecisionFamily,
    string Surface,
    RecordEligibility Eligibility);

public sealed record HistoricalRecordingManifest(
    int SchemaVersion,
    string Schema,
    string SessionId,
    DateTimeOffset CreatedAt,
    string RecorderVersion,
    string RecorderSourceRevision,
    string Platform,
    IReadOnlyList<string> SupportedFamilies,
    IReadOnlyList<string> NonClaims);

public sealed record HistoricalCoverageSummary(
    int SchemaVersion,
    string SessionId,
    long AdmittedRecords,
    long Invalidations,
    IReadOnlyDictionary<string, long> Families,
    IReadOnlyDictionary<string, long> InvalidationsByReason,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Read-rich frame shape carried by historical additive native-ledger rows.
/// It is retained only for archival validation; current semantic frames use
/// <see cref="CurrentDecisionFrame"/>.
/// </summary>
public sealed record HistoricalReadRichDecisionFrame(
    string SnapshotId,
    string InteractionId,
    string InteractionKind,
    string SurfaceSchema,
    string CatalogDigest,
    int CatalogCount,
    JsonNode Snapshot,
    IReadOnlyList<ReadEvidence> Reads);
