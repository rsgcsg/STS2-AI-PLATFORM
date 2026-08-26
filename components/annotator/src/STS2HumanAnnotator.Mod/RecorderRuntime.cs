using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.PlayerEnvironment.Protocol;
using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;

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

    private sealed record PendingDecision(
        string RecordId,
        string RunId,
        long Sequence,
        HumanActionContext Context,
        RecorderEnvironmentIdentity Environment,
        FrozenDecisionFrameV2 Pre,
        NativeWitnessEvidence NativeWitness,
        ExactMappingEvidence Mapping,
        RecordedBoundAction Action,
        string? NativeActionWitnessId,
        GameAction? NativeAction,
        DateTimeOffset Deadline);

    private static readonly object Gate = new();
    private static AnnotatorConfiguration? _configuration;
    private static V2RecordingStore? _store;
    private static PendingDecision? _pending;
    private static readonly AcceptedHumanActionLedger NativeActionLedger = new();
    private static readonly SemanticBoundaryTracker BoundaryTracker = new();
    private static readonly Dictionary<GameAction, NativeActionLifecycleSubscription>
        NativeActionSubscriptions = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<string, PendingDecision> NativeActionEvidence =
        new(StringComparer.Ordinal);
    private static StagedCardFrame? _stagedCardFrame;
    private static long _sequence;
    private static long _journalSequence;
    private static long _nativeActionEventSequence;
    private static long _semanticBoundaryEventSequence;
    private static DateTimeOffset _lastIdleStatusAt;
    private static DateTimeOffset _lastFrameProbeAt;
    private static DateTimeOffset _lastSemanticBoundaryProbeAt;
    private static ActionExecutor? _observedActionExecutor;
    private static bool _semanticBoundaryTraceHealthy = true;
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
    private static int _runSequence;
    private static bool _runActive;
    private static string _currentRunId = "run-unassigned";
    private static readonly HumanCaptureProfile CaptureProfile =
        HumanCaptureProfiles.CombatReadRichV2;
    private static readonly string[] RequiredReadKinds = CaptureProfile.Reads
        .Where(read => read.Required)
        .Select(read => read.Kind)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    private static readonly string[] DeclaredOutOfScopeActionFamilies =
    {
        "ordinary_combat.use_potion",
        "navigation_and_non_combat",
        "selectors_other_than_native_generated_card_choice"
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
                _pending == null
                    ? null
                    : new RecordingPendingStatus(
                        _pending.RecordId,
                        _pending.RunId,
                        _pending.Deadline),
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
                    HasPendingRecordingWorkUnsafe());
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
                        finalizeClose = !HasPendingRecordingWorkUnsafe();
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
            FinalizeClose();
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
        var manifest = new RecordingManifestV2(
            HumanRecorderV2Contract.SchemaVersion,
            HumanRecorderV2Contract.ManifestSchema,
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
        V2RecordingStore store = V2RecordingStore.Create(
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
        _nativeActionEventSequence = 0;
        _semanticBoundaryEventSequence = 0;
        _runSequence = 0;
        _runActive = false;
        _currentRunId = "run-unassigned";
        _pending = null;
        ResetNativeActionTrackingUnsafe();
        _semanticBoundaryTraceHealthy = true;
        _stagedCardFrame = null;
        _lastStoreSnapshot = store.GetSnapshot();
        _requiredReadsHealth = "not_checked";
        _lastPublishedHealth = null;
        _closeout = RecordingCloseoutStatus.Idle;
        _lifecycle = lifecycle;
        _runtimeState = "waiting_for_player_environment";
        _detail = lifecycle.Detail;
        AppendJournal("session_started", null, null, "V2 read-rich recording session started.");
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
        string? detail = null)
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
            detail);
    }

    private static void FinalizeClose()
    {
        IReadOnlyList<SemanticBoundaryTraceDraft> closeDrafts;
        lock (Gate)
        {
            if (_lifecycle.State != RecordingLifecycleState.Closing
                || HasPendingRecordingWorkUnsafe())
                return;
            closeDrafts = BoundaryTracker.CloseUnknown("recording_closed_before_semantic_boundary");
        }
        try
        {
            PersistSemanticBoundaryDrafts(closeDrafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }

        RecordingStoreSnapshot snapshot;
        lock (Gate)
        {
            if (_lifecycle.State != RecordingLifecycleState.Closing
                || HasPendingRecordingWorkUnsafe())
                return;
            AppendJournal("session_closed", null, _lastSnapshotId, "Session flushed and closed.");
            V2RecordingStore? store = _store;
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

    private static bool HasPendingRecordingWorkUnsafe() =>
        _pending != null || NativeActionLedger.HasUnresolvedLifecycle;

    private static void ResetNativeActionTrackingUnsafe()
    {
        foreach (NativeActionLifecycleSubscription subscription in NativeActionSubscriptions.Values)
            subscription.Dispose();
        NativeActionSubscriptions.Clear();
        NativeActionEvidence.Clear();
        NativeActionLedger.Reset();
        BoundaryTracker.Reset();
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

    internal static void StageCardPlay(CardModel card)
    {
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
        if (target != null)
            arguments["target"] = target;
        return TryEnterScope(
            "native_card_play_ui",
            nameof(PlayCardAction),
            new ProcessLocalObservedAction("play", card, arguments),
            card);
    }

    internal static NativeUiScopeEntry TryEnterGeneratedChoiceCardScope(CardModel card) =>
        TryEnterScope(
            "native_generated_card_choice_ui",
            "NChooseACardSelectionScreen.SelectHolder",
            new ProcessLocalObservedAction(
                "select",
                card,
                new Dictionary<string, object>(StringComparer.Ordinal)));

    internal static NativeUiScopeEntry TryEnterGeneratedChoiceSkipScope() =>
        TryEnterScope(
            "native_generated_card_choice_skip_ui",
            "NChooseACardSelectionScreen.OnSkipButtonReleased",
            new ProcessLocalObservedAction(
                "skip",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)));

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
        CardModel? stagedCard = null)
    {
        if (!AcceptingNewWitnesses())
            return default;

        try
        {
            ProcessLocalNativeWitnessFrame current = CaptureReadRichFrame();
            RecorderEnvironmentIdentity currentEnvironment = BuildEnvironment(current);
            List<string> currentBlockers = EligibilityBlockers(
                current,
                currentEnvironment,
                requireReads: true);
            ProcessLocalNativeWitnessFrame? selected = null;

            if (currentBlockers.Count == 0
                && (expectedAction == null || IsExact(current.Resolve(expectedAction))))
            {
                selected = current;
            }

            if (selected == null && expectedAction != null && stagedCard != null)
            {
                StagedCardFrame? staged;
                lock (Gate)
                {
                    staged = _stagedCardFrame;
                    _stagedCardFrame = null;
                }
                if (staged != null
                    && ReferenceEquals(staged.Card, stagedCard)
                    && SameNativeCardPlayContext(
                        staged.Decision,
                        staged.StagedAt,
                        current,
                        currentEnvironment,
                        DateTimeOffset.UtcNow)
                    && IsExact(staged.Decision.Frame.Resolve(expectedAction)))
                {
                    selected = staged.Decision.Frame;
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
                    current.Snapshot.SnapshotId,
                    "fail_closed");
                return new NativeUiScopeEntry(false, true);
            }

            // The service gate is checked after capture as well as before it. A
            // concurrent pause or close must not admit a new witness, but the
            // native UI call continues because this scope is recorder-only.
            lock (Gate)
            {
                if (!_initialized
                    || _lifecycle.State != RecordingLifecycleState.Recording)
                    return default;
                HumanActionScope.Enter(origin, expectedNativeActionType, selected);
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
                "implemented_runtime_error");
            return new NativeUiScopeEntry(false, true);
        }
    }

    internal static void ExitNativeUiScope(NativeUiScopeEntry entry)
    {
        if (entry.Entered)
            HumanActionScope.Exit();
        if (entry.DeferredFailure)
            HumanActionScope.ExitDeferredFailure();
    }

    private static bool IsExact(ProcessLocalNativeMatch match) =>
        string.Equals(match.Status, "exact_unique", StringComparison.Ordinal)
        && match.MatchCount == 1
        && match.BoundAction != null;

    internal static void ObserveAcceptedAction(GameAction action)
    {
        HumanActionContext? context = HumanActionScope.Current;
        if (context == null)
        {
            TryQuarantineDeferredAcceptedAction(action.GetType().Name);
            return;
        }
        string nativeActionType = action.GetType().Name;
        if (!context.AcceptsRootAction(nativeActionType))
            return;
        try
        {
            if (!TryDescribeAction(action, context, out ProcessLocalObservedAction? observed, out NativeWitnessEvidence? witness))
                return;
            ProcessLocalNativeMatch match = context.Frame.Resolve(observed!);
            if (!IsExact(match) || !context.TryClaimRootAction(nativeActionType))
                return;
            StartPending(context, witness!, match, action);
        }
        catch (Exception exception)
        {
            Quarantine(
                "native_action_observation_failed",
                exception.Message,
                context.Frame.Snapshot.SnapshotId,
                action.GetType().FullName,
                "implemented_runtime_error");
        }
    }

    internal static void ObservePlayCardExecutionAborted(PlayCardAction action)
    {
        if (!_semanticBoundaryTraceHealthy)
            return;
        string actionWitnessId = NativeWitnessIdentity.Get(action, "game_action");
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
        NativeWitnessEvidence witness)
    {
        HumanActionContext? context = HumanActionScope.Current;
        if (context == null)
        {
            TryQuarantineDeferredAcceptedAction(nativeActionType);
            return;
        }
        if (!context.AcceptsRootAction(nativeActionType))
            return;
        try
        {
            ProcessLocalNativeMatch match = context.Frame.Resolve(observed);
            if (!IsExact(match) || !context.TryClaimRootAction(nativeActionType))
                return;
            StartPending(context, witness, match);
        }
        catch (Exception exception)
        {
            Quarantine(
                "native_ui_action_observation_failed",
                exception.Message,
                context.Frame.Snapshot.SnapshotId,
                nativeActionType,
                "implemented_runtime_error");
        }
    }

    private static void TryQuarantineDeferredAcceptedAction(string nativeActionType)
    {
        DeferredHumanActionFailure? failure = HumanActionScope.CurrentDeferredFailure;
        if (failure == null || !failure.TryClaim(nativeActionType))
            return;
        Quarantine(
            failure.ReasonCode,
            failure.Detail,
            failure.SnapshotId,
            nativeActionType,
            failure.EvidenceLevel);
    }

    internal static void OnProcessFrame()
    {
        try
        {
            if (_store != null)
                UpdateRunLifecycle();
            EnsureActionExecutorObservation();
            PendingDecision? pending;
            lock (Gate)
                pending = _pending;
            if (pending != null)
            {
                TrySettle(pending);
                TryObserveSemanticDecisionBoundary();
                return;
            }

            TryObserveSemanticDecisionBoundary();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastFrameProbeAt < TimeSpan.FromMilliseconds(50))
                return;
            _lastFrameProbeAt = now;
            ProcessLocalNativeWitnessFrame frame = PlayerEnvironmentNativeWitness.Capture();
            RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
            List<string> blockers = EligibilityBlockers(frame, environment, requireReads: false);
            bool recovered = false;
            if (blockers.Count == 0)
            {
                lock (Gate)
                    recovered = NativeActionLedger.ObserveRecoveryBoundary();
            }
            if (recovered)
            {
                AppendJournal(
                    "native_action_recovery_boundary",
                    null,
                    frame.Snapshot.SnapshotId,
                    "All rapid accepted actions reached terminal lifecycle before this complete interactive boundary.");
            }
            if (blockers.Any(blocker => !string.Equals(
                         blocker,
                         "pre_frame_not_complete_interactive",
                         StringComparison.Ordinal)))
            {
                lock (Gate)
                    _stagedCardFrame = null;
            }

            if (now - _lastIdleStatusAt < TimeSpan.FromSeconds(1))
                return;
            _lastIdleStatusAt = now;
            if (_store != null)
            {
                ProcessLocalNativeWitnessFrame readFrame = CaptureReadRichFrame();
                frame = readFrame;
                environment = BuildEnvironment(readFrame);
                blockers = EligibilityBlockers(readFrame, environment, requireReads: true);
                _requiredReadsHealth = HasRequiredReads(readFrame) ? "healthy" : "unavailable";
            }
            RecordingLifecycleSnapshot lifecycle = GetRecordingLifecycle();
            string status = lifecycle.State switch
            {
                RecordingLifecycleState.Ready => "ready",
                RecordingLifecycleState.Paused => "recording_paused",
                RecordingLifecycleState.Closing => "recording_closing",
                RecordingLifecycleState.Closed => "recording_closed",
                _ => blockers.Count == 0
                    ? "ready_for_human_action"
                    : "fail_closed"
            };
            _runtimeState = status;
            if (lifecycle.State == RecordingLifecycleState.Recording)
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
        if (!_semanticBoundaryTraceHealthy || _store == null)
            return;
        string actionWitnessId = NativeWitnessIdentity.Get(action, "game_action");
        lock (Gate)
        {
            if (!BoundaryTracker.Contains(actionWitnessId))
                return;
        }

        try
        {
            ProcessLocalNativeWitnessFrame frame = CaptureReadRichFrame();
            SemanticBoundaryObservation boundary = CreateSemanticBoundaryObservation(
                frame,
                "before_next_human_action_execution",
                actionWitnessId);
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

    private static void TryObserveSemanticDecisionBoundary()
    {
        if (!_semanticBoundaryTraceHealthy || _store == null)
            return;
        bool needsBoundary;
        lock (Gate)
            needsBoundary = BoundaryTracker.NeedsBoundaryObservation;
        if (!needsBoundary)
            return;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastSemanticBoundaryProbeAt < TimeSpan.FromMilliseconds(50))
            return;
        _lastSemanticBoundaryProbeAt = now;

        try
        {
            ProcessLocalNativeWitnessFrame frame = CaptureReadRichFrame();
            ObserveSemanticDecisionBoundary(frame, "complete_interactive_observation");
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static void ObserveSemanticDecisionBoundary(
        ProcessLocalNativeWitnessFrame frame,
        string witnessKind)
    {
        if (!_semanticBoundaryTraceHealthy)
            return;
        SemanticBoundaryObservation boundary = CreateSemanticBoundaryObservation(
            frame,
            witnessKind,
            null);
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
        string? immediatelyConsumedByActionWitnessId)
    {
        RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
        bool complete = EligibilityBlockers(
            frame,
            environment,
            requireReads: true,
            includeRecordingLifecycle: false).Count == 0;
        PlayerEnvironmentSnapshot snapshot = frame.Snapshot;
        return new SemanticBoundaryObservation(
            witnessKind,
            DateTimeOffset.UtcNow,
            snapshot.SnapshotId,
            snapshot.Status,
            snapshot.BoundActions.Status,
            snapshot.Interaction.InteractionId,
            snapshot.Interaction.Kind,
            complete ? FreezeSemanticBoundary(frame) : null,
            immediatelyConsumedByActionWitnessId);
    }

    private static FrozenDecisionFrameV2 FreezeSemanticBoundary(
        ProcessLocalNativeWitnessFrame frame)
    {
        PlayerEnvironmentSnapshot snapshot = frame.Snapshot;
        return new FrozenDecisionFrameV2(
            snapshot.SnapshotId,
            snapshot.Interaction.InteractionId,
            snapshot.Interaction.Kind,
            snapshot.Interaction.ContentSchema,
            EvidenceIdentity.Sha256Json(snapshot.BoundActions),
            snapshot.BoundActions.Actions.Count,
            ToNode(snapshot),
            Array.Empty<ReadEvidence>());
    }

    private static void ObserveSemanticAccepted(PendingDecision pending, GameAction action)
    {
        if (!_semanticBoundaryTraceHealthy)
            return;
        try
        {
            IReadOnlyList<SemanticBoundaryTraceDraft> drafts;
            lock (Gate)
            {
                drafts = BoundaryTracker.Accept(
                    new SemanticActionReference(
                        pending.NativeActionWitnessId!,
                        pending.Sequence,
                        pending.RecordId,
                        pending.RunId,
                        action.GetType().Name,
                        action.Id,
                        pending.Pre.SnapshotId),
                    pending.Pre);
            }
            PersistSemanticBoundaryDrafts(drafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static void ObserveSemanticUiAction(PendingDecision pending)
    {
        if (!_semanticBoundaryTraceHealthy)
            return;
        try
        {
            string actionWitnessId = $"ui_action_{pending.RecordId}";
            var boundary = new SemanticBoundaryObservation(
                "human_choice_decision_pre",
                DateTimeOffset.UtcNow,
                pending.Pre.SnapshotId,
                "interactive",
                "complete",
                pending.Pre.InteractionId,
                pending.Pre.InteractionKind,
                pending.Pre,
                actionWitnessId);
            var action = new SemanticActionReference(
                actionWitnessId,
                pending.Sequence,
                pending.RecordId,
                pending.RunId,
                pending.NativeWitness.NativeActionType,
                null,
                pending.Pre.SnapshotId);
            var drafts = new List<SemanticBoundaryTraceDraft>();
            lock (Gate)
            {
                drafts.AddRange(BoundaryTracker.Accept(action, pending.Pre));
                drafts.AddRange(BoundaryTracker.ObserveBeforeActionExecution(
                    actionWitnessId,
                    boundary));
                drafts.AddRange(BoundaryTracker.Started(actionWitnessId));
                drafts.AddRange(BoundaryTracker.Finished(actionWitnessId));
            }
            PersistSemanticBoundaryDrafts(drafts);
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }
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

    private static void PersistSemanticBoundaryDrafts(
        IReadOnlyList<SemanticBoundaryTraceDraft> drafts)
    {
        if (drafts.Count == 0 || !_semanticBoundaryTraceHealthy)
            return;
        V2RecordingStore store = _store
            ?? throw new InvalidOperationException("No open recording store for semantic boundary evidence.");
        foreach (SemanticBoundaryTraceDraft draft in drafts)
        {
            long sequence = Interlocked.Increment(ref _semanticBoundaryEventSequence);
            store.AppendSemanticBoundaryEvent(new SemanticBoundaryTraceEvent(
                SemanticBoundaryTraceContract.SchemaVersion,
                SemanticBoundaryTraceContract.EventSchema,
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
                draft.Boundary,
                draft.SemanticPre,
                draft.SemanticSuccessor,
                draft.Detail,
                draft.NonClaims ?? Array.Empty<string>()));
        }
    }

    private static void DisableSemanticBoundaryTrace(Exception exception)
    {
        _semanticBoundaryTraceHealthy = false;
        _runtimeState = "semantic_boundary_trace_unknown";
        _detail = exception.Message;
        GD.PrintErr($"[STS2 Human Annotator] semantic boundary trace disabled: {exception}");
    }

    private static bool TryDescribeAction(
        GameAction action,
        HumanActionContext context,
        out ProcessLocalObservedAction? observed,
        out NativeWitnessEvidence? witness)
    {
        observed = null;
        witness = null;
        if (action is PlayCardAction play)
        {
            object? card = play.NetCombatCard.ToCardModelOrNull();
            if (card == null)
            {
                Quarantine(
                    "play_card_native_subject_missing",
                    "The accepted PlayCardAction no longer resolved its exact card model.",
                    context.Frame.Snapshot.SnapshotId,
                    nameof(PlayCardAction),
                    "native_witness_missing");
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

        string type = action.GetType().FullName ?? action.GetType().Name;
        Quarantine(
            "native_scope_contract_error",
            $"The configured root action {type} has no recorder mapping.",
            context.Frame.Snapshot.SnapshotId,
            type,
            "implemented_runtime_error");
        return false;
    }

    private static void StartPending(
        HumanActionContext context,
        NativeWitnessEvidence witness,
        ProcessLocalNativeMatch match,
        GameAction? nativeAction = null)
    {
        RecorderEnvironmentIdentity environment = BuildEnvironment(context.Frame);
        List<string> blockers = EligibilityBlockers(
            context.Frame,
            environment,
            requireReads: true,
            includeRecordingLifecycle: false);
        if (blockers.Count > 0)
        {
            Quarantine(
                "action_not_eligible",
                string.Join(",", blockers),
                context.Frame.Snapshot.SnapshotId,
                witness.NativeActionType,
                "fail_closed");
            return;
        }

        FrozenDecisionFrameV2 pre = Freeze(context.Frame, "pre", environment);
        PlayerEnvironmentBoundAction bound = match.BoundAction!;
        long sequence = Interlocked.Increment(ref _sequence);
        string recordId = $"record-{sequence:D8}-{Guid.NewGuid():N}";
        string? actionWitnessId = nativeAction == null
            ? null
            : NativeWitnessIdentity.Get(nativeAction, "game_action");
        var pending = new PendingDecision(
            recordId,
            _currentRunId,
            sequence,
            context,
            environment,
            pre,
            witness,
            new ExactMappingEvidence(match.Status, match.MatchCount, match.Evidence, match.Detail),
            new RecordedBoundAction(
                bound.BoundActionId,
                bound.Verb,
                bound.SubjectReferentId,
                bound.Arguments.ToDictionary(
                    argument => argument.Role,
                    argument => argument.ReferentId,
                    StringComparer.Ordinal),
            bound.Label),
            actionWitnessId,
            nativeAction,
            DateTimeOffset.UtcNow + _configuration!.SuccessorTimeout);

        if (nativeAction == null)
        {
            StartUiPending(pending);
            return;
        }

        PendingDecision? displaced;
        AcceptedActionAdmission admission;
        lock (Gate)
        {
            displaced = _pending;
            admission = NativeActionLedger.Accept(
                actionWitnessId!,
                externalCausalEvidenceOpen:
                    displaced != null && displaced.NativeActionWitnessId == null);
            if (admission.Accounted)
            {
                var subscription = new NativeActionLifecycleSubscription(
                    nativeAction,
                    actionWitnessId!,
                    sequence,
                    recordId,
                    ObserveNativeActionLifecycle);
                NativeActionSubscriptions.Add(nativeAction, subscription);
                NativeActionEvidence.Add(actionWitnessId!, pending);
            }
            if (!admission.StrictTransitionEligible)
                _pending = null;
            else
                _pending = pending;
        }

        bool acceptedPersisted = AppendNativeActionEvent(
            pending,
            nativeAction,
            NativeActionLifecycleKinds.Accepted,
            admission.PriorOpenActionIds,
            admission.StrictTransitionEligible ? "strict_candidate" : "unproven_overlap",
            admission.FailureCode);
        AppendJournal(
            "human_action_accepted",
            recordId,
            context.Frame.Snapshot.SnapshotId,
            $"{witness.NativeActionType}:{actionWitnessId}");

        if (!acceptedPersisted)
        {
            InvalidateForNativeLifecyclePersistenceUnknown(
                pending,
                NativeActionLifecycleKinds.Accepted);
            return;
        }

        if (admission.Accounted)
            ObserveSemanticAccepted(pending, nativeAction);

        if (!admission.Accounted)
        {
            AppendNativeActionEvent(
                pending,
                nativeAction,
                NativeActionLifecycleKinds.StrictTransitionInvalidated,
                admission.PriorOpenActionIds,
                "unproven_ledger_capacity",
                admission.FailureCode);
            if (displaced != null)
                InvalidatePendingForOverlap(displaced, actionWitnessId!);
            Quarantine(
                admission.FailureCode!,
                "The bounded native action ledger could not retain another unresolved action; the accepted root is explicitly invalidated.",
                context.Frame.Snapshot.SnapshotId,
                witness.NativeActionType,
                "native_lifecycle_untracked");
            return;
        }

        if (displaced != null && !admission.StrictTransitionEligible)
        {
            InvalidatePendingForOverlap(displaced, actionWitnessId!);
        }
        if (!admission.StrictTransitionEligible)
        {
            AppendNativeActionEvent(
                pending,
                nativeAction,
                NativeActionLifecycleKinds.StrictTransitionInvalidated,
                admission.PriorOpenActionIds,
                "unproven_overlap",
                "A later accepted Human root exists before a causal successor boundary was proven.");
            Quarantine(
                "rapid_input_transition_unproven",
                "The accepted Human root is retained in the native ledger, but overlap prevents a strict V2 successor claim.",
                context.Frame.Snapshot.SnapshotId,
                witness.NativeActionType,
                "decision_and_lifecycle_only");
            return;
        }

        lock (Gate)
        {
            _runtimeState = "waiting_for_stable_successor";
            _detail = null;
            PublishApplicationEvent(
                RecordingEventKind.DecisionPending,
                recordId,
                witness.NativeActionType);
            WriteStatus(environment, context.Frame.Snapshot.SnapshotId, Array.Empty<string>());
        }
    }

    private static void StartUiPending(PendingDecision pending)
    {
        ObserveSemanticUiAction(pending);
        PendingDecision? displaced;
        bool ambiguous;
        lock (Gate)
        {
            displaced = _pending;
            ambiguous = displaced != null || NativeActionLedger.HasOpenEvidence;
            _pending = ambiguous ? null : pending;
        }
        AppendJournal(
            "human_action_accepted",
            pending.RecordId,
            pending.Pre.SnapshotId,
            pending.NativeWitness.NativeActionType);
        if (ambiguous)
        {
            if (displaced != null)
                InvalidatePendingForOverlap(displaced, "source-local-ui-action");
            Quarantine(
                "rapid_input_transition_unproven",
                "The accepted UI action overlaps an open causal evidence window and cannot form a strict V2 transition.",
                pending.Pre.SnapshotId,
                pending.NativeWitness.NativeActionType,
                "decision_only");
            return;
        }
        _runtimeState = "waiting_for_stable_successor";
        _detail = null;
        PublishApplicationEvent(
            RecordingEventKind.DecisionPending,
            pending.RecordId,
            pending.NativeWitness.NativeActionType);
        WriteStatus(pending.Environment, pending.Pre.SnapshotId, Array.Empty<string>());
    }

    private static void InvalidatePendingForOverlap(
        PendingDecision pending,
        string laterActionWitnessId)
    {
        if (pending.NativeActionWitnessId != null)
        {
            NativeActionLedger.InvalidateStrictTransition(pending.NativeActionWitnessId);
            if (pending.NativeAction != null)
            {
                AppendNativeActionEvent(
                    pending,
                    pending.NativeAction,
                    NativeActionLifecycleKinds.StrictTransitionInvalidated,
                    new[] { laterActionWitnessId },
                    "unproven_overlap",
                    "A later accepted Human root arrived before a causal successor boundary was proven.");
            }
        }
        Quarantine(
            "rapid_input_transition_unproven",
            $"A later accepted Human root {laterActionWitnessId} arrived before this action's causal successor boundary was proven.",
            pending.Pre.SnapshotId,
            pending.NativeWitness.NativeActionType,
            "decision_and_lifecycle_only");
    }

    private static void ObserveNativeActionLifecycle(
        NativeActionLifecycleSubscription subscription,
        string kind)
    {
        PendingDecision? cancelledPending = null;
        PendingDecision evidence;
        bool terminal = NativeActionLifecycleKinds.IsTerminal(kind);
        lock (Gate)
        {
            if (!NativeActionSubscriptions.TryGetValue(
                    subscription.Action,
                    out NativeActionLifecycleSubscription? current)
                || !ReferenceEquals(current, subscription))
                return;
            if (!NativeActionEvidence.TryGetValue(
                    subscription.ActionWitnessId,
                    out PendingDecision? found))
                return;
            evidence = found;
            if (terminal)
                NativeActionLedger.MarkTerminal(subscription.ActionWitnessId, kind);
            if (kind == NativeActionLifecycleKinds.Cancelled
                && _pending?.NativeActionWitnessId == subscription.ActionWitnessId)
            {
                cancelledPending = _pending;
                _pending = null;
                NativeActionLedger.InvalidateStrictTransition(subscription.ActionWitnessId);
            }
        }

        bool lifecyclePersisted = AppendNativeActionEvent(
            evidence,
            subscription.Action,
            kind,
            Array.Empty<string>(),
            kind == NativeActionLifecycleKinds.Cancelled
                ? "unproven_cancelled"
                : "lifecycle_observed",
            null);
        if (!lifecyclePersisted)
            InvalidateForNativeLifecyclePersistenceUnknown(evidence, kind);
        else
            ObserveSemanticLifecycle(subscription, kind);
        if (cancelledPending != null)
        {
            AppendNativeActionEvent(
                cancelledPending,
                subscription.Action,
                NativeActionLifecycleKinds.StrictTransitionInvalidated,
                Array.Empty<string>(),
                "unproven_cancelled",
                "STS2 cancelled the accepted GameAction before a strict successor was proven.");
            Quarantine(
                "native_action_cancelled",
                "STS2 cancelled the accepted GameAction; decision and lifecycle remain accounted without a strict V2 transition.",
                cancelledPending.Pre.SnapshotId,
                cancelledPending.NativeWitness.NativeActionType,
                "decision_and_lifecycle_only");
        }

        if (terminal)
        {
            lock (Gate)
            {
                NativeActionSubscriptions.Remove(subscription.Action);
                NativeActionEvidence.Remove(subscription.ActionWitnessId);
                subscription.Dispose();
            }
            FinalizeClose();
        }
    }

    private static bool AppendNativeActionEvent(
        PendingDecision pending,
        GameAction action,
        string kind,
        IReadOnlyList<string> priorOpenActionIds,
        string transitionEvidence,
        string? detail)
    {
        try
        {
            V2RecordingStore store = _store
                ?? throw new InvalidOperationException("No open recording store for native lifecycle evidence.");
            store.AppendNativeActionEvent(new NativeActionLedgerEvent(
                NativeActionLedgerContract.SchemaVersion,
                NativeActionLedgerContract.EventSchema,
                $"native-event-{Guid.NewGuid():N}",
                SessionId!,
                TimelineId!,
                pending.RunId,
                Interlocked.Increment(ref _nativeActionEventSequence),
                pending.NativeActionWitnessId!,
                pending.Sequence,
                pending.RecordId,
                DateTimeOffset.UtcNow,
                kind,
                action.GetType().Name,
                action.Id,
                action.State.ToString().ToLowerInvariant(),
                priorOpenActionIds,
                transitionEvidence,
                detail,
                kind == NativeActionLifecycleKinds.Accepted ? pending.Pre : null,
                kind == NativeActionLifecycleKinds.Accepted ? pending.NativeWitness : null,
                kind == NativeActionLifecycleKinds.Accepted ? pending.Mapping : null,
                kind == NativeActionLifecycleKinds.Accepted ? pending.Action : null));
            return true;
        }
        catch (Exception exception)
        {
            _runtimeState = "native_lifecycle_persistence_unknown";
            _detail = exception.Message;
            GD.PrintErr($"[STS2 Human Annotator] native lifecycle persistence failed: {exception}");
            return false;
        }
    }

    private static void InvalidateForNativeLifecyclePersistenceUnknown(
        PendingDecision pending,
        string failedKind)
    {
        lock (Gate)
        {
            if (pending.NativeActionWitnessId != null)
                NativeActionLedger.InvalidateStrictTransition(pending.NativeActionWitnessId);
            if (ReferenceEquals(_pending, pending))
                _pending = null;
        }
        Quarantine(
            "native_lifecycle_persistence_unknown",
            $"Persistence of native lifecycle event {failedKind} is unknown; strict transition admission is permanently disabled for this action.",
            pending.Pre.SnapshotId,
            pending.NativeWitness.NativeActionType,
            "evidence_commit_unknown");
        FinalizeClose();
    }

    private static void TrySettle(PendingDecision pending)
    {
        if (DateTimeOffset.UtcNow > pending.Deadline)
        {
            ClearPendingWithInvalidation(
                pending,
                "stable_successor_timeout",
                "No different complete interactive successor was observed before the bounded timeout.",
                "successor_missing");
            return;
        }

        if (pending.NativeActionWitnessId != null
            && !NativeActionLedger.CanAdmitStrictTransition(pending.NativeActionWitnessId))
            return;

        ProcessLocalNativeWitnessFrame successorFrame = CaptureReadRichFrame();
        if (!string.Equals(
                successorFrame.Capabilities.Host.RuntimeInstanceId,
                pending.Environment.RuntimeInstanceId,
                StringComparison.Ordinal)
            || !string.Equals(
                successorFrame.Capabilities.EnvironmentFingerprint,
                pending.Environment.EnvironmentFingerprint,
                StringComparison.Ordinal))
        {
            ClearPendingWithInvalidation(
                pending,
                "runtime_identity_changed",
                "Runtime or environment identity changed before successor settlement.",
                "environment_changed");
            return;
        }

        PlayerEnvironmentSnapshot successor = successorFrame.Snapshot;
        if (!string.Equals(successor.Status, "interactive", StringComparison.Ordinal)
            || !string.Equals(successor.BoundActions.Status, "complete", StringComparison.Ordinal)
            || string.Equals(successor.SnapshotId, pending.Pre.SnapshotId, StringComparison.Ordinal))
            return;

        if (!HasRequiredReads(successorFrame))
            return;

        try
        {
            ObserveSemanticDecisionBoundary(successorFrame, "legacy_v2_successor");
        }
        catch (Exception exception)
        {
            DisableSemanticBoundaryTrace(exception);
        }

        HumanDecisionRecordV2 record;
        try
        {
            record = new HumanDecisionRecordV2(
                HumanRecorderV2Contract.SchemaVersion,
                HumanRecorderV2Contract.RecordSchema,
                pending.RecordId,
                SessionId!,
                pending.RunId,
                TimelineId!,
                pending.Sequence,
                DateTimeOffset.UtcNow,
                pending.Environment,
                CaptureProfile.ProfileId,
                pending.Pre,
                pending.NativeWitness,
                pending.Mapping,
                pending.Action,
                new StableSuccessorV2(
                    successor.SnapshotId,
                    successor.Status,
                    successor.Interaction.InteractionId,
                    successor.Interaction.Kind,
                    successor.ObservedAt,
                    ToNode(successor),
                    PersistReads(successorFrame, "successor", pending.Environment)),
                DecisionFamily(pending.Pre.InteractionKind),
                pending.Pre.SurfaceSchema,
                new RecordEligibility(
                    "admitted",
                    new[]
                    {
                        "singleplayer",
                        "exact_artifact_identity",
                        "exact_recording_modset",
                        "complete_pre_catalog",
                        "native_ui_scope",
                        "exact_unique_reference_mapping",
                        "different_complete_interactive_successor"
                    },
                    new[]
                    {
                        "not_business_completion",
                        "not_human_validated_until_owner_review",
                        "capture_profile_scoped"
                    }));
            _store!.AppendDecision(record);
        }
        catch (Exception exception)
        {
            // A record append can fail after a blob write. Its delivery state is
            // therefore unknown; invalidate once and never retry it per frame.
            ClearPendingWithInvalidation(
                pending,
                "decision_persistence_unknown",
                exception.Message,
                "evidence_commit_unknown");
            return;
        }
        AppendJournal(
            "decision_recorded",
            record.RecordId,
            successor.SnapshotId,
            record.Action.Verb);
        if (pending.NativeActionWitnessId != null && pending.NativeAction != null)
        {
            bool dispositionPersisted = AppendNativeActionEvent(
                pending,
                pending.NativeAction,
                NativeActionLifecycleKinds.StrictTransitionAdmitted,
                Array.Empty<string>(),
                "strict_v2_admitted",
                successor.SnapshotId);
            if (dispositionPersisted)
                NativeActionLedger.CompleteStrictTransition(pending.NativeActionWitnessId);
            else
            {
                lock (Gate)
                {
                    NativeActionLedger.InvalidateStrictTransition(pending.NativeActionWitnessId);
                    if (ReferenceEquals(_pending, pending))
                        _pending = null;
                }
                WriteStatus(
                    pending.Environment,
                    successor.SnapshotId,
                    new[] { "native_lifecycle_persistence_unknown" });
                FinalizeClose();
                return;
            }
        }
        PublishApplicationEvent(
            RecordingEventKind.DecisionRecorded,
            record.RecordId,
            record.Action.Verb);
        lock (Gate)
        {
            if (ReferenceEquals(_pending, pending))
                _pending = null;
        }
        _runtimeState = "record_appended";
        _detail = record.RecordId;
        WriteStatus(pending.Environment, successor.SnapshotId, Array.Empty<string>());
        GD.Print($"[STS2 Human Annotator] admitted {record.RecordId} {record.Action.Verb}");
        FinalizeClose();
    }

    private static FrozenDecisionFrameV2 Freeze(
        ProcessLocalNativeWitnessFrame frame,
        string phase,
        RecorderEnvironmentIdentity environment)
    {
        PlayerEnvironmentSnapshot snapshot = frame.Snapshot;
        return new FrozenDecisionFrameV2(
            snapshot.SnapshotId,
            snapshot.Interaction.InteractionId,
            snapshot.Interaction.Kind,
            snapshot.Interaction.ContentSchema,
            EvidenceIdentity.Sha256Json(snapshot.BoundActions),
            snapshot.BoundActions.Actions.Count,
            ToNode(snapshot),
            PersistReads(frame, phase, environment));
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

    private static ProcessLocalNativeWitnessFrame CaptureReadRichFrame() =>
        PlayerEnvironmentNativeWitness.Capture(RequiredReadKinds);

    private static bool HasRequiredReads(ProcessLocalNativeWitnessFrame frame) =>
        RequiredReadKinds.All(kind => frame.Reads.TryGetValue(kind, out ProcessLocalReadCapture? read)
            && string.Equals(read.Status, "materialized", StringComparison.Ordinal)
            && read.Read != null);

    private static IReadOnlyList<ReadEvidence> PersistReads(
        ProcessLocalNativeWitnessFrame frame,
        string phase,
        RecorderEnvironmentIdentity environment)
    {
        if (_store == null)
            throw new InvalidOperationException("The V2 recording store is unavailable.");
        var result = new List<ReadEvidence>();
        foreach (CaptureReadRequirement requirement in CaptureProfile.Reads
                     .Where(read => string.Equals(read.Phase, phase, StringComparison.Ordinal))
                     .OrderBy(read => read.Kind, StringComparer.Ordinal))
        {
            if (!frame.Reads.TryGetValue(requirement.Kind, out ProcessLocalReadCapture? captured)
                || captured.Read == null)
            {
                result.Add(_store.PersistRead(new CapturedReadPayload(
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
                    captured?.Detail)));
                continue;
            }
            PlayerEnvironmentReadResponse read = captured.Read;
            result.Add(_store.PersistRead(new CapturedReadPayload(
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
                null)));
        }
        return result;
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

    private static bool SameNativeCardPlayContext(
        ExactDecisionFrame cached,
        DateTimeOffset stagedAt,
        ProcessLocalNativeWitnessFrame current,
        RecorderEnvironmentIdentity currentEnvironment,
        DateTimeOffset observedAt) =>
        StagedCardPlayGuard.IsContinuous(
            cached.Environment.RuntimeInstanceId,
            cached.Environment.EnvironmentFingerprint,
            cached.Frame.Snapshot.Interaction.InteractionId,
            cached.Frame.Snapshot.Sequence,
            stagedAt,
            currentEnvironment.RuntimeInstanceId,
            currentEnvironment.EnvironmentFingerprint,
            current.Snapshot.Interaction.InteractionId,
            current.Snapshot.Sequence,
            observedAt,
            current.ExternalControllerActive,
            TimeSpan.FromSeconds(30));

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

    private static string? SupportedFamilyForNativeAction(string nativeActionType) =>
        nativeActionType switch
        {
            nameof(PlayCardAction) => "ordinary_combat.play_card",
            nameof(EndPlayerTurnAction) => "ordinary_combat.end_turn",
            "NChooseACardSelectionScreen.SelectHolder" => "native_generated_card_choice.select",
            "NChooseACardSelectionScreen.OnSkipButtonReleased" => "native_generated_card_choice.skip",
            _ => null
        };

    private static string DecisionFamily(string interactionKind) =>
        interactionKind.StartsWith("combat", StringComparison.Ordinal)
            ? "ordinary_combat"
            : interactionKind;

    private static void ClearPendingWithInvalidation(
        PendingDecision pending,
        string reason,
        string detail,
        string evidenceLevel)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(_pending, pending))
                return;
            if (pending.NativeActionWitnessId != null)
                NativeActionLedger.InvalidateStrictTransition(pending.NativeActionWitnessId);
        }
        if (pending.NativeActionWitnessId != null && pending.NativeAction != null)
        {
            AppendNativeActionEvent(
                pending,
                pending.NativeAction,
                NativeActionLifecycleKinds.StrictTransitionInvalidated,
                Array.Empty<string>(),
                "unproven",
                $"{reason}: {detail}");
        }
        Quarantine(
            reason,
            detail,
            pending.Pre.SnapshotId,
            pending.NativeWitness.NativeActionType,
            evidenceLevel);
        lock (Gate)
        {
            if (ReferenceEquals(_pending, pending))
                _pending = null;
        }
        FinalizeClose();
    }

    private static void Quarantine(
        string reason,
        string detail,
        string? snapshotId,
        string? nativeActionType,
        string evidenceLevel)
    {
        try
        {
            _store?.AppendInvalidation(new InvalidationRecord(
                HumanRecorderV2Contract.SchemaVersion,
                HumanRecorderV2Contract.InvalidationSchema,
                $"invalidation-{Guid.NewGuid():N}",
                SessionId!,
                _currentRunId,
                DateTimeOffset.UtcNow,
                reason,
                detail,
                snapshotId,
                nativeActionType,
                evidenceLevel));
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
            string? pendingRecordId;
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
                pendingRecordId = _pending?.RecordId;
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
            V2RecordingStore.WriteRuntimeStatus(
                _configuration.RuntimeStatusPath,
                new RecorderRuntimeStatus(
                    HumanRecorderContract.SchemaVersion,
                    HumanRecorderContract.RuntimeStatusSchema,
                    status,
                    DateTimeOffset.UtcNow,
                    System.Environment.ProcessId,
                    SessionId ?? "none",
                    _store?.DirectoryPath ?? _configuration.RecordingRoot,
                    environment,
                    snapshotId,
                    pendingRecordId,
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
            HumanRecorderV2Contract.SchemaVersion,
            HumanRecorderV2Contract.RunJournalSchema,
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

    private static void UpdateRunLifecycle()
    {
        bool inProgress = RunManager.Instance.IsInProgress;
        if (inProgress && !_runActive)
        {
            _runSequence++;
            _currentRunId = $"run-{_runSequence:D4}";
            _runActive = true;
            AppendJournal("run_started", null, null, null);
            PublishApplicationEvent(RecordingEventKind.RunStarted);
        }
        else if (!inProgress && _runActive)
        {
            AppendJournal("run_ended", null, null, "RunManager is no longer in progress.");
            PublishApplicationEvent(
                RecordingEventKind.RunEnded,
                detail: "RunManager is no longer in progress.");
            _runActive = false;
        }
    }
}
