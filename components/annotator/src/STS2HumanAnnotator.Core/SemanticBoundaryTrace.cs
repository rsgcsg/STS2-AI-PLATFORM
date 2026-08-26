namespace STS2HumanAnnotator.Core;

public static class SemanticBoundaryTraceContract
{
    public const int SchemaVersion = 1;
    public const string EventSchema = "sts2.human-annotator/semantic-boundary-trace-event-1";
}

public static class SemanticBoundaryTraceKinds
{
    public const string ActionAccepted = "action_accepted";
    public const string ActionStarted = "action_started";
    public const string ActionPausedForPlayerChoice = "action_paused_for_player_choice";
    public const string ActionReadyToResume = "action_ready_to_resume";
    public const string ActionResumed = "action_resumed";
    public const string ActionFinished = "action_finished";
    public const string ActionCancelledBeforeStart = "action_cancelled_before_start";
    public const string ActionCancelledAfterStart = "action_cancelled_after_start";
    public const string ActionAbortedBeforeCommit = "action_aborted_before_commit";
    public const string BoundaryObserved = "boundary_observed";
    public const string TransitionProved = "transition_proved";
    public const string TransitionUnknown = "transition_unknown";
}

public sealed record SemanticActionReference(
    string ActionWitnessId,
    long ActionSequence,
    string RecordId,
    string RunId,
    string NativeActionType,
    uint? NativeQueueId,
    string HumanObservationSnapshotId);

public sealed record SemanticBoundaryObservation(
    string WitnessKind,
    DateTimeOffset ObservedAt,
    string SnapshotId,
    string Status,
    string BoundActionsStatus,
    string InteractionId,
    string InteractionKind,
    FrozenDecisionFrameV2? State,
    string? ImmediatelyConsumedByActionWitnessId)
{
    public bool IsCompleteDecisionBoundary =>
        string.Equals(Status, "interactive", StringComparison.Ordinal)
        && string.Equals(BoundActionsStatus, "complete", StringComparison.Ordinal)
        && State != null;
}

public sealed record SemanticBoundaryTraceDraft(
    string Kind,
    SemanticActionReference Action,
    string ProofStatus,
    string? RelatedActionWitnessId = null,
    SemanticBoundaryObservation? Boundary = null,
    FrozenDecisionFrameV2? SemanticPre = null,
    FrozenDecisionFrameV2? SemanticSuccessor = null,
    string? Detail = null,
    IReadOnlyList<string>? NonClaims = null);

public sealed record SemanticBoundaryTraceEvent(
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
    SemanticBoundaryObservation? Boundary,
    FrozenDecisionFrameV2? SemanticPre,
    FrozenDecisionFrameV2? SemanticSuccessor,
    string? Detail,
    IReadOnlyList<string> NonClaims);

/// <summary>
/// Orders exact Human/native facts and decision-boundary observations. It does
/// not decide STS2 legality or infer effects; an incomplete boundary produces
/// unknown evidence rather than a transition.
/// </summary>
public sealed class SemanticBoundaryTracker
{
    private sealed class Entry
    {
        public Entry(SemanticActionReference action, FrozenDecisionFrameV2? semanticPre)
        {
            Action = action;
            SemanticPre = semanticPre;
        }

        public SemanticActionReference Action { get; }
        public FrozenDecisionFrameV2? SemanticPre { get; set; }
        public bool Started { get; set; }
        public bool Paused { get; set; }
        public bool Finished { get; set; }
        public bool Cancelled { get; set; }
        public bool Disposed { get; set; }
        public string? Disposition { get; set; }
    }

    private readonly int _capacity;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();

    public SemanticBoundaryTracker(int capacity = 128)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public bool Contains(string actionWitnessId) => _entries.ContainsKey(actionWitnessId);

    public bool NeedsBoundaryObservation =>
        _order.Select(id => _entries[id]).Any(IsWaitingForBoundary);

