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
    string? NativeOwnerWitnessId,
    NativeDecisionOriginEvidence? NativeOrigin = null)
{
    public const int CurrentSchemaVersion = 2;
}

public sealed record NativeDecisionOriginEvidence(
    string NativeActionWitnessId, string NativeActionType,
    string ChoiceContextType, string FactoryMechanism);

public static class DecisionOccurrenceValidator
{
    public static IReadOnlyList<string> Validate(DecisionOccurrenceIdentity value, string actionWitnessId)
    {
        var errors = new List<string>();
        if (value.SchemaVersion is not (1 or DecisionOccurrenceIdentity.CurrentSchemaVersion))
            errors.Add("decision_schema_invalid");
        if (new[] { value.DecisionId, value.CausalRootId, value.Surface, value.Family }.Any(string.IsNullOrWhiteSpace))
            errors.Add("decision_identity_missing");
        if (value.DecisionKind is not ("root" or "nested_selector" or "native_selector"))
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
        if (value.DecisionKind == "native_selector")
        {
            var origin = value.NativeOrigin;
            if (value.SchemaVersion != 2 || value.ParentDecisionId != null
                || value.CausalRootId == actionWitnessId
                || string.IsNullOrWhiteSpace(value.NativeOwnerWitnessId)
                || origin == null || origin.NativeActionWitnessId != value.CausalRootId
                || new[] { origin.NativeActionWitnessId, origin.NativeActionType,
                    origin.ChoiceContextType, origin.FactoryMechanism }.Any(string.IsNullOrWhiteSpace))
                errors.Add("decision_native_origin_invalid");
        }
        else if (value.NativeOrigin != null)
            errors.Add("decision_unexpected_native_origin");
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
            if (decision.DecisionKind == "native_selector")
            {
                var witness = value.Action.NativeWitness;
                if (value.Action.NativeMechanism != "direct_ui_commit" || value.Action.NativeQueueId != null
                    || witness?.Origin != "native_selector_input"
                    || witness.ArgumentWitnessIds == null
                    || witness.ArgumentWitnessIds.GetValueOrDefault("native_origin") != decision.CausalRootId
                    || witness.ArgumentWitnessIds.GetValueOrDefault("selector_owner") != decision.NativeOwnerWitnessId)
                    errors.Add("decision_native_origin_witness_mismatch");
            }
            var decisionFrame = decision.DecisionKind is "nested_selector" or "native_selector"
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
