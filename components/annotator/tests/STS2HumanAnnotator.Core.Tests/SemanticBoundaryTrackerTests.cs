using System.Text.Json;
using System.Text.Json.Nodes;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class SemanticBoundaryTrackerTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-08-26T00:00:00Z");

    [Fact]
    public void ExactActEnteredBoundarySettlesOnlyItsBoundActRoot()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference act = Action(
            "act-ready",
            1,
            "NRewardsScreen.OnProceedButtonPressed.act_change_ready") with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(act, State("human-act"));
        tracker.ObserveBeforeActionExecution(
            act.ActionWitnessId,
            Boundary("before-act", act.ActionWitnessId));
        tracker.Started(act.ActionWitnessId);
        tracker.Finished(act.ActionWitnessId);
        tracker.ObserveNativeCommit(act.ActionWitnessId, Completion(act.ActionWitnessId));

        SemanticActionReference unrelated = Action("unrelated", 2) with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(unrelated, State("human-unrelated"));

        SemanticBoundaryObservation entered = PostCommitBoundary("after-act") with
        {
            WitnessKind = SemanticBoundaryWitnessKinds.NativeActEntered,
            NativeDecisionOwnerReady = null
        };
        SemanticBoundaryTraceDraft settled = Assert.Single(
            tracker.ObserveDecisionBoundaryForAction(act.ActionWitnessId, entered));

        Assert.Equal(SemanticBoundaryTraceKinds.TransitionProved, settled.Kind);
        Assert.Equal(act.ActionWitnessId, settled.Action.ActionWitnessId);
        Assert.True(tracker.Contains(unrelated.ActionWitnessId));
        Assert.True(tracker.HasUnresolvedActions);
    }

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
            tracker.ObserveDecisionBoundary(PostCommitBoundary("s3")));

        Assert.Equal(("s0", "s1"), TransitionIds(a1));
        Assert.Equal(("s1", "s2"), TransitionIds(a2));
        Assert.Equal(("s2", "s3"), TransitionIds(a3));
        Assert.Equal("a2", a1.RelatedActionWitnessId);
        Assert.Equal("a3", a2.RelatedActionWitnessId);
    }

    [Fact]
    public void LaterAcceptedChoiceExecutingFirstBindsEachActionFromExecutionOrder()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("queued-end-turn", 1), State("turn-before-choice"));
        tracker.Accept(Action("choice", 2), State("choice-pre"));

        tracker.ObserveBeforeActionExecution("choice", Boundary("choice-pre", "choice"));
        tracker.Started("choice");
        tracker.Finished("choice");
        IReadOnlyList<SemanticBoundaryTraceDraft> beforeQueued =
            tracker.ObserveBeforeActionExecution(
                "queued-end-turn",
                Boundary("after-choice", "queued-end-turn"));
        tracker.Started("queued-end-turn");
        tracker.Finished("queued-end-turn");
        SemanticBoundaryTraceDraft queued = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("next-turn")));

        SemanticBoundaryTraceDraft choice = Assert.Single(
            beforeQueued,
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
        Assert.Equal(("choice-pre", "after-choice"), TransitionIds(choice));
        Assert.Equal(SemanticBoundaryTraceKinds.TransitionProved, queued.Kind);
        Assert.Equal(("after-choice", "next-turn"), TransitionIds(queued));
        SemanticBoundaryTraceDraft rebound = Assert.Single(
            beforeQueued,
            value => value.Kind == SemanticBoundaryTraceKinds.BoundaryObserved);
        Assert.Equal("execution_boundary_bound", rebound.ProofStatus);
        Assert.Null(rebound.RelatedActionWitnessId);
    }

    [Fact]
    public void LaterStartedActionCannotBeSkippedByEarlyRootSettlement()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference eventAction = Action(
            "event",
            1,
            "NEventRoom.OptionButtonClicked") with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(eventAction, State("human-event"));
        tracker.ObserveBeforeActionExecution(
            eventAction.ActionWitnessId,
            Boundary("event-before", eventAction.ActionWitnessId));
        tracker.Started(eventAction.ActionWitnessId);

        SemanticActionReference rewardClaim = Action(
            "reward-claim",
            2,
            "NRewardButton.OnRelease") with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(rewardClaim, State("human-reward"));
        tracker.ObserveBeforeActionExecution(
            rewardClaim.ActionWitnessId,
            Boundary("reward-before", rewardClaim.ActionWitnessId));
        tracker.Started(rewardClaim.ActionWitnessId);
        tracker.Finished(rewardClaim.ActionWitnessId);
        tracker.ObserveNativeCommit(
            rewardClaim.ActionWitnessId,
            Completion(rewardClaim.ActionWitnessId));

        // This root is already executing when the earlier Event action later
        // finishes and receives its Commit. It is therefore an intervening
        // Human effect even though it is not itself waiting for a boundary.
        SemanticActionReference rewardProceed = Action(
            "reward-proceed",
            3,
            "NRewardsScreen.OnProceedButtonPressed") with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(rewardProceed, State("human-proceed"));
        tracker.ObserveBeforeActionExecution(
            rewardProceed.ActionWitnessId,
            Boundary("proceed-before", rewardProceed.ActionWitnessId));
        tracker.Started(rewardProceed.ActionWitnessId);

        tracker.Finished(eventAction.ActionWitnessId);
        tracker.ObserveNativeCommit(
            eventAction.ActionWitnessId,
            Completion(eventAction.ActionWitnessId));

        SemanticActionReference next = Action("next", 4);
        tracker.Accept(next, State("human-next"));
        SemanticBoundaryTraceDraft eventDisposition = Assert.Single(
            tracker.ObserveBeforeActionExecution(
                next.ActionWitnessId,
                Boundary("next-state", next.ActionWitnessId)),
            value => value.Action.ActionWitnessId == eventAction.ActionWitnessId);

        Assert.Equal(SemanticBoundaryTraceKinds.TransitionUnknown, eventDisposition.Kind);
        Assert.Equal("intervening_human_action_before_boundary", eventDisposition.ProofStatus);
        Assert.Equal(rewardProceed.ActionWitnessId, eventDisposition.RelatedActionWitnessId);
        Assert.Null(eventDisposition.SemanticSuccessor);
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
            tracker.ObserveDecisionBoundary(PostCommitBoundary("reward-after-lethal")));

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
    public void PlayerChoiceResumeCallbackStaysOnNativeParentUntilCommitAndNextBoundary()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference parent = Action("choice-parent", 1) with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(parent, State("human-parent"));
        tracker.ObserveBeforeActionExecution(
            parent.ActionWitnessId,
            Boundary("combat-before", parent.ActionWitnessId));
        tracker.Started(parent.ActionWitnessId);
        tracker.PausedForPlayerChoice(parent.ActionWitnessId);
        tracker.ReadyToResume(parent.ActionWitnessId);

        // ActionExecutor's resume callback is the same parent GameAction, not
        // another execution boundary and not a self-successor.
        tracker.BeforeExecutionResume(parent.ActionWitnessId);
        Assert.True(tracker.HasUnresolvedActions);
        tracker.Resumed(parent.ActionWitnessId);
        tracker.Finished(parent.ActionWitnessId);
        NativeCompletionEvidence completion = Completion(parent.ActionWitnessId);
        SemanticBoundaryTraceDraft committed = Assert.Single(
            tracker.ObserveNativeCommit(parent.ActionWitnessId, completion));

        Assert.Equal(SemanticBoundaryTraceKinds.NativeCommitObserved, committed.Kind);
        Assert.Equal("native_commit_observed", committed.ProofStatus);
        Assert.True(tracker.HasUnresolvedActions);

        SemanticActionReference next = Action("next", 2);
        tracker.Accept(next, State("human-next"));
        SemanticBoundaryTraceDraft proved = Assert.Single(
            tracker.ObserveBeforeActionExecution(
                next.ActionWitnessId,
                Boundary("combat-after-choice", next.ActionWitnessId)),
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);

        Assert.Equal(parent.ActionWitnessId, proved.Action.ActionWitnessId);
        Assert.Equal("proved_native_commit_then_execution_handoff", proved.ProofStatus);
        Assert.Same(completion, proved.NativeCompletion);
        Assert.Empty(tracker.ObserveNativeCommit(parent.ActionWitnessId, completion));
    }

    [Fact]
    public void PlayerChoiceParentMayExposeMultipleExactContinuations()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference parent = Action("choice-parent-multi", 1) with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(parent, State("human-parent"));
        tracker.ObserveBeforeActionExecution(
            parent.ActionWitnessId,
            Boundary("combat-before", parent.ActionWitnessId));
        tracker.Started(parent.ActionWitnessId);
        tracker.PausedForPlayerChoice(parent.ActionWitnessId);

        NativeContinuationEvidence first = new(
            "continuation-first",
            "GameAction.BeforePausedForPlayerChoice",
            parent.ActionWitnessId,
            "game_action:choice-parent-multi",
            "game_action:choice-parent-multi",
            true);
        NativeContinuationEvidence second = first with
        {
            ContinuationId = "continuation-second"
        };

        Assert.Same(
            first,
            Assert.Single(tracker.ObserveNativeContinuation(parent.ActionWitnessId, first))
                .NativeContinuation);
        tracker.ReadyToResume(parent.ActionWitnessId);
        tracker.BeforeExecutionResume(parent.ActionWitnessId);
        tracker.Resumed(parent.ActionWitnessId);
        tracker.PausedForPlayerChoice(parent.ActionWitnessId);

        // The same native parent can pause again for another exact choice.
        // This is a second lifecycle witness, not a duplicate Human root.
        Assert.Same(
            second,
            Assert.Single(tracker.ObserveNativeContinuation(parent.ActionWitnessId, second))
                .NativeContinuation);
        Assert.True(tracker.CanOpenNextRoot);
    }

    [Fact]
    public void ExactPlayerChoiceContinuationLetsNestedHumanChoiceSettleParentWithoutFinish()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference parent = Action("choice-parent", 1) with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(parent, State("human-parent"));
        tracker.ObserveBeforeActionExecution(
            parent.ActionWitnessId,
            Boundary("combat-before", parent.ActionWitnessId));
        tracker.Started(parent.ActionWitnessId);
        tracker.PausedForPlayerChoice(parent.ActionWitnessId);

        Assert.False(tracker.CanOpenNextRoot);
        NativeContinuationEvidence continuation = new(
            "continuation-choice-parent",
            "GameAction.BeforePausedForPlayerChoice",
            parent.ActionWitnessId,
            "game_action:choice-parent",
            "game_action:choice-parent",
            true);
        SemanticBoundaryTraceDraft continuationDraft = Assert.Single(
            tracker.ObserveNativeContinuation(parent.ActionWitnessId, continuation));

        Assert.Equal(SemanticBoundaryTraceKinds.NativeContinuationObserved, continuationDraft.Kind);
        Assert.True(tracker.CanOpenNextRoot);

        SemanticActionReference child = Action(
            "choice-child",
            2,
            "NChooseACardSelectionScreen.SelectHolder");
        tracker.Accept(child, State("choice-human-observation"));
        SemanticBoundaryTraceDraft parentProved = Assert.Single(
            tracker.ObserveBeforeActionExecution(
                child.ActionWitnessId,
                Boundary("choice-state", child.ActionWitnessId)),
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);

        Assert.Equal(parent.ActionWitnessId, parentProved.Action.ActionWitnessId);
        Assert.Equal("proved_player_choice_boundary", parentProved.ProofStatus);
        Assert.Same(continuation, parentProved.NativeContinuation);
        Assert.Equal(("combat-before", "choice-state"), TransitionIds(parentProved));

        tracker.Started(child.ActionWitnessId);
        tracker.Finished(child.ActionWitnessId);
        tracker.ReadyToResume(parent.ActionWitnessId);
        tracker.BeforeExecutionResume(parent.ActionWitnessId);
        tracker.Resumed(parent.ActionWitnessId);
        SemanticBoundaryTraceDraft parentFinished = Assert.Single(tracker.Finished(parent.ActionWitnessId));

        Assert.Equal("lifecycle_finished_after_semantic_disposition", parentFinished.ProofStatus);
        Assert.Empty(tracker.ObserveDecisionBoundary(Boundary("later-state")));
    }

    [Fact]
    public void PlayerChoiceContinuationTraceRequiresExactPauseAndRoundTripsAsParentCommitEvidence()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference parent = Action("choice-parent", 1) with
        {
            RequiresNativePostCommit = true
        };
        SemanticActionReference child = Action(
            "choice-child",
            2,
            "NChooseACardSelectionScreen.SelectHolder");
        var drafts = new List<SemanticBoundaryTraceDraft>();
        drafts.AddRange(tracker.Accept(parent, State("human-parent")));
        drafts.AddRange(tracker.ObserveBeforeActionExecution(
            parent.ActionWitnessId,
            Boundary("combat-before", parent.ActionWitnessId)));
        drafts.AddRange(tracker.Started(parent.ActionWitnessId));
        drafts.AddRange(tracker.PausedForPlayerChoice(parent.ActionWitnessId));
        NativeContinuationEvidence continuation = new(
            "continuation-choice-parent",
            "GameAction.BeforePausedForPlayerChoice",
            parent.ActionWitnessId,
            "game_action:choice-parent",
            "game_action:choice-parent",
            true);
        drafts.AddRange(tracker.ObserveNativeContinuation(parent.ActionWitnessId, continuation));
        drafts.AddRange(tracker.Accept(child, State("human-child")));
        drafts.AddRange(tracker.ObserveBeforeActionExecution(
            child.ActionWitnessId,
            Boundary("choice-state", child.ActionWitnessId)));
        drafts.AddRange(tracker.Started(child.ActionWitnessId));
        drafts.AddRange(tracker.Finished(child.ActionWitnessId));
        drafts.AddRange(tracker.ObserveDecisionBoundary(PostCommitBoundary("after-choice")));

        SemanticBoundaryTraceEvent[] events = drafts
            .Select((draft, index) => Event(index + 1, draft))
            .ToArray();
        Assert.Empty(SemanticBoundaryTraceValidator.Validate(events));
        SemanticBoundaryTraceEvent proved = Assert.Single(events,
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved
                     && value.Action.ActionWitnessId == parent.ActionWitnessId);
        Assert.Equal(parent.ActionWitnessId, proved.Action.ActionWitnessId);
        Assert.Equal(child.ActionWitnessId, proved.RelatedActionWitnessId);
        Assert.Same(continuation, proved.NativeContinuation);

        SemanticBoundaryTraceEvent[] missingPause = events
            .Where(value => value.Kind != SemanticBoundaryTraceKinds.ActionPausedForPlayerChoice)
            .ToArray();
        Assert.Contains(
            "semantic_native_continuation_without_pause",
            SemanticBoundaryTraceValidator.Validate(missingPause));
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
            tracker.ObserveDecisionBoundary(PostCommitBoundary("s2")));

        Assert.Equal("semantic_state_incomplete_before_next_action", unknown.ProofStatus);
        Assert.Null(unknown.SemanticSuccessor);
        Assert.Equal("semantic_pre_unknown", a2Unknown.ProofStatus);
    }

    [Fact]
    public void CompleteStateAtExecutionBoundaryDoesNotRequireRepublishedActionCatalog()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("a1", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");
        tracker.Accept(Action("a2", 2), State("human-precommit"));

        IReadOnlyList<SemanticBoundaryTraceDraft> handoff =
            tracker.ObserveBeforeActionExecution("a2", StateOnlyExecutionBoundary("s1", "a2"));
        tracker.Started("a2");
        tracker.Finished("a2");
        SemanticBoundaryTraceDraft a2 = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("s2")));

        SemanticBoundaryTraceDraft a1 = Assert.Single(
            handoff,
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
        Assert.Equal("proved_execution_handoff_boundary", a1.ProofStatus);
        Assert.Equal(("s0", "s1"), TransitionIds(a1));
        Assert.Equal(("s1", "s2"), TransitionIds(a2));
        SemanticBoundaryTraceDraft executionBoundary = Assert.Single(
            handoff,
            value => value.Kind == SemanticBoundaryTraceKinds.BoundaryObserved);
        Assert.Equal("execution_boundary_bound", executionBoundary.ProofStatus);
        Assert.Equal("s1", executionBoundary.SemanticPre!.SnapshotId);
        Assert.Same(a1.SemanticSuccessor, executionBoundary.SemanticPre);
    }

    [Fact]
    public void HumanObservationIsEvidenceButNeverImplicitSemanticPre()
    {
        var tracker = new SemanticBoundaryTracker();

        SemanticBoundaryTraceDraft accepted = Assert.Single(
            tracker.Accept(Action("a1", 1), State("human-s0")));

        Assert.Equal("human-s0", accepted.HumanObservation!.SnapshotId);
        Assert.Null(accepted.SemanticPre);
        Assert.Equal("human_observation_recorded", accepted.ProofStatus);
    }

    [Fact]
    public void DraftProjectionCarriesEntryEvidenceAcrossLifecycleAndSettlement()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference action = Action("a1", 1) with
        {
            RequiresNativePostCommit = true
        };
        CurrentDecisionFrame humanObservation = State("human-s0");
        ExecutionSemanticActionSpaceEvidence actionSpace = new(
            ExecutionSemanticActionSpaceContract.SchemaVersion,
            ExecutionSemanticActionSpaceContract.Schema,
            action.ActionWitnessId,
            "before_execution",
            "captured",
            "combat_play_phase",
            new string('a', 64),
            JsonNode.Parse("{\"turn\":1}")!,
            new string('b', 64),
            new[]
            {
                new ExecutionSemanticAction(
                    "play|card-a1|",
                    "play",
                    "card-a1",
                    new Dictionary<string, string>(),
                    "native-test")
            },
            "play|card-a1|",
            "exact_once",
            1,
            new[] { "native-test" },
            new[] { "not_public_delivery_authority" },
            null);

        SemanticBoundaryTraceDraft accepted = Assert.Single(
            tracker.Accept(action, humanObservation));
        SemanticBoundaryObservation execution = Boundary("s0", action.ActionWitnessId) with
        {
            ExecutionSemanticActionSpace = actionSpace
        };
        tracker.ObserveBeforeActionExecution(action.ActionWitnessId, execution);
        tracker.Started(action.ActionWitnessId);
        tracker.Finished(action.ActionWitnessId);
        NativeCompletionEvidence completion = Completion(action.ActionWitnessId);
        SemanticBoundaryTraceDraft committed = Assert.Single(
            tracker.ObserveNativeCommit(action.ActionWitnessId, completion));
        SemanticBoundaryTraceDraft proved = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("s1")));

        Assert.Same(humanObservation, accepted.HumanObservation);
        Assert.Same(humanObservation, committed.HumanObservation);
        Assert.Same(humanObservation, proved.HumanObservation);
        Assert.Same(completion, committed.NativeCompletion);
        Assert.Same(completion, proved.NativeCompletion);
        Assert.Same(actionSpace, committed.ExecutionSemanticActionSpace);
        Assert.Same(actionSpace, proved.ExecutionSemanticActionSpace);
    }

    [Fact]
    public void UnknownPreDoesNotPoisonACompleteLaterExecutionBoundary()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("a1", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("a1", IncompleteBoundary("incomplete-s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");
        tracker.Accept(Action("a2", 2), State("human-a2"));

        IReadOnlyList<SemanticBoundaryTraceDraft> handoff =
            tracker.ObserveBeforeActionExecution("a2", StateOnlyExecutionBoundary("s1", "a2"));
        tracker.Started("a2");
        tracker.Finished("a2");
        SemanticBoundaryTraceDraft a2 = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("s2")));

        SemanticBoundaryTraceDraft a1 = Assert.Single(
            handoff,
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionUnknown);
        Assert.Equal("semantic_pre_unknown", a1.ProofStatus);
        Assert.Equal(("s1", "s2"), TransitionIds(a2));
    }

    [Fact]
    public void EndTurnConsumesTheSameCurrentStateProducedByPriorActionHandoff()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("play", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("play", Boundary("s0", "play"));
        tracker.Started("play");
        tracker.Finished("play");
        tracker.Accept(Action("end-turn", 2, "EndPlayerTurnAction"), State("human-end-turn"));

        IReadOnlyList<SemanticBoundaryTraceDraft> handoff = tracker.ObserveBeforeActionExecution(
            "end-turn",
            StateOnlyExecutionBoundary("s1", "end-turn"));
        tracker.Started("end-turn");
        tracker.Finished("end-turn");
        SemanticBoundaryTraceDraft endTurn = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("next-turn")));

        Assert.Equal(("s0", "s1"), TransitionIds(Assert.Single(
            handoff,
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved)));
        Assert.Equal(("s1", "next-turn"), TransitionIds(endTurn));
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
            tracker.ObserveDecisionBoundary(PostCommitBoundary("same")));

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
            tracker.CloseUnknown(RecordingClosePolicy.TerminalUnknownReason));

        Assert.Equal(SemanticBoundaryTraceKinds.TransitionUnknown, result.Kind);
        Assert.Equal(RecordingClosePolicy.TerminalUnknownReason, result.ProofStatus);
        Assert.Null(result.SemanticSuccessor);
        Assert.Contains("no_semantic_successor", result.NonClaims!);
        Assert.Empty(tracker.CloseUnknown("duplicate_close"));
    }

    [Fact]
    public void PreviewCloseUnknownDoesNotEraseRootsBeforePersistence()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("close-failure", 1), State("s0"));

        IReadOnlyList<SemanticBoundaryTraceDraft> preview =
            tracker.PreviewCloseUnknown(RecordingClosePolicy.TerminalUnknownReason);

        Assert.Single(preview);
        Assert.True(tracker.HasUnresolvedActions);

        tracker.CommitCloseUnknown();

        Assert.False(tracker.HasUnresolvedActions);
        Assert.Empty(tracker.PreviewCloseUnknown("duplicate_close"));
    }

    [Fact]
    public void AuthoritativeCloseAppendThenProjectionFailureCommitsExactlyOnce()
    {
        var beforeAppendFailure = new SemanticBoundaryTracker();
        beforeAppendFailure.Accept(Action("close-append-failure", 1), State("s0"));

        IReadOnlyList<SemanticBoundaryTraceDraft> beforePreview =
            beforeAppendFailure.PreviewCloseUnknown(RecordingClosePolicy.TerminalUnknownReason);
        int failedAppendAttempts = 0;
        try
        {
            failedAppendAttempts++;
            throw new InvalidOperationException("append failed before durable evidence");
        }
        catch (InvalidOperationException)
        {
            // The coordinator remains Closing and does not commit its preview.
        }

        Assert.Single(beforePreview);
        Assert.Equal(1, failedAppendAttempts);
        Assert.True(beforeAppendFailure.HasUnresolvedActions);

        var afterAppendFailure = new SemanticBoundaryTracker();
        afterAppendFailure.Accept(Action("close-projection-failure", 2), State("s1"));
        IReadOnlyList<SemanticBoundaryTraceDraft> afterPreview =
            afterAppendFailure.PreviewCloseUnknown(RecordingClosePolicy.TerminalUnknownReason);
        Assert.Single(afterPreview);
        int durableDispositionAppends = 0;

        try
        {
            durableDispositionAppends++;
            afterAppendFailure.CommitCloseUnknown();
            throw new InvalidOperationException("projection failed after durable evidence");
        }
        catch (InvalidOperationException)
        {
            // The coordinator records the projection failure but does not
            // retry the authoritative semantic append.
        }

        Assert.Equal(1, durableDispositionAppends);
        Assert.False(afterAppendFailure.HasUnresolvedActions);
        Assert.Empty(afterAppendFailure.PreviewCloseUnknown("duplicate_close"));
        afterAppendFailure.CommitCloseUnknown();
        Assert.Empty(afterAppendFailure.PreviewCloseUnknown("duplicate_close"));
    }

    [Fact]
    public void CapacityBoundsTheLiveCausalWindowRatherThanSessionHistory()
    {
        var tracker = new SemanticBoundaryTracker(capacity: 2);
        tracker.Accept(Action("a1", 1), State("s0"));
        tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");
        tracker.ObserveDecisionBoundary(PostCommitBoundary("s1"));
        tracker.Accept(Action("a2", 2), State("s1"));
        tracker.ObserveBeforeActionExecution("a2", Boundary("s1", "a2"));
        tracker.Started("a2");
        tracker.Finished("a2");
        tracker.ObserveDecisionBoundary(PostCommitBoundary("s2"));

        IReadOnlyList<SemanticBoundaryTraceDraft> accepted = tracker.Accept(
            Action("a3", 3),
            State("s2"));

        Assert.Single(accepted);
        Assert.Equal(SemanticBoundaryTraceKinds.ActionAccepted, accepted[0].Kind);
    }

    [Fact]
    public void LethalRewardCardChoiceAndMapRemainOneContinuousTimeline()
    {
        var tracker = new SemanticBoundaryTracker();

        tracker.Accept(Action("lethal", 1), State("combat-s0", "combat_turn"));
        tracker.ObserveBeforeActionExecution(
            "lethal",
            Boundary("combat-s0", "lethal", "combat_turn"));
        tracker.Started("lethal");
        tracker.Finished("lethal");
        SemanticBoundaryTraceDraft lethal = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("reward-s1", interactionKind: "reward_claim")));

        tracker.Accept(Action("claim", 2, "NRewardButton.OnRelease"), State("reward-s1", "reward_claim"));
        tracker.ObserveBeforeActionExecution(
            "claim",
            Boundary("reward-s1", "claim", "reward_claim"));
        tracker.Started("claim");
        tracker.Finished("claim");
        SemanticBoundaryTraceDraft claim = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("cards-s2", interactionKind: "card_reward_selection")));

        tracker.Accept(
            Action("select", 3, "NCardRewardSelectionScreen.SelectCard"),
            State("cards-s2", "card_reward_selection"));
        tracker.ObserveBeforeActionExecution(
            "select",
            Boundary("cards-s2", "select", "card_reward_selection"));
        tracker.Started("select");
        tracker.Finished("select");
        SemanticBoundaryTraceDraft select = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("map-s3", interactionKind: "map_route")));

        tracker.Accept(Action("map", 4, "VoteForMapCoordAction"), State("map-s3", "map_route"));
        tracker.ObserveBeforeActionExecution("map", Boundary("map-s3", "map", "map_route"));
        tracker.Started("map");
        tracker.Finished("map");
        SemanticBoundaryTraceDraft map = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("event-s4", interactionKind: "event_option")));

        Assert.Equal(("combat-s0", "reward-s1"), TransitionIds(lethal));
        Assert.Equal(("reward-s1", "cards-s2"), TransitionIds(claim));
        Assert.Equal(("cards-s2", "map-s3"), TransitionIds(select));
        Assert.Equal(("map-s3", "event-s4"), TransitionIds(map));
        Assert.False(tracker.HasUnresolvedActions);
    }

    [Theory]
    [InlineData("combat_turn", "run_deck,combat_piles")]
    [InlineData("generated_card_choice", "run_deck,combat_piles")]
    [InlineData("reward_claim", "run_deck")]
    [InlineData("card_reward_selection", "run_deck")]
    [InlineData("map_route", "run_deck")]
    [InlineData("shop_inventory", "run_deck,shop_catalog")]
    public void SemanticReadCompletenessIsInteractionSpecific(
        string interactionKind,
        string expectedKinds)
    {
        Assert.Equal(
            expectedKinds.Split(','),
            SemanticBoundaryReadPolicy.RequiredKinds(interactionKind));
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
    public void TraceValidatorRejectsProofContainingAnotherHumanExecution()
    {
        SemanticActionReference queued = Action("queued", 1);
        SemanticActionReference choice = Action("choice", 2);
        SemanticBoundaryTraceEvent[] events =
        {
            Event(1, SemanticBoundaryTraceKinds.ActionAccepted, queued),
            Event(2, SemanticBoundaryTraceKinds.ActionStarted, queued),
            Event(3, SemanticBoundaryTraceKinds.ActionAccepted, choice),
            Event(4, SemanticBoundaryTraceKinds.ActionStarted, choice),
            Event(5, SemanticBoundaryTraceKinds.ActionFinished, choice),
            Event(6, SemanticBoundaryTraceKinds.TransitionUnknown, choice),
            Event(7, SemanticBoundaryTraceKinds.ActionFinished, queued),
            Event(8, SemanticBoundaryTraceKinds.TransitionProved, queued) with
            {
                Boundary = Boundary("next"),
                SemanticPre = State("before"),
                SemanticSuccessor = State("next")
            }
        };

        IReadOnlyList<string> errors = SemanticBoundaryTraceValidator.Validate(events);

        Assert.Contains("semantic_transition_contains_intervening_human_action", errors);
    }

    [Fact]
    public void TraceValidatorRejectsPrecommitNotReboundToCompleteExecutionBoundary()
    {
        SemanticActionReference queued = Action("queued", 1);
        SemanticActionReference choice = Action("choice", 2);
        SemanticBoundaryTraceEvent[] events =
        {
            Event(1, SemanticBoundaryTraceKinds.ActionAccepted, queued),
            Event(2, SemanticBoundaryTraceKinds.ActionAccepted, choice),
            Event(3, SemanticBoundaryTraceKinds.ActionStarted, choice),
            Event(4, SemanticBoundaryTraceKinds.ActionFinished, choice),
            Event(5, SemanticBoundaryTraceKinds.TransitionUnknown, choice),
            Event(6, SemanticBoundaryTraceKinds.BoundaryObserved, queued) with
            {
                Boundary = Boundary("after-choice"),
                SemanticPre = State("after-choice")
            },
            Event(7, SemanticBoundaryTraceKinds.ActionStarted, queued),
            Event(8, SemanticBoundaryTraceKinds.ActionFinished, queued),
            Event(9, SemanticBoundaryTraceKinds.TransitionProved, queued) with
            {
                Boundary = Boundary("next"),
                SemanticPre = State("before-choice"),
                SemanticSuccessor = State("next")
            }
        };

        IReadOnlyList<string> errors = SemanticBoundaryTraceValidator.Validate(events);

        Assert.Contains("semantic_transition_pre_not_execution_boundary", errors);
    }

    [Fact]
    public void RecordingStorePersistsAdditiveSemanticTrace()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sts2-semantic-boundary-{Guid.NewGuid():N}");
        try
        {
            HumanCaptureProfile profile = HumanCaptureProfiles.CombatReadRich;
            var manifest = new CurrentRecordingManifest(
                CurrentRecordingContract.SchemaVersion,
                CurrentRecordingContract.ManifestSchema,
                "session-test",
                "timeline-test",
                T0,
                CurrentRecordingContract.ProductVersion,
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

            using (RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile))
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

    [Fact]
    public void CurrentTimelineEventsValidateAsOneExecutionBoundTransition()
    {
        var tracker = new SemanticBoundaryTracker();
        var drafts = new List<SemanticBoundaryTraceDraft>();
        drafts.AddRange(tracker.Accept(Action("a1", 1), State("human-s0")));
        drafts.AddRange(tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1")));
        drafts.AddRange(tracker.Started("a1"));
        drafts.AddRange(tracker.Finished("a1"));
        drafts.AddRange(tracker.ObserveDecisionBoundary(PostCommitBoundary("s1")));

        IReadOnlyList<string> errors = SemanticBoundaryTraceValidator.Validate(
            drafts.Select((draft, index) => Event(index + 1, draft)).ToArray());

        Assert.Empty(errors);
        Assert.False(tracker.HasUnresolvedActions);
    }

    [Fact]
    public void DirectUiCommitUsesTheCanonicalExecutionBoundary()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference action = Action(
            "direct-ui",
            1,
            "NPlayerHand.OnSelectModeConfirmButtonPressed") with
        {
            NativeMechanism = "direct_ui_commit"
        };
        var boundary = new SemanticBoundaryObservation(
            SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
            T0,
            "selection-s0",
            "interactive",
            "complete",
            "interaction-selection-s0",
            "combat_hand_card_selection",
            State("selection-s0", "combat_hand_card_selection"),
            action.ActionWitnessId);

        tracker.Accept(action, State("human-selection"));
        SemanticBoundaryTraceDraft bound = Assert.Single(
            tracker.ObserveBeforeActionExecution(action.ActionWitnessId, boundary));
        tracker.Started(action.ActionWitnessId);
        tracker.Finished(action.ActionWitnessId);
        SemanticBoundaryTraceDraft proved = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("combat-s1")));

        Assert.Equal("execution_boundary_bound", bound.ProofStatus);
        Assert.Equal(SemanticBoundaryTraceKinds.TransitionProved, proved.Kind);
        Assert.Equal("selection-s0", proved.SemanticPre!.SnapshotId);
        Assert.Equal("combat-s1", proved.SemanticSuccessor!.SnapshotId);
    }

    [Fact]
    public void NativeCommitDoesNotBecomeSuccessorUntilNextDecisionBoundary()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference action = Action("native-root", 1) with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(action, State("human-s0"));
        tracker.ObserveBeforeActionExecution("native-root", Boundary("s0", "native-root"));
        tracker.Started("native-root");
        tracker.Finished("native-root");

        NativeCompletionEvidence completion = new(
            "completion-native-root",
            "reward_claim",
            "native.select",
            "native-root",
            "task-native-root",
            "owner-native-root",
            "operand-native-root",
            null,
            true);
        SemanticBoundaryTraceDraft committed = Assert.Single(
            tracker.ObserveNativeCommit("native-root", completion));
        Assert.Equal(SemanticBoundaryTraceKinds.NativeCommitObserved, committed.Kind);
        Assert.True(tracker.HasUnresolvedActions);
        Assert.True(tracker.CanOpenNextRoot);

        tracker.Accept(Action("next-root", 2), State("human-s1"));
        IReadOnlyList<SemanticBoundaryTraceDraft> handoff =
            tracker.ObserveBeforeActionExecution(
                "next-root",
                Boundary("s1", "next-root"));
        SemanticBoundaryTraceDraft proved = Assert.Single(
            handoff,
            value => value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
        Assert.Equal("native-root", proved.Action.ActionWitnessId);
        Assert.Equal("s1", proved.SemanticSuccessor!.SnapshotId);
        Assert.Same(completion, proved.NativeCompletion);
    }

    [Fact]
    public void NativeOwnerReadyWithoutTypedEvidenceDoesNotSettle()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("native-root", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("native-root", Boundary("s0", "native-root"));
        tracker.Started("native-root");
        tracker.Finished("native-root");

        var unproved = new SemanticBoundaryObservation(
            SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady,
            T0,
            "s1",
            "interactive",
            "complete",
            "interaction-s1",
            "combat_turn",
            State("s1"),
            null);

        Assert.Empty(tracker.ObserveDecisionBoundary(unproved));
        Assert.True(tracker.HasUnresolvedActions);
    }

    [Fact]
    public void NativeOwnerReadyForDifferentDomainDoesNotSettle()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("native-root", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("native-root", Boundary("s0", "native-root"));
        tracker.Started("native-root");
        tracker.Finished("native-root");

        SemanticBoundaryObservation wrongDomain = PostCommitBoundary("s1") with
        {
            NativeDecisionOwnerReady = new NativeDecisionOwnerReadyEvidence(
                "map_route",
                "decision-owner-1",
                "MegaCrit.Sts2.Core.Combat.CombatState",
                "exact-native-test-seam")
        };

        Assert.Empty(tracker.ObserveDecisionBoundary(wrongDomain));
        Assert.True(tracker.HasUnresolvedActions);
    }

    [Fact]
    public void NativeOwnerReadyAndNextRootHandoffCannotDoubleSettle()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("first", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("first", Boundary("s0", "first"));
        tracker.Started("first");
        tracker.Finished("first");

        SemanticBoundaryTraceDraft proved = Assert.Single(
            tracker.ObserveDecisionBoundary(PostCommitBoundary("s1")));
        tracker.Accept(Action("next", 2), State("human-s1"));
        IReadOnlyList<SemanticBoundaryTraceDraft> handoff =
            tracker.ObserveBeforeActionExecution("next", Boundary("s1", "next"));

        Assert.Equal("first", proved.Action.ActionWitnessId);
        Assert.DoesNotContain(handoff, value => value.Action.ActionWitnessId == "first");
    }

    [Fact]
    public void NativeOwnerReadyWithIncompleteConnectorFrameDoesNotSettle()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("native-root", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("native-root", Boundary("s0", "native-root"));
        tracker.Started("native-root");
        tracker.Finished("native-root");

        SemanticBoundaryObservation incomplete = PostCommitBoundary("s1") with
        {
            BoundActionsStatus = "unavailable",
            State = null,
            StateCompleteness = "partial",
            RequiredReadsStatus = "unavailable"
        };

        Assert.Empty(tracker.ObserveDecisionBoundary(incomplete));
        Assert.True(tracker.HasUnresolvedActions);
    }

    [Fact]
    public void CardRewardOwnerCommitOpensTheExactChildDecision()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference claim = Action(
            "reward-claim",
            1,
            "NRewardButton.OnRelease") with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(claim, State("reward-s0", "reward_claim"));
        tracker.ObserveBeforeActionExecution(
            claim.ActionWitnessId,
            Boundary("reward-s0", claim.ActionWitnessId, "reward_claim"));
        tracker.Started(claim.ActionWitnessId);
        tracker.Finished(claim.ActionWitnessId);
        tracker.ObserveNativeCommit(
            claim.ActionWitnessId,
            new NativeCompletionEvidence(
                "card-owner-ready",
                "reward_claim",
                "NCardRewardSelectionScreen.ShowScreen",
                claim.ActionWitnessId,
                null,
                "card-reward-screen",
                "card-reward",
                null,
                true));

        Assert.True(tracker.CanOpenNextRoot);

        SemanticActionReference select = Action(
            "card-select",
            2,
            "NCardRewardSelectionScreen.SelectCard") with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(select, State("cards-s1", "card_reward_selection"));
        IReadOnlyList<SemanticBoundaryTraceDraft> handoff =
            tracker.ObserveBeforeActionExecution(
                select.ActionWitnessId,
                Boundary("cards-s1", select.ActionWitnessId, "card_reward_selection"));

        SemanticBoundaryTraceDraft proved = Assert.Single(
            handoff,
            value => value.Action.ActionWitnessId == claim.ActionWitnessId
                     && value.Kind == SemanticBoundaryTraceKinds.TransitionProved);
        Assert.Equal("reward-s0", proved.SemanticPre!.SnapshotId);
        Assert.Equal("cards-s1", proved.SemanticSuccessor!.SnapshotId);
    }

    [Fact]
    public void FailedNativeCompletionIsAccountedWithoutAFalseSuccessor()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference action = Action("native-root", 1) with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(action, State("human-s0"));
        tracker.ObserveBeforeActionExecution("native-root", Boundary("s0", "native-root"));
        tracker.Started("native-root");
        tracker.Finished("native-root");

        NativeCompletionEvidence completion = new(
            "completion-native-root",
            "reward_claim",
            "native.select",
            "native-root",
            "task-native-root",
            "owner-native-root",
            "operand-native-root",
            null,
            false);
        SemanticBoundaryTraceDraft unknown = Assert.Single(
            tracker.NativeCompletionFailed(
                "native-root",
                "native_completion_failed",
                "native task failed",
                completion));

        Assert.Equal(SemanticBoundaryTraceKinds.TransitionUnknown, unknown.Kind);
        Assert.Null(unknown.SemanticSuccessor);
        Assert.Same(completion, unknown.NativeCompletion);
    }

    [Fact]
    public void LaterExecutionBeforeEarlierCommitCannotCreateCrossHumanProof()
    {
        var tracker = new SemanticBoundaryTracker();
        SemanticActionReference first = Action("first", 1) with
        {
            RequiresNativePostCommit = true
        };
        SemanticActionReference second = Action("second", 2) with
        {
            RequiresNativePostCommit = true
        };
        tracker.Accept(first, State("human-first"));
        tracker.Accept(second, State("human-second"));
        tracker.ObserveBeforeActionExecution("first", Boundary("s0", "first"));
        tracker.Started("first");
        tracker.Finished("first");
        tracker.Started("second");
        tracker.Finished("second");

        SemanticBoundaryTraceDraft unknown = Assert.Single(
            tracker.ObserveNativeCommit("first", Completion("first")));

        Assert.Equal(SemanticBoundaryTraceKinds.TransitionUnknown, unknown.Kind);
        Assert.Equal("intervening_human_action_before_native_commit", unknown.ProofStatus);
        Assert.Null(unknown.SemanticSuccessor);
    }

    [Fact]
    public void ValidatorRequiresNativeCompletionOnNativeProofs()
    {
        SemanticActionReference action = Action("native-root", 1) with
        {
            RequiresNativePostCommit = true
        };
        SemanticBoundaryTraceEvent[] events =
        {
            Event(1, SemanticBoundaryTraceKinds.ActionAccepted, action),
            Event(2, SemanticBoundaryTraceKinds.ActionStarted, action),
            Event(3, SemanticBoundaryTraceKinds.ActionFinished, action),
            Event(4, SemanticBoundaryTraceKinds.TransitionProved, action) with
            {
                Boundary = PostCommitBoundary("s1"),
                SemanticPre = State("s0"),
                SemanticSuccessor = State("s1")
            }
        };

        Assert.Contains(
            "semantic_native_commit_identity_missing",
            SemanticBoundaryTraceValidator.Validate(events));
    }

    [Fact]
    public void PeriodicInteractiveObservationCannotProveNonPausedAction()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("a1", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");

        Assert.Empty(tracker.ObserveDecisionBoundary(Boundary("polled-s1")));
        Assert.Empty(tracker.ObserveDecisionBoundary(
            PostCommitBoundary("native-s1") with
            {
                WitnessKind = SemanticBoundaryWitnessKinds.NativeUiPostCommit
            }));
    }

    [Fact]
    public void HistoricalPollingSuccessorCannotProveSemanticBoundary()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("a1", 1), State("human-s0"));
        tracker.ObserveBeforeActionExecution("a1", Boundary("s0", "a1"));
        tracker.Started("a1");
        tracker.Finished("a1");

        SemanticBoundaryObservation legacyPolling = PostCommitBoundary("s1") with
        {
            WitnessKind = SemanticBoundaryWitnessKinds.HistoricalPollingSuccessor
        };

        Assert.Empty(tracker.ObserveDecisionBoundary(legacyPolling));
        Assert.True(tracker.HasUnresolvedActions);
    }

    [Fact]
    public void PlayerChoiceParentSurvivesChildAcceptanceUntilNativeFinish()
    {
        var tracker = new SemanticBoundaryTracker();
        tracker.Accept(Action("parent", 1), State("human-parent"));
        tracker.ObserveBeforeActionExecution("parent", Boundary("s0", "parent"));
        tracker.Started("parent");
        tracker.PausedForPlayerChoice("parent");
        tracker.ObserveDecisionBoundary(Boundary("choice-s1", interactionKind: "generated_card_choice"));

        tracker.Accept(Action("child", 2, "NChooseACardSelectionScreen.SelectHolder"), State("choice-s1"));

        Assert.True(tracker.Contains("parent"));
        Assert.Equal(
            "player_choice_supplied",
            Assert.Single(tracker.ReadyToResume("parent")).ProofStatus);
        tracker.Resumed("parent");
        Assert.Equal(
            "lifecycle_finished_after_semantic_disposition",
            Assert.Single(tracker.Finished("parent")).ProofStatus);

        tracker.Accept(Action("later", 3), State("human-later"));
        Assert.False(tracker.Contains("parent"));
    }

    [Fact]
    public void LegacySchemaOneTraceRemainsReadableWithoutChangingItsMeaning()
    {
        SemanticActionReference action = Action("legacy", 1);
        SemanticBoundaryTraceEvent accepted = Event(
            1,
            SemanticBoundaryTraceKinds.ActionAccepted,
            action) with
        {
            SchemaVersion = SemanticBoundaryTraceContract.LegacySchemaVersion,
            Schema = SemanticBoundaryTraceContract.LegacyEventSchema,
            SemanticPre = State("legacy-s0")
        };
        SemanticBoundaryTraceEvent cancelled = Event(
            2,
            SemanticBoundaryTraceKinds.ActionCancelledBeforeStart,
            action) with
        {
            SchemaVersion = SemanticBoundaryTraceContract.LegacySchemaVersion,
            Schema = SemanticBoundaryTraceContract.LegacyEventSchema
        };

        Assert.Empty(SemanticBoundaryTraceValidator.Validate(new[] { accepted, cancelled }));
    }

    private static SemanticActionReference Action(
        string id,
        long sequence,
        string nativeActionType = "PlayCardAction") => new(
        id,
        sequence,
        $"record-{id}",
        "run-0001",
        nativeActionType,
        (uint)sequence,
        $"human-{id}");

    private static SemanticBoundaryObservation Boundary(
        string snapshotId,
        string? nextAction = null,
        string interactionKind = "combat_turn") => new(
            nextAction == null
                ? SemanticBoundaryWitnessKinds.CompleteInteractiveObservation
                : SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
            T0,
            snapshotId,
            "interactive",
            "complete",
            $"interaction-{snapshotId}",
            interactionKind,
            State(snapshotId, interactionKind),
            nextAction);

    private static SemanticBoundaryObservation PostCommitBoundary(
        string snapshotId,
        string interactionKind = "combat_turn") => new(
            SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady,
            T0,
            snapshotId,
            "interactive",
            "complete",
            $"interaction-{snapshotId}",
            interactionKind,
            State(snapshotId, interactionKind),
            null)
        {
            NativeDecisionOwnerReady = new NativeDecisionOwnerReadyEvidence(
                interactionKind,
                $"decision-owner-{snapshotId}",
                "ExactNativeDecisionOwner",
                "exact-native-test-seam")
        };

    private static SemanticBoundaryObservation IncompleteBoundary(
        string snapshotId,
        string nextAction) => new(
            SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
            T0,
            snapshotId,
            "settling",
            "unavailable",
            $"interaction-{snapshotId}",
            "combat_turn",
            null,
            nextAction);

    private static SemanticBoundaryObservation StateOnlyExecutionBoundary(
        string snapshotId,
        string nextAction) => new(
            SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
            T0,
            snapshotId,
            "settling",
            "unavailable",
            $"interaction-{snapshotId}",
            "combat_turn",
            State(snapshotId),
            nextAction)
        {
            StateCompleteness = "complete",
            RequiredReadsStatus = "complete"
        };

    private static CurrentDecisionFrame State(
        string snapshotId,
        string interactionKind = "combat_turn") => new(
        snapshotId,
        $"interaction-{snapshotId}",
        interactionKind,
        $"sts2.player-environment/surface/{interactionKind}-1",
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

    private static SemanticBoundaryTraceEvent Event(
        int sequence,
        SemanticBoundaryTraceDraft draft) => new(
            SemanticBoundaryTraceContract.SchemaVersion,
            SemanticBoundaryTraceContract.EventSchema,
            $"event-{sequence}",
            "session-test",
            "timeline-test",
            "run-0001",
            sequence,
            T0.AddMilliseconds(sequence),
            draft.Kind,
            draft.Action,
            draft.ProofStatus,
            draft.RelatedActionWitnessId,
            draft.Boundary,
            draft.SemanticPre,
            draft.SemanticSuccessor,
            draft.Detail,
            draft.NonClaims ?? Array.Empty<string>())
        {
            HumanObservation = draft.HumanObservation,
            NativeCompletion = draft.NativeCompletion,
            NativeContinuation = draft.NativeContinuation,
            ExecutionSemanticActionSpace = draft.ExecutionSemanticActionSpace
        };

    private static NativeCompletionEvidence Completion(string actionWitnessId) => new(
        $"completion-{actionWitnessId}",
        "test",
        "native.test",
        actionWitnessId,
        $"task-{actionWitnessId}",
        $"owner-{actionWitnessId}",
        $"operand-{actionWitnessId}",
        null,
        true);
}
