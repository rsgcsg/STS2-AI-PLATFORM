namespace STS2HumanAnnotator.Core;

public static class SemanticBoundaryTraceContract
{
    public const int SchemaVersion = 2;
    public const string EventSchema = "sts2.human-annotator/semantic-boundary-trace-event-2";
    public const int LegacySchemaVersion = 1;
    public const string LegacyEventSchema = "sts2.human-annotator/semantic-boundary-trace-event-1";

    public static bool IsSupported(int schemaVersion, string schema) =>
        (schemaVersion == SchemaVersion && schema == EventSchema)
        || (schemaVersion == LegacySchemaVersion && schema == LegacyEventSchema);
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

/// <summary>
/// One read-only state capture. State completeness, action-catalog completeness,
/// and causal boundary proof are deliberately independent.
/// </summary>
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
    public string StateCompleteness { get; init; } = State == null ? "unavailable" : "complete";
    public string RequiredReadsStatus { get; init; } = State == null ? "unavailable" : "complete";
    public IReadOnlyList<string> StateBlockers { get; init; } = Array.Empty<string>();

    public bool HasCompleteSemanticState =>
        State != null
        && string.Equals(StateCompleteness, "complete", StringComparison.Ordinal)
        && string.Equals(RequiredReadsStatus, "complete", StringComparison.Ordinal);

    public bool IsCompleteDecisionBoundary =>
        HasCompleteSemanticState
        && string.Equals(Status, "interactive", StringComparison.Ordinal)
        && string.Equals(BoundActionsStatus, "complete", StringComparison.Ordinal);

    public bool IsExecutionBoundary =>
        !string.IsNullOrWhiteSpace(ImmediatelyConsumedByActionWitnessId)
        && string.Equals(WitnessKind, "before_next_human_action_execution", StringComparison.Ordinal);

    public bool CanBindExecutionPre => HasCompleteSemanticState && IsExecutionBoundary;

    public bool CanProveSemanticBoundary =>
        HasCompleteSemanticState && (IsExecutionBoundary || IsCompleteDecisionBoundary);
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
    IReadOnlyList<string>? NonClaims = null)
{
    public FrozenDecisionFrameV2? HumanObservation { get; init; }
}

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
    IReadOnlyList<string> NonClaims)
{
    public FrozenDecisionFrameV2? HumanObservation { get; init; }
}

/// <summary>
/// Coordinates a continuous causal timeline over exact Human/native facts.
/// Human observations explain what the player saw; only an execution boundary
/// or complete interactive boundary can establish semantic S.
/// </summary>
public sealed class SemanticBoundaryTracker
{
    private sealed class Entry
    {
        public Entry(SemanticActionReference action, FrozenDecisionFrameV2 humanObservation)
        {
            Action = action;
            HumanObservation = humanObservation;
        }

        public SemanticActionReference Action { get; }
        public FrozenDecisionFrameV2 HumanObservation { get; }
        public FrozenDecisionFrameV2? SemanticPre { get; set; }
        public bool Started { get; set; }
        public bool Paused { get; set; }
        public bool Finished { get; set; }
        public bool Disposed { get; set; }
        public long? ExecutionOrder { get; set; }
    }

    private readonly int _capacity;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();
    private FrozenDecisionFrameV2? _currentState;
    private long _executionSequence;

    public SemanticBoundaryTracker(int capacity = 128)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public bool Contains(string actionWitnessId) => _entries.ContainsKey(actionWitnessId);
    public bool HasUnresolvedActions => _order.Select(id => _entries[id]).Any(entry => !entry.Disposed);
    public bool NeedsBoundaryObservation => _order.Select(id => _entries[id]).Any(IsWaitingForBoundary);

