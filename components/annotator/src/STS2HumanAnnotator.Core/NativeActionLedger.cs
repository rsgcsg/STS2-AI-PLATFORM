namespace STS2HumanAnnotator.Core;

public static class NativeActionLedgerContract
{
    public const int SchemaVersion = 1;
    public const string EventSchema = "sts2.human-annotator/native-action-ledger-event-1";
}

public static class NativeActionLifecycleKinds
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

public sealed record NativeActionLedgerEvent(
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
    string? Detail);

public sealed record AcceptedActionAdmission(
    bool Accounted,
    string? FailureCode,
    IReadOnlyList<string> PriorOpenActionIds,
    IReadOnlyList<string> InvalidatedStrictCandidateIds,
    bool StrictTransitionEligible);

public sealed class AcceptedHumanActionLedger
{
    private sealed class Entry
    {
        public Entry(string actionWitnessId, bool strictTransitionEligible)
        {
            ActionWitnessId = actionWitnessId;
            StrictTransitionEligible = strictTransitionEligible;
        }

        public string ActionWitnessId { get; }
        public bool StrictTransitionEligible { get; set; }
        public string? TerminalKind { get; set; }
    }

    private readonly int _capacity;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private bool _recoveryBoundaryRequired;
    private bool _untrackedAcceptedAction;

    public AcceptedHumanActionLedger(int capacity = 64)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count => _entries.Count;
    public bool HasUnresolvedLifecycle => _entries.Values.Any(entry => entry.TerminalKind == null);
    public bool HasOpenEvidence => _entries.Count > 0;
    public bool RecoveryBoundaryRequired => _recoveryBoundaryRequired;

    public AcceptedActionAdmission Accept(
        string actionWitnessId,
        bool externalCausalEvidenceOpen = false)
    {
        if (string.IsNullOrWhiteSpace(actionWitnessId))
            throw new ArgumentException("Action witness ID is required.", nameof(actionWitnessId));
        if (_entries.ContainsKey(actionWitnessId))
        {
            return new AcceptedActionAdmission(
                false,
                "duplicate_native_action_identity",
                Array.Empty<string>(),
                Array.Empty<string>(),
                false);
        }
        if (_entries.Count >= _capacity)
        {
            string[] capacityPrior = _entries.Keys.ToArray();
            string[] capacityInvalidated = _entries.Values
                .Where(entry => entry.StrictTransitionEligible)
                .Select(entry => entry.ActionWitnessId)
                .ToArray();
            foreach (Entry entry in _entries.Values)
                entry.StrictTransitionEligible = false;
            _recoveryBoundaryRequired = true;
            _untrackedAcceptedAction = true;
            return new AcceptedActionAdmission(
                false,
                "native_action_ledger_capacity_exceeded",
                capacityPrior,
                capacityInvalidated,
                false);
        }

        string[] prior = _entries.Keys.ToArray();
        bool overlaps = prior.Length > 0
            || _recoveryBoundaryRequired
            || externalCausalEvidenceOpen;
        string[] invalidated = _entries.Values
            .Where(entry => entry.StrictTransitionEligible)
            .Select(entry => entry.ActionWitnessId)
            .ToArray();
        if (overlaps)
        {
            foreach (Entry entry in _entries.Values)
                entry.StrictTransitionEligible = false;
            _recoveryBoundaryRequired = true;
        }

        _entries.Add(actionWitnessId, new Entry(actionWitnessId, !overlaps));
        return new AcceptedActionAdmission(
            true,
            null,
            prior,
            invalidated,
            !overlaps);
    }

    public bool MarkTerminal(string actionWitnessId, string terminalKind)
    {
        if (!NativeActionLifecycleKinds.IsTerminal(terminalKind)
            || !_entries.TryGetValue(actionWitnessId, out Entry? entry))
            return false;
        entry.TerminalKind = terminalKind;
        if (terminalKind == NativeActionLifecycleKinds.Cancelled)
        {
            entry.StrictTransitionEligible = false;
            _recoveryBoundaryRequired = true;
        }
        return true;
    }

    public bool CanAdmitStrictTransition(string actionWitnessId) =>
        !_recoveryBoundaryRequired
        && _entries.Count == 1
        && _entries.TryGetValue(actionWitnessId, out Entry? entry)
        && entry.StrictTransitionEligible
        && entry.TerminalKind == NativeActionLifecycleKinds.Finished;

    public bool CompleteStrictTransition(string actionWitnessId)
    {
        if (!CanAdmitStrictTransition(actionWitnessId))
            return false;
        _entries.Remove(actionWitnessId);
        return true;
    }

    public bool InvalidateStrictTransition(string actionWitnessId)
    {
        if (!_entries.TryGetValue(actionWitnessId, out Entry? entry))
            return false;
        entry.StrictTransitionEligible = false;
        _recoveryBoundaryRequired = true;
        return true;
    }

    public bool ObserveRecoveryBoundary()
    {
        if (!_recoveryBoundaryRequired || HasUnresolvedLifecycle || _untrackedAcceptedAction)
            return false;
        _entries.Clear();
        _recoveryBoundaryRequired = false;
        _untrackedAcceptedAction = false;
        return true;
    }

