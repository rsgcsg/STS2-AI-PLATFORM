using System.Text.Json;
using System.Text.Json.Nodes;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class SemanticBoundaryTrackerTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-08-26T00:00:00Z");

    [Fact]
    public void RapidA1A2A3UsesBeforeExecutionBoundariesWithoutFalseAttribution()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("a1", 1), State("s0"));
        tracker.Accept(Action("a2", 2), State("human-transient-a2"));
        tracker.Accept(Action("a3", 3), State("human-transient-a3"));

        tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");
        SemanticBoundaryTraceDraft a1 = Assert.Single(
            tracker.ObserveBeforeActionExecution("a2", Boundary("s1", "a2")),
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
        tracker.Started("a2");
        tracker.Finished("a2");
        SemanticBoundaryTraceDraft a2 = Assert.Single(
            tracker.ObserveBeforeActionExecution("a3", Boundary("s2", "a3")),
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
        tracker.Started("a3");
        tracker.Finished("a3");
        SemanticBoundaryTraceDraft a3 = Assert.Single(
            tracker.ObserveDecisionBoundary(Boundary("s3")));

        Assert.Equal(("s0", "s1"), TransitionIds(a1));
        Assert.Equal(("s1", "s2"), TransitionIds(a2));
        Assert.Equal(("s2", "s3"), TransitionIds(a3));
        Assert.Equal("a2", a1.RelatedActionWitnessId);
        Assert.Equal("a3", a2.RelatedActionWitnessId);
    }

    [Fact]
    public void RapidLethalKeepsCancelledPrecommitButOnlySettlesExecutedAction()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("lethal", 1), State("combat-before"));
        tracker.Accept(Action("queued", 2), State("human-queued"));
        tracker.ObserveBeforeActionExecution("lethal", Boundary("combat-before", "lethal"));
        tracker.Started("lethal");
        tracker.Finished("lethal");

        SemanticBoundaryTraceDraft cancelled = Assert.Single(tracker.Cancelled("queued"));
        SemanticBoundaryTraceDraft terminal = Assert.Single(
            tracker.ObserveDecisionBoundary(Boundary("reward-after-lethal")));

        Assert.Equal(SemanticBoundaryTraceKinds.ActionCancelledBeforeStart, cancelled.Kind);
        Assert.Equal("not_a_successful_action", cancelled.ProofStatus);
        Assert.Equal(("combat-before", "reward-after-lethal"), TransitionIds(terminal));
    }

    [Fact]
    public void CancellationAfterStartNeverProducesSuccessfulTransition()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("cancelled", 1), State("s0"));
        tracker.ObserveBeforeActionExecution("cancelled", Boundary("s0", "cancelled"));
        tracker.Started("cancelled");

        SemanticBoundaryTraceDraft cancelled = Assert.Single(tracker.Cancelled("cancelled"));

        Assert.Equal(SemanticBoundaryTraceKinds.ActionCancelledAfterStart, cancelled.Kind);
        Assert.Equal("transition_unknown", cancelled.ProofStatus);
        Assert.Empty(tracker.ObserveDecisionBoundary(Boundary("later")));
    }

    [Fact]
    public void PlayCardAbortBeforeNativeCommitIsNotASuccessfulAction()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("stale-card", 1), State("s0"));
        tracker.ObserveBeforeActionExecution("stale-card", Boundary("s0", "stale-card"));
        tracker.Started("stale-card");

        SemanticBoundaryTraceDraft aborted = Assert.Single(
            tracker.AbortedBeforeCommit("stale-card"));
        SemanticBoundaryTraceDraft finished = Assert.Single(tracker.Finished("stale-card"));

        Assert.Equal(SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit, aborted.Kind);
        Assert.Equal("not_a_successful_action", aborted.ProofStatus);
        Assert.Equal("lifecycle_finished_after_semantic_disposition", finished.ProofStatus);
        Assert.Empty(tracker.ObserveDecisionBoundary(Boundary("later")));
    }

    [Fact]
    public void PlayerChoicePauseIsARealSuccessorBoundaryBeforeParentFinishes()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("choice-parent", 1), State("combat-before"));
        tracker.ObserveBeforeActionExecution(
            "choice-parent",
            Boundary("combat-before", "choice-parent"));
        tracker.Started("choice-parent");
        tracker.PausedForPlayerChoice("choice-parent");

        SemanticBoundaryTraceDraft proved = Assert.Single(
            tracker.ObserveDecisionBoundary(Boundary("card-choice")));
        tracker.ReadyToResume("choice-parent");
        tracker.Resumed("choice-parent");
        SemanticBoundaryTraceDraft finished = Assert.Single(tracker.Finished("choice-parent"));

        Assert.Equal("proved_player_choice_boundary", proved.ProofStatus);
        Assert.Equal(("combat-before", "card-choice"), TransitionIds(proved));
        Assert.Equal("lifecycle_finished_after_semantic_disposition", finished.ProofStatus);
        Assert.Empty(tracker.ObserveDecisionBoundary(Boundary("combat-after-choice")));
    }

    [Fact]
    public void IncompleteCaptureBeforeNextActionFailsClosedAndDoesNotUseLaterState()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("a1", 1), State("s0"));
        tracker.Accept(Action("a2", 2), State("human-a2"));
        tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");

        SemanticBoundaryTraceDraft unknown = Assert.Single(
            tracker.ObserveBeforeActionExecution("a2", IncompleteBoundary("settling", "a2")),
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionUnknown);
        tracker.Started("a2");
        tracker.Finished("a2");
        SemanticBoundaryTraceDraft a2Unknown = Assert.Single(
            tracker.ObserveDecisionBoundary(Boundary("s2")));

        Assert.Equal("boundary_incomplete_before_next_action", unknown.ProofStatus);
        Assert.Null(unknown.SemanticSuccessor);
        Assert.Equal("semantic_pre_unknown", a2Unknown.ProofStatus);
    }

    [Fact]
    public void SameSnapshotBoundaryIsUnknownInsteadOfAnInvalidProof()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("a1", 1), State("same"));
        tracker.ObserveBeforeActionExecution("a1", Boundary("same", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");

        SemanticBoundaryTraceDraft result = Assert.Single(
            tracker.ObserveDecisionBoundary(Boundary("same")));

        Assert.Equal(SemanticBoundaryTraceKinds.TransitionUnknown, result.Kind);
        Assert.Equal("successor_not_different", result.ProofStatus);
    }

    [Fact]
    public void SessionCloseAccountsForAnUnresolvedFinishedActionAsUnknown()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("a1", 1), State("s0"));
        tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");

        SemanticBoundaryTraceDraft result = Assert.Single(
            tracker.CloseUnknown("recording_closed_before_semantic_boundary"));

        Assert.Equal(SemanticBoundaryTraceKinds.TransitionUnknown, result.Kind);
        Assert.Equal("recording_closed_before_semantic_boundary", result.ProofStatus);
        Assert.Empty(tracker.CloseUnknown("duplicate_close"));
    }

    [Fact]
    public void CapacityBoundsTheLiveCausalWindowRatherThanSessionHistory()
    {
        var tracker = new SemanticBoundaryTracker(capacity: 2);
        tracker.Accept(Action("a1", 1), State("s0"));
        tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");
        tracker.ObserveDecisionBoundary(Boundary("s1"));
        tracker.Accept(Action("a2", 2), State("s1"));
        tracker.ObserveBeforeActionExecution("a2", Boundary("s1", "a2"));
        tracker.Started("a2");
        tracker.Finished("a2");
        tracker.ObserveDecisionBoundary(Boundary("s2"));

        IReadOnlyList<SemanticBoundaryTraceDraft> accepted = tracker.Accept(
            Action("a3", 3),
            State("s2"));

        Assert.Single(accepted);
        Assert.Equal(SemanticBoundaryTraceKinds.ActionAccepted, accepted[0].Kind);
    }

    [Fact]
    public void TraceValidatorRejectsFalseOrDuplicateTransitionDisposition()
    {
        SemanticActionReference action = Action("a1", 1);
        SemanticBoundaryTraceEvent accepted = Event(1, SemanticBoundaryTraceKinds.ActionAccepted, action);
        SemanticBoundaryTraceEvent started = Event(2, SemanticBoundaryTraceKinds.ActionStarted, action);
        SemanticBoundaryTraceEvent proved = Event(3, SemanticBoundaryTraceKinds.TransitionProved, action) with
        {
            Boundary = Boundary("same"),
            SemanticPre = State("same"),
            SemanticSuccessor = State("same")
        };
        SemanticBoundaryTraceEvent cancelled = Event(
            4,
            SemanticBoundaryTraceKinds.ActionCancelledAfterStart,
            action);

        IReadOnlyList<string> errors = SemanticBoundaryTraceValidator.Validate(
            new[] { accepted, started, proved, cancelled });

        Assert.Contains("semantic_transition_successor_not_different", errors);
        Assert.Contains("semantic_cancel_after_start_disposition_invalid", errors);
        Assert.Contains("semantic_action_has_multiple_dispositions", errors);
    }

    [Fact]
    public void TraceValidatorRejectsAbortWithoutStartedLifecycle()
    {
        SemanticActionReference action = Action("a1", 1);
        SemanticBoundaryTraceEvent accepted = Event(1, SemanticBoundaryTraceKinds.ActionAccepted, action);
        SemanticBoundaryTraceEvent aborted = Event(
            2,
            SemanticBoundaryTraceKinds.ActionAbortedBeforeCommit,
            action);

        IReadOnlyList<string> errors = SemanticBoundaryTraceValidator.Validate(
            new[] { accepted, aborted });

        Assert.Contains("semantic_abort_before_commit_disposition_invalid", errors);
    }

    [Fact]
    public void RecordingStorePersistsAdditiveSemanticTrace()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sts2-semantic-boundary-{Guid.NewGuid():N}");
        try
        {
            HumanCaptureProfile profile = HumanCaptureProfiles.CombatReadRichV2;
            var manifest = new RecordingManifestV2(
                HumanRecorderV2Contract.SchemaVersion,
                HumanRecorderV2Contract.ManifestSchema,
                "session-test",
                "timeline-test",
                T0,
                HumanRecorderContract.ProductVersion,
                new string('a', 40),
                "osx-arm64",
                profile.ProfileId,
                EvidenceIdentity.Sha256Json(profile),
                profile.SupportedActionFamilies,
                profile.NonClaims);
            SemanticBoundaryTraceEvent accepted = Event(
                1,
                SemanticBoundaryTraceKinds.ActionAccepted,
                Action("a1", 1));

            using (V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile))
                store.AppendSemanticBoundaryEvent(accepted);

            string path = Path.Combine(root, "session-test", "semantic-boundary-trace.jsonl");
            SemanticBoundaryTraceEvent persisted = JsonSerializer.Deserialize<SemanticBoundaryTraceEvent>(
                File.ReadAllText(path), EvidenceJson.Options)!;
            Assert.Equal(SemanticBoundaryTraceContract.EventSchema, persisted.Schema);
            Assert.Equal("a1", persisted.Action.ActionWitnessId);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static SemanticActionReference Action(string id, long sequence) => new(
        id,
        sequence,
        $"record-{id}",
        "run-0001",
        "PlayCardAction",
        (uint)sequence,
        $"human-{id}");

    private static SemanticBoundaryObservation Boundary(
        string snapshotId,
        string? nextAction = null) => new(
            nextAction == null
                ? "complete_interactive_observation"
                : "before_next_human_action_execution",
            T0,
            snapshotId,
            "interactive",
            "complete",
            $"interaction-{snapshotId}",
            "combat_turn",
            State(snapshotId),
            nextAction);

    private static SemanticBoundaryObservation IncompleteBoundary(
        string snapshotId,
        string nextAction) => new(
            "before_next_human_action_execution",
            T0,
            snapshotId,
            "settling",
            "unavailable",
            $"interaction-{snapshotId}",
            "combat_turn",
            null,
            nextAction);

    private static FrozenDecisionFrameV2 State(string snapshotId) => new(
        snapshotId,
        $"interaction-{snapshotId}",
        "combat_turn",
        "sts2.player-environment/surface/combat_turn-1",
        new string('a', 64),
        2,
        JsonNode.Parse($"{{\"snapshot_id\":\"{snapshotId}\"}}")!,
        Array.Empty<ReadEvidence>());

    private static (string Pre, string Successor) TransitionIds(
        SemanticBoundaryTraceDraft value) => (
            value.SemanticPre!.SnapshotId,
            value.SemanticSuccessor!.SnapshotId);

    private static SemanticBoundaryTraceEvent Event(
        long sequence,
        string kind,
        SemanticActionReference action) => new(
            SemanticBoundaryTraceContract.SchemaVersion,
            SemanticBoundaryTraceContract.EventSchema,
            $"event-{sequence}",
            "session-test",
            "timeline-test",
            "run-0001",
            sequence,
            T0.AddMilliseconds(sequence),
            kind,
            action,
            "test",
            null,
            null,
            null,
            null,
            null,
            Array.Empty<string>());
}
