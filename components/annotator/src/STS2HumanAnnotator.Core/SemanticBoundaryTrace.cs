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
        public long? ExecutionOrder { get; set; }
        public bool RequiresExecutionBoundaryRebind { get; set; }
        public string? SemanticPreUnknownReason { get; set; }
        public string? InterveningActionWitnessId { get; set; }
    }

    private readonly int _capacity;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();
    private long _executionSequence;

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
        Entry[] predecessors = WaitingForBoundaryInExecutionOrder();
        for (int index = 0; index < predecessors.Length; index++)
        {
            Entry predecessor = predecessors[index];
            drafts.AddRange(index == predecessors.Length - 1
                ? Settle(predecessor, boundary, next.Action.ActionWitnessId)
                : DisposeUnknown(
                    predecessor,
                    "intervening_human_action_before_boundary",
                    next.Action.ActionWitnessId,
                    "Another Human action began execution before a causal boundary for this action was captured."));
        }

        foreach (Entry preempted in _order
                     .Select(id => _entries[id])
                     .Where(entry => !entry.Disposed
                         && !entry.Started
                         && entry.Action.ActionSequence < next.Action.ActionSequence))
        {
            preempted.SemanticPre = null;
            preempted.RequiresExecutionBoundaryRebind = true;
            preempted.SemanticPreUnknownReason = "intervening_human_action_before_execution";
            preempted.InterveningActionWitnessId = next.Action.ActionWitnessId;
        }

        bool rebound = next.RequiresExecutionBoundaryRebind;
        string? interveningActionWitnessId = next.InterveningActionWitnessId;
        if (boundary.IsCompleteDecisionBoundary)
        {
            next.SemanticPre = boundary.State;
            next.RequiresExecutionBoundaryRebind = false;
            next.SemanticPreUnknownReason = null;
        }
        else if (next.RequiresExecutionBoundaryRebind || next.SemanticPre == null)
        {
            next.SemanticPre = null;
            next.SemanticPreUnknownReason ??= "execution_boundary_incomplete";
        }
        drafts.Add(Draft(
            SemanticBoundaryTraceKinds.BoundaryObserved,
            next,
            rebound && boundary.IsCompleteDecisionBoundary
                ? "complete_rebound_after_intervening_human_action"
                : next.SemanticPreUnknownReason
                    ?? (boundary.IsCompleteDecisionBoundary ? "complete" : "incomplete"),
            relatedActionWitnessId: interveningActionWitnessId,
            boundary: boundary,
            semanticPre: next.SemanticPre,
            detail: rebound && boundary.IsCompleteDecisionBoundary
                ? "A different accepted Human action executed first; this complete authoritative execution boundary safely rebinds the precommit to the S it immediately consumes."
                : next.SemanticPreUnknownReason == null
                    ? "Captured synchronously before the next tracked Human action begins execution."
                    : "No complete authoritative semantic pre-state was available before this Human action began execution."));
        return drafts;
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> Started(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Started = true;
        entry.ExecutionOrder ??= ++_executionSequence;
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
        Entry[] entries = WaitingForBoundaryInExecutionOrder();
        if (entries.Length == 0)
            return Array.Empty<SemanticBoundaryTraceDraft>();
        var drafts = new List<SemanticBoundaryTraceDraft>();
        for (int index = 0; index < entries.Length; index++)
        {
            Entry entry = entries[index];
            drafts.AddRange(index == entries.Length - 1
                ? Settle(entry, boundary, null)
                : DisposeUnknown(
                    entry,
                    "intervening_human_action_before_boundary",
                    entries[index + 1].Action.ActionWitnessId,
                    "Another Human action executed before a causal boundary for this action was observed."));
        }
        return drafts;
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
        _executionSequence = 0;
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
                    entry.SemanticPreUnknownReason ?? "semantic_pre_unknown",
                    relatedActionWitnessId: entry.InterveningActionWitnessId ?? nextActionWitnessId,
                    boundary: boundary,
                    detail: entry.SemanticPreUnknownReason == null
                        ? "A successor boundary was observed, but the action's semantic pre-state was not proved."
                        : "Another Human action executed after this precommit and before its native execution; a sequential S to A to S-prime sample cannot be proved.",
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

    private Entry[] WaitingForBoundaryInExecutionOrder() => _order
        .Select(id => _entries[id])
        .Where(IsWaitingForBoundary)
        .OrderBy(entry => entry.ExecutionOrder)
        .ToArray();

    private static IReadOnlyList<SemanticBoundaryTraceDraft> DisposeUnknown(
        Entry entry,
        string proofStatus,
        string relatedActionWitnessId,
        string detail)
    {
        entry.Disposed = true;
        entry.Disposition = SemanticBoundaryTraceKinds.TransitionUnknown;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.TransitionUnknown,
                entry,
                proofStatus,
                relatedActionWitnessId: relatedActionWitnessId,
                semanticPre: entry.SemanticPre,
                detail: detail,
                nonClaims: new[] { "no_semantic_successor", "intervening_human_effect" })
        };
    }

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

            SemanticBoundaryTraceEvent? provedEvent = actionEvents.FirstOrDefault(
                value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
            if (provedEvent != null)
            {
                SemanticBoundaryTraceEvent? startedEvent = actionEvents.FirstOrDefault(
                    value => value.Kind == SemanticBoundaryTraceKinds.ActionStarted);
                bool interveningHumanExecution = events.Any(value =>
                    value.Action.ActionWitnessId != accepted.Action.ActionWitnessId
                    && value.Kind == SemanticBoundaryTraceKinds.ActionStarted
                    && startedEvent != null
                    && value.Sequence > startedEvent.Sequence
                    && value.Sequence < provedEvent.Sequence);
                if (interveningHumanExecution)
                    errors.Add("semantic_transition_contains_intervening_human_action");

                SemanticBoundaryTraceEvent? executionBoundary = actionEvents
                    .Where(value => value.Kind == SemanticBoundaryTraceKinds.BoundaryObserved
                        && value.Sequence < (startedEvent?.Sequence ?? long.MaxValue)
                        && value.Boundary?.IsCompleteDecisionBoundary == true)
                    .LastOrDefault();
                if (executionBoundary?.Boundary?.State != null
                    && provedEvent.SemanticPre?.SnapshotId
                        != executionBoundary.Boundary.State.SnapshotId)
                    errors.Add("semantic_transition_pre_not_execution_boundary");
            }
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsTransitionDisposition(SemanticBoundaryTraceEvent value) =>
        value.Kind is SemanticBoundaryTraceKinds.TransitionProved
            or SemanticBoundaryTraceKinds.TransitionUnknown;
}
