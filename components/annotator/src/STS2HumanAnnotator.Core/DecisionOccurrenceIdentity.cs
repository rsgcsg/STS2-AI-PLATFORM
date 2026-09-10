namespace STS2HumanAnnotator.Core;

/// <summary>
/// Versioned non-authorizing identity attached to the existing semantic trace
/// and canonical transition. A decision is not a synthetic native GameAction.
/// Null on older records means unspecified historical decision semantics.
/// </summary>
public sealed record DecisionOccurrenceIdentity(
    int SchemaVersion,
    string DecisionId,
    string CausalRootId,
    string? ParentDecisionId,
    string Surface,
    string Family,
    string DecisionKind,
    string? NativeOwnerWitnessId)
{
    public const int CurrentSchemaVersion = 1;
}

public static class DecisionOccurrenceValidator
{
    public static IReadOnlyList<string> Validate(DecisionOccurrenceIdentity value, string actionWitnessId)
    {
        var errors = new List<string>();
        if (value.SchemaVersion != DecisionOccurrenceIdentity.CurrentSchemaVersion)
            errors.Add("decision_schema_invalid");
        if (new[] { value.DecisionId, value.CausalRootId, value.Surface, value.Family }.Any(string.IsNullOrWhiteSpace))
            errors.Add("decision_identity_missing");
        if (value.DecisionKind is not ("root" or "nested_selector"))
            errors.Add("decision_kind_invalid");
        if (value.DecisionKind == "root"
            && (value.ParentDecisionId != null || value.CausalRootId != actionWitnessId))
            errors.Add("decision_root_lineage_invalid");
        if (value.DecisionKind == "nested_selector"
            && (string.IsNullOrWhiteSpace(value.ParentDecisionId)
                || value.ParentDecisionId == value.DecisionId
                || value.CausalRootId == actionWitnessId
                || string.IsNullOrWhiteSpace(value.NativeOwnerWitnessId)))
            errors.Add("decision_nested_lineage_invalid");
        return errors;
    }

    // Acceptance, not canonical persistence, establishes parent existence.
    // An unresolved parent does not erase a separately proved child decision.
    public static IReadOnlyList<string> ValidateTrace(IReadOnlyList<SemanticBoundaryTraceEvent> events)
    {
        var errors = new List<string>();
        var accepted = new Dictionary<string, SemanticBoundaryTraceEvent>(StringComparer.Ordinal);
        foreach (SemanticBoundaryTraceEvent value in events)
        {
            DecisionOccurrenceIdentity? decision = value.Action.Decision;
            if (decision == null)
                continue;
            errors.AddRange(Validate(decision, value.Action.ActionWitnessId));
            var decisionFrame = decision.DecisionKind == "nested_selector"
                ? value.SemanticPre ?? value.HumanObservation : value.HumanObservation;
            if (decisionFrame != null && decision.Surface != decisionFrame.InteractionKind)
                errors.Add("decision_surface_mismatch");
            if (value.Kind == SemanticBoundaryTraceKinds.ActionAccepted)
            {
                if (decision.ParentDecisionId != null)
                {
                    if (!accepted.TryGetValue(decision.ParentDecisionId, out SemanticBoundaryTraceEvent? parent))
                        errors.Add("decision_parent_missing_or_not_prior");
                    else if (parent.SessionId != value.SessionId || parent.TimelineId != value.TimelineId
                        || parent.RunId != value.RunId || parent.Action.RunId != value.Action.RunId
                        || parent.Action.Decision!.CausalRootId != decision.CausalRootId
                        || parent.Sequence >= value.Sequence
                        || parent.Action.ActionSequence >= value.Action.ActionSequence)
                        errors.Add("decision_parent_lineage_mismatch");
                }
                if (!accepted.TryAdd(decision.DecisionId, value))
                    errors.Add("decision_id_duplicate");
            }
            else if (!accepted.TryGetValue(decision.DecisionId, out SemanticBoundaryTraceEvent? origin))
                errors.Add("decision_acceptance_missing");
            else if (origin.Action.Decision != decision
                || origin.Action.ActionWitnessId != value.Action.ActionWitnessId
                || origin.Action.ActionSequence != value.Action.ActionSequence
                || origin.SessionId != value.SessionId || origin.TimelineId != value.TimelineId
                || origin.RunId != value.RunId)
                errors.Add("decision_identity_changed");
        }
        // Explicit metadata cannot disappear on a later lifecycle event.
        var explicitActions = accepted.Values.Select(value => value.Action.ActionWitnessId).ToHashSet(StringComparer.Ordinal);
        foreach (SemanticBoundaryTraceEvent value in events)
            if (value.Action.Decision == null && explicitActions.Contains(value.Action.ActionWitnessId))
                errors.Add("decision_identity_missing_from_event");
        return errors;
    }
}
