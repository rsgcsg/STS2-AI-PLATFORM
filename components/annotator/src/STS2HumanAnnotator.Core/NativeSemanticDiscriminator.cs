using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class NativeSemanticDiscriminatorContract
{
    public const int SchemaVersion = 1;
    public const string EventSchema =
        "sts2.human-annotator/native-semantic-discriminator-event-1";
    public const string CanonicalBoundaryCaptureDelegatedDetail =
        "Canonical semantic boundary capture owns this execution snapshot.";
}

/// <summary>
/// Additive, read-only evidence from the runtime discriminator. It is not a
/// HumanDecision record and cannot authorize or execute an action.
/// </summary>
public sealed record NativeSemanticDiscriminatorEvent(
    int SchemaVersion,
    string Schema,
    string EventId,
    string SessionId,
    string TimelineId,
    string RunId,
    long Sequence,
    DateTimeOffset ObservedAt,
    string Phase,
    string ActionWitnessId,
    string NativeActionType,
    uint? NativeQueueId,
    string NativeState,
    string CaptureStatus,
    string Scope,
    string? SemanticStateDigest,
    JsonNode? SemanticState,
    string SemanticCatalogDigest,
    IReadOnlyList<string> SemanticActionKeys,
    string? ObservedActionKey,
    string? SemanticMembership,
    int? SemanticMatchCount,
    string UiSnapshotId,
    string UiSnapshotStatus,
    string UiInteractionKind,
    string UiCatalogStatus,
    int UiActionCount,
    string UiCatalogDigest,
    string? UiMembership,
    int? UiMatchCount,
    string? RelatedActionWitnessId,
    string? Detail,
    IReadOnlyList<string> NonClaims);

public sealed record NativeSemanticActionDisposition(
    string ActionWitnessId,
    string NativeActionType,
    string Disposition,
    string Membership,
    string? ExecutionStateDigest,
    IReadOnlyList<string> Reasons);

public sealed record NativeSemanticHandoffCandidate(
    string PriorActionWitnessId,
    string NextActionWitnessId,
    string SharedStateDigest,
    bool CrossedPlayerChoiceCommit);

public sealed record NativeSemanticDiscriminatorReport(
    string Status,
    int Accepted,
    int Successful,
    int Cancelled,
    int Aborted,
    int Unknown,
    int ExactOnceMembership,
    int PlayerChoicePauses,
    int PlayerChoiceResumes,
    IReadOnlyList<NativeSemanticActionDisposition> Actions,
    IReadOnlyList<NativeSemanticHandoffCandidate> HandoffCandidates,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> NonClaims);

public static class NativeSemanticDiscriminatorAnalyzer
{
    private static readonly HashSet<string> AllowedPhases = new(StringComparer.Ordinal)
    {
        "accepted",
        "before_execution",
        "before_execution_resume",
        "started",
        "paused_for_player_choice",
        "ready_to_resume",
        "resumed",
        "cancelled",
        "finished",
        "aborted_before_commit",
        "player_choice_commit"
    };

