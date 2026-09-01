namespace STS2HumanAnnotator.Core;

/// <summary>
/// Non-authorizing compatibility projection from one already-proved semantic
/// transition into the durable Decision V2 / canonical-transition formats.
/// Causal truth remains owned by <see cref="SemanticBoundaryTracker"/>; this
/// helper cannot settle an action or manufacture a successor boundary.
/// </summary>
public static class SemanticTransitionProjection
{
    public static HumanDecisionRecordV2 CreateDecision(
        SemanticBoundaryTraceDraft draft,
        RecorderEnvironmentIdentity environment,
        string sessionId,
        string timelineId,
        string captureProfileId)
    {
        if (draft.Kind != SemanticBoundaryTraceKinds.TransitionProved
            || draft.SemanticPre == null
            || draft.SemanticSuccessor == null
            || draft.Boundary == null
            || !draft.Boundary.CanProveSemanticBoundary)
        {
            throw new InvalidDataException(
                "Only a complete, already-proved semantic transition can be projected.");
        }
        if (draft.SemanticPre.SnapshotId == draft.SemanticSuccessor.SnapshotId)
            throw new InvalidDataException("A canonical successor must advance Snapshot identity.");
        if (draft.Action.NativeWitness == null
            || draft.Action.Mapping == null
            || draft.Action.BoundAction == null)
        {
            throw new InvalidDataException(
                "A semantic transition projection requires exact Human/native binding evidence.");
        }

        string decisionFamily = draft.SemanticPre.InteractionKind.StartsWith(
                "combat",
                StringComparison.Ordinal)
            ? "ordinary_combat"
            : draft.SemanticPre.InteractionKind;

        return new HumanDecisionRecordV2(
            HumanRecorderV2Contract.SchemaVersion,
            HumanRecorderV2Contract.RecordSchema,
            draft.Action.RecordId,
            sessionId,
            draft.Action.RunId,
            timelineId,
            draft.Action.ActionSequence,
            draft.Boundary.ObservedAt,
            environment,
            captureProfileId,
            draft.SemanticPre,
            draft.Action.NativeWitness,
            draft.Action.Mapping,
            draft.Action.BoundAction,
            new StableSuccessorV2(
                draft.SemanticSuccessor.SnapshotId,
                draft.Boundary.Status,
                draft.SemanticSuccessor.InteractionId,
                draft.SemanticSuccessor.InteractionKind,
                draft.Boundary.ObservedAt,
                draft.SemanticSuccessor.Snapshot,
                draft.SemanticSuccessor.Reads),
            decisionFamily,
            draft.SemanticPre.SurfaceSchema,
            new RecordEligibility(
                "admitted",
                new[]
                {
                    "singleplayer",
                    "exact_artifact_identity",
                    "exact_recording_modset",
                    "complete_semantic_pre",
                    "exact_unique_reference_mapping",
                    "trusted_semantic_successor_boundary",
                    "no_intervening_human_mutation",
                    "native_terminal_or_direct_commit_observed"
                },
                new[]
                {
                    "not_business_completion",
                    "not_human_validated_until_owner_review",
                    "capture_profile_scoped"
                }));
    }

    public static CanonicalTransitionEvidence CreateCanonical(
        SemanticBoundaryTraceDraft draft,
        SemanticFrameReference preStateRef,
        SemanticFrameReference successorRef,
        string sessionId,
        string timelineId)
    {
        if (draft.Kind != SemanticBoundaryTraceKinds.TransitionProved
            || draft.SemanticPre == null
            || draft.SemanticSuccessor == null
            || draft.Action.BoundAction == null)
        {
            throw new InvalidDataException(
                "Canonical evidence can only be projected from a proved semantic transition.");
        }
        if (preStateRef.SnapshotId != draft.SemanticPre.SnapshotId
            || successorRef.SnapshotId != draft.SemanticSuccessor.SnapshotId)
        {
            throw new InvalidDataException(
                "Canonical frame references must match the proved semantic transition.");
        }

        return new CanonicalTransitionEvidence(
            CanonicalTransitionEvidenceContract.SchemaVersion,
            CanonicalTransitionEvidenceContract.Schema,
            $"canonical-{draft.Action.RecordId}",
            sessionId,
            timelineId,
            draft.Action.RunId,
            draft.Action.ActionSequence,
            draft.Boundary?.ObservedAt ?? DateTimeOffset.UtcNow,
            CanonicalTransitionEvidenceContract.CollectionMode,
            $"epoch-{preStateRef.ContentSha256}",
            draft.Action.ActionWitnessId,
            draft.Action.NativeMechanism,
            preStateRef,
            draft.Action.BoundAction,
            successorRef,
            "canonical_s_a_s_prime",
            new[]
            {
                "complete_pre_state_and_catalog",
                "chosen_action_exactly_once_in_pre_catalog",
                "one_mutation_in_flight",
                "native_terminal_or_direct_commit_observed",
                "no_intervening_human_mutation",
                "complete_authoritative_successor"
            },
            new[]
            {
                "not_business_completion",
                "not_human_validated_until_owner_review",
                "capture_profile_scoped"
            });
    }
}
