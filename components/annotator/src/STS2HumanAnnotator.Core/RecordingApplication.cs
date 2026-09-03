namespace STS2HumanAnnotator.Core;

public static class RecordingApplicationContract
{
    public const string CommandSchema = "sts2.ai-platform/recording-command-1";
    public const string CommandResultSchema = "sts2.ai-platform/recording-command-result-1";
    public const string StatusSchema = "sts2.ai-platform/recording-status-3";
    public const string EventBatchSchema = "sts2.ai-platform/recording-event-batch-1";
}

public enum RecordingLifecycleState
{
    Ready,
    Recording,
    Paused,
    Closing,
    Closed
}

public enum RecordingCommandKind
{
    StartNewSession,
    Pause,
    Resume,
    Close
}

public sealed record RecordingCommand(
    string CommandId,
    RecordingCommandKind Kind,
    string? CaptureProfileId = null,
    string Schema = RecordingApplicationContract.CommandSchema);

public sealed record RecordingLifecycleSnapshot(
    RecordingLifecycleState State,
    string? SessionId,
    DateTimeOffset ChangedAt,
    string Detail)
{
    public static RecordingLifecycleSnapshot Ready(DateTimeOffset changedAt) =>
        new(
            RecordingLifecycleState.Ready,
            null,
            changedAt,
            "Recorder runtime is ready; no recording session is open.");
}

public sealed record RecordingCommandResult(
    bool Accepted,
    bool Pending,
    string Code,
    string Detail,
    RecordingLifecycleSnapshot Lifecycle,
    string Schema = RecordingApplicationContract.CommandResultSchema);

public sealed record RecordingSessionStatus(
    string SessionId,
    string TimelineId,
    string RunId,
    string CaptureProfileId,
    string RecordingDirectory,
    DateTimeOffset StartedAt,
    DateTimeOffset? ClosedAt);

public sealed record RecordingCounters(
    long Records,
    long Invalidations,
    long ReadsMaterialized,
    long ReadsFailed);

public sealed record RecordingStoreSnapshot(
    RecordingCounters Counters,
    RecordingItemStatus? LastRecord,
    RecordingItemStatus? LastInvalidation,
    IReadOnlyDictionary<string, long> RecordedActionFamilies,
    IReadOnlyDictionary<string, long> InvalidatedNativeActions,
    IReadOnlyDictionary<string, long> InvalidationsByReason,
    string AppendHealth,
    string DiskHealth,
    string? LastError,
    bool Closed);

public sealed record RecordingItemStatus(
    string Id,
    string Kind,
    DateTimeOffset ObservedAt,
    string? Detail);

public sealed record RecordingPendingStatus(
    string RecordId,
    string RunId,
    DateTimeOffset Deadline);

public sealed record RecordingHealthStatus(
    string RequiredReads,
    string Append,
    string Disk,
    string? LastError,
    DateTimeOffset CheckedAt);

public sealed record RecordingCloseoutStatus(
    string State,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? CompletedAt,
    string? Detail)
{
    public static RecordingCloseoutStatus Idle { get; } =
        new("idle", null, null, null);
}

public sealed record RecordingScopeStatus(
    IReadOnlyList<string> SupportedActionFamilies,
    IReadOnlyDictionary<string, long> RecordedByActionFamily,
    IReadOnlyDictionary<string, long> AcceptedFailedClosedByActionFamily,
    IReadOnlyDictionary<string, long> InvalidationsByReason,
    IReadOnlyList<string> SupportedNotObserved,
    IReadOnlyList<string> DeclaredOutOfScope,
    string Detail);

public sealed record RecordingApplicationStatus(
    string Schema,
    DateTimeOffset ObservedAt,
    int ProcessId,
    RecordingLifecycleSnapshot Lifecycle,
    RecordingSessionStatus? Session,
    RecordingCounters Counters,
    RecordingPendingStatus? PendingDecision,
    RecordingItemStatus? LastRecord,
    RecordingItemStatus? LastInvalidation,
    RecordingHealthStatus Health,
    RecordingScopeStatus Scope,
    RecordingCloseoutStatus Closeout,
    string RuntimeState,
    string Detail,
    RecorderEnvironmentIdentity? Environment,
    string? CurrentSnapshotId,
    IReadOnlyList<string> Blockers,
    long LatestEventSequence);

/// <summary>
/// Validates the bounded native UI interval between selecting a hand card and
/// STS2 attempting its native PlayCardAction. The second frame is expected to
/// be transient, so snapshot/catalog equality would reject legitimate plays.
/// </summary>
public static class StagedCardPlayGuard
{
    public static bool IsContinuous(
        string stagedRuntimeInstanceId,
        string stagedEnvironmentFingerprint,
        string stagedInteractionId,
        long stagedSequence,
        DateTimeOffset stagedAt,
        string currentRuntimeInstanceId,
        string currentEnvironmentFingerprint,
        string currentInteractionId,
        long currentSequence,
        DateTimeOffset observedAt,
        bool externalControllerActive,
        TimeSpan maximumAge)
    {
        return !externalControllerActive
            && maximumAge > TimeSpan.Zero
            && observedAt >= stagedAt
            && observedAt - stagedAt <= maximumAge
            && currentSequence >= stagedSequence
            && string.Equals(
                stagedRuntimeInstanceId,
                currentRuntimeInstanceId,
                StringComparison.Ordinal)
            && string.Equals(
                stagedEnvironmentFingerprint,
                currentEnvironmentFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                stagedInteractionId,
                currentInteractionId,
                StringComparison.Ordinal);
    }
}

public enum RecordingEventKind
{
    RuntimeReady,
    SessionStarted,
    SessionPaused,
    SessionResumed,
    SessionCloseRequested,
    SessionClosed,
    RunStarted,
    RunEnded,
    DecisionPending,
    DecisionRecorded,
    DecisionInvalidated,
    HealthChanged,
    CommandRejected
}