    public IReadOnlyList<SemanticBoundaryTraceDraft> Accept(
        SemanticActionReference action,
        FrozenDecisionFrameV2 humanObservation)
    {
        if (_entries.ContainsKey(action.ActionWitnessId))
            throw new InvalidOperationException("Semantic action identity was accepted twice.");
        PruneDisposed();
        if (_entries.Count >= _capacity)
            throw new InvalidOperationException("Semantic boundary tracker capacity was exceeded.");

        var entry = new Entry(action, humanObservation);
        _entries.Add(action.ActionWitnessId, entry);
        _order.Add(action.ActionWitnessId);
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionAccepted,
                entry,
                "human_observation_recorded",
                detail: "The Human observation is retained separately; acceptance does not establish semantic S.",
                humanObservation: humanObservation,
                nonClaims: new[] { "acceptance_does_not_bind_semantic_pre" })
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> ObserveBeforeActionExecution(
        string nextActionWitnessId,
        SemanticBoundaryObservation boundary)
    {
        Entry next = Required(nextActionWitnessId);
        if (boundary.ImmediatelyConsumedByActionWitnessId != nextActionWitnessId)
            throw new InvalidOperationException("Execution boundary does not bind the action it immediately precedes.");

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

        _currentState = boundary.CanBindExecutionPre ? boundary.State : null;
        next.SemanticPre = _currentState;
        drafts.Add(Draft(
            SemanticBoundaryTraceKinds.BoundaryObserved,
            next,
            boundary.CanBindExecutionPre
                ? "execution_boundary_bound"
                : "execution_boundary_state_incomplete",
            boundary: boundary,
            semanticPre: next.SemanticPre,
            detail: boundary.CanBindExecutionPre
                ? "Captured synchronously before this exact Human action begins execution; catalog publication is not required for semantic state completeness."
                : "No complete authoritative state was available immediately before this Human action began execution.",
            nonClaims: boundary.CanBindExecutionPre
                ? new[] { "action_catalog_does_not_authorize_semantic_state" }
                : new[] { "semantic_pre_unknown" }));
        return drafts;
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> Started(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Started = true;
        entry.ExecutionOrder ??= ++_executionSequence;
        _currentState = null;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionStarted,
                entry,
                entry.SemanticPre == null ? "semantic_pre_unknown" : "execution_consumed_current_s",
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
                    : "awaiting_next_semantic_boundary",
                semanticPre: entry.SemanticPre)
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> AbortedBeforeCommit(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Disposed = true;
        _currentState = entry.SemanticPre;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit,
                entry,
                "not_a_successful_action",
                semanticPre: entry.SemanticPre,
                detail: "The exact PlayCardAction path returned before native resource spend/OnPlay Commit; the consumed S remains current.",
                nonClaims: new[] { "no_semantic_successor" })
        };
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> Cancelled(string actionWitnessId)
    {
        Entry entry = Required(actionWitnessId);
        entry.Disposed = true;
        if (entry.Started)
            _currentState = null;
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
        if (!boundary.IsCompleteDecisionBoundary)
            return Array.Empty<SemanticBoundaryTraceDraft>();

        Entry[] entries = WaitingForBoundaryInExecutionOrder();
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
        _currentState = boundary.State;
        return drafts;
    }

    public IReadOnlyList<SemanticBoundaryTraceDraft> CloseUnknown(string proofStatus)
    {
        var drafts = new List<SemanticBoundaryTraceDraft>();
        foreach (Entry entry in _order.Select(id => _entries[id]).Where(value => !value.Disposed))
        {
            entry.Disposed = true;
            drafts.Add(Draft(
                SemanticBoundaryTraceKinds.TransitionUnknown,
                entry,
                proofStatus,
                semanticPre: entry.SemanticPre,
                detail: "The recording drain ended before another complete semantic boundary was proved.",
                nonClaims: new[] { "no_semantic_successor" }));
        }
        _currentState = null;
        return drafts;
    }

    public void Reset()
    {
        _entries.Clear();
        _order.Clear();
        _currentState = null;
        _executionSequence = 0;
    }

