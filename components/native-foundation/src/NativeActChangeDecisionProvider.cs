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
    public const string AcceptedSeam =
        "ActChangeSynchronizer.SetLocalPlayerReady->ActionQueueSynchronizer.RequestEnqueue";
    public const string VoteActionType = nameof(VoteToMoveToNextActAction);
    public const string CommitSeam =
        "VoteToMoveToNextActAction.ExecuteAction->ActChangeSynchronizer.OnPlayerReady";
    public const string OwnerReadySeam = "ActChangeSynchronizer.OnPlayerReady";
    public const string ConditionalNextBoundary =
        "ActChangeSynchronizer.OnPlayerReady(all_ready)->RunManager.EnterNextAct->ActEntered";

    /// <summary>
    /// Typed, process-local facts for a consumer that already owns the native
    /// callback. No Harmony callback is installed here and no successor is
    /// synthesized by the contract.
    /// </summary>
    public static NativeActChangeFactContract Contract { get; } =
        new(
            AcceptedSeam,
            VoteActionType,
            CommitSeam,
            OwnerReadySeam,
            ConditionalNextBoundary);

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
                OwnerReadySeam,
                ConditionalNextBoundary,
                "The next boundary is conditional on all native readiness votes; it is not asserted by Capture.");
        }
        catch (Exception exception)
        {
            return Unavailable($"{exception.GetType().Name}: {exception.Message}");
        }
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
            OwnerReadySeam,
            ConditionalNextBoundary,
            detail);
}

public sealed record NativeActChangeFactContract(
    string AcceptedSeam,
    string VoteActionType,
    string CommitSeam,
    string OwnerReadySeam,
    string ConditionalNextBoundary);

public sealed record NativeActChangeDecision(
    string Status,
    string Scope,
    int? CurrentActIndex,
    bool IsWaitingForOtherPlayers,
    string AcceptedSeam,
    string VoteActionType,
    string CommitSeam,
    string OwnerReadySeam,
    string ConditionalNextBoundary,
    string? Detail);
