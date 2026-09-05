using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.PlayerEnvironment.Protocol;
using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;
using STS2Platform.NativeFoundation;

namespace STS2HumanAnnotator.Mod;

internal static class RecorderRuntime
{
    private sealed record ExactDecisionFrame(
        ProcessLocalNativeWitnessFrame Frame,
        RecorderEnvironmentIdentity Environment);

    private sealed record StagedCardFrame(
        ExactDecisionFrame Decision,
        CardModel Card,
        DateTimeOffset StagedAt);

    private sealed record ArmedPotionUse(
        long Generation,
        ExactDecisionFrame? Decision,
        string SessionId,
        string TimelineId,
        string? FailureReason,
        string? FailureDetail,
        string? FailureSnapshotId,
        string? FailureEvidenceLevel);

    private static readonly object Gate = new();
    private static AnnotatorConfiguration? _configuration;
    private static RecordingSessionStore? _store;
    private static readonly SemanticBoundaryTracker BoundaryTracker = new();
    private static readonly Dictionary<GameAction, NativeActionLifecycleSubscription>
        NativeActionSubscriptions = new(ReferenceEqualityComparer.Instance);
    private static readonly HashSet<string> SemanticOnlyNativeActionIds =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, RecorderEnvironmentIdentity>
        SemanticProjectionEnvironments = new(StringComparer.Ordinal);
    private static readonly NativePostCommitCompletionLedger NativePostCommitCompletions = new();
    private static readonly Queue<NativeTaskCompletion> QueuedNativePostCommitCompletions = new();
    private static StagedCardFrame? _stagedCardFrame;
    private static readonly Dictionary<PotionModel, ArmedPotionUse> ArmedPotionUses =
        new(ReferenceEqualityComparer.Instance);
    private static long _potionArmGeneration;
    private static long _sequence;
    private static long _journalSequence;
    private static long _semanticBoundaryEventSequence;
    private static long _nativePostCommitGeneration;
    private static DateTimeOffset _lastIdleStatusAt;
    private static volatile bool _statusRefreshRequested;
    private static ActionExecutor? _observedActionExecutor;
    private static bool _semanticBoundaryTraceHealthy = true;
    private static bool _nativeRunStartedObserved;
    private static string _runtimeState = "initializing";
    private static string? _detail;
    private static RecorderEnvironmentIdentity? _lastEnvironment;
    private static string? _lastSnapshotId;
    private static IReadOnlyList<string> _lastBlockers = Array.Empty<string>();
    private static RecordingLifecycleSnapshot _lifecycle =
        RecordingLifecycleSnapshot.Ready(DateTimeOffset.UnixEpoch);
    private static readonly RecordingEventStream ApplicationEvents = new();
    private static readonly RecordingCommandLedger CommandLedger = new();
    private static RecordingStoreSnapshot _lastStoreSnapshot = new(
        new RecordingCounters(0, 0, 0, 0),
        null,
        null,
        new Dictionary<string, long>(StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal),
        "not_open",
        "not_open",
        null,
        true);
    private static RecordingCloseoutStatus _closeout = RecordingCloseoutStatus.Idle;
    private static DateTimeOffset? _sessionStartedAt;
    private static DateTimeOffset? _sessionClosedAt;
    private static string? _recordingDirectory;
    private static string? _sourceRevision;
    private static string _requiredReadsHealth = "not_active";
    private static string? _lastPublishedHealth;
    private static bool _initializationStarted;
    private static bool _initialized;
    // A close disposition is previewed before it is committed to the causal
    // tracker. If the multi-stream store write fails, keep the session open
    // and the roots unresolved; never close on an in-memory-only unknown.
    private static bool _closeDispositionPersistenceFailed;
    private static bool _closeProjectionPersistenceFailed;
    private static int _runSequence;
    private static bool _runActive;
    // A native RunManager.OnEnded observation is the only authoritative
    // terminal marker.  Polling IsInProgress may describe a transition, but
    // it must never publish a successful terminal disposition by itself.
    private static bool _nativeRunEndedObserved;
    private static string _currentRunId = "run-unassigned";
    private static readonly HumanCaptureProfile CaptureProfile =
        HumanCaptureProfiles.FullRunReadRich;
    private static readonly string[] DeclaredOutOfScopeActionFamilies =
    {
        "unverified_selector_families"
    };

    internal static string? SessionId { get; private set; }

    internal static string? TimelineId { get; private set; }

    internal static void Initialize(AnnotatorConfiguration configuration)
    {
        lock (Gate)
        {
            if (_initializationStarted)
                return;
            _initializationStarted = true;
        }

        _configuration = configuration;
        NativeDecisionOwnerReadyProvider.Observed += ObserveNativeDecisionOwnerReady;
        RunManager.Instance.ActEntered += ObserveNativeActEntered;
        Assembly assembly = typeof(RecorderMod).Assembly;
        _sourceRevision = ReadSourceRevision(assembly);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (Gate)
        {
            _lifecycle = RecordingLifecycleSnapshot.Ready(now);
            _initialized = true;
            _runtimeState = "ready";
            _detail = _lifecycle.Detail;
        }
        PublishApplicationEvent(RecordingEventKind.RuntimeReady, detail: _detail);
        WriteStatus(null, null, new[] { "no_open_recording_session", "no_current_exact_frame" });
    }

    internal static RecordingLifecycleSnapshot GetRecordingLifecycle()
    {
        lock (Gate)
            return _lifecycle;
    }

    /// <summary>
    /// Returns the exact active Human root only when the current native
    /// callback is part of that root's declared lifecycle. This is used by
    /// native controls whose game-owned async operation starts after the
    /// control has synchronously disabled its options; it never selects a
    /// queued/FIFO root or creates a new authority.
    /// </summary>
    internal static string? CurrentSemanticActionWitnessId(string nativeActionType)
    {
        HumanActionContext? context = HumanActionScope.Current;
        return context != null
            && context.AcceptsRootAction(nativeActionType)
            ? context.ActionWitnessId
            : null;
    }

    internal static RecordingApplicationStatus GetRecordingApplicationStatus()
    {
        lock (Gate)
        {
            RecordingStoreSnapshot store = _store?.GetSnapshot() ?? _lastStoreSnapshot;
            return new RecordingApplicationStatus(
                RecordingApplicationContract.StatusSchema,
                DateTimeOffset.UtcNow,
                System.Environment.ProcessId,
                _lifecycle,
                BuildSessionStatus(),
                store.Counters,
                null,
                store.LastRecord,
                store.LastInvalidation,
                new RecordingHealthStatus(
                    RequiredReadHealth(),
                    store.AppendHealth,
                    store.DiskHealth,
                    store.LastError,
                    DateTimeOffset.UtcNow),
                BuildScopeStatus(store),
                _closeout,
                _runtimeState,
                _detail ?? _lifecycle.Detail,
                _lastEnvironment,
                _lastSnapshotId,
                _lastBlockers.ToArray(),
                ApplicationEvents.LatestSequence);
        }
    }

    internal static RecordingEventBatch ReadRecordingEvents(long afterSequence) =>
        ApplicationEvents.ReadAfter(afterSequence);

    internal static RecordingCommandResult ExecuteRecordingCommand(RecordingCommand command)
    {
        if (!string.Equals(
                command.Schema,
                RecordingApplicationContract.CommandSchema,
                StringComparison.Ordinal))
            return RejectedCommand("unsupported_command_schema", "Recording command schema is unsupported.");
        if (string.IsNullOrWhiteSpace(command.CommandId))
            return RejectedCommand("invalid_command_id", "Recording command_id is required.");

        RecordingCommandResult result;
        RecorderEnvironmentIdentity? environment;
        string? snapshotId;
        IReadOnlyList<string> blockers;
        bool initialized;
        bool finalizeClose = false;

        lock (Gate)
        {
            initialized = _initialized;
            if (CommandLedger.TryGet(command.CommandId, out RecordingCommandResult? existing))
                return existing!;
            if (!initialized)
            {
                result = new RecordingCommandResult(
                    false,
                    false,
                    "not_initialized",
                    "Recording controls are unavailable before the recorder runtime starts.",
                    _lifecycle);
                environment = _lastEnvironment;
                snapshotId = _lastSnapshotId;
                blockers = _lastBlockers;
            }
            else if (IsStateNoOp(command.Kind, _lifecycle.State))
            {
                string state = _lifecycle.State.ToString().ToLowerInvariant();
                result = new RecordingCommandResult(
                    true,
                    _lifecycle.State == RecordingLifecycleState.Closing,
                    $"already_{state}",
                    $"Recording is already {state}.",
                    _lifecycle);
                environment = _lastEnvironment;
                snapshotId = _lastSnapshotId;
                blockers = _lastBlockers;
                _detail = result.Detail;
            }
            else
            {
                string? newSessionId = command.Kind == RecordingCommandKind.StartNewSession
                    ? $"session-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}"
                    : null;
                result = RecordingLifecycleStateMachine.Apply(
                    _lifecycle,
                    command.Kind,
                    newSessionId,
                    DateTimeOffset.UtcNow,
                    pendingRoot: HasPendingRecordingWorkUnsafe());
                if (result.Accepted)
                {
                    if (command.Kind == RecordingCommandKind.StartNewSession)
                    {
                        try
                        {
                            StartSession(result.Lifecycle, command.CaptureProfileId);
                        }
                        catch (Exception exception)
                        {
                            _runtimeState = "session_start_failed";
                            _detail = exception.Message;
                            _lastStoreSnapshot = _lastStoreSnapshot with
                            {
                                AppendHealth = "failed",
                                DiskHealth = "failed",
                                LastError = exception.Message
                            };
                            result = new RecordingCommandResult(
                                false,
                                false,
                                "session_start_failed",
                                exception.Message,
                                _lifecycle);
                        }
                    }
                    else
                    {
                        _lifecycle = result.Lifecycle;
                    }
                    if (_lifecycle.State != RecordingLifecycleState.Recording)
                        _stagedCardFrame = null;
                    if (command.Kind == RecordingCommandKind.Close && result.Accepted)
                    {
                        _closeout = new RecordingCloseoutStatus(
                            "closing",
                            DateTimeOffset.UtcNow,
                            null,
                            result.Detail);
                        finalizeClose = true;
                    }
                }
                _detail = result.Detail;
                _runtimeState = RuntimeStatusForLifecycle(_lifecycle.State, _runtimeState);
                environment = _lastEnvironment;
                snapshotId = _lastSnapshotId;
                blockers = _lastBlockers;
            }
            CommandLedger.Remember(command.CommandId, result);
        }

        if (!initialized)
            return result;

        PublishCommandEvent(command, result);
        if (result.Accepted && command.Kind != RecordingCommandKind.StartNewSession)
            AppendJournal(result.Code, null, snapshotId, result.Detail);
        if (finalizeClose)
        {
            FinalizeClose();
        }
        WriteStatus(
            environment,
            snapshotId,
            blockers.Concat(LifecycleBlockers(GetRecordingLifecycle().State)).Distinct(StringComparer.Ordinal).ToArray());
        return result;
    }

    private static bool IsStateNoOp(
        RecordingCommandKind command,
        RecordingLifecycleState state) =>
        command switch
        {
            RecordingCommandKind.Pause => state == RecordingLifecycleState.Paused,
            RecordingCommandKind.Resume => state == RecordingLifecycleState.Recording,
            RecordingCommandKind.Close => state is RecordingLifecycleState.Closing or RecordingLifecycleState.Closed,
            _ => false
        };

