namespace STS2HumanAnnotator.Core;

public static class SemanticEvidenceContract
{
    public const int SchemaVersion = 3;
    public const string EventSchema = "sts2.human-annotator/semantic-evidence-event-3";
}

/// <summary>
/// An immutable reference to one exact FrozenDecisionFrame. The role of the
/// frame is carried by the event property that contains this reference.
/// </summary>
public sealed record SemanticFrameReference(
    string SnapshotId,
    string ContentSha256,
    string ObjectRef);

public sealed record SemanticBoundaryObservationReference(
    string WitnessKind,
    DateTimeOffset ObservedAt,
    string SnapshotId,
    string Status,
    string BoundActionsStatus,
    string InteractionId,
    string InteractionKind,
    SemanticFrameReference? StateRef,
    string? ImmediatelyConsumedByActionWitnessId)
{
    public string StateCompleteness { get; init; } = StateRef == null ? "unavailable" : "complete";
    public string RequiredReadsStatus { get; init; } = StateRef == null ? "unavailable" : "complete";
    public IReadOnlyList<string> StateBlockers { get; init; } = Array.Empty<string>();
    // Additive schema-3 evidence. Older events deserialize with null and remain
    // fail-closed for native-decision-owner-ready proofs.
    public NativeDecisionOwnerReadyEvidence? NativeDecisionOwnerReady { get; init; }
}

/// <summary>
/// Native completion identity attached to the semantic proof it caused. This
/// is evidence metadata only; it never authorizes or executes an action.
/// </summary>
public sealed record NativeCompletionEvidence(
    string CompletionId,
    string Family,
    string Kind,
    string? ActionWitnessId,
    string? TaskWitnessId,
    string? NativeOwnerWitnessId,
    string? NativeOperandWitnessId,
    string? NativeLineageWitnessId,
    bool Succeeded);

/// <summary>
/// Ordered semantic timeline event. Frames are stored once and referenced by
/// role so lifecycle facts stay compact without weakening causal validation.
/// </summary>
public sealed record SemanticEvidenceEvent(
    int SchemaVersion,
    string Schema,
    string EventId,
    string SessionId,
    string TimelineId,
    string RunId,
    long Sequence,
    DateTimeOffset ObservedAt,
    string Kind,
    SemanticActionReference Action,
    string ProofStatus,
    string? RelatedActionWitnessId,
    SemanticBoundaryObservationReference? Boundary,
    SemanticFrameReference? ExecutionPreRef,
    SemanticFrameReference? SuccessorRef,
    string? Detail,
    IReadOnlyList<string> NonClaims)
{
    public SemanticFrameReference? HumanObservationRef { get; init; }
    public NativeCompletionEvidence? NativeCompletion { get; init; }
}