public sealed record RecordingEvent(
    long Sequence,
    string EventId,
    RecordingEventKind Kind,
    DateTimeOffset ObservedAt,
    string? SessionId,
    string? RunId,
    string? RecordId,
    string? Detail);

public sealed record RecordingEventBatch(
    long RequestedAfterSequence,
    long OldestAvailableSequence,
    long LatestSequence,
    bool Gap,
    IReadOnlyList<RecordingEvent> Events,
    string Schema = RecordingApplicationContract.EventBatchSchema);

public sealed class RecordingEventStream
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Queue<RecordingEvent> _events = new();
    private long _sequence;

    public RecordingEventStream(int capacity = 512)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public long LatestSequence
    {
        get
        {
            lock (_gate)
                return _sequence;
        }
    }

    public RecordingEvent Publish(
        RecordingEventKind kind,
        DateTimeOffset observedAt,
        string? sessionId = null,
        string? runId = null,
        string? recordId = null,
        string? detail = null)
    {
        lock (_gate)
        {
            long sequence = ++_sequence;
            var value = new RecordingEvent(
                sequence,
                $"recording-event-{sequence:D8}",
                kind,
                observedAt,
                sessionId,
                runId,
                recordId,
                detail);
            _events.Enqueue(value);
            while (_events.Count > _capacity)
                _events.Dequeue();
            return value;
        }
    }

    public RecordingEventBatch ReadAfter(long afterSequence)
    {
        if (afterSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(afterSequence));

        lock (_gate)
        {
            long oldest = _events.Count == 0 ? _sequence + 1 : _events.Peek().Sequence;
            bool gap = afterSequence < oldest - 1;
            return new RecordingEventBatch(
                afterSequence,
                oldest,
                _sequence,
                gap,
                gap
                    ? Array.Empty<RecordingEvent>()
                    : _events.Where(value => value.Sequence > afterSequence).ToArray());
        }
    }
}

public sealed class RecordingCommandLedger
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<string, RecordingCommandResult> _results = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();

    public RecordingCommandLedger(int capacity = 256)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public bool TryGet(string commandId, out RecordingCommandResult? result)
    {
        lock (_gate)
            return _results.TryGetValue(commandId, out result);
    }

    public void Remember(string commandId, RecordingCommandResult result)
    {
        if (string.IsNullOrWhiteSpace(commandId))
            throw new ArgumentException("A command id is required.", nameof(commandId));
        lock (_gate)
        {
            if (_results.ContainsKey(commandId))
                return;
            _results.Add(commandId, result);
            _order.Enqueue(commandId);
            while (_order.Count > _capacity)
                _results.Remove(_order.Dequeue());
        }
    }
}

public static class RecordingLifecycleStateMachine
{
    public static RecordingCommandResult Apply(
        RecordingLifecycleSnapshot current,
        RecordingCommandKind command,
        string? newSessionId,
        DateTimeOffset changedAt,
        bool pendingDecision)
    {
        return command switch
        {
            RecordingCommandKind.StartNewSession
                when current.State is RecordingLifecycleState.Ready or RecordingLifecycleState.Closed
                    && !string.IsNullOrWhiteSpace(newSessionId) =>
                Accepted(
                    RecordingLifecycleState.Recording,
                    newSessionId,
                    changedAt,
                    "session_started",
                    "Recording session started and is accepting eligible native-human decisions."),
            RecordingCommandKind.Pause when current.State == RecordingLifecycleState.Recording =>
                Accepted(
                    RecordingLifecycleState.Paused,
                    current.SessionId,
                    changedAt,
                    "recording_paused",
                    pendingDecision
                        ? "Recording is paused for new witnesses; the admitted pending decision will still settle."
                        : "Recording is paused; no new decision will be admitted."),
            RecordingCommandKind.Resume when current.State == RecordingLifecycleState.Paused =>
                Accepted(
                    RecordingLifecycleState.Recording,
                    current.SessionId,
                    changedAt,
                    "recording_resumed",
                    "Recording resumed and is accepting eligible native-human decisions."),
            RecordingCommandKind.Close
                when current.State is RecordingLifecycleState.Recording or RecordingLifecycleState.Paused =>
                Accepted(
                    RecordingLifecycleState.Closing,
                    current.SessionId,
                    changedAt,
                    "recording_close_requested",
                    pendingDecision
                        ? "Close is waiting for the admitted pending decision to settle or invalidate."
                        : "Close was accepted and the session is ready to flush.",
                    pendingDecision),
            _ => new RecordingCommandResult(
                false,
                false,
                "invalid_transition",
                $"Cannot {command.ToString().ToLowerInvariant()} while recording is {current.State.ToString().ToLowerInvariant()}.",
                current)
        };
    }

    public static RecordingLifecycleSnapshot MarkClosed(
        RecordingLifecycleSnapshot current,
        DateTimeOffset changedAt)
    {
        if (current.State != RecordingLifecycleState.Closing)
            throw new InvalidOperationException("Only a closing recording session can be marked closed.");
        return new RecordingLifecycleSnapshot(
            RecordingLifecycleState.Closed,
            current.SessionId,
            changedAt,
            "Recording session is closed; a new isolated session may now be started.");
    }

    private static RecordingCommandResult Accepted(
        RecordingLifecycleState state,
        string? sessionId,
        DateTimeOffset changedAt,
        string code,
        string detail,
        bool pending = false)
    {
        var lifecycle = new RecordingLifecycleSnapshot(
            state,
            sessionId,
            changedAt,
            detail);
        return new RecordingCommandResult(true, pending, code, detail, lifecycle);
    }
}