    private IReadOnlyList<SemanticBoundaryTraceDraft> Settle(
        Entry entry,
        SemanticBoundaryObservation boundary,
        string? nextActionWitnessId)
    {
        if (!boundary.CanProveSemanticBoundary)
        {
            if (nextActionWitnessId == null)
                return Array.Empty<SemanticBoundaryTraceDraft>();
            entry.Disposed = true;
            return new[]
            {
                Draft(
                    SemanticBoundaryTraceKinds.TransitionUnknown,
                    entry,
                    "semantic_state_incomplete_before_next_action",
                    relatedActionWitnessId: nextActionWitnessId,
                    boundary: boundary,
                    semanticPre: entry.SemanticPre,
                    detail: "The next Human action is about to execute, but the authoritative player-visible state capture was incomplete.",
                    nonClaims: new[] { "no_semantic_successor", "next_action_effect_not_included" })
            };
        }
        if (entry.SemanticPre == null)
        {
            entry.Disposed = true;
            return new[]
            {
                Draft(
                    SemanticBoundaryTraceKinds.TransitionUnknown,
                    entry,
                    "semantic_pre_unknown",
                    relatedActionWitnessId: nextActionWitnessId,
                    boundary: boundary,
                    detail: "A causal successor boundary exists, but this action did not consume a proved semantic S.",
                    nonClaims: new[] { "no_complete_s_a_s_prime" })
            };
        }
        if (string.Equals(entry.SemanticPre.SnapshotId, boundary.State!.SnapshotId, StringComparison.Ordinal))
        {
            entry.Disposed = true;
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
                    detail: "The proved boundary retained the same Snapshot identity; no successful state transition is claimed.",
                    nonClaims: new[] { "no_complete_s_a_s_prime" })
            };
        }

        entry.Disposed = true;
        return new[]
        {
            Draft(
                SemanticBoundaryTraceKinds.TransitionProved,
                entry,
                entry.Paused
                    ? "proved_player_choice_boundary"
                    : nextActionWitnessId == null
                        ? "proved_interactive_decision_boundary"
                        : "proved_execution_handoff_boundary",
                relatedActionWitnessId: nextActionWitnessId,
                boundary: boundary,
                semanticPre: entry.SemanticPre,
                semanticSuccessor: boundary.State,
                detail: nextActionWitnessId == null
                    ? "A complete interactive semantic boundary was observed after native execution."
                    : "A complete authoritative state was captured before the next Human effect; catalog publication was not used as boundary authority.",
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
        !entry.Disposed && entry.Started && (entry.Finished || entry.Paused);

    private void PruneDisposed()
    {
        foreach (string id in _order.Where(id => _entries[id].Disposed).ToArray())
        {
            _entries.Remove(id);
            _order.Remove(id);
        }
    }

    private Entry Required(string actionWitnessId) =>
        _entries.TryGetValue(actionWitnessId, out Entry? entry)
            ? entry
            : throw new InvalidOperationException($"Unknown semantic action witness: {actionWitnessId}");

    private IReadOnlyList<SemanticBoundaryTraceDraft> Lifecycle(
        string actionWitnessId,
        string kind,
        string proofStatus)
    {
        Entry entry = Required(actionWitnessId);
        return new[] { Draft(kind, entry, proofStatus, semanticPre: entry.SemanticPre) };
    }

    private static SemanticBoundaryTraceDraft Draft(
        string kind,
        Entry entry,
        string proofStatus,
        string? relatedActionWitnessId = null,
        SemanticBoundaryObservation? boundary = null,
        FrozenDecisionFrameV2? semanticPre = null,
        FrozenDecisionFrameV2? semanticSuccessor = null,
        string? detail = null,
        FrozenDecisionFrameV2? humanObservation = null,
        IReadOnlyList<string>? nonClaims = null) =>
        new(
            kind,
            entry.Action,
            proofStatus,
            relatedActionWitnessId,
            boundary,
            semanticPre,
            semanticSuccessor,
            detail,
            nonClaims)
        {
            HumanObservation = humanObservation
        };
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
        long previousSequence = 0;
        foreach (SemanticBoundaryTraceEvent value in events)
        {
            if (!SemanticBoundaryTraceContract.IsSupported(value.SchemaVersion, value.Schema))
                errors.Add("semantic_boundary_trace_schema_invalid");
            if (value.Sequence <= previousSequence)
                errors.Add("semantic_boundary_trace_sequence_invalid");
            previousSequence = Math.Max(previousSequence, value.Sequence);
            if (!KnownKinds.Contains(value.Kind))
                errors.Add("semantic_boundary_trace_kind_invalid");
            if (value.Kind == SemanticBoundaryTraceKinds.TransitionProved)
            {
                bool boundaryValid = value.SchemaVersion == SemanticBoundaryTraceContract.LegacySchemaVersion
                    ? value.Boundary?.IsCompleteDecisionBoundary == true
                    : value.Boundary?.CanProveSemanticBoundary == true;
                if (value.SemanticPre == null || value.SemanticSuccessor == null || !boundaryValid)
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
                errors.Add("semantic_action_acceptance_invalid");
            if (accepted.SchemaVersion == SemanticBoundaryTraceContract.SchemaVersion
                && (accepted.HumanObservation == null || accepted.SemanticPre != null))
                errors.Add("semantic_human_observation_not_separated_from_pre");

            bool started = actionEvents.Any(value => value.Kind == SemanticBoundaryTraceKinds.ActionStarted);
            if (actionEvents.Count(value => value.Kind == SemanticBoundaryTraceKinds.ActionStarted) > 1)
                errors.Add("semantic_action_started_twice");
            foreach (SemanticBoundaryTraceEvent disposition in actionEvents.Where(IsDisposition))
            {
                bool lifecycleFinished = actionEvents.Any(value =>
                    value.Sequence < disposition.Sequence
                    && value.Kind is SemanticBoundaryTraceKinds.ActionFinished
                        or SemanticBoundaryTraceKinds.ActionPausedForPlayerChoice);
                if (disposition.Kind == SemanticBoundaryTraceKinds.TransitionProved
                    && (!started || !lifecycleFinished))
                    errors.Add("semantic_transition_lifecycle_incomplete");
                if (disposition.Kind == SemanticBoundaryTraceKinds.ActionCancelledBeforeStart && started)
                    errors.Add("semantic_cancel_before_start_disposition_invalid");
                if (disposition.Kind == SemanticBoundaryTraceKinds.ActionCancelledAfterStart && !started)
                    errors.Add("semantic_cancel_after_start_disposition_invalid");
                if (disposition.Kind == SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit && !started)
                    errors.Add("semantic_abort_before_commit_disposition_invalid");
            }
            int dispositionCount = actionEvents.Count(IsDisposition);
            if (dispositionCount > 1)
                errors.Add("semantic_action_has_multiple_dispositions");
            if (dispositionCount != 1)
                errors.Add("semantic_action_disposition_not_exactly_one");

            SemanticBoundaryTraceEvent? proved = actionEvents.FirstOrDefault(
                value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
            SemanticBoundaryTraceEvent? startedEvent = actionEvents.FirstOrDefault(
                value => value.Kind == SemanticBoundaryTraceKinds.ActionStarted);
            if (proved != null && startedEvent != null)
            {
                bool interveningHumanExecution = events.Any(value =>
                    value.Action.ActionWitnessId != accepted.Action.ActionWitnessId
                    && value.Kind == SemanticBoundaryTraceKinds.ActionStarted
                    && value.Sequence > startedEvent.Sequence
                    && value.Sequence < proved.Sequence);
                if (interveningHumanExecution)
                    errors.Add("semantic_transition_contains_intervening_human_action");

                SemanticBoundaryTraceEvent? executionBoundary = actionEvents
                    .Where(value => value.Kind == SemanticBoundaryTraceKinds.BoundaryObserved
                        && value.Sequence < startedEvent.Sequence
                        && value.Boundary?.CanBindExecutionPre == true)
                    .LastOrDefault();
                if (proved.SchemaVersion == SemanticBoundaryTraceContract.SchemaVersion
                    && (executionBoundary?.Boundary?.State == null
                        || proved.SemanticPre?.SnapshotId != executionBoundary.Boundary.State.SnapshotId))
                    errors.Add("semantic_transition_pre_not_execution_boundary");
            }
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsDisposition(SemanticBoundaryTraceEvent value) =>
        value.Kind is SemanticBoundaryTraceKinds.TransitionProved
            or SemanticBoundaryTraceKinds.TransitionUnknown
            or SemanticBoundaryTraceKinds.ActionCancelledBeforeStart
            or SemanticBoundaryTraceKinds.ActionCancelledAfterStart
            or SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit;
}
