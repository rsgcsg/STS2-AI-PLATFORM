namespace STS2HumanAnnotator.Core;

/// <summary>
/// Historical durable native-lifecycle evidence contract. New runtime causal
/// admission is owned exclusively by SemanticBoundaryTracker; this contract
/// remains readable so existing recordings and audits retain their meaning.
/// </summary>
public static class HistoricalNativeActionLedgerContract
{
    public const int SchemaVersion = 2;
    public const string EventSchema = "sts2.human-annotator/native-action-ledger-event-2";
    public const int LegacySchemaVersion = 1;
    public const string LegacyEventSchema = "sts2.human-annotator/native-action-ledger-event-1";

    public static bool IsSupported(int version, string schema) =>
        (version == SchemaVersion && schema == EventSchema)
        || (version == LegacySchemaVersion && schema == LegacyEventSchema);
}

public static class HistoricalNativeActionLifecycleKinds
{
    public const string Accepted = "accepted";
    public const string Started = "started";
    public const string PausedForPlayerChoice = "paused_for_player_choice";
    public const string ReadyToResume = "ready_to_resume";
    public const string Resumed = "resumed";
    public const string Cancelled = "cancelled";
    public const string Finished = "finished";
    public const string StrictTransitionInvalidated = "strict_transition_invalidated";
    public const string StrictTransitionAdmitted = "strict_transition_admitted";

    public static bool IsTerminal(string kind) =>
        kind is Cancelled or Finished;
}

public sealed record HistoricalNativeActionLedgerEvent(
    int SchemaVersion,
    string Schema,
    string EventId,
    string SessionId,
    string TimelineId,
    string RunId,
    long Sequence,
    string ActionWitnessId,
    long ActionSequence,
    string RecordId,
    DateTimeOffset ObservedAt,
    string Kind,
    string NativeActionType,
    uint? NativeQueueId,
    string NativeState,
    IReadOnlyList<string> PriorOpenActionIds,
    string TransitionEvidence,
    string? Detail,
    HistoricalReadRichDecisionFrame? DecisionPre = null,
    NativeWitnessEvidence? NativeWitness = null,
    ExactMappingEvidence? Mapping = null,
    RecordedBoundAction? BoundAction = null);

/// <summary>
/// Validator for historical additive native-action-ledger streams. It is not
/// a runtime admission policy and does not authorize semantic successors.
/// </summary>
public static class HistoricalNativeActionLedgerValidator
{
    private static readonly HashSet<string> KnownKinds = new(StringComparer.Ordinal)
    {
        HistoricalNativeActionLifecycleKinds.Accepted,
        HistoricalNativeActionLifecycleKinds.Started,
        HistoricalNativeActionLifecycleKinds.PausedForPlayerChoice,
        HistoricalNativeActionLifecycleKinds.ReadyToResume,
        HistoricalNativeActionLifecycleKinds.Resumed,
        HistoricalNativeActionLifecycleKinds.Cancelled,
        HistoricalNativeActionLifecycleKinds.Finished,
        HistoricalNativeActionLifecycleKinds.StrictTransitionInvalidated,
        HistoricalNativeActionLifecycleKinds.StrictTransitionAdmitted
    };

