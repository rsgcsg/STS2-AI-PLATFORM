using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Core.Tests;

// In-memory tracker fixtures are encoded through the one current writer.
// Production no longer exposes the historical inline-event append API.
internal static class SemanticRecordingFixture
{
    public static void AppendSemanticBoundaryEvent(this RecordingSessionStore store,
        SemanticBoundaryTraceEvent value) => store.AppendSemanticBoundaryEvents(new[] { value });

    public static void AppendSemanticBoundaryEvents(this RecordingSessionStore store,
        IReadOnlyList<SemanticBoundaryTraceEvent> values)
    {
        SemanticFrameReference? Frame(CurrentDecisionFrame? value) =>
            value == null ? null : store.PersistSemanticFrame(value);
        store.AppendSemanticEvidenceEvents(values.Select(value => new SemanticEvidenceEvent(
            SemanticEvidenceContract.SchemaVersion, SemanticEvidenceContract.EventSchema,
            value.EventId, value.SessionId, value.TimelineId, value.RunId, value.Sequence,
            value.ObservedAt, value.Kind, value.Action, value.ProofStatus,
            value.RelatedActionWitnessId,
            value.Boundary == null ? null : SemanticBoundaryObservationCodec.Encode(value.Boundary, store.PersistSemanticFrame),
            Frame(value.SemanticPre), Frame(value.SemanticSuccessor), value.Detail, value.NonClaims)
        {
            HumanObservationRef = Frame(value.HumanObservation),
            NativeCompletion = value.NativeCompletion,
            NativeContinuation = value.NativeContinuation,
            NativeHumanContinuation = value.NativeHumanContinuation,
            ExecutionSemanticActionSpaceRef = value.ExecutionSemanticActionSpace == null
                ? null : store.PersistExecutionSemanticActionSpace(value.ExecutionSemanticActionSpace)
        }).ToArray());
    }
}
