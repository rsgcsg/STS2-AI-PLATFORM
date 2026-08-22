using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.PlayerEnvironment.Protocol;
using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

internal static class RecorderRuntime
{
    private sealed record PendingDecision(
        string RecordId,
        string RunId,
        long Sequence,
        HumanActionContext Context,
        RecorderEnvironmentIdentity Environment,
        FrozenDecisionFrame Pre,
        NativeWitnessEvidence NativeWitness,
        ExactMappingEvidence Mapping,
        RecordedBoundAction Action,
        DateTimeOffset Deadline);

    private static readonly object Gate = new();
    private static AnnotatorConfiguration? _configuration;
    private static RecordingStore? _store;
    private static PendingDecision? _pending;
    private static long _sequence;
    private static DateTimeOffset _lastIdleStatusAt;
    private static string _runtimeState = "initializing";
    private static string? _detail;
    private static int _runSequence;
    private static bool _runActive;
    private static string _currentRunId = "run-unassigned";

    internal static string SessionId { get; private set; } = "uninitialized";

    internal static void Initialize(AnnotatorConfiguration configuration)
    {
        _configuration = configuration;
        SessionId = $"session-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}";
        Assembly assembly = typeof(RecorderMod).Assembly;
        string sourceRevision = ReadSourceRevision(assembly);
        var manifest = new RecordingManifest(
            HumanRecorderContract.SchemaVersion,
            HumanRecorderContract.ManifestSchema,
            SessionId,
            DateTimeOffset.UtcNow,
            RecorderMod.Version,
            sourceRevision,
            System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            new[] { "ordinary_combat.play_card", "ordinary_combat.end_turn" },
            new[]
            {
                "not_human_validated",
                "ordinary_combat_only",
                "potion_and_noncombat_actions_unsupported",
                "receipt_is_recording_evidence_not_gameplay_completion"
            });
        _store = RecordingStore.Create(configuration.RecordingRoot, manifest);
        _runtimeState = "waiting_for_player_environment";
        WriteStatus(null, null, new[] { "no_current_exact_frame" });
    }

    internal static bool TryEnterScope(string origin)
    {
        try
        {
            HumanActionScope.Enter(origin);
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

    internal static void ObserveAcceptedAction(GameAction action)
    {
        HumanActionContext? context = HumanActionScope.Current;
        if (context == null)
            return;
        try
        {
            if (!TryDescribeAction(action, context, out ProcessLocalObservedAction? observed, out NativeWitnessEvidence? witness))
                return;
            StartPending(context, observed!, witness!);
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

            if (DateTimeOffset.UtcNow - _lastIdleStatusAt < TimeSpan.FromSeconds(1))
                return;
            _lastIdleStatusAt = DateTimeOffset.UtcNow;
            ProcessLocalNativeWitnessFrame frame = PlayerEnvironmentNativeWitness.Capture();
            RecorderEnvironmentIdentity environment = BuildEnvironment(frame);
            string status = EligibilityBlockers(frame, environment).Count == 0
                ? "ready_for_human_action"
                : "fail_closed";
            _runtimeState = status;
            WriteStatus(environment, frame.Snapshot.SnapshotId, EligibilityBlockers(frame, environment));
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
            "unsupported_native_action_type",
            $"The current recorder scope does not admit {type}.",
            context.Frame.Snapshot.SnapshotId,
            type,
            "declared_unsupported");
        return false;
    }

    private static void StartPending(
        HumanActionContext context,
        ProcessLocalObservedAction observed,
        NativeWitnessEvidence witness)
    {
        RecorderEnvironmentIdentity environment = BuildEnvironment(context.Frame);
        List<string> blockers = EligibilityBlockers(context.Frame, environment);
        ProcessLocalNativeMatch match = context.Frame.Resolve(observed);
        if (!string.Equals(match.Status, "exact_unique", StringComparison.Ordinal)
            || match.MatchCount != 1
            || match.BoundAction == null)
        {
            blockers.Add($"mapping_{match.Status}");
        }
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
                Freeze(context.Frame.Snapshot),
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

        ProcessLocalNativeWitnessFrame successorFrame = PlayerEnvironmentNativeWitness.Capture();
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

        var record = new HumanDecisionRecord(
            HumanRecorderContract.SchemaVersion,
            HumanRecorderContract.RecordSchema,
            pending.RecordId,
            SessionId,
            pending.RunId,
            pending.Sequence,
            DateTimeOffset.UtcNow,
            pending.Environment,
            pending.Pre,
            pending.NativeWitness,
            pending.Mapping,
            pending.Action,
            new StableSuccessor(
                successor.SnapshotId,
                successor.Status,
                successor.Interaction.InteractionId,
                successor.Interaction.Kind,
                successor.ObservedAt,
                ToNode(successor)),
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
                    "ordinary_combat_only"
                }));
        _store!.AppendDecision(record);
        lock (Gate)
            _pending = null;
        _runtimeState = "record_appended";
        _detail = record.RecordId;
        WriteStatus(pending.Environment, successor.SnapshotId, Array.Empty<string>());
        GD.Print($"[STS2 Human Annotator] admitted {record.RecordId} {record.Action.Verb}");
    }

