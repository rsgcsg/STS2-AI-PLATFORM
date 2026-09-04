using System;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// Read-only facts for the normal boss/act transition.  STS2 accepts the
/// ready input by enqueueing a vote action, and only that action's execution
/// reaches the native readiness Commit.  No successor state is synthesized.
/// </summary>
public static class NativeActChangeDecisionProvider
{
    public static event Action<NativeActChangeObservation>? Observed;

    public const string AcceptedSeam =
        "ActChangeSynchronizer.SetLocalPlayerReady->ActionQueueSynchronizer.RequestEnqueue";
    public const string VoteActionType = nameof(VoteToMoveToNextActAction);
    public const string CommitSeam =
        "VoteToMoveToNextActAction.ExecuteAction->ActChangeSynchronizer.OnPlayerReady";
    public const string ConditionalNextBoundary =
        "ActChangeSynchronizer.OnPlayerReady(all_ready)->RunManager.EnterNextAct->ActEntered";

    public static NativeActChangeDecision Capture()
    {
        try
        {
            RunState? runState = RunManager.Instance.DebugOnlyGetState();
            if (runState == null)
                return Unavailable("No current RunState is available.");

            bool waiting = RunManager.Instance.ActChangeSynchronizer
                .IsWaitingForOtherPlayers();
            return new NativeActChangeDecision(
                "captured",
                "act_change",
                runState.CurrentActIndex,
                waiting,
                AcceptedSeam,
                VoteActionType,
                CommitSeam,
                ConditionalNextBoundary,
                "The next boundary is conditional on all native readiness votes; it is not asserted by Capture.");
        }
        catch (Exception exception)
        {
            return Unavailable($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>
    /// Exact read-only description used by a native owner patch after
    /// SetLocalPlayerReady returns.  The method does not imply that the
    /// queued action has executed.
    /// </summary>
    public static NativeActChangeObservation ObserveReadyRequest()
    {
        NativeActChangeObservation observation = new(
            "ActChangeSynchronizer.SetLocalPlayerReady",
            AcceptedSeam,
            CommitSeam,
            ConditionalNextBoundary,
            "accepted_enqueue_not_commit");
        Observed?.Invoke(observation);
        return observation;
    }

    /// <summary>
    /// Exact read-only description used after VoteToMoveToNextActAction's
    /// native ExecuteAction returns.  This is the readiness Commit, not a
    /// claim that EnterNextAct or ActEntered has already occurred.
    /// </summary>
    public static NativeActChangeObservation ObserveVoteCommit(
        VoteToMoveToNextActAction action)
    {
        NativeActChangeObservation observation = new(
            VoteActionType,
            AcceptedSeam,
            CommitSeam,
            ConditionalNextBoundary,
            $"commit_observed:act={action.CurrentActIndex}:owner={action.OwnerId}");
        Observed?.Invoke(observation);
        return observation;
    }

    private static NativeActChangeDecision Unavailable(string detail) =>
        new(
            "unavailable",
            "act_change",
            null,
            false,
            AcceptedSeam,
            VoteActionType,
            CommitSeam,
            ConditionalNextBoundary,
            detail);
}

public sealed record NativeActChangeDecision(
    string Status,
    string Scope,
    int? CurrentActIndex,
    bool IsWaitingForOtherPlayers,
    string AcceptedSeam,
    string VoteActionType,
    string CommitSeam,
    string ConditionalNextBoundary,
    string? Detail);

public sealed record NativeActChangeObservation(
    string NativeOwner,
    string AcceptedSeam,
    string CommitSeam,
    string ConditionalNextBoundary,
    string Status);
