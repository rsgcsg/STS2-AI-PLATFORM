using MegaCrit.Sts2.Core.GameActions;
using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

/// <summary>
/// Additive read-only runtime experiment. Failures here never affect gameplay,
/// Human action admission, or the canonical Decision V2/timeline streams.
/// </summary>
internal static class NativeSemanticDiscriminatorRuntime
{
    private static readonly object Gate = new();
    private static readonly HashSet<GameAction> TrackedActions =
        new(ReferenceEqualityComparer.Instance);
    private static long _sequence;

    internal static void Reset()
    {
        lock (Gate)
            TrackedActions.Clear();
        Interlocked.Exchange(ref _sequence, 0);
    }

    internal static void Observe(
        V2RecordingStore? store,
        string? sessionId,
        string? timelineId,
        string runId,
        string phase,
        GameAction action,
        ProcessLocalNativeWitnessFrame? uiFrame = null,
        string? relatedActionWitnessId = null,
        bool capture = true,
        string? detail = null)
    {
        if (store == null || sessionId == null || timelineId == null)
            return;
        bool terminal = phase is NativeActionLifecycleKinds.Cancelled
            or NativeActionLifecycleKinds.Finished;
        lock (Gate)
        {
            if (phase == NativeActionLifecycleKinds.Accepted)
                TrackedActions.Add(action);
            else if (!TrackedActions.Contains(action))
                return;
        }
        try
        {
            ProcessLocalNativeSemanticCapture? value = capture
                ? PlayerEnvironmentNativeSemanticWitness.Capture(phase, action, uiFrame)
                : null;
            string actionWitnessId = NativeWitnessIdentity.Get(action, "game_action");
            store.AppendNativeSemanticDiscriminatorEvent(CreateEvent(
                sessionId,
                timelineId,
                runId,
                phase,
                actionWitnessId,
                action.GetType().Name,
                action.Id,
                action.State.ToString().ToLowerInvariant(),
                value,
                relatedActionWitnessId,
                detail));
        }
        catch (Exception exception)
        {
            // The discriminator is an experiment, not an evidence gate.
            Godot.GD.PrintErr(
                $"[STS2 Human Annotator] native semantic discriminator observation failed: {exception}");
        }
        finally
        {
            if (terminal)
            {
                lock (Gate)
                    TrackedActions.Remove(action);
            }
        }
    }

    internal static void ObserveDirectCommit(
        V2RecordingStore? store,
        string? sessionId,
        string? timelineId,
        string runId,
        string actionWitnessId,
        string nativeActionType,
        ProcessLocalNativeWitnessFrame frame,
        string? parentActionWitnessId)
    {
        if (store == null || sessionId == null || timelineId == null)
            return;
        try
        {
            ProcessLocalNativeSemanticCapture value =
                PlayerEnvironmentNativeSemanticWitness.Capture(
                    "player_choice_commit",
                    observedAction: null,
                    uiFrame: frame);
            store.AppendNativeSemanticDiscriminatorEvent(CreateEvent(
                sessionId,
                timelineId,
                runId,
                "player_choice_commit",
                actionWitnessId,
                nativeActionType,
                null,
                "direct_ui_commit",
                value,
                parentActionWitnessId,
                "The selector commit is a typed direct UI action within the parent GameAction lineage."));
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"[STS2 Human Annotator] native semantic discriminator direct commit failed: {exception}");
        }
    }

    internal static void ObserveLifecycleOnly(
        V2RecordingStore? store,
        string? sessionId,
        string? timelineId,
        string runId,
        string phase,
        GameAction action) =>
        Observe(
            store,
            sessionId,
            timelineId,
            runId,
            phase,
            action,
            capture: false,
            detail: NativeSemanticDiscriminatorContract.LifecycleOnlyDetail);

    private static NativeSemanticDiscriminatorEvent CreateEvent(
        string sessionId,
        string timelineId,
        string runId,
        string phase,
        string actionWitnessId,
        string nativeActionType,
        uint? nativeQueueId,
        string nativeState,
        ProcessLocalNativeSemanticCapture? capture,
        string? relatedActionWitnessId,
        string? detail)
    {
        ProcessLocalUiCatalogObservation? ui = capture?.UiCatalog;
        return new NativeSemanticDiscriminatorEvent(
            NativeSemanticDiscriminatorContract.SchemaVersion,
            NativeSemanticDiscriminatorContract.EventSchema,
            $"native-semantic-event-{Guid.NewGuid():N}",
            sessionId,
            timelineId,
            runId,
            Interlocked.Increment(ref _sequence),
            DateTimeOffset.UtcNow,
            phase,
            actionWitnessId,
            nativeActionType,
            nativeQueueId,
            nativeState,
            capture?.Status ?? "not_sampled",
            capture?.Scope ?? "not_sampled",
            capture?.SemanticStateDigest,
            capture?.SemanticState?.DeepClone(),
            capture?.SemanticCatalogDigest ?? "not_sampled",
            capture?.SemanticActions.Select(action => action.Key).ToArray()
                ?? Array.Empty<string>(),
            capture?.ObservedAction?.Key,
            capture?.ObservedAction?.Membership,
            capture?.ObservedAction?.SemanticMatchCount,
            ui?.SnapshotId ?? "not_sampled",
            ui?.SnapshotStatus ?? "not_sampled",
            ui?.InteractionKind ?? "not_sampled",
            ui?.BoundActionsStatus ?? "not_sampled",
            ui?.ActionCount ?? 0,
            ui?.CatalogDigest ?? "not_sampled",
            ui?.ObservedMembership,
            ui?.ObservedMatchCount,
            relatedActionWitnessId,
            detail ?? capture?.Detail,
            capture?.NonClaims ?? new[] { "capture_not_sampled_for_lifecycle_only_event" });
    }
}