    private static FrozenDecisionFrame Freeze(PlayerEnvironmentSnapshot snapshot) => new(
        snapshot.SnapshotId,
        snapshot.Interaction.InteractionId,
        snapshot.Interaction.Kind,
        snapshot.Interaction.ContentSchema,
        EvidenceIdentity.Sha256Json(snapshot.BoundActions),
        snapshot.BoundActions.Actions.Count,
        ToNode(snapshot));

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

    private static List<string> EligibilityBlockers(
        ProcessLocalNativeWitnessFrame frame,
        RecorderEnvironmentIdentity environment)
    {
        var blockers = new List<string>();
        if (!RunManager.Instance.IsInProgress
            || RunManager.Instance.NetService?.Type != NetGameType.Singleplayer)
            blockers.Add("not_singleplayer_run");
        if (frame.ExternalControllerActive)
            blockers.Add("external_controller_active");
        if (!string.Equals(frame.Snapshot.Status, "interactive", StringComparison.Ordinal)
            || !string.Equals(frame.Snapshot.BoundActions.Status, "complete", StringComparison.Ordinal)
            || frame.Snapshot.BoundActions.Actions.Count == 0)
            blockers.Add("pre_frame_not_complete_interactive");
        if (!string.Equals(environment.ModsetStatus, "canary_exact_observer_modset", StringComparison.Ordinal))
            blockers.Add("exact_observer_modset_canary_missing");
        if (!IsCommit(environment.Connector.SourceRevision)
            || !IsCommit(environment.Annotator.SourceRevision))
            blockers.Add("source_revision_not_exact");
        return blockers;
    }

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
                HumanRecorderContract.SchemaVersion,
                HumanRecorderContract.InvalidationSchema,
                $"invalidation-{Guid.NewGuid():N}",
                SessionId,
                _currentRunId,
                DateTimeOffset.UtcNow,
                reason,
                detail,
                snapshotId,
                nativeActionType,
                evidenceLevel));
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
            _store.WriteRuntimeStatus(
                _configuration.RuntimeStatusPath,
                new RecorderRuntimeStatus(
                    HumanRecorderContract.SchemaVersion,
                    HumanRecorderContract.RuntimeStatusSchema,
                    _runtimeState,
                    DateTimeOffset.UtcNow,
                    System.Environment.ProcessId,
                    SessionId,
                    _store.DirectoryPath,
                    environment,
                    snapshotId,
                    _pending?.RecordId,
                    _detail,
                    blockers,
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

    private static string ReadSourceRevision(Assembly assembly) =>
        ReadAssemblyMetadata(assembly, "SourceRevision");

    private static string ReadAssemblyMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value
        ?? "unavailable";

    private static bool IsCommit(string value) =>
        value.Length == 40 && value.All(Uri.IsHexDigit);

    private static void UpdateRunLifecycle()
    {
        bool inProgress = RunManager.Instance.IsInProgress;
        if (inProgress && !_runActive)
        {
            _runSequence++;
            _currentRunId = $"run-{_runSequence:D4}";
            _runActive = true;
        }
        else if (!inProgress)
        {
            _runActive = false;
        }
    }
}