    public void Reset()
    {
        _entries.Clear();
        _recoveryBoundaryRequired = false;
        _untrackedAcceptedAction = false;
    }
}

public static class NativeActionLedgerValidator
{
    private static readonly HashSet<string> KnownKinds = new(StringComparer.Ordinal)
    {
        NativeActionLifecycleKinds.Accepted,
        NativeActionLifecycleKinds.Started,
        NativeActionLifecycleKinds.PausedForPlayerChoice,
        NativeActionLifecycleKinds.ReadyToResume,
        NativeActionLifecycleKinds.Resumed,
        NativeActionLifecycleKinds.Cancelled,
        NativeActionLifecycleKinds.Finished,
        NativeActionLifecycleKinds.StrictTransitionInvalidated,
        NativeActionLifecycleKinds.StrictTransitionAdmitted
    };

    public static IReadOnlyList<string> Validate(IReadOnlyList<NativeActionLedgerEvent> events)
    {
        var errors = new List<string>();
        var accepted = new Dictionary<string, NativeActionLedgerEvent>(StringComparer.Ordinal);
        var terminal = new HashSet<string>(StringComparer.Ordinal);
        var lastLifecycleKind = new Dictionary<string, string>(StringComparer.Ordinal);
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        long previousSequence = 0;

        foreach (NativeActionLedgerEvent value in events)
        {
            if (value.SchemaVersion != NativeActionLedgerContract.SchemaVersion
                || value.Schema != NativeActionLedgerContract.EventSchema)
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

            if (value.Kind == NativeActionLifecycleKinds.Accepted)
            {
                if (!accepted.TryAdd(value.ActionWitnessId, value))
                    errors.Add("native_action_accepted_duplicate");
                else
                    lastLifecycleKind.Add(value.ActionWitnessId, value.Kind);
                continue;
            }
            if (!accepted.TryGetValue(value.ActionWitnessId, out NativeActionLedgerEvent? first))
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
                && value.Kind is not NativeActionLifecycleKinds.StrictTransitionInvalidated
                    and not NativeActionLifecycleKinds.StrictTransitionAdmitted)
                errors.Add("native_action_lifecycle_after_terminal");
            if (IsLifecycleKind(value.Kind)
                && !IsAllowedLifecycleTransition(lastLifecycleKind[value.ActionWitnessId], value.Kind))
                errors.Add("native_action_lifecycle_order_invalid");
            if (IsLifecycleKind(value.Kind))
                lastLifecycleKind[value.ActionWitnessId] = value.Kind;
            if (NativeActionLifecycleKinds.IsTerminal(value.Kind))
                terminal.Add(value.ActionWitnessId);
        }

        foreach (IGrouping<string, NativeActionLedgerEvent> actionEvents in events
                     .GroupBy(value => value.ActionWitnessId, StringComparer.Ordinal))
        {
            NativeActionLedgerEvent[] dispositions = actionEvents.Where(value =>
                    value.Kind is NativeActionLifecycleKinds.StrictTransitionInvalidated
                        or NativeActionLifecycleKinds.StrictTransitionAdmitted)
                .ToArray();
            if (dispositions.Length != 1)
                errors.Add("native_action_disposition_not_exactly_one");
            if (dispositions.Any(value =>
                    value.Kind == NativeActionLifecycleKinds.StrictTransitionAdmitted)
                && !actionEvents.Any(value => value.Kind == NativeActionLifecycleKinds.Finished))
                errors.Add("native_action_strict_transition_before_finish");
        }

        return errors;
    }

    private static bool IsLifecycleKind(string kind) =>
        kind is NativeActionLifecycleKinds.Started
            or NativeActionLifecycleKinds.PausedForPlayerChoice
            or NativeActionLifecycleKinds.ReadyToResume
            or NativeActionLifecycleKinds.Resumed
            or NativeActionLifecycleKinds.Cancelled
            or NativeActionLifecycleKinds.Finished;

    private static bool IsAllowedLifecycleTransition(string previous, string current) =>
        current switch
        {
            NativeActionLifecycleKinds.Started => previous == NativeActionLifecycleKinds.Accepted,
            NativeActionLifecycleKinds.PausedForPlayerChoice => previous is
                NativeActionLifecycleKinds.Started or NativeActionLifecycleKinds.Resumed,
            NativeActionLifecycleKinds.ReadyToResume =>
                previous == NativeActionLifecycleKinds.PausedForPlayerChoice,
            NativeActionLifecycleKinds.Resumed => previous == NativeActionLifecycleKinds.ReadyToResume,
            NativeActionLifecycleKinds.Cancelled => previous is
                NativeActionLifecycleKinds.Accepted
                    or NativeActionLifecycleKinds.Started
                    or NativeActionLifecycleKinds.PausedForPlayerChoice
                    or NativeActionLifecycleKinds.ReadyToResume
                    or NativeActionLifecycleKinds.Resumed,
            NativeActionLifecycleKinds.Finished => previous is
                NativeActionLifecycleKinds.Started or NativeActionLifecycleKinds.Resumed,
            _ => false
        };
}
