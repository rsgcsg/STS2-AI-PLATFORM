namespace STS2HumanAnnotator.Core;

/// <summary>
/// Non-authorizing compatibility projection from one already-proved semantic
/// transition into the durable current decision and canonical-transition formats.
/// Causal truth remains owned by <see cref="SemanticBoundaryTracker"/>; this
/// helper cannot settle an action or manufacture a successor boundary.
/// </summary>
public static class SemanticTransitionProjection
{
    public static CurrentDecisionRecord CreateDecision(
        SemanticBoundaryTraceDraft draft,
        RecorderEnvironmentIdentity environment,
        string sessionId,
        string timelineId,
        string captureProfileId)
    {
        if (draft.Kind != SemanticBoundaryTraceKinds.TransitionProved
            || draft.HumanObservation == null
            || draft.SemanticSuccessor == null
            || draft.Boundary == null
            || !draft.Boundary.CanProveSemanticBoundary)
        {
            throw new InvalidDataException(
                "Only a complete, already-proved semantic transition can be projected.");
        }
        if (draft.SemanticPre?.SnapshotId == draft.SemanticSuccessor.SnapshotId)
            throw new InvalidDataException("A canonical successor must advance Snapshot identity.");
        if (draft.Action.NativeWitness == null
            || draft.Action.Mapping == null
            || draft.Action.BoundAction == null)
        {
            throw new InvalidDataException(
                "A semantic transition projection requires exact Human/native binding evidence.");
        }

        string decisionFamily = draft.HumanObservation.InteractionKind.StartsWith(
                "combat",
                StringComparison.Ordinal)
            ? "ordinary_combat"
            : draft.HumanObservation.InteractionKind;

        return new CurrentDecisionRecord(
            CurrentRecordingContract.SchemaVersion,
            CurrentRecordingContract.RecordSchema,
            draft.Action.RecordId,
            sessionId,
            draft.Action.RunId,
            timelineId,
            draft.Action.ActionSequence,
            draft.Boundary.ObservedAt,
            environment,
            captureProfileId,
            draft.HumanObservation,
            draft.Action.NativeWitness,
            draft.Action.Mapping,
            draft.Action.BoundAction,
            new CurrentSuccessor(
                draft.SemanticSuccessor.SnapshotId,
                draft.Boundary.Status,
                draft.SemanticSuccessor.InteractionId,
                draft.SemanticSuccessor.InteractionKind,
                draft.Boundary.ObservedAt,
                draft.SemanticSuccessor.Snapshot,
                draft.SemanticSuccessor.Reads),
            decisionFamily,
            draft.HumanObservation.SurfaceSchema,
            new RecordEligibility(
                "admitted",
                new[]
                {
                    "singleplayer",
                    "exact_artifact_identity",
                    "exact_recording_modset",
                    "complete_human_observation_catalog",
                    "exact_unique_reference_mapping",
                    "trusted_semantic_successor_boundary",
                    "no_intervening_human_mutation",
                    "native_terminal_direct_commit_or_player_choice_continuation_observed"
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
        ExecutionSemanticActionSpaceReference? executionSemanticActionSpaceRef,
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

        string actionSpaceAuthority;
        if (draft.ExecutionSemanticActionSpace != null)
        {
            IReadOnlyList<string> errors = ExecutionSemanticActionSpaceValidator.Validate(
                draft.ExecutionSemanticActionSpace,
                draft.Action);
            if (errors.Count > 0)
                throw new InvalidDataException(
                    $"Execution semantic action space is invalid: {string.Join(',', errors)}");
            if (executionSemanticActionSpaceRef == null
                || executionSemanticActionSpaceRef.ActionWitnessId
                    != draft.Action.ActionWitnessId
                || executionSemanticActionSpaceRef.SemanticStateDigest
                    != draft.ExecutionSemanticActionSpace.SemanticStateDigest
                || executionSemanticActionSpaceRef.SemanticCatalogDigest
                    != draft.ExecutionSemanticActionSpace.SemanticCatalogDigest)
            {
                throw new InvalidDataException(
                    "Execution semantic action-space reference does not match the proved transition.");
            }
            actionSpaceAuthority = "native_semantic_execution";
        }
        else
        {
            if (!string.Equals(
                    draft.Action.NativeMechanism,
                    "direct_ui_commit",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "A game-action transition requires a typed native execution action space.");
            }
            if (!PublicCatalogContainsExactlyOnce(draft.SemanticPre, draft.Action.BoundAction))
                throw new InvalidDataException(
                    "No complete authoritative execution action space contains the Human action.");
            actionSpaceAuthority = "public_bound_actions";
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
            null,
            draft.Action.ActionWitnessId,
            draft.Action.NativeMechanism,
            preStateRef,
            draft.Action.BoundAction,
            successorRef,
            "canonical_s_a_s_prime",
            new[]
            {
                "complete_execution_state",
                "chosen_action_exactly_once_in_authoritative_action_space",
                "exact_human_native_action_correlation",
                "native_terminal_direct_commit_or_player_choice_continuation_observed",
                "no_intervening_human_mutation",
                "complete_authoritative_successor"
            },
            new[]
            {
                "not_business_completion",
                "not_human_validated_until_owner_review",
                "capture_profile_scoped"
            })
        {
            ActionSpaceAuthority = actionSpaceAuthority,
            ExecutionSemanticActionSpaceRef = executionSemanticActionSpaceRef
        };
    }

    private static bool PublicCatalogContainsExactlyOnce(
        CurrentDecisionFrame frame,
        RecordedBoundAction selected)
    {
        if (frame.Snapshot["completeness"]?["status"]?.GetValue<string>() != "complete"
            || frame.Snapshot["bound_actions"]?["status"]?.GetValue<string>() != "complete"
            || frame.Snapshot["bound_actions"]?["actions"] is not System.Text.Json.Nodes.JsonArray actions
            || frame.CatalogCount != actions.Count)
            return false;
        int matches = actions.Count(candidate =>
            candidate?["bound_action_id"]?.GetValue<string>() == selected.BoundActionId
            && candidate?["verb"]?.GetValue<string>() == selected.Verb
            && candidate?["subject_referent_id"]?.GetValue<string>() == selected.SubjectReferentId
            && PublicArgumentsMatch(candidate?["arguments"], selected.Arguments));
        return matches == 1;
    }

    private static bool PublicArgumentsMatch(
        System.Text.Json.Nodes.JsonNode? node,
        IReadOnlyDictionary<string, string> expected)
    {
        if (node is not System.Text.Json.Nodes.JsonArray values)
            return expected.Count == 0;
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Text.Json.Nodes.JsonNode? value in values)
        {
            string? role = value?["role"]?.GetValue<string>();
            string? referent = value?["referent_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(role)
                || string.IsNullOrWhiteSpace(referent)
                || !actual.TryAdd(role, referent))
                return false;
        }
        return actual.Count == expected.Count
            && actual.All(pair => expected.TryGetValue(pair.Key, out string? referent)
                                  && referent == pair.Value);
    }
}