    public IReadOnlyList<SemanticBoundaryTraceDraft> Accept(
        SemanticActionReference action,
        FrozenDecisionFrameV2 humanDecisionPre)
    {
        if (_entries.ContainsKey(action.ActionWitnessId))
            throw new InvalidOperationException("Semantic action identity was accepted twice.");
        PruneTerminalDisposed();
        if (_entries.Count >= _capacity)
            throw new InvalidOperationException("Semantic boundary tracker capacity was exceeded.");

        bool hasEarlierOpenAction = _order
            .Select(id => _entries[id])
            .Any(entry => !entry.Disposed);
        var entry = new Entry(action, hasEarlierOpenAction ? null : humanDecisionPre);
        _entries.Add(action.ActionWitnessId, entry);
        _order.Add(action.ActionWitnessId);
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionAccepted,
                entry,
                hasEarlierOpenAction ? "precommitted" : "human_observation_boundary",
                semanticPre: entry.SemanticPre,
                detail: hasEarlierOpenAction
                    ? "Human observation is retained, but semantic pre-state waits for the preceding action boundary."
                    : "No earlier open Human action existed; the complete Human decision frame is the initial semantic pre-state.")
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> ObserveBeforeActionExecution(
        string nextActionWitnessId,
        SemanticBoundaryObservation boundary)
    {
        Entry next = Required(nextActionWitnessId);
        var drafts = new List<SemanticBoundaryTraceDraft>();
        Entry? predecessor = WaitingForBoundaryBefore(next.Action.ActionSequence);
        if (predecessor != null)
            drafts.AddRange(Settle(predecessor, boundary, next.Action.ActionWitnessId));

        if (boundary.IsCompleteDecisionBoundary && next.SemanticPre == null)
            next.SemanticPre = boundary.State;
        drafts.Add(Draft(
            SemanticBoundaryTraceKinds.BoundaryObserved,
            next,
            boundary.IsCompleteDecisionBoundary ? "complete" : "incomplete",
            boundary: boundary,
            semanticPre: next.SemanticPre,
            detail: "Captured synchronously before the next tracked Human GameAction begins execution."));
        return drafts;
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> Started(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Started = true;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionStarted,
                entry,
                entry.SemanticPre == null ? "semantic_pre_unknown" : "execution_started",
                semanticPre: entry.SemanticPre)
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> PausedForPlayerChoice(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Paused = true;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionPausedForPlayerChoice,
                entry,
                "awaiting_player_choice_boundary",
                semanticPre: entry.SemanticPre)
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> ReadyToResume(string actionWitnessId) =>
        Lifecycle(actionWitnessId, SemanticBoundaryTraceKinds.ActionReadyToResume, "player_choice_supplied");

    public IReadOnlyList<SemanticBoundaryTraceDraft> Resumed(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Paused = false;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionResumed,
                entry,
                "native_execution_resumed",
                semanticPre: entry.SemanticPre)
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> Finished(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Finished = true;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionFinished,
                entry,
                entry.Disposed
                    ? "lifecycle_finished_after_semantic_disposition"
                    : "awaiting_next_decision_boundary",
                semanticPre: entry.SemanticPre)
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> AbortedBeforeCommit(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Disposed = true;
        entry.Disposition = SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit,
                entry,
                "not_a_successful_action",
                semanticPre: entry.SemanticPre,
                detail: "The exact PlayCardAction execution path found that its card was no longer in hand and returned before native resource spend/OnPlay Commit.",
                nonClaims: new[] { "no_semantic_successor" })
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> Cancelled(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Cancelled = true;
        entry.Disposed = true;
        entry.Disposition = entry.Started
            ? SemanticBoundaryTraceKinds.ActionCancelledAfterStart
            : SemanticBoundaryTraceKinds.ActionCancelledBeforeStart;
        return new[]
        {
            Draft(
                entry.Started
                    ? SemanticBoundaryTraceKinds.ActionCancelledAfterStart
                    : SemanticBoundaryTraceKinds.ActionCancelledBeforeStart,
                entry,
                entry.Started ? "transition_unknown" : "not_a_successful_action",
                semanticPre: entry.SemanticPre,
                detail: entry.Started
                    ? "STS2 cancelled after execution started; no successful semantic transition is claimed."
                    : "STS2 cancelled before execution started; the Human precommit is retained but no successful A exists.",
                nonClaims: new[] { "no_semantic_successor" })
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> ObserveDecisionBoundary(
        SemanticBoundaryObservation boundary)
    {
        Entry? entry = WaitingForBoundaryBefore(long.MaxValue);
        return entry == null
            ? Array.Empty<SemanticBoundaryTraceDraft>()
            : Settle(entry, boundary, null);
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> CloseUnknown(string proofStatus)
    {
        var drafts = new List<SemanticBoundaryTraceDraft>();
        foreach (Entry entry in _order.Select(id => _entries[id]).Where(value => !value.Disposed))
        {
            entry.Disposed = true;
            entry.Disposition = SemanticBoundaryTraceKinds.TransitionUnknown;
            drafts.Add(Draft(
                SemanticBoundaryTraceKinds.TransitionUnknown,
                entry,
                proofStatus,
                semanticPre: entry.SemanticPre,
                detail: "The recording session closed before another complete semantic decision boundary was proved.",
                nonClaims: new[] { "no_semantic_successor" }));
        }
        return drafts;
    }

    public void Reset()
    {
        _entries.Clear();
        _order.Clear();
    }

    private IReadOnlyList<SemanticBoundaryTraceDraft> Settle(
        Entry entry,
        SemanticBoundaryObservation boundary,
        string? nextActionWitnessId)
    {
        if (!boundary.IsCompleteDecisionBoundary)
        {
            if (nextActionWitnessId == null)
                return Array.Empty<SemanticBoundaryTraceDraft>();
            entry.Disposed = true;
            entry.Disposition = SemanticBoundaryTraceKinds.TransitionUnknown;
            return new[]
            {
                Draft(
                    SemanticBoundaryTraceKinds.TransitionUnknown,
                    entry,
                    "boundary_incomplete_before_next_action",
                    relatedActionWitnessId: nextActionWitnessId,
                    boundary: boundary,
                    semanticPre: entry.SemanticPre,
                    detail: "The next Human action is about to execute, but the authoritative capture was not a complete decision state.",
                    nonClaims: new[] { "no_semantic_successor", "next_action_effect_not_included" })
            };
        }
        if (entry.SemanticPre == null)
        {
            entry.Disposed = true;
            entry.Disposition = SemanticBoundaryTraceKinds.TransitionUnknown;
            return new[]
            {
                Draft(
                    SemanticBoundaryTraceKinds.TransitionUnknown,
                    entry,
                    "semantic_pre_unknown",
                    relatedActionWitnessId: nextActionWitnessId,
                    boundary: boundary,
                    semanticSuccessor: boundary.State,
                    detail: "A successor boundary was observed, but the action's semantic pre-state was not proved.",
                nonClaims: new[] { "no_complete_s_a_s_prime" })
            };
        }
        if (string.Equals(
                entry.SemanticPre.SnapshotId,
                boundary.State!.SnapshotId,
                StringComparison.Ordinal))
        {
            entry.Disposed = true;
            entry.Disposition = SemanticBoundaryTraceKinds.TransitionUnknown;
            return new[]
            {
                Draft(
                    SemanticBoundaryTraceKinds.TransitionUnknown,
                    entry,
                    "successor_not_different",
                    relatedActionWitnessId: nextActionWitnessId,
                    boundary: boundary,
                    semanticPre: entry.SemanticPre,
                    semanticSuccessor: boundary.State,
                    detail: "The complete boundary retained the same Snapshot identity; no successful state transition is claimed.",
                    nonClaims: new[] { "no_complete_s_a_s_prime" })
            };
        }

        entry.Disposed = true;
        entry.Disposition = SemanticBoundaryTraceKinds.TransitionProved;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.TransitionProved,
                entry,
                entry.Paused ? "proved_player_choice_boundary" : "proved_next_decision_boundary",
                relatedActionWitnessId: nextActionWitnessId,
                boundary: boundary,
                semanticPre: entry.SemanticPre,
                semanticSuccessor: boundary.State,
                detail: nextActionWitnessId == null
                    ? "A complete authoritative decision boundary was observed after native execution."
                    : "A complete authoritative boundary was captured before the next Human action began execution.",
                nonClaims: new[] { "not_business_outcome", "not_inferred_gameplay_effects" })
        };
    }

    private Entry? WaitingForBoundaryBefore(long actionSequence) => _order
        .Select(id => _entries[id])
        .Where(entry => entry.Action.ActionSequence < actionSequence)
        .FirstOrDefault(IsWaitingForBoundary);

    private static bool IsWaitingForBoundary(Entry entry) =>
        !entry.Disposed
        && !entry.Cancelled
        && entry.Started
        && (entry.Finished || entry.Paused);

    private void PruneTerminalDisposed()
    {
        string[] removable = _order
            .Where(id =>
            {
                Entry entry = _entries[id];
                return entry.Disposed && (entry.Finished || entry.Cancelled);
            })
            .ToArray();
        foreach (string id in removable)
        {
            _entries.Remove(id);
            _order.Remove(id);
        }
    }

    private IReadOnlyList<SemanticBoundaryTraceDraft> Lifecycle(
        string actionWitnessId,
        string kind,
        string proofStatus)
    {
        Entry entry = Required(actionWitnessId);
        return new[] { Draft(kind, entry, proofStatus, semanticPre: entry.SemanticPre) };
    }

    private Entry Required(string actionWitnessId) =>
        _entries.TryGetValue(actionWitnessId, out Entry? entry)
            ? entry
            : throw new InvalidOperationException($"Unknown semantic action {actionWitnessId}.");

    private static SemanticBoundaryTraceDraft Draft(
        string kind,
        Entry entry,
        string proofStatus,
        string? relatedActionWitnessId = null,
        SemanticBoundaryObservation? boundary = null,
        FrozenDecisionFrameV2? semanticPre = null,
        FrozenDecisionFrameV2? semanticSuccessor = null,
        string? detail = null,
        IReadOnlyList<string>? nonClaims = null) => new(
            kind,
            entry.Action,
            proofStatus,
            relatedActionWitnessId,
            boundary,
            semanticPre,
            semanticSuccessor,
            detail,
            nonClaims ?? Array.Empty<string>());
}

public static class SemanticBoundaryTraceValidator
{
    private static readonly HashSet<string> KnownKinds = new(StringComparer.Ordinal)
    {
        SemanticBoundaryTraceKinds.ActionAccepted,
        SemanticBoundaryTraceKinds.ActionStarted,
        SemanticBoundaryTraceKinds.ActionPausedForPlayerChoice,
        SemanticBoundaryTraceKinds.ActionReadyToResume,
        SemanticBoundaryTraceKinds.ActionResumed,
        SemanticBoundaryTraceKinds.ActionFinished,
        SemanticBoundaryTraceKinds.ActionCancelledBeforeStart,
        SemanticBoundaryTraceKinds.ActionCancelledAfterStart,
        SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit,
        SemanticBoundaryTraceKinds.BoundaryObserved,
        SemanticBoundaryTraceKinds.TransitionProved,
        SemanticBoundaryTraceKinds.TransitionUnknown
    };

    public static IReadOnlyList<string> Validate(IReadOnlyList<SemanticBoundaryTraceEvent> events)
    {
        var errors = new List<string>();
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        long priorSequence = 0;
        foreach (SemanticBoundaryTraceEvent value in events)
        {
            if (value.SchemaVersion != SemanticBoundaryTraceContract.SchemaVersion
                || value.Schema != SemanticBoundaryTraceContract.EventSchema)
                errors.Add("semantic_boundary_schema_invalid");
            if (priorSequence == 0 && value.Sequence != 1)
                errors.Add("semantic_boundary_sequence_does_not_start_at_one");
            if (value.Sequence <= priorSequence)
                errors.Add("semantic_boundary_sequence_invalid");
            priorSequence = value.Sequence;
            if (!eventIds.Add(value.EventId))
                errors.Add("semantic_boundary_event_id_duplicate");
            if (!KnownKinds.Contains(value.Kind)
                || string.IsNullOrWhiteSpace(value.Action.ActionWitnessId)
                || value.Action.ActionSequence <= 0
                || string.IsNullOrWhiteSpace(value.Action.RecordId)
                || string.IsNullOrWhiteSpace(value.Action.RunId)
                || string.IsNullOrWhiteSpace(value.Action.NativeActionType))
                errors.Add("semantic_boundary_event_invalid");
            if (value.Kind == SemanticBoundaryTraceKinds.TransitionProved)
            {
                if (value.SemanticPre == null
                    || value.SemanticSuccessor == null
                    || value.Boundary?.IsCompleteDecisionBoundary != true)
                    errors.Add("semantic_transition_proof_incomplete");
                else if (value.SemanticPre.SnapshotId == value.SemanticSuccessor.SnapshotId)
                    errors.Add("semantic_transition_successor_not_different");
            }
        }

        foreach (IGrouping<string, SemanticBoundaryTraceEvent> group in events
                     .GroupBy(value => value.Action.ActionWitnessId, StringComparer.Ordinal))
        {
            SemanticBoundaryTraceEvent[] actionEvents = group.OrderBy(value => value.Sequence).ToArray();
            SemanticBoundaryTraceEvent accepted = actionEvents[0];
            if (accepted.Kind != SemanticBoundaryTraceKinds.ActionAccepted
                || actionEvents.Count(value => value.Kind == SemanticBoundaryTraceKinds.ActionAccepted) != 1)
                errors.Add("semantic_action_does_not_start_at_accepted");
            if (actionEvents.Any(value => value.Action != accepted.Action
                || value.SessionId != accepted.SessionId
                || value.TimelineId != accepted.TimelineId
                || value.RunId != accepted.RunId))
                errors.Add("semantic_action_identity_drift");
            bool started = actionEvents.Any(value => value.Kind == SemanticBoundaryTraceKinds.ActionStarted);
            bool proved = actionEvents.Any(value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
            if (actionEvents.Count(value => value.Kind == SemanticBoundaryTraceKinds.ActionStarted) > 1)
                errors.Add("semantic_action_started_duplicate");
            foreach (SemanticBoundaryTraceEvent disposition in actionEvents.Where(IsTransitionDisposition))
            {
                bool priorFinishedOrPaused = actionEvents.Any(value =>
                    value.Sequence < disposition.Sequence
                    && value.Kind is SemanticBoundaryTraceKinds.ActionFinished
                        or SemanticBoundaryTraceKinds.ActionPausedForPlayerChoice);
                if (!started || !priorFinishedOrPaused)
                    errors.Add("semantic_transition_disposition_before_boundary_eligibility");
            }
            if (actionEvents.Any(value => value.Kind == SemanticBoundaryTraceKinds.ActionCancelledBeforeStart)
                && started)
                errors.Add("semantic_cancel_before_start_conflicts_with_started");
            if (actionEvents.Any(value => value.Kind == SemanticBoundaryTraceKinds.ActionCancelledAfterStart)
                && (!started || proved))
                errors.Add("semantic_cancel_after_start_disposition_invalid");
            if (actionEvents.Any(value => value.Kind == SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit)
                && (!started || proved))
                errors.Add("semantic_abort_before_commit_disposition_invalid");
            if (actionEvents.Count(value => value.Kind is
                    SemanticBoundaryTraceKinds.TransitionProved
                    or SemanticBoundaryTraceKinds.TransitionUnknown
                    or SemanticBoundaryTraceKinds.ActionCancelledBeforeStart
                    or SemanticBoundaryTraceKinds.ActionCancelledAfterStart
                    or SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit) > 1)
                errors.Add("semantic_action_has_multiple_dispositions");
            if (actionEvents.Count(value => value.Kind is
                    SemanticBoundaryTraceKinds.TransitionProved
                    or SemanticBoundaryTraceKinds.TransitionUnknown
                    or SemanticBoundaryTraceKinds.ActionCancelledBeforeStart
                    or SemanticBoundaryTraceKinds.ActionCancelledAfterStart
                    or SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit) != 1)
                errors.Add("semantic_action_disposition_not_exactly_one");
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsTransitionDisposition(SemanticBoundaryTraceEvent value) =>
        value.Kind is SemanticBoundaryTraceKinds.TransitionProved
            or SemanticBoundaryTraceKinds.TransitionUnknown;
}