    private static void StartSession(
        RecordingLifecycleSnapshot lifecycle,
        string? requestedCaptureProfileId)
    {
        if (_configuration == null || _sourceRevision == null || lifecycle.SessionId == null)
            throw new InvalidOperationException("Recorder runtime initialization is incomplete.");
        if (requestedCaptureProfileId != null
            && !string.Equals(requestedCaptureProfileId, CaptureProfile.ProfileId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported capture profile {requestedCaptureProfileId}; expected {CaptureProfile.ProfileId}.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string timelineId = $"timeline-{Guid.NewGuid():N}";
        var manifest = new CurrentRecordingManifest(
            CurrentRecordingContract.SchemaVersion,
            CurrentRecordingContract.ManifestSchema,
            lifecycle.SessionId,
            timelineId,
            now,
            RecorderMod.Version,
            _sourceRevision,
            System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            CaptureProfile.ProfileId,
            EvidenceIdentity.Sha256Json(CaptureProfile),
            CaptureProfile.SupportedActionFamilies,
            CaptureProfile.NonClaims.Append("not_human_validated").ToArray());
        RecordingSessionStore store = RecordingSessionStore.Create(
            _configuration.RecordingRoot,
            manifest,
            CaptureProfile);

        _store = store;
        SessionId = lifecycle.SessionId;
        TimelineId = timelineId;
        _recordingDirectory = store.DirectoryPath;
        _sessionStartedAt = now;
        _sessionClosedAt = null;
        _sequence = 0;
        _journalSequence = 0;
        _semanticBoundaryEventSequence = 0;
        _lastIdleStatusAt = DateTimeOffset.MinValue;
        _statusRefreshRequested = true;
        _runSequence = 0;
        _runActive = false;
        _nativeRunStartedObserved = false;
        _nativeRunEndedObserved = false;
        _currentRunId = "run-unassigned";
        ResetNativeActionTrackingUnsafe();
        _semanticBoundaryTraceHealthy = true;
        _stagedCardFrame = null;
        _lastStoreSnapshot = store.GetSnapshot();
        _requiredReadsHealth = "not_checked";
        _lastPublishedHealth = null;
        _closeDispositionPersistenceFailed = false;
        _closeProjectionPersistenceFailed = false;
        _closeout = RecordingCloseoutStatus.Idle;
        _lifecycle = lifecycle;
        _runtimeState = "waiting_for_player_environment";
        _detail = lifecycle.Detail;
        AppendJournal("session_started", null, null, "Current read-rich recording session started.");
    }

    private static RecordingSessionStatus? BuildSessionStatus()
    {
        if (SessionId == null
            || TimelineId == null
            || _recordingDirectory == null
            || _sessionStartedAt == null)
            return null;
        return new RecordingSessionStatus(
            SessionId,
            TimelineId,
            _currentRunId,
            CaptureProfile.ProfileId,
            _recordingDirectory,
            _sessionStartedAt.Value,
            _sessionClosedAt);
    }

    private static string RequiredReadHealth()
    {
        if (_lifecycle.State is RecordingLifecycleState.Ready or RecordingLifecycleState.Closed)
            return "not_active";
        return _requiredReadsHealth;
    }

    private static RecordingCommandResult RejectedCommand(string code, string detail)
    {
        lock (Gate)
            return new RecordingCommandResult(false, false, code, detail, _lifecycle);
    }

    private static void PublishCommandEvent(
        RecordingCommand command,
        RecordingCommandResult result)
    {
        if (result.Code.StartsWith("already_", StringComparison.Ordinal))
            return;
        RecordingEventKind kind = result.Accepted
            ? command.Kind switch
            {
                RecordingCommandKind.StartNewSession => RecordingEventKind.SessionStarted,
                RecordingCommandKind.Pause => RecordingEventKind.SessionPaused,
                RecordingCommandKind.Resume => RecordingEventKind.SessionResumed,
                RecordingCommandKind.Close => RecordingEventKind.SessionCloseRequested,
                _ => RecordingEventKind.CommandRejected
            }
            : RecordingEventKind.CommandRejected;
        PublishApplicationEvent(kind, detail: $"{result.Code}: {result.Detail}");
    }

    private static void PublishApplicationEvent(
        RecordingEventKind kind,
        string? recordId = null,
        string? detail = null,
        RecordingActionProjection? action = null)
    {
        string? sessionId;
        string? runId;
        lock (Gate)
        {
            sessionId = SessionId;
            runId = SessionId == null ? null : _currentRunId;
        }
        ApplicationEvents.Publish(
            kind,
            DateTimeOffset.UtcNow,
            sessionId,
            runId,
            recordId,
            detail,
            action);
    }

    private static RecordingActionProjection ToActionProjection(RecordedBoundAction action) =>
        new(
            action.Verb,
            action.BoundActionId,
            action.SubjectReferentId,
            new Dictionary<string, string>(action.Arguments, StringComparer.Ordinal),
            action.Label,
            null);

    private static void FinalizeClose()
    {
        IReadOnlyList<SemanticBoundaryTraceDraft> closeDrafts;
        lock (Gate)
        {
            if (_lifecycle.State != RecordingLifecycleState.Closing
                || _closeDispositionPersistenceFailed
                || _closeProjectionPersistenceFailed)
                return;
            if (!_semanticBoundaryTraceHealthy)
            {
                _closeDispositionPersistenceFailed = true;
                _runtimeState = "close_disposition_persistence_failed";
                _detail = "Semantic boundary trace is unavailable; close disposition cannot be persisted.";
                _closeout = _closeout with
                {
                    State = "closing",
                    Detail = "Close disposition persistence failed; accepted roots remain unresolved."
                };
                return;
            }
            closeDrafts = BoundaryTracker.PreviewCloseUnknown(
                RecordingClosePolicy.TerminalUnknownReason);
        }
        bool authoritativeDispositionPersisted = closeDrafts.Count == 0;
        bool derivedProjectionFailed = false;
        try
        {
            PersistSemanticBoundaryDrafts(
                closeDrafts,
                onAuthoritativeSemanticAppend: () =>
                {
                    lock (Gate)
                    {
                        if (_lifecycle.State != RecordingLifecycleState.Closing
                            || authoritativeDispositionPersisted)
                            return;
                        BoundaryTracker.CommitCloseUnknown();
                        authoritativeDispositionPersisted = true;
                    }
                },
                onDerivedProjectionFailure: () => derivedProjectionFailed = true);
        }
        catch (Exception exception)
        {
            // A failure before the semantic stream append leaves roots live;
            // a later projection failure follows a durable disposition and
            // must not make that root appear unresolved again.
            bool dispositionWasDurable = authoritativeDispositionPersisted;
            lock (Gate)
            {
                if (dispositionWasDurable)
                {
                    _closeProjectionPersistenceFailed = true;
                    _runtimeState = "close_projection_persistence_failed";
                }
                else
                {
                    _closeDispositionPersistenceFailed = true;
                    _runtimeState = "close_disposition_persistence_failed";
                }
                _detail = exception.Message;
                _closeout = _closeout with
                {
                    State = "closing",
                    Detail = dispositionWasDurable
                        ? "Close projection persistence failed after the authoritative disposition; the session remains auditable but open."
                        : "Close disposition persistence failed; accepted roots remain unresolved."
                };
            }
            Quarantine(
                dispositionWasDurable
                    ? "close_projection_persistence_failed"
                    : "close_disposition_persistence_failed",
                exception.Message,
                _lastSnapshotId,
                null,
                "evidence_commit_unknown");
            // Quarantine is itself an application projection and may update
            // the transient runtime label. Restore the close phase after that
            // best-effort diagnostic write so the failure stays explicit.
            lock (Gate)
            {
                _runtimeState = dispositionWasDurable
                    ? "close_projection_persistence_failed"
                    : "close_disposition_persistence_failed";
                _detail = exception.Message;
            }
            GD.PrintErr($"[STS2 Human Annotator] close persistence failed: {exception}");
            return;
        }

        if (!authoritativeDispositionPersisted)
            return;
        if (derivedProjectionFailed)
        {
            lock (Gate)
            {
                _closeProjectionPersistenceFailed = true;
                _runtimeState = "close_projection_persistence_failed";
                _detail = "A derived semantic projection failed after the authoritative disposition was persisted.";
                _closeout = _closeout with
                {
                    State = "closing",
                    Detail = "Close projection persistence failed after the authoritative disposition; the session remains auditable but open."
                };
            }
            return;
        }

        // Native callbacks are only discarded after every unresolved root has
        // a durable terminal disposition. The queue remains transport, not a
        // second source of truth.
        TerminateClosePendingWork();

        RecordingStoreSnapshot snapshot;
        lock (Gate)
        {
            if (_lifecycle.State != RecordingLifecycleState.Closing
                || HasPendingRecordingWorkUnsafe())
                return;
            AppendJournal("session_closed", null, _lastSnapshotId, "Session flushed and closed.");
            RecordingSessionStore? store = _store;
            store?.Dispose();
            snapshot = store?.GetSnapshot() ?? _lastStoreSnapshot;
            _lastStoreSnapshot = snapshot;
            _store = null;
            _sessionClosedAt = DateTimeOffset.UtcNow;
            _lifecycle = RecordingLifecycleStateMachine.MarkClosed(_lifecycle, _sessionClosedAt.Value);
            _closeout = new RecordingCloseoutStatus(
                "closed",
                _closeout.RequestedAt,
                _sessionClosedAt,
                "Session journal and evidence streams were flushed and closed.");
            _runtimeState = "recording_closed";
            _detail = _closeout.Detail;
            _stagedCardFrame = null;
            _requiredReadsHealth = "not_active";
            ResetNativeActionTrackingUnsafe();
        }
        PublishApplicationEvent(RecordingEventKind.SessionClosed, detail: _closeout.Detail);
    }

    private static void TerminateClosePendingWork()
    {
        lock (Gate)
        {
            if (_lifecycle.State != RecordingLifecycleState.Closing)
                return;
            Interlocked.Increment(ref _nativePostCommitGeneration);
            QueuedNativePostCommitCompletions.Clear();
            NativePostCommitCompletions.Reset();
            foreach (NativeActionLifecycleSubscription subscription in NativeActionSubscriptions.Values)
                subscription.Dispose();
            NativeActionSubscriptions.Clear();
            SemanticOnlyNativeActionIds.Clear();
            ArmedPotionUses.Clear();
        }
    }

    private static bool HasPendingRecordingWorkUnsafe() =>
        HasNativePendingRecordingWorkUnsafe() || BoundaryTracker.HasUnresolvedActions;

    private static bool HasNativePendingRecordingWorkUnsafe() =>
        SemanticOnlyNativeActionIds.Count > 0
        || NativePostCommitCompletions.Count > 0
        || QueuedNativePostCommitCompletions.Count > 0;

    private static void ResetNativeActionTrackingUnsafe()
    {
        Interlocked.Increment(ref _nativePostCommitGeneration);
        QueuedNativePostCommitCompletions.Clear();
        NativePostCommitCompletions.Reset();
        foreach (NativeActionLifecycleSubscription subscription in NativeActionSubscriptions.Values)
            subscription.Dispose();
        NativeActionSubscriptions.Clear();
        SemanticOnlyNativeActionIds.Clear();
        SemanticProjectionEnvironments.Clear();
        ArmedPotionUses.Clear();
        BoundaryTracker.Reset();
        NativeSemanticDiscriminatorRuntime.Reset();
        _nativeRunEndedObserved = false;
    }

    private static IReadOnlyList<string> LifecycleBlockers(RecordingLifecycleState state) =>
        state switch
        {
            RecordingLifecycleState.Ready => new[] { "no_open_recording_session" },
            RecordingLifecycleState.Paused => new[] { "recording_paused" },
            RecordingLifecycleState.Closing => new[] { "recording_closing" },
            RecordingLifecycleState.Closed => new[] { "recording_closed" },
            _ => Array.Empty<string>()
        };

    private static string RuntimeStatusForLifecycle(
        RecordingLifecycleState state,
        string fallback) =>
        state switch
        {
            RecordingLifecycleState.Ready => "ready",
            RecordingLifecycleState.Paused => "recording_paused",
            RecordingLifecycleState.Closing => "recording_closing",
            RecordingLifecycleState.Closed => "recording_closed",
            _ => fallback
        };

    private static bool AcceptingNewWitnesses()
    {
        lock (Gate)
            return _initialized && _lifecycle.State == RecordingLifecycleState.Recording;
    }

    /// <summary>
    /// Non-authorizing capture check for a new Human root. Acceptance and
    /// execution-order capture belong to the semantic tracker; an unresolved
    /// prior root must not prevent STS2 from accepting and recording another
    /// Human action. Failure never blocks STS2 input.
    /// </summary>
    private static bool CanOpenSemanticEvidenceWindow()
    {
        RecordingLifecycleState lifecycleState = GetRecordingLifecycle().State;
        return lifecycleState == RecordingLifecycleState.Recording
            || HumanActionScope.Current != null;
    }

    internal static void StageCardPlay(CardModel card)
    {
        if (!CanOpenSemanticEvidenceWindow())
        {
            lock (Gate)
                _stagedCardFrame = null;
            return;
        }
        try
        {
            ProcessLocalNativeWitnessFrame frame = CaptureReadRichFrame();
            RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
            var staged = EligibilityBlockers(frame, environment, requireReads: true).Count == 0
                ? new StagedCardFrame(
                    new ExactDecisionFrame(frame, environment),
                    card,
                    DateTimeOffset.UtcNow)
                : null;
            lock (Gate)
                _stagedCardFrame = staged;
        }
        catch
        {
            lock (Gate)
                _stagedCardFrame = null;
        }
    }

    internal static NativeUiScopeEntry TryEnterCardScope(CardModel card, Creature? target)
    {
        var arguments = new Dictionary<string, object>(StringComparer.Ordinal);
        ProcessLocalObservedAction observed;
        if (target != null)
            arguments["target"] = target;
        observed = new ProcessLocalObservedAction("play", card, arguments);
        return TryEnterScope(
            "native_card_play_ui",
            nameof(PlayCardAction),
            observed,
            card,
            semanticSelection: observed);
    }

    internal readonly record struct PotionUseArmHandle(
        PotionModel Potion,
        long Generation);

    internal static PotionUseArmHandle? ArmPotionUse(PotionModel? potion)
    {
        if (potion == null || !AcceptingNewWitnesses() || HumanActionScope.Current != null)
            return null;
        if (!CanOpenSemanticEvidenceWindow())
        {
            lock (Gate)
            {
                if (!_initialized
                    || _lifecycle.State != RecordingLifecycleState.Recording
                    || SessionId == null
                    || TimelineId == null)
                    return null;
                long generation = ++_potionArmGeneration;
                ArmedPotionUses[potion] = new ArmedPotionUse(
                    generation,
                    null,
                    SessionId,
                    TimelineId,
                    "semantic_causal_overlap",
                    "A prior Human root is not ready for an exact next-root handoff; potion use continues without a canonical transition claim.",
                    _lastSnapshotId,
                    "decision_and_lifecycle_only");
                return new PotionUseArmHandle(potion, generation);
            }
        }
        try
        {
            ProcessLocalNativeWitnessFrame frame = CaptureReadRichFrame();
            RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
            IReadOnlyList<string> blockers = EligibilityBlockers(
                frame,
                environment,
                requireReads: true);
            lock (Gate)
            {
                if (!_initialized
                    || _lifecycle.State != RecordingLifecycleState.Recording
                    || SessionId == null
                    || TimelineId == null)
                    return null;
                long generation = ++_potionArmGeneration;
                ArmedPotionUses[potion] = blockers.Count == 0
                    ? new ArmedPotionUse(
                        generation,
                        new ExactDecisionFrame(frame, environment),
                        SessionId,
                        TimelineId,
                        null,
                        null,
                        null,
                        null)
                    : new ArmedPotionUse(
                        generation,
                        null,
                        SessionId,
                        TimelineId,
                        "potion_pre_frame_capture_failed",
                        string.Join(",", blockers),
                        frame.Snapshot.SnapshotId,
                        "fail_closed");
                return new PotionUseArmHandle(potion, generation);
            }
        }
        catch (Exception exception)
        {
            lock (Gate)
            {
                if (!_initialized
                    || _lifecycle.State != RecordingLifecycleState.Recording
                    || SessionId == null
                    || TimelineId == null)
                    return null;
                long generation = ++_potionArmGeneration;
                ArmedPotionUses[potion] = new ArmedPotionUse(
                    generation,
                    null,
                    SessionId,
                    TimelineId,
                    "potion_pre_frame_capture_failed",
                    exception.Message,
                    null,
                    "implemented_runtime_error");
                return new PotionUseArmHandle(potion, generation);
            }
        }
    }

    internal static NativeUiScopeEntry TryEnterPotionUseScope(
        PotionModel potion,
        Creature? target)
    {
        ArmedPotionUse? armed;
        lock (Gate)
        {
            if (!ArmedPotionUses.Remove(potion, out armed)
                || !_initialized
                || _lifecycle.State != RecordingLifecycleState.Recording
                || armed.SessionId != SessionId
                || armed.TimelineId != TimelineId)
                return default;
        }

        if (armed.FailureReason != null)
        {
            HumanActionScope.EnterDeferredFailure(
                nameof(UsePotionAction),
                armed.FailureReason,
                armed.FailureDetail ?? "Potion use pre-frame capture was not authoritative.",
                armed.FailureSnapshotId,
                armed.FailureEvidenceLevel ?? "fail_closed");
            return new NativeUiScopeEntry(false, true);
        }

        if (armed.Decision == null)
        {
            HumanActionScope.EnterDeferredFailure(
                nameof(UsePotionAction),
                "potion_exact_mapping_failed",
                "The exact potion and target no longer match the BoundAction captured when Human potion use began.",
                null,
                "fail_closed");
            return new NativeUiScopeEntry(false, true);
        }

        ExactDecisionFrame decision = armed.Decision;
        ProcessLocalObservedAction observed = ObservedPotionUse(potion, target);
        ProcessLocalNativeMatch match = decision.Frame.Resolve(observed);
        if (!IsExact(match) && target == null && potion.Owner.Creature != null)
        {
            ProcessLocalObservedAction ownerTarget =
                ObservedPotionUse(potion, potion.Owner.Creature);
            ProcessLocalNativeMatch ownerMatch = decision.Frame.Resolve(ownerTarget);
            if (IsExact(ownerMatch))
            {
                observed = ownerTarget;
                match = ownerMatch;
            }
        }

        if (!IsExact(match))
        {
            HumanActionScope.EnterDeferredFailure(
                nameof(UsePotionAction),
                "potion_exact_mapping_failed",
                "The exact potion and target no longer match exactly one frozen BoundAction.",
                decision.Frame.Snapshot.SnapshotId,
                "fail_closed");
            return new NativeUiScopeEntry(false, true);
        }

        ProcessLocalNativeSemanticCapture semanticDecision =
            PlayerEnvironmentNativeSemanticWitness.Capture(
                "before_native_action_admission",
                uiFrame: decision.Frame,
                semanticNativeActionType: nameof(UsePotionAction),
                semanticSelection: observed);
        HumanActionScope.Enter(
            "native_potion_use_ui",
            nameof(UsePotionAction),
            observed,
            decision.Frame,
            semanticDecision);
        return new NativeUiScopeEntry(true, false);
    }

    private static ProcessLocalObservedAction ObservedPotionUse(
        PotionModel potion,
        Creature? target)
    {
        var arguments = new Dictionary<string, object>(StringComparer.Ordinal);
        if (target != null)
            arguments["target"] = target;
        return new ProcessLocalObservedAction("use", potion, arguments);
    }

    internal static void ClearPotionUseArm(PotionUseArmHandle? handle)
    {
        if (!handle.HasValue)
            return;
        PotionUseArmHandle value = handle.Value;
        lock (Gate)
        {
            if (ArmedPotionUses.TryGetValue(value.Potion, out ArmedPotionUse? armed)
                && armed.Generation == value.Generation)
                ArmedPotionUses.Remove(value.Potion);
        }
    }

    internal static NativeUiScopeEntry TryEnterGeneratedChoiceCardScope(
        NChooseACardSelectionScreen screen,
        NCardHolder holder) =>
        TryEnterScope(
            "native_generated_card_choice_ui",
            "NChooseACardSelectionScreen.SelectHolder",
            new ProcessLocalObservedAction(
                "select",
                holder.CardModel,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            occurrence: GeneratedChoiceOccurrence(
                "NChooseACardSelectionScreen.SelectHolder",
                "select",
                screen,
                holder.CardModel,
                holder));

    internal static NativeUiScopeEntry TryEnterGeneratedChoiceSkipScope(
        NChooseACardSelectionScreen screen) =>
        TryEnterScope(
            "native_generated_card_choice_skip_ui",
            "NChooseACardSelectionScreen.OnSkipButtonReleased",
            new ProcessLocalObservedAction(
                "skip",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            occurrence: GeneratedChoiceOccurrence(
                "NChooseACardSelectionScreen.OnSkipButtonReleased",
                "skip",
                screen));

    internal static void ObserveGeneratedChoiceCard(CardModel card) =>
        ObserveAcceptedUiAction(
            "NChooseACardSelectionScreen.SelectHolder",
            new ProcessLocalObservedAction(
                "select",
                card,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_generated_card_choice_ui",
                "NChooseACardSelectionScreen.SelectHolder",
                NativeWitnessIdentity.Get(card, "card"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));

    internal static void ObserveGeneratedChoiceSkip() =>
        ObserveAcceptedUiAction(
            "NChooseACardSelectionScreen.OnSkipButtonReleased",
            new ProcessLocalObservedAction(
                "skip",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_generated_card_choice_skip_ui",
                "NChooseACardSelectionScreen.OnSkipButtonReleased",
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));

    internal static NativeUiScopeEntry TryEnterScope(
        string origin,
        string expectedNativeActionType,
        ProcessLocalObservedAction? expectedAction = null,
        CardModel? stagedCard = null,
        ProcessLocalObservedAction? semanticSelection = null,
        HumanActionOccurrenceEvidence? occurrence = null)
    {
        if (!AcceptingNewWitnesses())
            return default;
        if (!CanOpenSemanticEvidenceWindow())
        {
            HumanActionScope.EnterDeferredFailure(
                expectedNativeActionType,
                "semantic_causal_overlap",
                "A prior Human root is not ready for an exact next-root handoff; native input continues without a canonical transition claim.",
                _lastSnapshotId,
                "decision_and_lifecycle_only",
                occurrence);
            return new NativeUiScopeEntry(false, true);
        }

        try
        {
            ProcessLocalNativeWitnessFrame? selected = null;
            ProcessLocalNativeWitnessFrame? current = null;
            List<string> currentBlockers = new();

            if (expectedAction != null && stagedCard != null)
            {
                StagedCardFrame? staged;
                lock (Gate)
                {
                    staged = _stagedCardFrame;
                    _stagedCardFrame = null;
                }
                if (staged != null
                    && ReferenceEquals(staged.Card, stagedCard)
                    && DateTimeOffset.UtcNow - staged.StagedAt <= TimeSpan.FromSeconds(30)
                    && IsExact(staged.Decision.Frame.Resolve(expectedAction)))
                {
                    selected = staged.Decision.Frame;
                }
            }

            if (selected == null)
            {
                current = CaptureReadRichFrame();
                RecorderEnvironmentIdentity currentEnvironment = BuildEnvironment(current);
                currentBlockers = EligibilityBlockers(
                    current,
                    currentEnvironment,
                    requireReads: true);
                if (currentBlockers.Count == 0
                    && (expectedAction == null || IsExact(current.Resolve(expectedAction))))
                {
                    selected = current;
                }
            }

            if (selected == null)
            {
                HumanActionScope.EnterDeferredFailure(
                    expectedNativeActionType,
                    "pre_frame_capture_failed",
                    string.Join(",", currentBlockers.Count == 0
                        ? new[] { "no_same_context_authoritative_frame" }
                        : currentBlockers.Append("no_same_context_authoritative_frame")),
                    current?.Snapshot.SnapshotId,
                    "fail_closed",
                    occurrence);
                return new NativeUiScopeEntry(false, true);
            }

            ProcessLocalNativeSemanticCapture? semanticDecision = semanticSelection == null
                ? null
                : PlayerEnvironmentNativeSemanticWitness.Capture(
                    "before_native_action_admission",
                    uiFrame: selected,
                    semanticNativeActionType: expectedNativeActionType,
                    semanticSelection: semanticSelection);
            lock (Gate)
            {
                if (!_initialized
                    || _lifecycle.State != RecordingLifecycleState.Recording)
                    return default;
                HumanActionScope.Enter(
                    origin,
                    expectedNativeActionType,
                    expectedAction,
                    selected,
                    semanticDecision,
                    occurrence: occurrence);
            }
            return new NativeUiScopeEntry(true, false);
        }
        catch (Exception exception)
        {
            HumanActionScope.EnterDeferredFailure(
                expectedNativeActionType,
                "pre_frame_capture_failed",
                exception.Message,
                null,
                "implemented_runtime_error",
                occurrence);
            return new NativeUiScopeEntry(false, true);
        }
    }

    internal static NativeUiScopeEntry TryEnterSemanticScope(
        string origin,
        string nativeActionType,
        ProcessLocalObservedAction observed,
        NativePostCommitCompletionExpectation? completionExpectation = null,
        ProcessLocalObservedAction? nativeSemanticSelection = null)
    {
        if (!AcceptingNewWitnesses())
            return default;
        if (HumanActionScope.Current != null)
        {
            HumanActionScope.EnterDeferredFailure(
                nativeActionType,
                "semantic_causal_overlap",
                "A nested native UI callback was accepted while another Human root was staged; it cannot claim that root.",
                _lastSnapshotId,
                "failed_closed");
            return new NativeUiScopeEntry(false, true);
        }
        if (!CanOpenSemanticEvidenceWindow())
        {
            HumanActionScope.EnterDeferredFailure(
                nativeActionType,
                "semantic_causal_overlap",
                "A prior Human root is not ready for an exact next-root handoff; native input continues without a canonical transition claim.",
                _lastSnapshotId,
                "decision_and_lifecycle_only");
            return new NativeUiScopeEntry(false, true);
        }
        try
        {
            ProcessLocalNativeWitnessFrame frame = CaptureSemanticFrame();
            ProcessLocalNativeSemanticCapture semanticDecision =
                PlayerEnvironmentNativeSemanticWitness.Capture(
                    "before_native_action_admission",
                    uiFrame: frame,
                    semanticNativeActionType: nativeActionType,
                    semanticSelection: nativeSemanticSelection ?? observed);
            RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
            IReadOnlyList<string> blockers = SemanticWitnessBlockers(frame, environment);
            ProcessLocalNativeMatch match = frame.Resolve(observed);
            if (blockers.Count > 0 || !IsExact(match))
            {
                HumanActionScope.EnterDeferredFailure(
                    nativeActionType,
                    "semantic_pre_frame_capture_failed",
                    string.Join(",", blockers.Concat(new[] { match.Status }).Distinct(StringComparer.Ordinal)),
                    frame.Snapshot.SnapshotId,
                    "fail_closed");
                return new NativeUiScopeEntry(false, true);
            }
            lock (Gate)
            {
                if (!_initialized || _lifecycle.State != RecordingLifecycleState.Recording)
                    return default;
                string? actionWitnessId = completionExpectation == null
                    ? null
                    : $"ui-root-{Guid.NewGuid():N}";
                if (completionExpectation != null)
                {
                    if (SessionId is not { } sessionId)
                    {
                        HumanActionScope.EnterDeferredFailure(
                            nativeActionType,
                            "native_completion_ledger_unavailable",
                            "The recording session identity is unavailable; the UI input continues without strict transition evidence.",
                            frame.Snapshot.SnapshotId,
                            "decision_and_lifecycle_only");
                        return new NativeUiScopeEntry(false, true);
                    }
                    bool registered = NativePostCommitCompletions.Register(
                        new NativePostCommitCompletionRegistration(
                            sessionId,
                            Interlocked.Read(ref _nativePostCommitGeneration),
                            actionWitnessId!,
                            completionExpectation));
                    if (!registered)
                    {
                        HumanActionScope.EnterDeferredFailure(
                            nativeActionType,
                            "native_completion_ledger_unavailable",
                            "The exact native completion could not be staged; the UI input continues without strict transition evidence.",
                            frame.Snapshot.SnapshotId,
                            "decision_and_lifecycle_only");
                        return new NativeUiScopeEntry(false, true);
                    }
                }
                HumanActionScope.Enter(
                    origin,
                    nativeActionType,
                    observed,
                    frame,
                    semanticDecision,
                    actionWitnessId,
                    completionExpectation);
                return new NativeUiScopeEntry(true, false, actionWitnessId);
            }
        }
        catch (Exception exception)
        {
            HumanActionScope.EnterDeferredFailure(
                nativeActionType,
                "semantic_pre_frame_capture_failed",
                exception.Message,
                null,
                "implemented_runtime_error");
            return new NativeUiScopeEntry(false, true);
        }
    }

    internal static void ExitNativeUiScope(NativeUiScopeEntry entry)
    {
        if (entry.Entered)
        {
            HumanActionContext? context = HumanActionScope.Current;
            if (context?.CompletionExpectation != null
                && !context.RootActionClaimed
                && entry.ActionWitnessId is { } actionWitnessId)
            {
                lock (Gate)
                    NativePostCommitCompletions.Remove(actionWitnessId);
            }
            HumanActionScope.Exit();
        }
        if (entry.DeferredFailure)
            HumanActionScope.ExitDeferredFailure();
    }

    private static bool IsExact(ProcessLocalNativeMatch match) =>
        string.Equals(match.Status, "exact_unique", StringComparison.Ordinal)
        && match.MatchCount == 1
        && match.BoundAction != null;

    private static ProcessLocalNativeMatch? ResolveAcceptedUiMatch(
        HumanActionContext? context,
        ProcessLocalObservedAction observed)
    {
        if (context == null)
            return null;
        try
        {
            return context.Frame.Resolve(observed);
        }
        catch
        {
            // The accepted observer will claim this root as a mapping
            // failure, producing one failed-closed occurrence instead of
            // allowing a resolve exception to become a repeated drop.
            return null;
        }
    }

    internal static void ObserveAcceptedAction(GameAction action)
    {
        // Some native UI callbacks enqueue a known child action inside the
        // UI method whose accepted occurrence is already being staged. The
        // exact object binding is installed in RequestEnqueue's Prefix, before
        // GameAction.OnEnqueued fires. Do not let the generic observer create
        // a second disposition or a NativeTypeMismatch for that same root.
        if (NativeUiCompletionRootBindings.Contains(action))
            return;

        HumanActionContext? context = HumanActionScope.Current;
        string nativeActionType = action.GetType().Name;
        try
        {
            ProcessLocalObservedAction? observed = null;
            NativeWitnessEvidence? witness = null;
            string? failureReason = null;
            string? failureDetail = null;
            string? failureEvidence = null;
            bool hasMapping = context != null
                && TryDescribeAction(
                    action,
                    context,
                    out observed,
                    out witness,
                    out failureReason,
                    out failureDetail,
                    out failureEvidence);
            ProcessLocalNativeMatch? resolvedMatch = null;
            if (hasMapping)
            {
                try
                {
                    resolvedMatch = context!.Frame.Resolve(observed!);
                }
                catch (Exception exception)
                {
                    hasMapping = false;
                    failureReason = "native_action_exact_mapping_failed";
                    failureDetail = exception.Message;
                    failureEvidence = "failed_closed";
                }
            }
            AcceptedDecisionObserver.Outcome outcome = AcceptedDecisionObserver.Observe(
                nativeActionType,
                context,
                resolvedMatch,
                hasMapping);

            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.DeferredFailure)
            {
                TryQuarantineDeferredAcceptedAction(
                    nativeActionType,
                    observed,
                    witness,
                    occurrence: context == null
                        ? GameActionOccurrence(action)
                        : null);
                return;
            }
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.NativeTypeMismatch)
            {
                // A staged Human root with the wrong accepted native type is
                // an owned failed-closed ingress, not an unowned callback.
                // The observer has claimed the rejection bit, so a duplicate
                // callback will take the Duplicate branch below.
                Quarantine(
                    "human_action_native_type_mismatch",
                    "An accepted native GameAction did not match the staged native action type.",
                    context?.Frame.Snapshot.SnapshotId,
                    nativeActionType,
                    "failed_closed",
                    context?.Occurrence ?? OccurrenceFrom(nativeActionType, context!, observed, witness));
                return;
            }
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.NoScope
                || outcome.Kind == AcceptedDecisionObserver.OutcomeKind.Duplicate)
            {
                // GameAction.OnEnqueued is also raised for native/internal
                // actions that were never staged as Human input. They remain
                // deliberately unowned and must not become phantom roots.
                TryQuarantineDeferredAcceptedAction(
                    action.GetType().Name,
                    occurrence: new HumanActionOccurrenceEvidence(
                        $"human-occurrence-{Guid.NewGuid():N}",
                        action.GetType().Name,
                        SupportedFamilyForNativeAction(action.GetType().Name) ?? action.GetType().Name,
                        action.GetType().Name,
                        NativeWitnessIdentity.Get(action, "game_action"),
                        new Dictionary<string, string>(StringComparer.Ordinal),
                        null,
                        null,
                        null,
                        null,
                        "GameAction.accepted",
                        "failed_closed"));
                return;
            }
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.MappingFailure)
            {
                Quarantine(
                    failureReason ?? "native_action_exact_mapping_failed",
                    failureDetail ?? "The accepted native GameAction did not retain an exact Human mapping.",
                    context?.Frame.Snapshot.SnapshotId,
                    nativeActionType,
                    failureEvidence ?? "failed_closed",
                    context == null
                        ? GameActionOccurrence(action)
                        : OccurrenceFrom(nativeActionType, context, observed, witness));
                return;
            }
            if (outcome.Kind != AcceptedDecisionObserver.OutcomeKind.Accepted
                || outcome.Context is not { } acceptedContext
                || outcome.Match is not { } acceptedMatch
                || witness is not { } acceptedWitness)
                return;
            NativeSemanticDiscriminatorRuntime.Observe(
                _store,
                SessionId,
                TimelineId,
                _currentRunId,
                NativeActionLifecycleKinds.Accepted,
                action,
                acceptedContext.Frame);
            StartSemanticNativeAction(acceptedContext, acceptedWitness, acceptedMatch, action);
        }
        catch (Exception exception)
        {
            Quarantine(
                "native_action_observation_failed",
                exception.Message,
                context?.Frame.Snapshot.SnapshotId ?? _lastSnapshotId,
                action.GetType().FullName,
                "implemented_runtime_error",
                context == null ? GameActionOccurrence(action) : OccurrenceFrom(nativeActionType, context, null, null));
        }
    }

    internal static void ObservePlayCardExecutionAborted(PlayCardAction action)
    {
        string actionWitnessId = NativeWitnessIdentity.Get(action, "game_action");
        NativeSemanticDiscriminatorRuntime.Observe(
            _store,
            SessionId,
            TimelineId,
            _currentRunId,
            "aborted_before_commit",
            action,
            capture: false,
            detail: "STS2 removed the queued card before PlayCardAction could Commit.");

        if (!_semanticBoundaryTraceHealthy)
            return;
        try
        {
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
            {
                if (!BoundaryTracker.Contains(actionWitnessId))
                    return;
                drafts = BoundaryTracker.AbortedBeforeCommit(actionWitnessId);
            }
            PersistSemanticBoundaryDrafts(drafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    internal static void ObserveAcceptedUiAction(
        string nativeActionType,
        ProcessLocalObservedAction observed,
        NativeWitnessEvidence witness) =>
        ObserveAcceptedSemanticUiAction(nativeActionType, observed, witness);

    internal static bool ObserveAcceptedSemanticUiAction(
        string nativeActionType,
        ProcessLocalObservedAction observed,
        NativeWitnessEvidence witness,
        bool captureImmediatePostCommitBoundary = true,
        string? actionWitnessId = null)
    {
        HumanActionContext? context = HumanActionScope.Current;
        try
        {
            AcceptedDecisionObserver.Outcome outcome = AcceptedDecisionObserver.Observe(
                nativeActionType,
                context,
                ResolveAcceptedUiMatch(context, observed),
                context != null);
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.DeferredFailure)
            {
                TryQuarantineDeferredAcceptedAction(nativeActionType, observed, witness);
                return false;
            }
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.NoScope)
            {
                Quarantine(
                    "human_action_accepted_without_scope",
                    "An accepted native UI mutation had no staged Human witness.",
                    _lastSnapshotId,
                    nativeActionType,
                    "failed_closed",
                    OccurrenceFrom(nativeActionType, observed, witness));
                return false;
            }
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.NativeTypeMismatch)
            {
                Quarantine(
                    "human_action_native_type_mismatch",
                    "An accepted native UI mutation did not match the staged native action type.",
                    context?.Frame.Snapshot.SnapshotId,
                    nativeActionType,
                    "failed_closed",
                    context?.Occurrence ?? OccurrenceFrom(nativeActionType, observed, witness));
                return false;
            }
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.Duplicate)
                return false;
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.MappingFailure)
            {
                Quarantine(
                    "human_action_exact_mapping_failed",
                    "The accepted native mutation did not retain its exact pre-action BoundAction mapping.",
                    context?.Frame.Snapshot.SnapshotId,
                    nativeActionType,
                    "failed_closed",
                    context?.Occurrence ?? OccurrenceFrom(nativeActionType, observed, witness));
                return false;
            }
            if (outcome.Kind != AcceptedDecisionObserver.OutcomeKind.Accepted
                || outcome.Context is not { } acceptedContext
                || outcome.Match is not { } match)
                return false;
            ProcessLocalNativeWitnessFrame? postCommitFrame = null;
            if (captureImmediatePostCommitBoundary)
            {
                try
                {
                    postCommitFrame = CaptureSemanticFrame();
                }
                catch
                {
                    // The Human root remains accounted. No later state is
                    // backfilled as a successor when this observation fails.
                }
            }
            return StartSemanticUiAction(
                acceptedContext.Frame,
                acceptedContext.NativeSemanticDecision,
                BuildEnvironment(acceptedContext.Frame),
                nativeActionType,
                witness,
                match,
                postCommitFrame,
                acceptedContext.CompletionExpectation,
                actionWitnessId ?? (acceptedContext.CompletionExpectation == null
                    ? null
                    : acceptedContext.ActionWitnessId),
                actionWitnessId == null
                    ? null
                    : NativeUiCompletionRootBindings.TryGetAction(actionWitnessId,
                        out GameAction? boundAction)
                        ? boundAction
                        : null);
        }
        catch (Exception exception)
        {
            Quarantine(
                "accepted_ui_observation_failed",
                exception.Message,
                context?.Frame.Snapshot.SnapshotId ?? _lastSnapshotId,
                nativeActionType,
                "implemented_runtime_error",
                context?.Occurrence ?? OccurrenceFrom(nativeActionType, observed, witness));
            DisableSemanticBoundaryTrace(exception);
            return false;
        }
    }

    /// <summary>
    /// Persists exactly one failed-closed occurrence for an accepted UI root
    /// whose native carrier could not be proven. This is intentionally kept
    /// separate from the normal accepted path: the root gate is claimed once,
    /// but no semantic action or guessed carrier is created.
    /// </summary>
    internal static void ObserveAcceptedSemanticUiFailure(
        string nativeActionType,
        ProcessLocalObservedAction observed,
        NativeWitnessEvidence witness,
        string reason,
        string detail)
    {
        HumanActionContext? context = HumanActionScope.Current;
        try
        {
            AcceptedDecisionObserver.Outcome outcome = AcceptedDecisionObserver.Observe(
                nativeActionType,
                context,
                match: null,
                hasMapping: false);
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.DeferredFailure)
            {
                TryQuarantineDeferredAcceptedAction(nativeActionType, observed, witness);
                return;
            }
            if (outcome.Kind == AcceptedDecisionObserver.OutcomeKind.Duplicate)
                return;

            Quarantine(
                reason,
                detail,
                context?.Frame.Snapshot.SnapshotId ?? _lastSnapshotId,
                nativeActionType,
                "decision_and_lifecycle_only",
                context?.Occurrence ?? OccurrenceFrom(nativeActionType, observed, witness));
        }
        catch (Exception exception)
        {
            // Quarantine itself is best-effort and must not throw into a
            // native callback. Keep the implementation error explicit.
            NativeUiObservationSafety.Report(
                "accepted_ui_failure",
                $"{reason}: {exception.Message}");
        }
        finally
        {
            // This root is durably invalidated rather than awaiting a native
            // completion. Do not leave its in-memory completion expectation
            // behind after the exactly-once failed-closed disposition.
            if (context?.CompletionExpectation != null)
            {
                lock (Gate)
                    NativePostCommitCompletions.Remove(context.ActionWitnessId);
            }
        }
    }

    /// <summary>
    /// Keeps a native completion-carrier failure auditable without inventing a
    /// terminal disposition. The exact Human root remains pending until a
    /// real native lifecycle or closeout disposition proves its outcome.
    /// </summary>
    internal static void ObserveSemanticUiNativeCommitBindingFailure(
        string? actionWitnessId,
        string family,
        string kind,
        string detail)
    {
        try
        {
            lock (Gate)
            {
                if (_store == null)
                    return;
                AppendJournal(
                    "native_commit_binding_pending",
                    actionWitnessId,
                    _lastSnapshotId,
                    $"root={(actionWitnessId ?? "unavailable")};family={family};kind={kind};{detail}");
                _runtimeState = "native_commit_binding_pending";
                _detail = detail;
            }
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report(
                "native_commit_binding_pending",
                exception);
        }
    }

    private static void TryQuarantineDeferredAcceptedAction(
        string nativeActionType,
        ProcessLocalObservedAction? observed = null,
        NativeWitnessEvidence? witness = null,
        HumanActionOccurrenceEvidence? occurrence = null)
    {
        DeferredHumanActionFailure? failure = HumanActionScope.CurrentDeferredFailure;
        if (failure == null || !failure.TryClaim(nativeActionType))
            return;
        Quarantine(
            failure.ReasonCode,
            failure.Detail,
            failure.SnapshotId,
            nativeActionType,
            failure.EvidenceLevel,
            failure.Occurrence ?? occurrence ?? (observed != null && witness != null
                ? OccurrenceFrom(nativeActionType, observed, witness)
                : null));
    }

    internal static void OnProcessFrame()
    {
        try
        {
            if (_store != null)
                UpdateRunLifecycle();
            EnsureActionExecutorObservation();
            NativeTaskCompletion[] queuedPostCommitCompletions;
            lock (Gate)
            {
                queuedPostCommitCompletions = QueuedNativePostCommitCompletions.ToArray();
                QueuedNativePostCommitCompletions.Clear();
            }
            foreach (NativeTaskCompletion completion in queuedPostCommitCompletions)
                ObserveNativePostCommitCompletion(completion);

            FinalizeClose();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            RecorderFrameWorkPlan plan = RecorderFrameWorkPlanner.Plan(
                now,
                _lastIdleStatusAt,
                _statusRefreshRequested);
            if (!plan.HasWork)
                return;

            _lastIdleStatusAt = now;
            _statusRefreshRequested = false;
            ProcessLocalNativeWitnessFrame frame = MeasureStore(
                "idle_status_refresh",
                CaptureReadRichFrame);
            RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
            List<string> blockers = EligibilityBlockers(frame, environment, requireReads: false);
            if (blockers.Any(blocker => !string.Equals(
                         blocker,
                         "pre_frame_not_complete_interactive",
                         StringComparison.Ordinal)))
            {
                lock (Gate)
                    _stagedCardFrame = null;
            }

            blockers = EligibilityBlockers(frame, environment, requireReads: true);
            bool semanticBoundaryPending;
            lock (Gate)
            {
                semanticBoundaryPending = BoundaryTracker.HasUnresolvedActions
                    && !BoundaryTracker.CanOpenNextRoot;
            }
            if (semanticBoundaryPending)
                blockers.Add("semantic_successor_boundary_pending");
            _requiredReadsHealth = HasRequiredReads(frame) ? "healthy" : "unavailable";
            RecordingLifecycleSnapshot lifecycle = GetRecordingLifecycle();
            string status = lifecycle.State switch
            {
                RecordingLifecycleState.Ready => "ready",
                RecordingLifecycleState.Paused => "recording_paused",
                RecordingLifecycleState.Closing => "recording_closing",
                RecordingLifecycleState.Closed => "recording_closed",
                _ when semanticBoundaryPending => "awaiting_semantic_successor_boundary",
                _ => blockers.Count == 0
                    ? "ready_for_human_action"
                    : "fail_closed"
            };
            _runtimeState = status;
            if (lifecycle.State == RecordingLifecycleState.Recording && !semanticBoundaryPending)
                _detail = null;
            WriteStatus(
                environment,
                frame.Snapshot.SnapshotId,
                blockers.Concat(LifecycleBlockers(lifecycle.State)).Distinct(StringComparer.Ordinal).ToArray());
        }
        catch (Exception exception)
        {
            _runtimeState = "observer_error";
            _detail = exception.Message;
            WriteStatus(null, null, new[] { "player_environment_capture_failed" });
        }
    }

    private static void EnsureActionExecutorObservation()
    {
        ActionExecutor? current = null;
        try
        {
            if (RunManager.Instance.IsInProgress)
                current = RunManager.Instance.ActionExecutor;
        }
        catch
        {
            // RunManager is not mounted during menus and run transitions.
        }
        if (ReferenceEquals(current, _observedActionExecutor))
            return;
        if (_observedActionExecutor != null)
            _observedActionExecutor.BeforeActionExecuted -= ObserveBeforeActionExecution;
        _observedActionExecutor = current;
        if (_observedActionExecutor != null)
            _observedActionExecutor.BeforeActionExecuted += ObserveBeforeActionExecution;
    }

    private static void ObserveBeforeActionExecution(GameAction action)
    {
        string actionWitnessId = NativeWitnessIdentity.Get(action, "game_action");
        string phase = action.State.ToString() == "ReadyToResumeExecuting"
            ? "before_execution_resume"
            : "before_execution";

        // ActionExecutor raises BeforeActionExecuted for every queue pass,
        // including a resumed PlayerChoice parent. GameAction.BeforeExecuted
        // is first-execution-only, and STS2 resumes the same GameAction object;
        // this callback is lifecycle evidence, not a new semantic pre-state or
        // a successor boundary. Keep the exact parent root open until its
        // native Finished/Commit and a later causal boundary are observed.
        if (phase == "before_execution_resume")
        {
            NativeSemanticDiscriminatorRuntime.Observe(
                _store,
                SessionId,
                TimelineId,
                _currentRunId,
                phase,
                action,
                capture: false,
                detail: NativeSemanticDiscriminatorContract.PlayerChoiceResumeDetail);
            try
            {
                lock (Gate)
                {
                    if (_semanticBoundaryTraceHealthy
                        && BoundaryTracker.Contains(actionWitnessId))
                    {
                        BoundaryTracker.BeforeExecutionResume(actionWitnessId);
                    }
                }
            }
            catch (Exception exception)
            {
                DisableSemanticBoundaryTrace(exception);
            }
            return;
        }

        bool canonicalBoundaryWillCapture;
        lock (Gate)
        {
            canonicalBoundaryWillCapture = _semanticBoundaryTraceHealthy
                && BoundaryTracker.Contains(actionWitnessId);
        }
        if (!canonicalBoundaryWillCapture)
        {
            NativeSemanticDiscriminatorRuntime.Observe(
                _store,
                SessionId,
                TimelineId,
                _currentRunId,
                phase,
                action);
            return;
        }
        if (!_semanticBoundaryTraceHealthy || _store == null)
            return;
        lock (Gate)
        {
            if (!BoundaryTracker.Contains(actionWitnessId))
                return;
        }

        try
        {
            ProcessLocalNativeWitnessFrame frame = CaptureSemanticFrame();
            ProcessLocalNativeSemanticCapture semanticCapture =
                PlayerEnvironmentNativeSemanticWitness.Capture(phase, action, frame);
            NativeActionLifecycleSubscription? subscription;
            lock (Gate)
                NativeActionSubscriptions.TryGetValue(action, out subscription);
            ExecutionSemanticActionSpaceEvidence? actionSpace =
                subscription?.NativeSemanticDecision;
            actionSpace ??= phase == "before_execution"
                ? ToExecutionSemanticActionSpace(
                    actionWitnessId,
                    semanticCapture,
                    subscription?.HumanBoundActionId)
                : null;
            NativeSemanticDiscriminatorRuntime.Observe(
                _store,
                SessionId,
                TimelineId,
                _currentRunId,
                phase,
                action,
                capture: false,
                detail: NativeSemanticDiscriminatorContract.CanonicalBoundaryCaptureDelegatedDetail,
                capturedValue: semanticCapture);
            SemanticBoundaryObservation boundary = CreateSemanticBoundaryObservation(
                frame,
                SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
                actionWitnessId,
                executionSemanticActionSpace: actionSpace);
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
                drafts = BoundaryTracker.ObserveBeforeActionExecution(actionWitnessId, boundary);
            PersistSemanticBoundaryDrafts(drafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static void ObserveSemanticDecisionBoundary(
        ProcessLocalNativeWitnessFrame frame,
        string witnessKind,
        CurrentDecisionFrame? completeState = null,
        NativeDecisionOwnerReadyEvidence? nativeDecisionOwnerReady = null)
    {
        if (!_semanticBoundaryTraceHealthy)
            return;
        lock (Gate)
        {
            if (!BoundaryTracker.HasUnresolvedActions)
                return;
        }
        if (!string.Equals(frame.Snapshot.Status, "interactive", StringComparison.Ordinal)
            || !string.Equals(
                frame.Snapshot.BoundActions.Status,
                "complete",
                StringComparison.Ordinal))
        {
            return;
        }
        SemanticBoundaryObservation boundary = CreateSemanticBoundaryObservation(
            frame,
            witnessKind,
            null,
            completeState,
            nativeDecisionOwnerReady);
        if (!boundary.IsCompleteDecisionBoundary)
            return;
        IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
        lock (Gate)
            drafts = BoundaryTracker.ObserveDecisionBoundary(boundary);
        PersistSemanticBoundaryDrafts(drafts);
    }

    private static SemanticBoundaryObservation CreateSemanticBoundaryObservation(
        ProcessLocalNativeWitnessFrame frame,
        string witnessKind,
        string? immediatelyConsumedByActionWitnessId,
        CurrentDecisionFrame? completeState = null,
        NativeDecisionOwnerReadyEvidence? nativeDecisionOwnerReady = null,
        ExecutionSemanticActionSpaceEvidence? executionSemanticActionSpace = null)
    {
        RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
        IReadOnlyList<string> stateBlockers = SemanticStateBlockers(frame, environment);
        bool stateComplete = stateBlockers.Count == 0;
        PlayerEnvironmentSnapshot snapshot = frame.Snapshot;
        return new SemanticBoundaryObservation(
            witnessKind,
            DateTimeOffset.UtcNow,
            snapshot.SnapshotId,
            snapshot.Status,
            snapshot.BoundActions.Status,
            snapshot.Interaction.InteractionId,
            snapshot.Interaction.Kind,
            stateComplete ? completeState ?? FreezeSemanticBoundary(frame, environment) : null,
            immediatelyConsumedByActionWitnessId)
        {
            NativeDecisionOwnerReady = nativeDecisionOwnerReady,
            ExecutionSemanticActionSpace = executionSemanticActionSpace,
            StateCompleteness = stateComplete ? "complete" : "partial",
            RequiredReadsStatus = HasSemanticRequiredReads(frame) ? "complete" : "unavailable",
            StateBlockers = stateBlockers
        };
    }

    private static ExecutionSemanticActionSpaceEvidence? ToExecutionSemanticActionSpace(
        string actionWitnessId,
        ProcessLocalNativeSemanticCapture capture,
        string? humanBoundActionId)
    {
        if (capture.SemanticState == null
            || string.IsNullOrWhiteSpace(capture.SemanticStateDigest)
            || capture.ObservedAction is not
            {
                Key.Length: > 0,
                Membership: "exact_once",
                SemanticMatchCount: 1
            })
            return null;
        return new ExecutionSemanticActionSpaceEvidence(
            ExecutionSemanticActionSpaceContract.SchemaVersion,
            ExecutionSemanticActionSpaceContract.Schema,
            actionWitnessId,
            capture.Phase,
            capture.Status,
            capture.Scope,
            capture.SemanticStateDigest,
            capture.SemanticState.DeepClone(),
            capture.SemanticCatalogDigest,
            capture.SemanticActions.Select(candidate => new ExecutionSemanticAction(
                candidate.Key,
                candidate.Verb,
                candidate.SubjectReferentId,
                new Dictionary<string, string>(candidate.Arguments, StringComparer.Ordinal),
                candidate.NativeLegalityBasis)).ToArray(),
            capture.ObservedAction.Key,
            capture.ObservedAction.Membership,
            capture.ObservedAction.SemanticMatchCount,
            capture.Evidence.ToArray(),
            capture.NonClaims.ToArray(),
            capture.Detail)
        {
            HumanBoundActionId = humanBoundActionId
        };
    }

    private static void ObserveNativeDecisionOwnerReady(
        NativeDecisionOwnerReadyObservation observation)
    {
        if (!_semanticBoundaryTraceHealthy || _store == null)
            return;
        lock (Gate)
        {
            if (!BoundaryTracker.NeedsBoundaryObservation)
                return;
        }

        try
        {
            ProcessLocalNativeWitnessFrame frame = CaptureSemanticFrame();
            if (!string.Equals(
                    frame.Snapshot.Interaction.Kind,
                    observation.Domain,
                    StringComparison.Ordinal))
            {
                return;
            }

            ObserveSemanticDecisionBoundary(
                frame,
                SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady,
                nativeDecisionOwnerReady: new NativeDecisionOwnerReadyEvidence(
                    observation.Domain,
                    NativeWitnessIdentity.Get(observation.NativeOwner, "decision_owner"),
                    observation.NativeOwnerType,
                    observation.NativeMechanism));
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static CurrentDecisionFrame FreezeSemanticBoundary(
        ProcessLocalNativeWitnessFrame frame,
        RecorderEnvironmentIdentity environment,
        string readPhase = "semantic")
    {
        PlayerEnvironmentSnapshot snapshot = frame.Snapshot;
        return MeasureStore(
            "freeze_semantic_boundary",
            () => new CurrentDecisionFrame(
                snapshot.SnapshotId,
                snapshot.Interaction.InteractionId,
                snapshot.Interaction.Kind,
                snapshot.Interaction.ContentSchema,
                EvidenceIdentity.Sha256Json(snapshot.BoundActions),
                snapshot.BoundActions.Actions.Count,
                ToNode(snapshot),
                PersistReads(frame, readPhase, environment)));
    }

    private static IReadOnlyList<string> SemanticStateBlockers(
        ProcessLocalNativeWitnessFrame frame,
        RecorderEnvironmentIdentity environment)
    {
        var blockers = new List<string>();
        if (!RunManager.Instance.IsInProgress
            || RunManager.Instance.NetService?.Type != NetGameType.Singleplayer)
            blockers.Add("not_singleplayer_run");
        if (frame.ExternalControllerActive)
            blockers.Add("external_controller_active");
        blockers.AddRange(frame.Snapshot.Completeness.Missing.Where(
            value => !string.Equals(
                value,
                "finite_bound_action_projection_incomplete",
                StringComparison.Ordinal)));
        if (!RecordingEnvironmentAdmission.IsExactModset(environment.ModsetStatus))
            blockers.Add("exact_recording_modset_missing");
        if (!IsCommit(environment.Connector.SourceRevision)
            || !IsCommit(environment.Annotator.SourceRevision))
            blockers.Add("source_revision_not_exact");
        if (!HasSemanticRequiredReads(frame))
            blockers.Add("required_read_evidence_unavailable");
        return blockers.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool StartSemanticUiAction(
        ProcessLocalNativeWitnessFrame frame,
        ProcessLocalNativeSemanticCapture? nativeSemanticDecision,
        RecorderEnvironmentIdentity environment,
        string nativeActionType,
        NativeWitnessEvidence witness,
        ProcessLocalNativeMatch match,
        ProcessLocalNativeWitnessFrame? postCommitFrame = null,
        NativePostCommitCompletionExpectation? completionExpectation = null,
        string? actionWitnessIdOverride = null,
        GameAction? lifecycleAction = null)
    {
        bool started = false;
        try
        {
            if (!_semanticBoundaryTraceHealthy || _store == null)
                return false;
            IReadOnlyList<string> blockers = SemanticWitnessBlockers(frame, environment);
            if (blockers.Count > 0 || !IsExact(match))
            {
                Quarantine(
                    "semantic_action_not_eligible",
                    string.Join(",", blockers.Concat(new[] { match.Status }).Distinct(StringComparer.Ordinal)),
                    frame.Snapshot.SnapshotId,
                    nativeActionType,
                    "fail_closed");
                return false;
            }

            CurrentDecisionFrame humanObservation = FreezeSemanticBoundary(frame, environment);
            long sequence = Interlocked.Increment(ref _sequence);
            string recordId = $"semantic-record-{sequence:D8}-{Guid.NewGuid():N}";
            string actionWitnessId = actionWitnessIdOverride ?? $"ui-action-{recordId}";
            SemanticActionReference action = CreateSemanticActionReference(
            actionWitnessId,
            sequence,
            recordId,
            nativeActionType,
            null,
            humanObservation,
            "direct_ui_commit",
            witness,
            match) with
        {
            RequiresNativePostCommit = completionExpectation != null
        };
            SemanticBoundaryObservation executionBoundary = CreateSemanticBoundaryObservation(
            frame,
            SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
            actionWitnessId,
            humanObservation,
            executionSemanticActionSpace: nativeSemanticDecision == null
                ? null
                : ToExecutionSemanticActionSpace(
                    actionWitnessId,
                    nativeSemanticDecision,
                    match.BoundAction!.BoundActionId));
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
            {
            SemanticProjectionEnvironments[actionWitnessId] = environment;
            var result = new List<SemanticBoundaryTraceDraft>();
            result.AddRange(BoundaryTracker.Accept(action, humanObservation));
            result.AddRange(BoundaryTracker.ObserveBeforeActionExecution(
                actionWitnessId,
                executionBoundary));
            result.AddRange(BoundaryTracker.Started(actionWitnessId));
            if (completionExpectation == null)
                result.AddRange(BoundaryTracker.Finished(actionWitnessId));
            if (lifecycleAction != null)
            {
                NativeActionLifecycleSubscription subscription =
                    new(
                        lifecycleAction,
                        actionWitnessId,
                        sequence,
                        recordId,
                        match.BoundAction!.BoundActionId,
                        nativeSemanticDecision == null
                            ? null
                            : ToExecutionSemanticActionSpace(
                                actionWitnessId,
                                nativeSemanticDecision,
                                match.BoundAction.BoundActionId),
                        ObserveSemanticOnlyNativeActionLifecycle,
                        finishIsNativeCommit: completionExpectation == null);
                NativeActionSubscriptions[lifecycleAction] = subscription;
                SemanticOnlyNativeActionIds.Add(actionWitnessId);
            }
                drafts = result;
            }
            started = true;
            PersistSemanticBoundaryDrafts(drafts);
            if (postCommitFrame != null)
            {
                try
                {
                    ObserveSemanticDecisionBoundary(
                        postCommitFrame,
                        SemanticBoundaryWitnessKinds.NativeUiPostCommit);
                }
                catch (Exception exception)
                {
                    DisableSemanticBoundaryTrace(exception);
                }
            }
            GameAction? parent = NativePlayerChoiceLineage.Capture().ParentAction;
            NativeSemanticDiscriminatorRuntime.ObserveDirectCommit(
                _store,
                SessionId,
                TimelineId,
                _currentRunId,
                actionWitnessId,
                nativeActionType,
                frame,
                parent == null ? null : NativeWitnessIdentity.Get(parent, "game_action"));
            AppendJournal(
                "semantic_human_action_accepted",
                recordId,
                humanObservation.SnapshotId,
                $"{nativeActionType}:{actionWitnessId}");
            return true;
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report("semantic_ui_action.start", exception);
            return false;
        }
        finally
        {
            if (!started)
                CleanupUnstartedSemanticUiAction(actionWitnessIdOverride, lifecycleAction);
        }
    }

    private static void CleanupUnstartedSemanticUiAction(
        string? actionWitnessId,
        GameAction? lifecycleAction)
    {
        if (string.IsNullOrWhiteSpace(actionWitnessId)
            && lifecycleAction == null)
            return;
        try
        {
            if (!string.IsNullOrWhiteSpace(actionWitnessId))
            {
                lock (Gate)
                    NativePostCommitCompletions.Remove(actionWitnessId);
            }
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report("semantic_ui_action.cleanup_completion", exception);
        }
        try
        {
            if (lifecycleAction != null)
                NativeUiCompletionRootBindings.TakeIfMatches(
                    lifecycleAction,
                    actionWitnessId);
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report("semantic_ui_action.cleanup_binding", exception);
        }
    }

    internal static void ObserveSemanticUiNativeCommit(
        string actionWitnessId,
        string family,
        string kind,
        object? nativeOwner = null,
        object? nativeOperand = null,
        object? nativeLineage = null) =>
        ObserveSemanticUiNativeCommitCore(
            actionWitnessId,
            family,
            kind,
            nativeOwner,
            nativeOperand,
            nativeLineage);

    internal static void ObserveSemanticUiNativeCommit(
        string family,
        string kind,
        object? nativeOwner = null,
        object? nativeOperand = null,
        object? nativeLineage = null,
        string? expectedActionWitnessId = null) =>
        ObserveSemanticUiNativeCommitCore(
            expectedActionWitnessId,
            family,
            kind,
            nativeOwner,
            nativeOperand,
            nativeLineage);

    private static void ObserveSemanticUiNativeCommitCore(
        string? expectedActionWitnessId,
        string family,
        string kind,
        object? nativeOwner,
        object? nativeOperand,
        object? nativeLineage)
    {
        if (string.IsNullOrWhiteSpace(family)
            || string.IsNullOrWhiteSpace(kind))
            return;
        try
        {
            string? sessionId = SessionId;
            long generation = Interlocked.Read(ref _nativePostCommitGeneration);
            string operationWitnessId = $"native-sync-{Guid.NewGuid():N}";
            NativePostCommitCompletionResolution resolution;
            lock (Gate)
            {
                NativeTaskBindingResolution binding = sessionId == null
                    ? new NativeTaskBindingResolution("no_match", null, "No recording session is active.")
                    : NativePostCommitCompletions.BindTask(
                        new NativeTaskObservation(
                            sessionId,
                            generation,
                            kind,
                            operationWitnessId,
                            nativeOwner == null
                                ? null
                                : NativeWitnessIdentity.Get(nativeOwner, "native_owner"),
                            nativeOperand == null
                                ? null
                                : NativeWitnessIdentity.Get(nativeOperand, "native_operand"),
                            nativeLineage == null
                                ? null
                                : NativeWitnessIdentity.Get(nativeLineage, "native_lineage")),
                        expectedActionWitnessId);
                resolution = !binding.IsMatched || sessionId == null
                    ? new NativePostCommitCompletionResolution(
                        binding.Status,
                        null,
                        null,
                        binding.Detail)
                    : NativePostCommitCompletions.CompleteTask(
                        new NativeTaskCompletion(
                            sessionId,
                            generation,
                            $"native-completion-{Guid.NewGuid():N}",
                            operationWitnessId,
                            true));
            }
            if (!resolution.IsMatched
                || expectedActionWitnessId != null
                    && resolution.Registration?.ActionWitnessId != expectedActionWitnessId
                || resolution.Completion is not { } nativeCompletion
                || !string.Equals(nativeCompletion.Family, family, StringComparison.Ordinal))
            {
                Quarantine(
                    $"native_sync_binding_{resolution.Status}",
                    resolution.Detail ?? "The synchronous native Commit did not match its exact Human root.",
                    null,
                    kind,
                    "decision_and_lifecycle_only");
                return;
            }
            string actionWitnessId = resolution.Registration!.ActionWitnessId;
            NativeCompletionEvidence completion = ToCompletionEvidence(
                nativeCompletion,
                actionWitnessId);
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
            {
                if (!BoundaryTracker.Contains(actionWitnessId))
                    return;
                var result = new List<SemanticBoundaryTraceDraft>();
                result.AddRange(BoundaryTracker.Finished(actionWitnessId));
                result.AddRange(BoundaryTracker.ObserveNativeCommit(actionWitnessId, completion));
                drafts = result;
            }
            PersistSemanticBoundaryDrafts(drafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static void StartSemanticNativeAction(
        HumanActionContext context,
        NativeWitnessEvidence witness,
        ProcessLocalNativeMatch match,
        GameAction action)
    {
        if (!_semanticBoundaryTraceHealthy || _store == null)
            return;
        RecorderEnvironmentIdentity environment = BuildEnvironment(context.Frame);
        IReadOnlyList<string> blockers = SemanticWitnessBlockers(context.Frame, environment);
        if (blockers.Count > 0 || !IsExact(match))
        {
            Quarantine(
                "semantic_action_not_eligible",
                string.Join(",", blockers.Concat(new[] { match.Status }).Distinct(StringComparer.Ordinal)),
                context.Frame.Snapshot.SnapshotId,
                witness.NativeActionType,
                "fail_closed");
            return;
        }

        CurrentDecisionFrame humanObservation = FreezeSemanticBoundary(context.Frame, environment);
        PlayerEnvironmentBoundAction boundAction = match.BoundAction!;
        long sequence = Interlocked.Increment(ref _sequence);
        string recordId = $"semantic-record-{sequence:D8}-{Guid.NewGuid():N}";
        string actionWitnessId = NativeWitnessIdentity.Get(action, "game_action");
        SemanticActionReference semanticAction = CreateSemanticActionReference(
            actionWitnessId,
            sequence,
            recordId,
            action.GetType().Name,
            action.Id,
            humanObservation,
            "game_action",
            witness,
            match) with
        {
            RequiresNativePostCommit = true
        };
        IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
        lock (Gate)
        {
            SemanticProjectionEnvironments[actionWitnessId] = environment;
            ExecutionSemanticActionSpaceEvidence? nativeDecision =
                context.NativeSemanticDecision == null
                    ? null
                    : ToExecutionSemanticActionSpace(
                        actionWitnessId,
                        context.NativeSemanticDecision,
                        boundAction.BoundActionId);
            drafts = BoundaryTracker.Accept(semanticAction, humanObservation);
            var subscription = new NativeActionLifecycleSubscription(
                action,
                actionWitnessId,
                sequence,
                recordId,
                boundAction.BoundActionId,
                nativeDecision,
                ObserveSemanticOnlyNativeActionLifecycle);
            NativeActionSubscriptions.Add(action, subscription);
            SemanticOnlyNativeActionIds.Add(actionWitnessId);
        }
        PersistSemanticBoundaryDrafts(drafts);
        AppendJournal(
            "semantic_human_action_accepted",
            recordId,
            humanObservation.SnapshotId,
            $"{action.GetType().Name}:{actionWitnessId}");
    }

    private static SemanticActionReference CreateSemanticActionReference(
        string actionWitnessId,
        long sequence,
        string recordId,
        string nativeActionType,
        uint? nativeQueueId,
        CurrentDecisionFrame humanObservation,
        string nativeMechanism,
        NativeWitnessEvidence witness,
        ProcessLocalNativeMatch match)
    {
        PlayerEnvironmentBoundAction bound = match.BoundAction!;
        return new SemanticActionReference(
            actionWitnessId,
            sequence,
            recordId,
            _currentRunId,
            nativeActionType,
            nativeQueueId,
            humanObservation.SnapshotId)
        {
            NativeMechanism = nativeMechanism,
            NativeWitness = witness,
            Mapping = new ExactMappingEvidence(
                match.Status,
                match.MatchCount,
                match.Evidence,
                match.Detail),
            BoundAction = new RecordedBoundAction(
                bound.BoundActionId,
                bound.Verb,
                bound.SubjectReferentId,
                bound.Arguments.ToDictionary(
                    argument => argument.Role,
                    argument => argument.ReferentId,
                    StringComparer.Ordinal),
                bound.Label)
        };
    }

    private static void ObserveSemanticLifecycle(
        NativeActionLifecycleSubscription subscription,
        string kind)
    {
        if (!_semanticBoundaryTraceHealthy)
            return;
        try
        {
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
            {
                drafts = kind switch
                {
                    NativeActionLifecycleKinds.Started =>
                        BoundaryTracker.Started(subscription.ActionWitnessId),
                    NativeActionLifecycleKinds.PausedForPlayerChoice =>
                        BoundaryTracker.PausedForPlayerChoice(subscription.ActionWitnessId),
                    NativeActionLifecycleKinds.ReadyToResume =>
                        BoundaryTracker.ReadyToResume(subscription.ActionWitnessId),
                    NativeActionLifecycleKinds.Resumed =>
                        BoundaryTracker.Resumed(subscription.ActionWitnessId),
                    NativeActionLifecycleKinds.Cancelled =>
                        BoundaryTracker.Cancelled(subscription.ActionWitnessId),
                    NativeActionLifecycleKinds.Finished =>
                        BoundaryTracker.Finished(subscription.ActionWitnessId),
                    _ => Array.Empty<SemanticBoundaryTraceDraft>()
                };
            }
            PersistSemanticBoundaryDrafts(drafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static void ObserveSemanticOnlyNativeActionLifecycle(
        NativeActionLifecycleSubscription subscription,
        string kind)
    {
        bool terminal = NativeActionLifecycleKinds.IsTerminal(kind);
        bool currentSubscription = false;
        try
        {
            lock (Gate)
            {
                currentSubscription = NativeActionSubscriptions.TryGetValue(
                        subscription.Action,
                        out NativeActionLifecycleSubscription? current)
                    && ReferenceEquals(current, subscription)
                    && SemanticOnlyNativeActionIds.Contains(subscription.ActionWitnessId);
            }
            if (!currentSubscription)
                return;

            // Each downstream observer is isolated. A diagnostics or
            // projection failure must never escape a Harmony event callback
            // or prevent the terminal cleanup below.
            try
            {
                NativeSemanticDiscriminatorRuntime.ObserveLifecycleOnly(
                    _store,
                    SessionId,
                    TimelineId,
                    _currentRunId,
                    kind,
                    subscription.Action);
            }
            catch (Exception exception)
            {
                NativeUiObservationSafety.Report("semantic_lifecycle.discriminator", exception);
            }
            try
            {
                ObserveSemanticLifecycle(subscription, kind);
            }
            catch (Exception exception)
            {
                NativeUiObservationSafety.Report("semantic_lifecycle.boundary", exception);
            }
            if (kind == NativeActionLifecycleKinds.PausedForPlayerChoice)
            {
                try
                {
                    ObserveNativeContinuation(
                        subscription.ActionWitnessId,
                        new NativeContinuationEvidence(
                            $"native-continuation-{Guid.NewGuid():N}",
                            "GameAction.BeforePausedForPlayerChoice",
                            subscription.ActionWitnessId,
                            NativeWitnessIdentity.Get(subscription.Action, "game_action"),
                            NativeWitnessIdentity.Get(subscription.Action, "game_action"),
                            true));
                }
                catch (Exception exception)
                {
                    NativeUiObservationSafety.Report("semantic_lifecycle.continuation", exception);
                }
            }
            if (kind == NativeActionLifecycleKinds.Finished
                && subscription.FinishIsNativeCommit)
            {
                try
                {
                    ObserveNativeCommit(
                        subscription.ActionWitnessId,
                        new NativeCompletionEvidence(
                            $"native-completion-{Guid.NewGuid():N}",
                            SupportedFamilyForNativeAction(subscription.NativeActionType, null)
                                ?? "native_game_action",
                            "GameAction.Finished",
                            subscription.ActionWitnessId,
                            null,
                            NativeWitnessIdentity.Get(subscription.Action, "game_action"),
                            null,
                            null,
                            true));
                }
                catch (Exception exception)
                {
                    NativeUiObservationSafety.Report("semantic_lifecycle.commit", exception);
                }
            }
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report("semantic_lifecycle.callback", exception);
        }
        finally
        {
            if (terminal && currentSubscription)
            {
                try
                {
                    bool removed = false;
                    lock (Gate)
                    {
                        if (NativeActionSubscriptions.TryGetValue(
                                subscription.Action,
                                out NativeActionLifecycleSubscription? current)
                            && ReferenceEquals(current, subscription))
                        {
                            NativeActionSubscriptions.Remove(subscription.Action);
                            SemanticOnlyNativeActionIds.Remove(subscription.ActionWitnessId);
                            removed = true;
                        }
                    }
                    if (removed)
                    {
                        try
                        {
                            subscription.Dispose();
                        }
                        catch (Exception exception)
                        {
                            NativeUiObservationSafety.Report("semantic_lifecycle.dispose", exception);
                        }
                    }
                }
                catch (Exception exception)
                {
                    NativeUiObservationSafety.Report("semantic_lifecycle.cleanup", exception);
                }
                try
                {
                    // A cancelled/finished bound UI action may never reach its
                    // explicit Commit patch. Clear only this exact action object;
                    // no other root can be removed by a witness collision.
                    NativeUiCompletionRootBindings.Take(subscription.Action);
                }
                catch (Exception exception)
                {
                    NativeUiObservationSafety.Report("semantic_lifecycle.binding_cleanup", exception);
                }
                try
                {
                    FinalizeClose();
                }
                catch (Exception exception)
                {
                    NativeUiObservationSafety.Report("semantic_lifecycle.closeout", exception);
                }
            }
        }
    }

    private static void ObserveNativeCommit(
        string actionWitnessId,
        NativeCompletionEvidence completion)
    {
        try
        {
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
            {
                drafts = BoundaryTracker.ObserveNativeCommit(
                    actionWitnessId,
                    completion);
            }
            PersistSemanticBoundaryDrafts(drafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static void ObserveNativeContinuation(
        string actionWitnessId,
        NativeContinuationEvidence continuation)
    {
        try
        {
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
            {
                drafts = BoundaryTracker.ObserveNativeContinuation(
                    actionWitnessId,
                    continuation);
            }
            PersistSemanticBoundaryDrafts(drafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static void ObserveNativePostCommitCompletion(
        NativeTaskCompletion taskCompletion)
    {
        NativePostCommitCompletionResolution resolution;
        lock (Gate)
            resolution = NativePostCommitCompletions.CompleteTask(taskCompletion);

        if (!resolution.IsMatched)
        {
            Quarantine(
                $"native_completion_{resolution.Status}",
                resolution.Detail ?? "The native completion had no exact Human root.",
                null,
                taskCompletion.TaskWitnessId,
                "decision_and_lifecycle_only");
            return;
        }

        NativePostCommitCompletionRegistration registration = resolution.Registration!;
        NativePostCommitCompletion completion = resolution.Completion!;
        if (!completion.Succeeded)
        {
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
            {
                drafts = BoundaryTracker.NativeCompletionFailed(
                    registration.ActionWitnessId,
                    "native_completion_failed",
                    "STS2 reported the exact native completion as cancelled, faulted, or unsuccessful; no successor is claimed.",
                    ToCompletionEvidence(completion, registration.ActionWitnessId));
            }
            PersistSemanticBoundaryDrafts(drafts);
            return;
        }

        IReadOnlyList<SemanticBoundaryTraceDraft> finished;
        lock (Gate)
            finished = BoundaryTracker.Finished(registration.ActionWitnessId);
        PersistSemanticBoundaryDrafts(finished);
        ObserveNativeCommit(
            registration.ActionWitnessId,
            ToCompletionEvidence(completion, registration.ActionWitnessId));
    }

    private static NativeCompletionEvidence ToCompletionEvidence(
        NativePostCommitCompletion completion,
        string actionWitnessId) =>
        new(
            completion.CompletionId,
            completion.Family,
            completion.Kind,
            actionWitnessId,
            completion.TaskWitnessId,
            completion.NativeOwnerWitnessId,
            completion.NativeOperandWitnessId,
            completion.NativeLineageWitnessId,
            completion.Succeeded);

    internal static void QueueNativePostCommitBoundary(
        Task task,
        string kind,
        object? nativeOwner = null,
        object? nativeOperand = null,
        object? nativeLineage = null,
        string? expectedActionWitnessId = null)
    {
        QueueNativePostCommitBoundary(
            task,
            kind,
            succeeded: completed => completed.Status == TaskStatus.RanToCompletion,
            nativeOwner: nativeOwner,
            nativeOperand: nativeOperand,
            nativeLineage: nativeLineage,
            expectedActionWitnessId: expectedActionWitnessId);
    }

    internal static void QueueNativePostCommitBoundary(
        Task<bool> task,
        string kind,
        object? nativeOwner = null,
        object? nativeOperand = null,
        object? nativeLineage = null,
        string? expectedActionWitnessId = null)
    {
        QueueNativePostCommitBoundary(
            task,
            kind,
            succeeded: completed => completed.Status == TaskStatus.RanToCompletion
                && completed is Task<bool> result
                && result.Result,
            nativeOwner: nativeOwner,
            nativeOperand: nativeOperand,
            nativeLineage: nativeLineage,
            expectedActionWitnessId: expectedActionWitnessId);
    }

    private static void QueueNativePostCommitBoundary<TTask>(
        TTask task,
        string kind,
        Func<Task, bool> succeeded,
        object? nativeOwner,
        object? nativeOperand,
        object? nativeLineage,
        string? expectedActionWitnessId)
        where TTask : Task
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrWhiteSpace(kind))
            return;
        long generation = Interlocked.Read(ref _nativePostCommitGeneration);
        string? sessionId = SessionId;
        string? nativeOwnerWitnessId = nativeOwner == null
            ? null
            : NativeWitnessIdentity.Get(nativeOwner, "native_owner");
        string? nativeOperandWitnessId = nativeOperand == null
            ? null
            : NativeWitnessIdentity.Get(nativeOperand, "native_operand");
        string? nativeLineageWitnessId = nativeLineage == null
            ? null
            : NativeWitnessIdentity.Get(nativeLineage, "native_lineage");
        string taskWitnessId = NativeWitnessIdentity.Get(task, "native_task");
        NativeTaskBindingResolution binding;
        bool hasPendingExpectation = false;
        lock (Gate)
        {
            binding = sessionId == null
                ? new NativeTaskBindingResolution(
                    "no_match",
                    null,
                    "The recording session identity is unavailable.")
                : NativePostCommitCompletions.BindTask(
                    new NativeTaskObservation(
                        sessionId,
                        generation,
                        kind,
                        taskWitnessId,
                        nativeOwnerWitnessId,
                        nativeOperandWitnessId,
                        nativeLineageWitnessId),
                    expectedActionWitnessId);
            hasPendingExpectation = sessionId != null
                && NativePostCommitCompletions.HasPendingExpectation(
                    sessionId,
                    generation,
                    kind);
        }
        if (!binding.IsMatched || sessionId == null)
        {
            // Native Task callbacks can run for internal/non-Human
            // continuations. Only quarantine an unmatched callback when a
            // staged Human root explicitly expects this exact native kind;
            // identity mismatches against such an expectation remain
            // fail-closed. Never create an invalidation for an unowned task.
            if (hasPendingExpectation)
            {
                Quarantine(
                    $"native_task_binding_{binding.Status}",
                    binding.Detail ?? "The native Task could not bind to exactly one Human root.",
                    null,
                    kind,
                    "decision_and_lifecycle_only");
            }
            return;
        }
        _ = task.ContinueWith(
            completed =>
            {
                NativeTaskCompletion signal = new(
                    sessionId,
                    generation,
                    $"native-completion-{Guid.NewGuid():N}",
                    taskWitnessId,
                    succeeded(completed));
                lock (Gate)
                {
                    if (generation == _nativePostCommitGeneration
                        && _store != null
                        && string.Equals(SessionId, sessionId, StringComparison.Ordinal))
                    {
                        QueuedNativePostCommitCompletions.Enqueue(signal);
                    }
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void PersistSemanticBoundaryDrafts(
        IReadOnlyList<SemanticBoundaryTraceDraft> drafts,
        Action? onAuthoritativeSemanticAppend = null,
        Action? onDerivedProjectionFailure = null)
    {
        if (drafts.Count == 0 || !_semanticBoundaryTraceHealthy)
            return;
        RecordingSessionStore store = _store
            ?? throw new InvalidOperationException("No open recording store for semantic boundary evidence.");
        var events = new List<SemanticEvidenceEvent>(drafts.Count);
        foreach (SemanticBoundaryTraceDraft draft in drafts)
        {
            long sequence = Interlocked.Increment(ref _semanticBoundaryEventSequence);
            SemanticFrameReference? humanObservationRef = draft.HumanObservation == null
                ? null
                : store.PersistSemanticFrame(draft.HumanObservation);
            SemanticFrameReference? executionPreRef = draft.SemanticPre == null
                ? null
                : store.PersistSemanticFrame(draft.SemanticPre);
            SemanticFrameReference? successorRef = draft.SemanticSuccessor == null
                ? null
                : store.PersistSemanticFrame(draft.SemanticSuccessor);
            ExecutionSemanticActionSpaceReference? executionSemanticActionSpaceRef =
                draft.ExecutionSemanticActionSpace == null
                    ? null
                    : store.PersistExecutionSemanticActionSpace(
                        draft.ExecutionSemanticActionSpace);
            SemanticBoundaryObservationReference? boundary = draft.Boundary == null
                ? null
                : ToReference(store, draft.Boundary);
            events.Add(new SemanticEvidenceEvent(
                SemanticEvidenceContract.SchemaVersion,
                SemanticEvidenceContract.EventSchema,
                $"semantic-event-{Guid.NewGuid():N}",
                SessionId!,
                TimelineId!,
                draft.Action.RunId,
                sequence,
                DateTimeOffset.UtcNow,
                draft.Kind,
                draft.Action,
                draft.ProofStatus,
                draft.RelatedActionWitnessId,
                boundary,
                executionPreRef,
                successorRef,
                draft.Detail,
                draft.NonClaims ?? Array.Empty<string>())
            {
                HumanObservationRef = humanObservationRef,
                NativeCompletion = draft.NativeCompletion,
                NativeContinuation = draft.NativeContinuation,
                ExecutionSemanticActionSpaceRef = executionSemanticActionSpaceRef
            });
        }
        store.AppendSemanticEvidenceEvents(events);
        onAuthoritativeSemanticAppend?.Invoke();

        // Completion registrations are in-memory correlation state. Remove
        // them only after the terminal disposition has reached the durable
        // semantic stream; otherwise a failed append would strand an
        // unresolved tracker root with no remaining exact completion path.
        foreach (SemanticBoundaryTraceDraft draft in drafts.Where(IsTerminalWithoutNativeCompletion))
        {
            lock (Gate)
                NativePostCommitCompletions.Remove(draft.Action.ActionWitnessId);
        }

        foreach (SemanticBoundaryTraceDraft draft in drafts.Where(value =>
                     value.Kind == SemanticBoundaryTraceKinds.ActionAccepted
                     && SupportedFamilyForSemanticAction(value.Action) is { } family
                     && CaptureProfile.SupportedActionFamilies.Contains(family, StringComparer.Ordinal)
                     && value.Action.BoundAction != null))
        {
            PublishApplicationEvent(
                RecordingEventKind.RootPending,
                draft.Action.RecordId,
                draft.Action.NativeActionType,
                ToActionProjection(draft.Action.BoundAction!));
        }

        bool derivedProjectionFailed = false;
        foreach (SemanticBoundaryTraceDraft draft in drafts.Where(value =>
                     value.Kind == SemanticBoundaryTraceKinds.TransitionProved))
        {
            if (!TryPersistDerivedTransitionProjection(store, draft))
                derivedProjectionFailed = true;
        }
        if (derivedProjectionFailed)
        {
            onDerivedProjectionFailure?.Invoke();
            // Close supplies a callback so it can remain explicitly open
            // after the authoritative disposition. Other lifecycle writes
            // retain their existing cleanup path after the projection has
            // already been quarantined above.
            if (onDerivedProjectionFailure != null)
                return;
        }
        foreach (SemanticBoundaryTraceDraft draft in drafts.Where(IsSemanticDisposition))
        {
            if (draft.Kind != SemanticBoundaryTraceKinds.TransitionProved
                && draft.Action.BoundAction != null)
            {
                PublishApplicationEvent(
                    RecordingEventKind.DecisionInvalidated,
                    draft.Action.RecordId,
                    $"{draft.Kind}: {draft.Detail}",
                    ToActionProjection(draft.Action.BoundAction));
            }
            lock (Gate)
                SemanticProjectionEnvironments.Remove(draft.Action.ActionWitnessId);
        }
    }

    private static void PersistDerivedTransitionProjection(
        RecordingSessionStore store,
        SemanticBoundaryTraceDraft draft) =>
        _ = TryPersistDerivedTransitionProjection(store, draft);

    private static bool TryPersistDerivedTransitionProjection(
        RecordingSessionStore store,
        SemanticBoundaryTraceDraft draft)
    {
        string? family = SupportedFamilyForSemanticAction(draft.Action);
        if (family == null
            || !CaptureProfile.SupportedActionFamilies.Contains(family, StringComparer.Ordinal))
            return true;

        try
        {
            SemanticFrameReference preRef = store.PersistSemanticFrame(draft.SemanticPre!);
            SemanticFrameReference successorRef = store.PersistSemanticFrame(draft.SemanticSuccessor!);
            ExecutionSemanticActionSpaceReference? executionSemanticActionSpaceRef =
                draft.ExecutionSemanticActionSpace == null
                    ? null
                    : store.PersistExecutionSemanticActionSpace(
                        draft.ExecutionSemanticActionSpace);
            CanonicalTransitionEvidence canonical = SemanticTransitionProjection.CreateCanonical(
                draft,
                preRef,
                successorRef,
                executionSemanticActionSpaceRef,
                SessionId!,
                TimelineId!);
            IReadOnlyList<string> canonicalErrors =
                CanonicalTransitionEvidenceValidator.Validate(canonical);
            if (canonicalErrors.Count > 0)
                throw new InvalidDataException(
                    $"Canonical semantic projection failed validation: {string.Join(',', canonicalErrors)}");
            store.AppendCanonicalTransition(canonical);

            RecorderEnvironmentIdentity? environment;
            lock (Gate)
                SemanticProjectionEnvironments.TryGetValue(
                    draft.Action.ActionWitnessId,
                    out environment);
            CurrentDecisionRecord? record = null;
            bool currentDecisionAppended = false;
            IReadOnlyList<string> compatibilityErrors;
            if (environment == null)
            {
                compatibilityErrors = new[] { "root_environment_missing" };
            }
            else
            {
                record = SemanticTransitionProjection.CreateDecision(
                    draft,
                    environment,
                    SessionId!,
                    TimelineId!,
                    CaptureProfile.ProfileId);
                RecordValidationResult recordValidation =
                    CurrentDecisionRecordValidator.Validate(record);
                RecordValidationResult profileValidation =
                    HumanCaptureProfileValidator.ValidateRecord(CaptureProfile, record);
                compatibilityErrors = recordValidation.Errors
                    .Concat(profileValidation.Errors)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (recordValidation.Valid && profileValidation.Valid)
                {
                    store.AppendDecision(record);
                    currentDecisionAppended = true;
                }
            }

            string eventId = currentDecisionAppended
                ? record!.RecordId
                : canonical.TransitionId;
            AppendJournal(
                "canonical_transition_recorded",
                eventId,
                canonical.SuccessorRef.SnapshotId,
                canonical.Action.Verb);
            if (compatibilityErrors.Count > 0)
            {
                AppendJournal(
                    "current_decision_projection_omitted",
                    canonical.TransitionId,
                    canonical.SuccessorRef.SnapshotId,
                    string.Join(",", compatibilityErrors));
            }
            PublishApplicationEvent(
                RecordingEventKind.DecisionRecorded,
                draft.Action.RecordId,
                canonical.Action.Verb,
                ToActionProjection(canonical.Action));
            _runtimeState = "record_appended";
            _detail = canonical.TransitionId;
            WriteStatus(environment, canonical.SuccessorRef.SnapshotId, Array.Empty<string>());
            GD.Print($"[STS2 Human Annotator] admitted {canonical.TransitionId} {canonical.Action.Verb} from semantic proof");
            return true;
        }
        catch (Exception exception)
        {
            Quarantine(
                "semantic_projection_persistence_unknown",
                exception.Message,
                draft.SemanticPre?.SnapshotId,
                draft.Action.NativeActionType,
                "evidence_commit_unknown");
            return false;
        }
    }

    private static bool IsTerminalWithoutNativeCompletion(SemanticBoundaryTraceDraft draft) =>
        draft.Action.RequiresNativePostCommit
        && (draft.Kind is SemanticBoundaryTraceKinds.TransitionUnknown
            or SemanticBoundaryTraceKinds.ActionCancelledBeforeStart
            or SemanticBoundaryTraceKinds.ActionCancelledAfterStart
            or SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit);

    private static bool IsSemanticDisposition(SemanticBoundaryTraceDraft draft) =>
        draft.Kind is SemanticBoundaryTraceKinds.TransitionProved
            or SemanticBoundaryTraceKinds.TransitionUnknown
            or SemanticBoundaryTraceKinds.ActionCancelledBeforeStart
            or SemanticBoundaryTraceKinds.ActionCancelledAfterStart
            or SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit;

    private static SemanticBoundaryObservationReference ToReference(
        RecordingSessionStore store,
        SemanticBoundaryObservation boundary) =>
        SemanticBoundaryObservationCodec.Encode(boundary, store.PersistSemanticFrame);

    private static void DisableSemanticBoundaryTrace(Exception exception)
    {
        lock (Gate)
            SemanticProjectionEnvironments.Clear();
        _semanticBoundaryTraceHealthy = false;
        _runtimeState = "semantic_boundary_trace_unknown";
        _detail = exception.Message;
        GD.PrintErr($"[STS2 Human Annotator] semantic boundary trace disabled: {exception}");
    }

    private static bool TryDescribeAction(
        GameAction action,
        HumanActionContext context,
        out ProcessLocalObservedAction? observed,
        out NativeWitnessEvidence? witness,
        out string? failureReason,
        out string? failureDetail,
        out string? failureEvidence)
    {
        observed = null;
        witness = null;
        failureReason = null;
        failureDetail = null;
        failureEvidence = null;
        if (action is PlayCardAction play)
        {
            object? card = play.NetCombatCard.ToCardModelOrNull();
            if (card == null)
            {
                failureReason = "play_card_native_subject_missing";
                failureDetail = "The accepted PlayCardAction no longer resolved its exact card model.";
                failureEvidence = "native_witness_missing";
                return false;
            }
            var arguments = new Dictionary<string, object>(StringComparer.Ordinal);
            var argumentWitnesses = new Dictionary<string, string>(StringComparer.Ordinal);
            if (play.Target != null)
            {
                arguments["target"] = play.Target;
                argumentWitnesses["target"] = NativeWitnessIdentity.Get(play.Target, "target");
            }
            observed = new ProcessLocalObservedAction("play", card, arguments);
            witness = new NativeWitnessEvidence(
                context.Origin,
                nameof(PlayCardAction),
                NativeWitnessIdentity.Get(card, "card"),
                argumentWitnesses,
                DateTimeOffset.UtcNow);
            return true;
        }

        if (action is EndPlayerTurnAction)
        {
            observed = new ProcessLocalObservedAction(
                "end_turn",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal));
            witness = new NativeWitnessEvidence(
                context.Origin,
                nameof(EndPlayerTurnAction),
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow);
            return true;
        }

        if (context.ExpectedAction is { } expected)
        {
            observed = expected;
            var argumentWitnesses = expected.Arguments.ToDictionary(
                argument => argument.Key,
                argument => NativeWitnessIdentity.Get(argument.Value, argument.Key),
                StringComparer.Ordinal);
            witness = new NativeWitnessEvidence(
                context.Origin,
                action.GetType().Name,
                expected.Subject == null
                    ? null
                    : NativeWitnessIdentity.Get(expected.Subject, "subject"),
                argumentWitnesses,
                DateTimeOffset.UtcNow);
            return true;
        }

        string type = action.GetType().FullName ?? action.GetType().Name;
        failureReason = "native_scope_contract_error";
        failureDetail = $"The configured root action {type} has no recorder mapping.";
        failureEvidence = "implemented_runtime_error";
        return false;
    }

    private static RecorderEnvironmentIdentity BuildEnvironment(
        ProcessLocalNativeWitnessFrame frame)
    {
        PlayerEnvironmentCapabilitiesResponse capabilities = frame.Capabilities;
        Assembly gameAssembly = typeof(RunManager).Assembly;
        Assembly annotatorAssembly = typeof(RecorderMod).Assembly;
        return new RecorderEnvironmentIdentity(
            new ExactGameIdentity(
                capabilities.Game.Version,
                capabilities.Game.Commit,
                EvidenceIdentity.Sha256File(gameAssembly.Location),
                gameAssembly.ManifestModule.ModuleVersionId.ToString("D")),
            new ExactArtifactIdentity(
                capabilities.Host.Name,
                capabilities.Host.Version,
                capabilities.Host.Implementation.SourceRevision ?? "unavailable",
                frame.SourceDigest,
                capabilities.Host.Implementation.ArtifactSha256 ?? "unavailable",
                capabilities.Host.Implementation.ModuleVersionId ?? "unavailable"),
            new ExactArtifactIdentity(
                "STS2 Native UI Human Annotator",
                RecorderMod.Version,
                ReadSourceRevision(annotatorAssembly),
                ReadAssemblyMetadata(annotatorAssembly, "AnnotatorSourceDigest"),
                EvidenceIdentity.Sha256File(annotatorAssembly.Location),
                annotatorAssembly.ManifestModule.ModuleVersionId.ToString("D")),
            capabilities.ProtocolVersion,
            capabilities.Host.RuntimeInstanceId,
            capabilities.EnvironmentFingerprint,
            capabilities.Game.Modset.Status,
            capabilities.Game.Modset.Fingerprint);
    }

    private static ProcessLocalNativeWitnessFrame CaptureReadRichFrame()
    {
        ProcessLocalNativeWitnessFrame frame = MeasureStore(
            "read_rich_snapshot_capture",
            () => PlayerEnvironmentNativeWitness.Capture(SemanticBoundaryReadPolicy.RequiredKinds));
        RecordCaptureSubphases("read_rich", frame);
        return frame;
    }

    private static ProcessLocalNativeWitnessFrame CaptureSemanticFrame()
    {
        ProcessLocalNativeWitnessFrame frame = MeasureStore(
            "semantic_snapshot_capture",
            () => PlayerEnvironmentNativeWitness.Capture(SemanticBoundaryReadPolicy.RequiredKinds));
        RecordCaptureSubphases("semantic", frame);
        return frame;
    }

    private static void RecordCaptureSubphases(
        string callSite,
        ProcessLocalNativeWitnessFrame frame)
    {
        RecordingSessionStore? store = _store;
        if (store == null)
            return;
        foreach (ProcessLocalCaptureTiming timing in frame.CaptureTimings)
        {
            store.ObservePerformance(
                $"full_capture.{callSite}.{timing.Phase}",
                timing.ElapsedMicroseconds);
        }
    }

    private static T MeasureStore<T>(string phase, Func<T> operation)
    {
        RecordingSessionStore? store = _store;
        return store == null ? operation() : store.Measure(phase, operation);
    }

    private static bool HasRequiredReads(ProcessLocalNativeWitnessFrame frame) =>
        HasSemanticRequiredReads(frame);

    private static bool HasSemanticRequiredReads(ProcessLocalNativeWitnessFrame frame) =>
        SemanticBoundaryReadPolicy.RequiredKinds(frame.Snapshot.Interaction.Kind)
            .All(kind => frame.Reads.TryGetValue(kind, out ProcessLocalReadCapture? read)
                && string.Equals(read.Status, "materialized", StringComparison.Ordinal)
                && read.Read != null);

    private static IReadOnlyList<string> SemanticWitnessBlockers(
        ProcessLocalNativeWitnessFrame frame,
        RecorderEnvironmentIdentity environment)
    {
        var blockers = new List<string>(SemanticStateBlockers(frame, environment));
        if (!string.Equals(frame.Snapshot.Status, "interactive", StringComparison.Ordinal))
            blockers.Add("pre_frame_not_interactive");
        if (!string.Equals(frame.Snapshot.BoundActions.Status, "complete", StringComparison.Ordinal)
            || frame.Snapshot.BoundActions.Actions.Count == 0)
        {
            blockers.Add("pre_frame_bound_actions_incomplete");
        }
        return blockers.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<ReadEvidence> PersistReads(
        ProcessLocalNativeWitnessFrame frame,
        string phase,
        RecorderEnvironmentIdentity environment)
    {
        if (_store == null)
            throw new InvalidOperationException("The current recording store is unavailable.");
        var captures = new List<CapturedReadPayload>();
        IEnumerable<CaptureReadRequirement> requirements = string.Equals(
                phase,
                "semantic",
                StringComparison.Ordinal)
            ? SemanticBoundaryReadPolicy.RequiredKinds(frame.Snapshot.Interaction.Kind)
                .Select(kind => new CaptureReadRequirement(phase, kind, true))
            : CaptureProfile.Reads.Where(read =>
                string.Equals(read.Phase, phase, StringComparison.Ordinal)
                && (read.InteractionKind == null
                    || string.Equals(
                        read.InteractionKind,
                        frame.Snapshot.Interaction.Kind,
                        StringComparison.Ordinal)));
        foreach (CaptureReadRequirement requirement in requirements
                     .OrderBy(read => read.Kind, StringComparer.Ordinal))
        {
            if (!frame.Reads.TryGetValue(requirement.Kind, out ProcessLocalReadCapture? captured)
                || captured.Read == null)
            {
                captures.Add(new CapturedReadPayload(
                    $"read:{requirement.Kind}",
                    requirement.Kind,
                    frame.Snapshot.SnapshotId,
                    environment.RuntimeInstanceId,
                    environment.EnvironmentFingerprint,
                    captured?.Status ?? "not_available",
                    null,
                    null,
                    null,
                    DateTimeOffset.UtcNow,
                    captured?.ErrorCode ?? "required_read_missing",
                    captured?.Detail));
                continue;
            }
            PlayerEnvironmentReadResponse read = captured.Read;
            captures.Add(new CapturedReadPayload(
                read.ReadId,
                read.Kind,
                read.ObservedSnapshotId,
                read.Session.RuntimeInstanceId,
                read.Session.EnvironmentFingerprint,
                "materialized",
                read.ContentSchema,
                read.Content.DeepClone(),
                ToNode(read.Completeness),
                read.ObservedAt,
                null,
                null));
        }
        return _store.PersistReads(captures);
    }

    private static List<string> EligibilityBlockers(
        ProcessLocalNativeWitnessFrame frame,
        RecorderEnvironmentIdentity environment,
        bool requireReads,
        bool includeRecordingLifecycle = true)
    {
        var blockers = new List<string>();
        RecordingLifecycleState lifecycleState = GetRecordingLifecycle().State;
        if (includeRecordingLifecycle && lifecycleState != RecordingLifecycleState.Recording)
            blockers.AddRange(LifecycleBlockers(lifecycleState));
        if (!RunManager.Instance.IsInProgress
            || RunManager.Instance.NetService?.Type != NetGameType.Singleplayer)
            blockers.Add("not_singleplayer_run");
        if (frame.ExternalControllerActive)
            blockers.Add("external_controller_active");
        if (!string.Equals(frame.Snapshot.Status, "interactive", StringComparison.Ordinal)
            || !string.Equals(frame.Snapshot.BoundActions.Status, "complete", StringComparison.Ordinal)
            || frame.Snapshot.BoundActions.Actions.Count == 0)
            blockers.Add("pre_frame_not_complete_interactive");
        if (!RecordingEnvironmentAdmission.IsExactModset(environment.ModsetStatus))
            blockers.Add("exact_recording_modset_missing");
        if (!IsCommit(environment.Connector.SourceRevision)
            || !IsCommit(environment.Annotator.SourceRevision))
            blockers.Add("source_revision_not_exact");
        if (requireReads && !HasRequiredReads(frame))
            blockers.Add("required_read_evidence_unavailable");
        return blockers;
    }

    private static RecordingScopeStatus BuildScopeStatus(RecordingStoreSnapshot store)
    {
        var failedClosed = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach ((string nativeAction, long count) in store.InvalidatedNativeActions)
        {
            string? family = SupportedFamilyForNativeAction(nativeAction);
            if (family != null)
                failedClosed[family] = failedClosed.GetValueOrDefault(family) + count;
        }
        string[] notObserved = CaptureProfile.SupportedActionFamilies
            .Where(family => !store.RecordedActionFamilies.ContainsKey(family)
                && !failedClosed.ContainsKey(family))
            .OrderBy(family => family, StringComparer.Ordinal)
            .ToArray();
        return new RecordingScopeStatus(
            CaptureProfile.SupportedActionFamilies,
            store.RecordedActionFamilies,
            failedClosed,
            store.InvalidationsByReason,
            notObserved,
            DeclaredOutOfScopeActionFamilies,
            "Only supported_action_families are eligible for recording; all other gameplay actions are outside this capture profile.");
    }

    private static string? SupportedFamilyForSemanticAction(SemanticActionReference action)
    {
        if (action.NativeActionType == nameof(PickRelicAction)
            && string.Equals(action.BoundAction?.Verb, "skip", StringComparison.Ordinal))
        {
            return "treasure_room.skip";
        }
        if (action.NativeActionType == "NEventRoom.OptionButtonClicked")
        {
            return action.BoundAction?.Verb switch
            {
                "proceed_event" => "event_option.proceed",
                "choose_event_option" or "activate" => "event_option.choose",
                _ => null
            };
        }
        if (action.NativeActionType is "NPlayerHand.SelectCardInPlayMode"
            or "NPlayerHand.SelectCardInDiscardMode"
            or "NPlayerHand.SelectCardInUpgradeMode")
            return "combat_hand_selector.select";
        if (action.NativeActionType == "NSelectedHandCardContainer.DeselectHolder")
            return "combat_hand_selector.deselect";
        if (action.NativeActionType == "NPlayerHand.OnSelectModeConfirmButtonPressed")
            return "combat_hand_selector.confirm";
        if (action.NativeActionType == "MerchantEntry.OnTryPurchaseWrapper")
        {
            return action.BoundAction?.Verb == "open_shop_card_removal"
                ? "shop_inventory.card_removal"
                : action.BoundAction?.Verb is "purchase_shop_card"
                    or "purchase_shop_relic"
                    or "purchase_shop_potion"
                    ? "shop_inventory.purchase"
                    : null;
        }
        if (action.NativeActionType == "NMerchantRoom.OpenInventory")
            return "shop_room.open";
        if (action.NativeActionType == "NMerchantRoom.HideScreen")
            return "shop_room.proceed";
        if (action.NativeActionType == "NMerchantInventory.Close")
            return "shop_inventory.close";
        return SupportedFamilyForNativeAction(action.NativeActionType);
    }

    private static string? SupportedFamilyForNativeAction(
        string nativeActionType,
        ProcessLocalNativeMatch? match = null)
    {
        if (nativeActionType == nameof(PickRelicAction)
            && string.Equals(match?.BoundAction?.Verb, "skip", StringComparison.Ordinal))
        {
            return "treasure_room.skip";
        }

        return nativeActionType switch
        {
            nameof(PlayCardAction) => "ordinary_combat.play_card",
            nameof(EndPlayerTurnAction) => "ordinary_combat.end_turn",
            nameof(UsePotionAction) => "ordinary_combat.use_potion",
            "NChooseACardSelectionScreen.SelectHolder" => "native_generated_card_choice.select",
            "NChooseACardSelectionScreen.OnSkipButtonReleased" => "native_generated_card_choice.skip",
            "NChooseARelicSelection.SelectHolder" => "boss_relic.select",
            "NChooseARelicSelection.OnSkipButtonReleased" => "boss_relic.skip",
            "NPotionPopup.OnDiscardButtonPressed" => "reward_potion_belt.discard_replace",
            nameof(DiscardPotionGameAction) => "reward_potion_belt.discard_replace",
            nameof(VoteForMapCoordAction) => "map_navigation.travel",
            "NRewardButton.OnRelease" => "reward_claim.claim",
            "NRewardsScreen.OnProceedButtonPressed" => "reward_claim.proceed",
            "NRewardsScreen.OnProceedButtonPressed.act_change_ready" => "act_change.ready",
            "NCardRewardSelectionScreen.SelectCard" => "card_reward_selection.select",
            "NTreasureRoom.OnChestButtonReleased" => "treasure_room.open",
            nameof(PickRelicAction) => "treasure_room.select",
            "NTreasureRoom.OnProceedButtonPressed" => "treasure_room.proceed",
            "NEventRoom.OptionButtonClicked" => "event_option.choose",
            "RestSiteSynchronizer.ChooseLocalOption" => "rest_site.choose",
            "NRestSiteRoom.OnProceedButtonReleased" => "rest_site.proceed",
            "NPlayerHand.SelectCardInPlayMode" => "combat_hand_selector.select",
            "NPlayerHand.SelectCardInDiscardMode" => "combat_hand_selector.select",
            "NPlayerHand.SelectCardInUpgradeMode" => "combat_hand_selector.select",
            "NSelectedHandCardContainer.DeselectHolder" => "combat_hand_selector.deselect",
            "NPlayerHand.OnSelectModeConfirmButtonPressed" => "combat_hand_selector.confirm",
            "NMerchantRoom.OpenInventory" => "shop_room.open",
            "NMerchantRoom.HideScreen" => "shop_room.proceed",
            "NMerchantInventory.Close" => "shop_inventory.close",
            "MerchantEntry.OnTryPurchaseWrapper" => "shop_inventory.purchase",
            _ => null
        };
    }

    private static HumanActionOccurrenceEvidence GeneratedChoiceOccurrence(
        string nativeActionType,
        string verb,
        NChooseACardSelectionScreen screen,
        CardModel? card = null,
        NCardHolder? holder = null)
    {
        NativePlayerChoiceLineage lineage = NativePlayerChoiceLineage.Capture();
        return new HumanActionOccurrenceEvidence(
            $"human-occurrence-{Guid.NewGuid():N}",
            nativeActionType,
            "generated_card_choice",
            verb,
            card == null ? null : NativeWitnessIdentity.Get(card, "card"),
            holder == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["selected_card_holder"] = NativeWitnessIdentity.Get(holder, "card_holder")
                },
            NativeWitnessIdentity.Get(screen, "choice_owner"),
            lineage.ParentAction == null
                ? null
                : NativeWitnessIdentity.Get(lineage.ParentAction, "game_action"),
            lineage.ParentActionType,
            lineage.ParentState,
            nativeActionType,
            "failed_closed");
    }

    private static HumanActionOccurrenceEvidence OccurrenceFrom(
        string nativeActionType,
        ProcessLocalObservedAction observed,
        NativeWitnessEvidence witness) =>
        new(
            $"human-occurrence-{Guid.NewGuid():N}",
            nativeActionType,
            SupportedFamilyForNativeAction(nativeActionType) ?? nativeActionType,
            observed.Verb,
            witness.SubjectWitnessId,
            new Dictionary<string, string>(StringComparer.Ordinal),
            null,
            null,
            null,
            null,
            witness.NativeActionType,
            "failed_closed");

    private static HumanActionOccurrenceEvidence OccurrenceFrom(
        string nativeActionType,
        HumanActionContext context,
        ProcessLocalObservedAction? observed,
        NativeWitnessEvidence? witness)
    {
        if (context.Occurrence is { } occurrence)
            return occurrence;
        if (observed != null && witness != null)
            return OccurrenceFrom(nativeActionType, observed, witness);

        ProcessLocalObservedAction fallback = context.ExpectedAction
            ?? new ProcessLocalObservedAction(
                "accepted",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal));
        return new HumanActionOccurrenceEvidence(
            $"human-occurrence-{Guid.NewGuid():N}",
            nativeActionType,
            SupportedFamilyForNativeAction(nativeActionType) ?? nativeActionType,
            fallback.Verb,
            fallback.Subject == null
                ? null
                : NativeWitnessIdentity.Get(fallback.Subject, "subject"),
            new Dictionary<string, string>(StringComparer.Ordinal),
            null,
            null,
            null,
            null,
            witness?.NativeActionType ?? nativeActionType,
            "failed_closed");
    }

    private static HumanActionOccurrenceEvidence GameActionOccurrence(GameAction action) =>
        new(
            $"human-occurrence-{Guid.NewGuid():N}",
            action.GetType().Name,
            SupportedFamilyForNativeAction(action.GetType().Name) ?? action.GetType().Name,
            action.GetType().Name,
            NativeWitnessIdentity.Get(action, "game_action"),
            new Dictionary<string, string>(StringComparer.Ordinal),
            null,
            null,
            null,
            null,
            "GameAction.accepted",
            "failed_closed");

    private static void Quarantine(
        string reason,
        string detail,
        string? snapshotId,
        string? nativeActionType,
        string evidenceLevel,
        HumanActionOccurrenceEvidence? humanOccurrence = null)
    {
        try
        {
            _store?.AppendInvalidation(new InvalidationRecord(
                CurrentRecordingContract.SchemaVersion,
                CurrentRecordingContract.InvalidationSchema,
                $"invalidation-{Guid.NewGuid():N}",
                SessionId!,
                _currentRunId,
                DateTimeOffset.UtcNow,
                reason,
                detail,
                snapshotId,
                nativeActionType,
                evidenceLevel)
            {
                HumanOccurrence = humanOccurrence
            });
            AppendJournal("decision_invalidated", null, snapshotId, $"{reason}: {detail}");
            PublishApplicationEvent(
                RecordingEventKind.DecisionInvalidated,
                detail: $"{reason}: {detail}");
            _runtimeState = "quarantined";
            _detail = $"{reason}: {detail}";
            GD.PrintErr($"[STS2 Human Annotator] quarantined {reason}: {detail}");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[STS2 Human Annotator] failed to persist invalidation: {exception}");
        }
    }

    private static void WriteStatus(
        RecorderEnvironmentIdentity? environment,
        string? snapshotId,
        IReadOnlyList<string> blockers)
    {
        if (_configuration == null)
            return;
        try
        {
            string runtimeState;
            string? runtimeDetail;
            bool semanticBoundaryTraceHealthy;
            RecordingLifecycleSnapshot lifecycle;
            lock (Gate)
            {
                runtimeState = _runtimeState;
                runtimeDetail = _detail;
                semanticBoundaryTraceHealthy = _semanticBoundaryTraceHealthy;
                lifecycle = _lifecycle;
            }
            string status = RuntimeStatusForLifecycle(lifecycle.State, runtimeState);
            string? detail = lifecycle.State == RecordingLifecycleState.Recording
                ? runtimeDetail
                : lifecycle.Detail;
            IReadOnlyList<string> effectiveBlockers = blockers
                .Concat(LifecycleBlockers(lifecycle.State))
                .Concat(semanticBoundaryTraceHealthy
                    ? Array.Empty<string>()
                    : new[] { "semantic_boundary_trace_unavailable" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            lock (Gate)
            {
                _lastEnvironment = environment;
                _lastSnapshotId = snapshotId;
                _lastBlockers = effectiveBlockers;
            }
            RecordingStoreSnapshot storeStatus = _store?.GetSnapshot() ?? _lastStoreSnapshot;
            string health = string.Join('|', new[]
            {
                RequiredReadHealth(),
                semanticBoundaryTraceHealthy ? "semantic_boundary_trace_healthy" : "semantic_boundary_trace_unavailable",
                storeStatus.AppendHealth,
                storeStatus.DiskHealth,
                storeStatus.LastError ?? string.Empty
            });
            bool healthChanged;
            lock (Gate)
            {
                healthChanged = !string.Equals(_lastPublishedHealth, health, StringComparison.Ordinal);
                _lastPublishedHealth = health;
            }
            if (healthChanged)
                PublishApplicationEvent(RecordingEventKind.HealthChanged, detail: health);
            RecordingSessionStore.WriteRuntimeStatus(
                _configuration.RuntimeStatusPath,
                new RecorderRuntimeStatus(
                    CurrentRecordingContract.SchemaVersion,
                    CurrentRecordingContract.RuntimeStatusSchema,
                    status,
                    DateTimeOffset.UtcNow,
                    System.Environment.ProcessId,
                    SessionId ?? "none",
                    _store?.DirectoryPath ?? _configuration.RecordingRoot,
                    environment,
                    snapshotId,
                    null,
                    detail,
                    effectiveBlockers,
                    new[]
                    {
                        "status_is_not_human_validation",
                        "installed_is_not_loaded",
                        "loaded_is_not_recording_evidence"
                    }));
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[STS2 Human Annotator] failed to write runtime status: {exception.Message}");
        }
    }

    private static JsonNode ToNode<T>(T value) =>
        JsonSerializer.SerializeToNode(value, EvidenceJson.Options)
        ?? throw new InvalidOperationException("Evidence serialization returned null.");

    private static string ReadSourceRevision(Assembly assembly)
    {
        string componentRevision = ReadAssemblyMetadata(assembly, "AnnotatorSourceRevision");
        return componentRevision == "unavailable"
            ? ReadAssemblyMetadata(assembly, "SourceRevision")
            : componentRevision;
    }

    private static string ReadAssemblyMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value
        ?? "unavailable";

    private static bool IsCommit(string value) =>
        value.Length == 40 && value.All(Uri.IsHexDigit);

    private static void AppendJournal(
        string kind,
        string? recordId,
        string? snapshotId,
        string? detail)
    {
        if (_store == null)
            return;
        long sequence = Interlocked.Increment(ref _journalSequence);
        _store.AppendRunEvent(new RunJournalEvent(
            CurrentRecordingContract.SchemaVersion,
            CurrentRecordingContract.RunJournalSchema,
            $"event-{sequence:D8}-{Guid.NewGuid():N}",
            SessionId!,
            _currentRunId,
            TimelineId!,
            sequence,
            DateTimeOffset.UtcNow,
            kind,
            recordId,
            snapshotId,
            detail));
    }

    /// <summary>
    /// Records the exact native terminal seam.  RunManager.OnEnded is called
    /// by both the victory and defeat paths, so this marker proves only that
    /// STS2 reached its native terminal method; it does not synthesize a
    /// successor, settle pending roots, or infer a reward/map transition.
    /// </summary>
    internal static void ObserveNativeRunEnded(bool isVictory)
    {
        string detail = $"RunManager.OnEnded(isVictory={isVictory.ToString().ToLowerInvariant()})";
        lock (Gate)
        {
            if (_store == null || _nativeRunEndedObserved)
                return;
            _nativeRunEndedObserved = true;
            _nativeRunStartedObserved = false;
            _runActive = false;
            AppendJournal("run_ended_native", null, _lastSnapshotId, detail);
            _statusRefreshRequested = true;
        }
        PublishApplicationEvent(RecordingEventKind.RunEnded, detail: detail);
    }

    /// <summary>
    /// Records the exact native run-start seam. RunManager.Launch is invoked
    /// after STS2 has initialized the RunState and before the run scene enters
    /// its first act. It is the authority for a native start marker; the
    /// recorder never infers a native start from a status poll.
    /// </summary>
    internal static void ObserveNativeRunStarted()
    {
        lock (Gate)
        {
            if (_store == null || _nativeRunStartedObserved || _runActive)
                return;
            _runSequence++;
            _currentRunId = $"run-{_runSequence:D4}";
            _runActive = true;
            _nativeRunStartedObserved = true;
            _statusRefreshRequested = true;
            AppendJournal(
                "run_started_native",
                null,
                null,
                "RunManager.Launch completed with an initialized RunState.");
        }
        PublishApplicationEvent(RecordingEventKind.RunStarted);
    }

    /// <summary>
    /// The vote action's body calls ActChangeSynchronizer.OnPlayerReady. This
    /// is a typed owner-ready fact, not a GameAction.Finished substitute and
    /// not a claim that every vote caused a transition.
    /// </summary>
    internal static void ObserveNativeActChangeOwnerReady(
        string actionWitnessId,
        VoteToMoveToNextActAction action)
    {
        lock (Gate)
        {
            if (_store == null || !BoundaryTracker.Contains(actionWitnessId))
                return;
            AppendJournal(
                "act_change_owner_ready",
                null,
                _lastSnapshotId,
                $"{NativeActChangeDecisionProvider.OwnerReadySeam};action={NativeWitnessIdentity.Get(action, "game_action")};act={action.CurrentActIndex}");
        }
    }

    /// <summary>
    /// RunManager raises ActEntered after EnterAct has entered the new map
    /// room. The exact RunManager object carries the pending act root; this
    /// method never consumes an unrelated root or infers a successor act.
    /// </summary>
    private static void ObserveNativeActEntered()
    {
        string? actionWitnessId = NativeUiCompletionRootBindings.Take(RunManager.Instance);
        if (actionWitnessId == null || _store == null)
            return;
        try
        {
            ProcessLocalNativeWitnessFrame frame = CaptureSemanticFrame();
            SemanticBoundaryObservation boundary = CreateSemanticBoundaryObservation(
                frame,
                SemanticBoundaryWitnessKinds.NativeActEntered,
                null);
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
                drafts = BoundaryTracker.ObserveDecisionBoundaryForAction(
                    actionWitnessId,
                    boundary);
            PersistSemanticBoundaryDrafts(drafts);
            AppendJournal(
                "act_entered_native",
                null,
                frame.Snapshot.SnapshotId,
                $"RunManager.ActEntered;root={actionWitnessId}");
        }
        catch (Exception exception)
        {
            // The native event remains authoritative evidence, but an
            // incomplete post-entry read leaves the root explicitly pending
            // for closeout; no later frame is backfilled here.
            try
            {
                AppendJournal(
                    "act_entered_boundary_unavailable",
                    null,
                    _lastSnapshotId,
                    exception.Message);
            }
            catch (Exception journalException)
            {
                NativeUiObservationSafety.Report(
                    "act_entered.boundary_unavailable",
                    journalException);
            }
        }
    }

    private static void UpdateRunLifecycle()
    {
        bool inProgress = RunManager.Instance.IsInProgress;
        if (inProgress && !_runActive && !_nativeRunEndedObserved)
        {
            _runSequence++;
            _currentRunId = $"run-{_runSequence:D4}";
            _runActive = true;
            _nativeRunStartedObserved = false;
            _statusRefreshRequested = true;
            AppendJournal(
                "run_observed_in_progress",
                null,
                null,
                "Recorder observed an already-active run without a native start witness.");
            PublishApplicationEvent(RecordingEventKind.RunStarted);
        }
        else if (!inProgress && _runActive)
        {
            // This is only a lifecycle observation.  Without the native
            // OnEnded callback it is deliberately unproved and must not be
            // surfaced as RecordingEventKind.RunEnded.
            AppendJournal(
                "run_ended_unproved",
                null,
                null,
                "RunManager is no longer in progress without a native OnEnded witness.");
            _runActive = false;
            _statusRefreshRequested = true;
        }
        if (!inProgress)
        {
            _nativeRunStartedObserved = false;
            _nativeRunEndedObserved = false;
        }
    }
}