    public static IReadOnlyList<string> Validate(IReadOnlyList<HistoricalNativeActionLedgerEvent> events)
    {
        var errors = new List<string>();
        var accepted = new Dictionary<string, HistoricalNativeActionLedgerEvent>(StringComparer.Ordinal);
        var terminal = new HashSet<string>(StringComparer.Ordinal);
        var lastLifecycleKind = new Dictionary<string, string>(StringComparer.Ordinal);
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        long previousSequence = 0;

        foreach (HistoricalNativeActionLedgerEvent value in events)
        {
            if (!HistoricalNativeActionLedgerContract.IsSupported(value.SchemaVersion, value.Schema))
                errors.Add("native_action_schema_invalid");
            if (previousSequence == 0 && value.Sequence != 1)
                errors.Add("native_action_sequence_does_not_start_at_one");
            if (value.Sequence <= previousSequence)
                errors.Add("native_action_sequence_not_strictly_increasing");
            previousSequence = Math.Max(previousSequence, value.Sequence);
            if (!eventIds.Add(value.EventId))
                errors.Add("native_action_event_id_duplicate");
            if (string.IsNullOrWhiteSpace(value.ActionWitnessId)
                || value.ActionSequence <= 0
                || string.IsNullOrWhiteSpace(value.RecordId)
                || string.IsNullOrWhiteSpace(value.NativeActionType)
                || !KnownKinds.Contains(value.Kind))
                errors.Add("native_action_event_invalid");

            if (value.Kind == HistoricalNativeActionLifecycleKinds.Accepted)
            {
                if (!accepted.TryAdd(value.ActionWitnessId, value))
                    errors.Add("native_action_accepted_duplicate");
                else
                    lastLifecycleKind.Add(value.ActionWitnessId, value.Kind);
                if (value.SchemaVersion == HistoricalNativeActionLedgerContract.SchemaVersion
                    && !HasExactDecisionEvidence(value))
                    errors.Add("native_action_decision_evidence_invalid");
                continue;
            }
            if (value.SchemaVersion == HistoricalNativeActionLedgerContract.SchemaVersion
                && HasAnyDecisionEvidence(value))
                errors.Add("native_action_decision_evidence_repeated");
            if (!accepted.TryGetValue(value.ActionWitnessId, out HistoricalNativeActionLedgerEvent? first))
            {
                errors.Add("native_action_event_before_accepted");
                continue;
            }
            if (first.ActionSequence != value.ActionSequence
                || first.RecordId != value.RecordId
                || first.SessionId != value.SessionId
                || first.TimelineId != value.TimelineId
                || first.NativeActionType != value.NativeActionType)
                errors.Add("native_action_identity_drift");
            if (terminal.Contains(value.ActionWitnessId)
                && value.Kind is not HistoricalNativeActionLifecycleKinds.StrictTransitionInvalidated
                    and not HistoricalNativeActionLifecycleKinds.StrictTransitionAdmitted)
                errors.Add("native_action_lifecycle_after_terminal");
            if (IsLifecycleKind(value.Kind)
                && !IsAllowedLifecycleTransition(lastLifecycleKind[value.ActionWitnessId], value.Kind))
                errors.Add("native_action_lifecycle_order_invalid");
            if (IsLifecycleKind(value.Kind))
                lastLifecycleKind[value.ActionWitnessId] = value.Kind;
            if (HistoricalNativeActionLifecycleKinds.IsTerminal(value.Kind))
                terminal.Add(value.ActionWitnessId);
        }

        foreach (IGrouping<string, HistoricalNativeActionLedgerEvent> actionEvents in events
                     .GroupBy(value => value.ActionWitnessId, StringComparer.Ordinal))
        {
            HistoricalNativeActionLedgerEvent[] dispositions = actionEvents.Where(value =>
                    value.Kind is HistoricalNativeActionLifecycleKinds.StrictTransitionInvalidated
                        or HistoricalNativeActionLifecycleKinds.StrictTransitionAdmitted)
                .ToArray();
            if (dispositions.Length != 1)
                errors.Add("native_action_disposition_not_exactly_one");
            if (dispositions.Any(value =>
                    value.Kind == HistoricalNativeActionLifecycleKinds.StrictTransitionAdmitted)
                && !actionEvents.Any(value => value.Kind == HistoricalNativeActionLifecycleKinds.Finished))
                errors.Add("native_action_strict_transition_before_finish");
        }

        return errors;
    }

    private static bool HasExactDecisionEvidence(HistoricalNativeActionLedgerEvent value) =>
        value.DecisionPre != null
        && value.NativeWitness != null
        && value.Mapping is { Status: "exact_unique", MatchCount: 1 }
        && value.BoundAction != null
        && !string.IsNullOrWhiteSpace(value.DecisionPre.SnapshotId)
        && !string.IsNullOrWhiteSpace(value.DecisionPre.InteractionId)
        && value.DecisionPre.CatalogCount > 0
        && !string.IsNullOrWhiteSpace(value.DecisionPre.CatalogDigest)
        && !string.IsNullOrWhiteSpace(value.BoundAction.BoundActionId)
        && !string.IsNullOrWhiteSpace(value.BoundAction.Verb);

    private static bool HasAnyDecisionEvidence(HistoricalNativeActionLedgerEvent value) =>
        value.DecisionPre != null
        || value.NativeWitness != null
        || value.Mapping != null
        || value.BoundAction != null;

    private static bool IsLifecycleKind(string kind) =>
        kind is HistoricalNativeActionLifecycleKinds.Started
            or HistoricalNativeActionLifecycleKinds.PausedForPlayerChoice
            or HistoricalNativeActionLifecycleKinds.ReadyToResume
            or HistoricalNativeActionLifecycleKinds.Resumed
            or HistoricalNativeActionLifecycleKinds.Cancelled
            or HistoricalNativeActionLifecycleKinds.Finished;

    private static bool IsAllowedLifecycleTransition(string previous, string current) =>
        current switch
        {
            HistoricalNativeActionLifecycleKinds.Started => previous == HistoricalNativeActionLifecycleKinds.Accepted,
            HistoricalNativeActionLifecycleKinds.PausedForPlayerChoice => previous is
                HistoricalNativeActionLifecycleKinds.Started or HistoricalNativeActionLifecycleKinds.Resumed,
            HistoricalNativeActionLifecycleKinds.ReadyToResume =>
                previous == HistoricalNativeActionLifecycleKinds.PausedForPlayerChoice,
            HistoricalNativeActionLifecycleKinds.Resumed => previous == HistoricalNativeActionLifecycleKinds.ReadyToResume,
            HistoricalNativeActionLifecycleKinds.Cancelled => previous is
                HistoricalNativeActionLifecycleKinds.Accepted
                    or HistoricalNativeActionLifecycleKinds.Started
                    or HistoricalNativeActionLifecycleKinds.PausedForPlayerChoice
                    or HistoricalNativeActionLifecycleKinds.ReadyToResume
                    or HistoricalNativeActionLifecycleKinds.Resumed,
            HistoricalNativeActionLifecycleKinds.Finished => previous is
                HistoricalNativeActionLifecycleKinds.Started or HistoricalNativeActionLifecycleKinds.Resumed,
            _ => false
        };
}