    public static NativeSemanticDiscriminatorReport Analyze(
        IReadOnlyList<NativeSemanticDiscriminatorEvent> input)
    {
        NativeSemanticDiscriminatorEvent[] events = input
            .OrderBy(value => value.Sequence)
            .ToArray();
        var errors = ValidateEnvelope(events);
        var dispositions = new List<NativeSemanticActionDisposition>();
        foreach (IGrouping<string, NativeSemanticDiscriminatorEvent> group in events
                     .Where(value => value.Phase != "player_choice_commit")
                     .GroupBy(value => value.ActionWitnessId, StringComparer.Ordinal))
        {
            NativeSemanticDiscriminatorEvent[] actionEvents = group.ToArray();
            int acceptedCount = actionEvents.Count(value => value.Phase == "accepted");
            bool cancelled = actionEvents.Any(value => value.Phase == "cancelled");
            bool aborted = actionEvents.Any(value => value.Phase == "aborted_before_commit");
            bool finished = actionEvents.Any(value => value.Phase == "finished");
            NativeSemanticDiscriminatorEvent[] initialExecutions = actionEvents
                .Where(value => value.Phase == "before_execution")
                .ToArray();
            var reasons = new List<string>();
            if (acceptedCount == 0)
                reasons.Add("accepted_event_missing");
            else if (acceptedCount > 1)
                reasons.Add("accepted_event_duplicate");
            if (actionEvents.Select(value => value.RunId).Distinct(StringComparer.Ordinal).Count() != 1)
                reasons.Add("action_run_identity_changed");
            if (initialExecutions.Length > 1)
                reasons.Add("multiple_initial_execution_boundaries");
            NativeSemanticDiscriminatorEvent? execution = initialExecutions.SingleOrDefault();
            if (cancelled && finished)
                reasons.Add("cancelled_and_finished_both_observed");

            string disposition;
            string membership;
            if (aborted)
            {
                disposition = "aborted";
                membership = execution?.SemanticMembership ?? "not_executed";
            }
            else if (cancelled)
            {
                disposition = "cancelled";
                membership = execution?.SemanticMembership ?? "not_executed";
            }
            else if (finished)
            {
                bool delegated = execution != null
                    && execution.CaptureStatus == "not_sampled"
                    && execution.Detail == NativeSemanticDiscriminatorContract
                        .CanonicalBoundaryCaptureDelegatedDetail;
                membership = delegated
                    ? "delegated_to_canonical_boundary"
                    : execution?.SemanticMembership ?? "unknown";
                disposition = execution?.SemanticMembership == "exact_once"
                    ? "successful_membership_proved"
                    : delegated
                        ? "successful_capture_delegated"
                        : "successful_membership_unknown";
                if (execution == null)
                    reasons.Add("successful_action_missing_execution_boundary");
                else if (!delegated && execution.SemanticMembership != "exact_once")
                    reasons.Add("successful_action_not_exact_once_in_semantic_catalog");
            }
            else
            {
                disposition = "unresolved";
                membership = execution?.SemanticMembership ?? "unknown";
                reasons.Add("terminal_disposition_missing");
            }
            dispositions.Add(new NativeSemanticActionDisposition(
                group.Key,
                actionEvents[0].NativeActionType,
                disposition,
                membership,
                execution?.SemanticStateDigest,
                reasons));
            errors.AddRange(reasons.Select(reason => $"{group.Key}:{reason}"));
        }

        IReadOnlyList<NativeSemanticHandoffCandidate> handoffs = BuildHandoffs(events);
        int successful = dispositions.Count(value =>
            value.Disposition.StartsWith("successful_", StringComparison.Ordinal));
        int unknown = dispositions.Count(value =>
            value.Disposition is "successful_membership_unknown" or "unresolved");
        return new NativeSemanticDiscriminatorReport(
            errors.Count == 0 ? "pass" : "fail",
            events.Count(value => value.Phase == "accepted"),
            successful,
            dispositions.Count(value => value.Disposition == "cancelled"),
            dispositions.Count(value => value.Disposition == "aborted"),
            unknown,
            dispositions.Count(value => value.Membership == "exact_once"),
            events.Count(value => value.Phase == "paused_for_player_choice"),
            events.Count(value => value.Phase == "resumed"),
            dispositions,
            handoffs,
            errors.Distinct(StringComparer.Ordinal).ToArray(),
            new[]
            {
                "runtime_discriminator_does_not_authorize_actions",
                "handoff_is_a_causal_candidate_not_a_business_outcome",
                "end_turn_finished_requires_state_change_evidence_for_commit",
                "full_run_surface_completeness_not_claimed"
            });
    }

    private static List<string> ValidateEnvelope(
        IReadOnlyList<NativeSemanticDiscriminatorEvent> events)
    {
        var errors = new List<string>();
        long expected = 1;
        string? sessionId = events.FirstOrDefault()?.SessionId;
        string? timelineId = events.FirstOrDefault()?.TimelineId;
        foreach (NativeSemanticDiscriminatorEvent value in events)
        {
            if (value.SchemaVersion != NativeSemanticDiscriminatorContract.SchemaVersion
                || value.Schema != NativeSemanticDiscriminatorContract.EventSchema)
                errors.Add("unsupported_native_semantic_discriminator_schema");
            if (value.Sequence != expected++)
                errors.Add("native_semantic_discriminator_sequence_gap");
            if (!string.Equals(value.SessionId, sessionId, StringComparison.Ordinal)
                || !string.Equals(value.TimelineId, timelineId, StringComparison.Ordinal))
                errors.Add("native_semantic_discriminator_stream_identity_changed");
            if (!AllowedPhases.Contains(value.Phase))
                errors.Add("native_semantic_discriminator_phase_unknown");
            if (string.IsNullOrWhiteSpace(value.ActionWitnessId)
                || string.IsNullOrWhiteSpace(value.NativeActionType))
                errors.Add("native_semantic_discriminator_identity_missing");
        }
        if (events.Select(value => value.EventId).Distinct(StringComparer.Ordinal).Count()
            != events.Count)
            errors.Add("native_semantic_discriminator_event_id_duplicate");
        return errors;
    }

    private static IReadOnlyList<NativeSemanticHandoffCandidate> BuildHandoffs(
        IReadOnlyList<NativeSemanticDiscriminatorEvent> events)
    {
        var result = new List<NativeSemanticHandoffCandidate>();
        NativeSemanticDiscriminatorEvent? priorFinished = null;
        foreach (NativeSemanticDiscriminatorEvent value in events)
        {
            if (value.Phase == "finished")
            {
                bool cancelledOrAborted = events.Any(candidate =>
                    candidate.ActionWitnessId == value.ActionWitnessId
                    && candidate.Sequence <= value.Sequence
                    && candidate.Phase is "cancelled" or "aborted_before_commit");
                priorFinished = cancelledOrAborted ? null : value;
                continue;
            }
            if (value.Phase != "before_execution" || value.SemanticStateDigest == null)
                continue;
            if (priorFinished != null
                && priorFinished.ActionWitnessId != value.ActionWitnessId)
            {
                bool crossedChoice = events.Any(candidate =>
                    candidate.Sequence > priorFinished.Sequence
                    && candidate.Sequence < value.Sequence
                    && candidate.Phase == "player_choice_commit");
                result.Add(new NativeSemanticHandoffCandidate(
                    priorFinished.ActionWitnessId,
                    value.ActionWitnessId,
                    value.SemanticStateDigest,
                    crossedChoice));
            }
            priorFinished = null;
        }
        return result;
    }
}
