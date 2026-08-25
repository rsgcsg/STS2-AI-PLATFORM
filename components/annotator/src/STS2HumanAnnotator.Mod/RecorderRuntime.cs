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
        DateTimeOffset Deadline);

    private static readonly object Gate = new();
    private static AnnotatorConfiguration? _configuration;
    private static V2RecordingStore? _store;
    private static PendingDecision? _pending;
    private static StagedCardFrame? _stagedCardFrame;
    private static long _sequence;
    private static long _journalSequence;
    private static DateTimeOffset _lastIdleStatusAt;
    private static DateTimeOffset _lastFrameProbeAt;
    private static string _runtimeState = "initializing";
    private static string? _detail;
    private static RecorderEnvironmentIdentity? _lastEnvironment;
    private static string? _lastSnapshotId;
    private static IReadOnlyList<string> _lastBlockers = Array.Empty<string>();
    private static RecordingControlSnapshot _recordingControl =
        RecordingControlSnapshot.Initial(DateTimeOffset.UnixEpoch);
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

    internal static string SessionId { get; private set; } = "uninitialized";

    internal static string TimelineId { get; private set; } = "uninitialized";

    internal static void Initialize(AnnotatorConfiguration configuration)
    {
        lock (Gate)
        {
            if (_initializationStarted)
                return;
            _initializationStarted = true;
        }

        _configuration = configuration;
        SessionId = $"session-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}";
        TimelineId = $"timeline-{Guid.NewGuid():N}";
        Assembly assembly = typeof(RecorderMod).Assembly;
        string sourceRevision = ReadSourceRevision(assembly);
        var manifest = new RecordingManifestV2(
            HumanRecorderV2Contract.SchemaVersion,
            HumanRecorderV2Contract.ManifestSchema,
            SessionId,
            TimelineId,
            DateTimeOffset.UtcNow,
            RecorderMod.Version,
            sourceRevision,
            System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            CaptureProfile.ProfileId,
            EvidenceIdentity.Sha256Json(CaptureProfile),
            CaptureProfile.SupportedActionFamilies,
            CaptureProfile.NonClaims.Append("not_human_validated").ToArray());
        _store = V2RecordingStore.Create(configuration.RecordingRoot, manifest, CaptureProfile);
        lock (Gate)
        {
            _recordingControl = RecordingControlStateMachine.Start(SessionId, DateTimeOffset.UtcNow);
            _initialized = true;
            _runtimeState = "waiting_for_player_environment";
            _detail = _recordingControl.Detail;
        }
        AppendJournal("session_started", null, null, "V2 read-rich recording initialized.");
        WriteStatus(null, null, new[] { "no_current_exact_frame" });
    }

    internal static RecordingControlSnapshot GetRecordingControlStatus()
    {
        lock (Gate)
            return _recordingControl;
    }

    internal static RecordingApplicationStatus GetRecordingApplicationStatus()
    {
        lock (Gate)
        {
            return new RecordingApplicationStatus(
                _recordingControl,
                _runtimeState,
                _detail ?? _recordingControl.Detail,
                _lastEnvironment,
                _lastSnapshotId,
                _lastBlockers.ToArray());
        }
    }

    internal static RecordingControlResult ApplyRecordingControl(RecordingControlCommand command)
    {
        RecordingControlResult result;
        RecorderEnvironmentIdentity? environment;
        string? snapshotId;
        IReadOnlyList<string> blockers;
        bool initialized;

        lock (Gate)
        {
            initialized = _initialized;
            if (!initialized)
            {
                result = new RecordingControlResult(
                    false,
                    "not_initialized",
                    "Recording controls are unavailable before the recorder runtime starts.",
                    _recordingControl);
                environment = _lastEnvironment;
                snapshotId = _lastSnapshotId;
                blockers = _lastBlockers;
            }
            else if (IsIdempotent(command, _recordingControl.State))
            {
                string state = _recordingControl.State.ToString().ToLowerInvariant();
                result = new RecordingControlResult(
                    true,
                    $"already_{state}",
                    $"Recording is already {state}.",
                    _recordingControl);
                environment = _lastEnvironment;
                snapshotId = _lastSnapshotId;
                blockers = _lastBlockers;
                _detail = result.Detail;
            }
            else
            {
                result = RecordingControlStateMachine.Apply(
                    _recordingControl,
                    command,
                    SessionId,
                    DateTimeOffset.UtcNow);
                if (result.Accepted)
                {
                    _recordingControl = result.Snapshot;
                    if (_recordingControl.State != RecordingControlState.Recording)
                        _stagedCardFrame = null;
                }
                _detail = result.Detail;
                _runtimeState = RuntimeStatusForControl(_recordingControl.State, _runtimeState);
                environment = _lastEnvironment;
                snapshotId = _lastSnapshotId;
                blockers = _lastBlockers;
            }
        }

        if (!initialized)
            return result;

        string journalKind = result.Accepted
            ? result.Code.StartsWith("already_", StringComparison.Ordinal)
                ? "recording_control_noop"
                : result.Snapshot.State switch
                {
                    RecordingControlState.Paused => "recording_paused",
                    RecordingControlState.Closed => "recording_closed",
                    RecordingControlState.Recording => "recording_resumed",
                    _ => "recording_control_changed"
                }
            : "recording_control_rejected";
        AppendJournal(journalKind, null, snapshotId, result.Detail);
        WriteStatus(
            environment,
            snapshotId,
            blockers.Concat(ControlBlockers(result.Snapshot.State)).Distinct(StringComparer.Ordinal).ToArray());
        return result;
    }

    private static bool IsIdempotent(
        RecordingControlCommand command,
        RecordingControlState state) =>
        command switch
        {
            RecordingControlCommand.Pause => state == RecordingControlState.Paused,
            RecordingControlCommand.Resume => state == RecordingControlState.Recording,
            RecordingControlCommand.Close => state == RecordingControlState.Closed,
            _ => false
        };

    private static IReadOnlyList<string> ControlBlockers(RecordingControlState state) =>
        state switch
        {
            RecordingControlState.Paused => new[] { "recording_paused" },
            RecordingControlState.Closed => new[] { "recording_closed" },
            _ => Array.Empty<string>()
        };

    private static string RuntimeStatusForControl(
        RecordingControlState state,
        string fallback) =>
        state switch
        {
            RecordingControlState.Paused => "recording_paused",
            RecordingControlState.Closed => "recording_closed",
            _ => fallback
        };

    private static bool AcceptingNewWitnesses()
    {
        lock (Gate)
            return _initialized && _recordingControl.State == RecordingControlState.Recording;
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

    internal static bool TryEnterCardScope(CardModel card, Creature? target)
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

    internal static bool TryEnterGeneratedChoiceCardScope(CardModel card) =>
        TryEnterScope(
            "native_generated_card_choice_ui",
            "NChooseACardSelectionScreen.SelectHolder",
            new ProcessLocalObservedAction(
                "select",
                card,
                new Dictionary<string, object>(StringComparer.Ordinal)));

    internal static bool TryEnterGeneratedChoiceSkipScope() =>
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

    internal static bool TryEnterScope(
        string origin,
        string expectedNativeActionType,
        ProcessLocalObservedAction? expectedAction = null,
        CardModel? stagedCard = null)
    {
        if (!AcceptingNewWitnesses())
            return false;

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
                    && DateTimeOffset.UtcNow - staged.StagedAt <= TimeSpan.FromSeconds(30)
                    && SameDecisionContext(staged.Decision, current, currentEnvironment)
                    && IsExact(staged.Decision.Frame.Resolve(expectedAction)))
                {
                    selected = staged.Decision.Frame;
                }
            }

            if (selected == null)
            {
                Quarantine(
                    "pre_frame_capture_failed",
                    string.Join(",", currentBlockers.Count == 0
                        ? new[] { "no_same_context_authoritative_frame" }
                        : currentBlockers.Append("no_same_context_authoritative_frame")),
                    current.Snapshot.SnapshotId,
                    expectedNativeActionType,
                    "fail_closed");
                return false;
            }

            // The service gate is checked after capture as well as before it. A
            // concurrent pause or close must not admit a new witness, but the
            // native UI call continues because this scope is recorder-only.
            lock (Gate)
            {
                if (!_initialized
                    || _recordingControl.State != RecordingControlState.Recording)
                    return false;
                HumanActionScope.Enter(origin, expectedNativeActionType, selected);
            }
            return true;
        }
        catch (Exception exception)
        {
            Quarantine(
                "pre_frame_capture_failed",
                exception.Message,
                null,
                origin,
                "implemented_runtime_error");
            return false;
        }
    }

    private static bool IsExact(ProcessLocalNativeMatch match) =>
        string.Equals(match.Status, "exact_unique", StringComparison.Ordinal)
        && match.MatchCount == 1
        && match.BoundAction != null;

    internal static void ObserveAcceptedAction(GameAction action)
    {
        HumanActionContext? context = HumanActionScope.Current;
        if (context == null)
            return;
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
            StartPending(context, witness!, match);
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

    internal static void ObserveAcceptedUiAction(
        string nativeActionType,
        ProcessLocalObservedAction observed,
        NativeWitnessEvidence witness)
    {
        HumanActionContext? context = HumanActionScope.Current;
        if (context == null || !context.AcceptsRootAction(nativeActionType))
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

    internal static void OnProcessFrame()
    {
        try
        {
            UpdateRunLifecycle();
            PendingDecision? pending;
            lock (Gate)
                pending = _pending;
            if (pending != null)
            {
                TrySettle(pending);
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastFrameProbeAt < TimeSpan.FromMilliseconds(50))
                return;
            _lastFrameProbeAt = now;
            ProcessLocalNativeWitnessFrame frame = PlayerEnvironmentNativeWitness.Capture();
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

            if (now - _lastIdleStatusAt < TimeSpan.FromSeconds(1))
                return;
            _lastIdleStatusAt = now;
            RecordingControlSnapshot control = GetRecordingControlStatus();
            string status = control.State switch
            {
                RecordingControlState.Paused => "recording_paused",
                RecordingControlState.Closed => "recording_closed",
                _ => blockers.Count == 0
                    ? "ready_for_human_action"
                    : "fail_closed"
            };
            _runtimeState = status;
            if (control.State == RecordingControlState.Recording)
                _detail = null;
            WriteStatus(
                environment,
                frame.Snapshot.SnapshotId,
                blockers.Concat(ControlBlockers(control.State)).Distinct(StringComparer.Ordinal).ToArray());
        }
        catch (Exception exception)
        {
            _runtimeState = "observer_error";
            _detail = exception.Message;
            WriteStatus(null, null, new[] { "player_environment_capture_failed" });
        }
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
        ProcessLocalNativeMatch match)
    {
        RecorderEnvironmentIdentity environment = BuildEnvironment(context.Frame);
        List<string> blockers = EligibilityBlockers(
            context.Frame,
            environment,
            requireReads: true,
            includeRecordingControl: false);
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
        lock (Gate)
        {
            if (_pending != null)
            {
                Quarantine(
                    "overlapping_action_before_successor",
                    "A second accepted native action arrived before the previous stable successor.",
                    context.Frame.Snapshot.SnapshotId,
                    witness.NativeActionType,
                    "lifecycle_ambiguous");
                return;
            }

            PlayerEnvironmentBoundAction bound = match.BoundAction!;
            long sequence = Interlocked.Increment(ref _sequence);
            string recordId = $"record-{sequence:D8}-{Guid.NewGuid():N}";
            _pending = new PendingDecision(
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
                DateTimeOffset.UtcNow + _configuration!.SuccessorTimeout);
            _runtimeState = "waiting_for_stable_successor";
            _detail = null;
            AppendJournal(
                "human_action_accepted",
                recordId,
                context.Frame.Snapshot.SnapshotId,
                witness.NativeActionType);
            WriteStatus(environment, context.Frame.Snapshot.SnapshotId, Array.Empty<string>());
        }
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

        var record = new HumanDecisionRecordV2(
            HumanRecorderV2Contract.SchemaVersion,
            HumanRecorderV2Contract.RecordSchema,
            pending.RecordId,
            SessionId,
            pending.RunId,
            TimelineId,
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
                    "exact_observer_modset_canary",
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
        lock (Gate)
            _pending = null;
        AppendJournal(
            "decision_recorded",
            record.RecordId,
            successor.SnapshotId,
            record.Action.Verb);
        _runtimeState = "record_appended";
        _detail = record.RecordId;
        WriteStatus(pending.Environment, successor.SnapshotId, Array.Empty<string>());
        GD.Print($"[STS2 Human Annotator] admitted {record.RecordId} {record.Action.Verb}");
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
        bool includeRecordingControl = true)
    {
        var blockers = new List<string>();
        RecordingControlState controlState = GetRecordingControlStatus().State;
        if (includeRecordingControl && controlState != RecordingControlState.Recording)
            blockers.AddRange(ControlBlockers(controlState));
        if (!RunManager.Instance.IsInProgress
            || RunManager.Instance.NetService?.Type != NetGameType.Singleplayer)
            blockers.Add("not_singleplayer_run");
        if (frame.ExternalControllerActive)
            blockers.Add("external_controller_active");
        if (!string.Equals(frame.Snapshot.Status, "interactive", StringComparison.Ordinal)
            || !string.Equals(frame.Snapshot.BoundActions.Status, "complete", StringComparison.Ordinal)
            || frame.Snapshot.BoundActions.Actions.Count == 0)
            blockers.Add("pre_frame_not_complete_interactive");
        if (!string.Equals(environment.ModsetStatus, "exact_platform_modset", StringComparison.Ordinal)
            && !string.Equals(environment.ModsetStatus, "canary_exact_observer_modset", StringComparison.Ordinal))
            blockers.Add("exact_recording_modset_missing");
        if (!IsCommit(environment.Connector.SourceRevision)
            || !IsCommit(environment.Annotator.SourceRevision))
            blockers.Add("source_revision_not_exact");
        if (requireReads && !HasRequiredReads(frame))
            blockers.Add("required_read_evidence_unavailable");
        return blockers;
    }

    private static bool SameDecisionContext(
        ExactDecisionFrame cached,
        ProcessLocalNativeWitnessFrame current,
        RecorderEnvironmentIdentity currentEnvironment) =>
        string.Equals(
            cached.Environment.RuntimeInstanceId,
            currentEnvironment.RuntimeInstanceId,
            StringComparison.Ordinal)
        && string.Equals(
            cached.Environment.EnvironmentFingerprint,
            currentEnvironment.EnvironmentFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            cached.Frame.Snapshot.Interaction.InteractionId,
            current.Snapshot.Interaction.InteractionId,
            StringComparison.Ordinal)
        && string.Equals(
            cached.Frame.Snapshot.SnapshotId,
            current.Snapshot.SnapshotId,
            StringComparison.Ordinal)
        && cached.Frame.Snapshot.Sequence == current.Snapshot.Sequence
        && cached.Frame.Snapshot.BoundActions.Actions
            .Select(action => action.BoundActionId)
            .SequenceEqual(
                current.Snapshot.BoundActions.Actions.Select(action => action.BoundActionId),
                StringComparer.Ordinal)
        && !current.ExternalControllerActive;

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
            _pending = null;
        }
        Quarantine(
            reason,
            detail,
            pending.Pre.SnapshotId,
            pending.NativeWitness.NativeActionType,
            evidenceLevel);
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
                SessionId,
                _currentRunId,
                DateTimeOffset.UtcNow,
                reason,
                detail,
                snapshotId,
                nativeActionType,
                evidenceLevel));
            AppendJournal("decision_invalidated", null, snapshotId, $"{reason}: {detail}");
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
        if (_store == null || _configuration == null)
            return;
        try
        {
            string? pendingRecordId;
            string runtimeState;
            string? runtimeDetail;
            RecordingControlSnapshot control;
            lock (Gate)
            {
                runtimeState = _runtimeState;
                runtimeDetail = _detail;
                control = _recordingControl;
                pendingRecordId = _pending?.RecordId;
            }
            string status = RuntimeStatusForControl(control.State, runtimeState);
            string? detail = control.State == RecordingControlState.Recording
                ? runtimeDetail
                : control.Detail;
            IReadOnlyList<string> effectiveBlockers = blockers
                .Concat(ControlBlockers(control.State))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            lock (Gate)
            {
                _lastEnvironment = environment;
                _lastSnapshotId = snapshotId;
                _lastBlockers = effectiveBlockers;
            }
            _store.WriteRuntimeStatus(
                _configuration.RuntimeStatusPath,
                new RecorderRuntimeStatus(
                    HumanRecorderContract.SchemaVersion,
                    HumanRecorderContract.RuntimeStatusSchema,
                    status,
                    DateTimeOffset.UtcNow,
                    System.Environment.ProcessId,
                    SessionId,
                    _store.DirectoryPath,
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
            SessionId,
            _currentRunId,
            TimelineId,
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
        }
        else if (!inProgress && _runActive)
        {
            AppendJournal("run_ended", null, null, "RunManager is no longer in progress.");
            _runActive = false;
        }
    }
}
