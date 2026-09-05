namespace STS2HumanAnnotator.Core;

public static class SemanticEvidenceContract
{
    public const int SchemaVersion = 4;
    public const string EventSchema = "sts2.human-annotator/semantic-evidence-event-4";
    public const int LegacySchemaVersion = 3;
    public const string LegacyEventSchema = "sts2.human-annotator/semantic-evidence-event-3";

    public static bool IsCurrent(int schemaVersion, string schema) =>
        schemaVersion == SchemaVersion && schema == EventSchema;

    public static bool IsSupported(int schemaVersion, string schema) =>
        IsCurrent(schemaVersion, schema)
        || (schemaVersion == LegacySchemaVersion && schema == LegacyEventSchema);
}

/// <summary>
/// An immutable reference to one exact CurrentDecisionFrame. The role of the
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
    // Current owner-ready evidence. Older archival events deserialize with null
    // and remain fail-closed for native-decision-owner-ready proofs.
    public NativeDecisionOwnerReadyEvidence? NativeDecisionOwnerReady { get; init; }
}

/// <summary>
/// Keeps the in-memory and durable boundary representations structurally
/// aligned. Frame persistence and frame resolution remain owned by the caller;
/// this codec only maps evidence fields and never establishes semantic proof.
/// </summary>
public static class SemanticBoundaryObservationCodec
{
    public static SemanticBoundaryObservationReference Encode(
        SemanticBoundaryObservation observation,
        Func<CurrentDecisionFrame, SemanticFrameReference> persistFrame)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(persistFrame);

        return new SemanticBoundaryObservationReference(
            observation.WitnessKind,
            observation.ObservedAt,
            observation.SnapshotId,
            observation.Status,
            observation.BoundActionsStatus,
            observation.InteractionId,
            observation.InteractionKind,
            observation.State == null ? null : persistFrame(observation.State),
            observation.ImmediatelyConsumedByActionWitnessId)
        {
            StateCompleteness = observation.StateCompleteness,
            RequiredReadsStatus = observation.RequiredReadsStatus,
            StateBlockers = observation.StateBlockers,
            NativeDecisionOwnerReady = observation.NativeDecisionOwnerReady
        };
    }

    public static SemanticBoundaryObservation Materialize(
        SemanticBoundaryObservationReference reference,
        CurrentDecisionFrame? resolvedState)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new SemanticBoundaryObservation(
            reference.WitnessKind,
            reference.ObservedAt,
            reference.SnapshotId,
            reference.Status,
            reference.BoundActionsStatus,
            reference.InteractionId,
            reference.InteractionKind,
            resolvedState,
            reference.ImmediatelyConsumedByActionWitnessId)
        {
            StateCompleteness = reference.StateCompleteness,
            RequiredReadsStatus = reference.RequiredReadsStatus,
            StateBlockers = reference.StateBlockers,
            NativeDecisionOwnerReady = reference.NativeDecisionOwnerReady
        };
    }
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
/// Exact native continuation identity attached when STS2 pauses one accepted
/// GameAction to obtain a nested player choice. It is neither a second action
/// authority nor a successor: it proves only that the same native parent has
/// reached STS2's PlayerChoice continuation seam.
/// </summary>
public sealed record NativeContinuationEvidence(
    string ContinuationId,
    string Kind,
    string ActionWitnessId,
    string? NativeOwnerWitnessId,
    string? NativeLineageWitnessId,
    bool Succeeded);

/// <summary>
/// One exact accepted Human continuation inside an already-owned parent root.
/// The child selector is not a second action root and this evidence is neither
/// native Commit nor successor authority.  It only preserves the exact
/// screen-owned accepted occurrence and its parent correlation.
/// </summary>
public sealed record NativeHumanContinuationEvidence(
    string OccurrenceId,
    string Family,
    string Verb,
    string ParentActionWitnessId,
    string NativeOwnerWitnessId,
    string NativeMechanism,
    string? NativeSubjectWitnessId,
    IReadOnlyDictionary<string, string> NativeOperands,
    string Disposition);

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
    public NativeContinuationEvidence? NativeContinuation { get; init; }
    public NativeHumanContinuationEvidence? NativeHumanContinuation { get; init; }
    public ExecutionSemanticActionSpaceReference? ExecutionSemanticActionSpaceRef { get; init; }
}
