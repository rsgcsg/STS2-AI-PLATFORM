using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class NativePostCommitCompletionLedgerTests
{
    [Fact]
    public void NativeTaskBindsAfterUiScopeUsingExactOperationIdentity()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        NativeTaskBindingResolution binding = ledger.BindTask(
            new NativeTaskObservation(
                "session-a",
                1,
                "native.select",
                "task-a",
                "owner-a",
                "reward-a"));

        Assert.True(binding.IsMatched);
        Assert.Equal("root-a", binding.Binding!.ActionWitnessId);
        Assert.Equal("reward_claim", binding.Binding.Family);

        NativePostCommitCompletionResolution completion = ledger.CompleteTask(
            new NativeTaskCompletion(
                "session-a",
                1,
                "completion-a",
                "task-a",
                true));

        Assert.True(completion.IsMatched);
        Assert.Equal("root-a", completion.Registration!.ActionWitnessId);
        Assert.Equal("root-a", completion.Completion!.ActionWitnessId);
    }

    [Fact]
    public void SharedNativeMethodGetsFamilyFromExactRootRatherThanCallback()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(new NativePostCommitCompletionRegistration(
            "session-a",
            1,
            "reward-proceed",
            new NativePostCommitCompletionExpectation(
                "reward_proceed",
                "RunManager.ProceedFromTerminalRewardsScreen",
                "run-manager",
                "reward-room"))));
        Assert.True(ledger.Register(new NativePostCommitCompletionRegistration(
            "session-a",
            1,
            "treasure-proceed",
            new NativePostCommitCompletionExpectation(
                "treasure_proceed",
                "RunManager.ProceedFromTerminalRewardsScreen",
                "run-manager",
                "treasure-room"))));

        NativeTaskBindingResolution binding = ledger.BindTask(
            new NativeTaskObservation(
                "session-a",
                1,
                "RunManager.ProceedFromTerminalRewardsScreen",
                "task-shared",
                "run-manager",
                "reward-room"));

        Assert.True(binding.IsMatched);
        Assert.Equal("reward-proceed", binding.Binding!.ActionWitnessId);
        Assert.Equal("reward_proceed", binding.Binding.Family);
    }

    [Fact]
    public void OneNativeRootMayBindOneOfSeveralExactSts2CommitRoutes()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(new NativePostCommitCompletionRegistration(
            "session-a",
            1,
            "reward-proceed",
            new NativePostCommitCompletionExpectation(
                "reward_proceed",
                "RunManager.ProceedFromTerminalRewardsScreen",
                AlternativeKinds: new[]
                {
                    "RewardsSetSynchronizer.SkipLocalRewardsSet"
                }))));

        NativeTaskBindingResolution binding = ledger.BindTask(
            new NativeTaskObservation(
                "session-a",
                1,
                "RewardsSetSynchronizer.SkipLocalRewardsSet",
                "sync-operation-a"));

        Assert.True(binding.IsMatched);
        Assert.Equal("RewardsSetSynchronizer.SkipLocalRewardsSet", binding.Binding!.Kind);
        Assert.Equal("reward-proceed", binding.Binding.ActionWitnessId);
    }

    [Fact]
    public void AmbiguousTaskBindingFailsClosedWithoutConsumingRoots()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(new NativePostCommitCompletionRegistration(
            "session-a",
            1,
            "root-a",
            new NativePostCommitCompletionExpectation(
                "reward_claim",
                "native.select",
                "owner"))));
        Assert.True(ledger.Register(new NativePostCommitCompletionRegistration(
            "session-a",
            1,
            "root-b",
            new NativePostCommitCompletionExpectation(
                "treasure_open",
                "native.select",
                "owner"))));

        NativeTaskBindingResolution binding = ledger.BindTask(
            new NativeTaskObservation(
                "session-a",
                1,
                "native.select",
                "task-a",
                "owner"));

        Assert.Equal("ambiguous", binding.Status);
        Assert.Equal(2, ledger.Count);
    }

    [Fact]
    public void ExactRootHintResolvesSharedNativeCallbackWithoutFallback()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(new NativePostCommitCompletionRegistration(
            "session-a",
            1,
            "root-a",
            new NativePostCommitCompletionExpectation(
                "reward_proceed",
                "native.shared"))));
        Assert.True(ledger.Register(new NativePostCommitCompletionRegistration(
            "session-a",
            1,
            "root-b",
            new NativePostCommitCompletionExpectation(
                "treasure_proceed",
                "native.shared"))));

        NativeTaskObservation observation = new(
            "session-a",
            1,
            "native.shared",
            "task-a");
        NativeTaskBindingResolution binding = ledger.BindTask(observation, "root-b");

        Assert.True(binding.IsMatched);
        Assert.Equal("root-b", binding.Binding!.ActionWitnessId);
        Assert.Equal(2, ledger.Count);
        Assert.Equal(
            "no_match",
            ledger.BindTask(
                observation with { Kind = "native.other", TaskWitnessId = "task-b" },
                "root-a").Status);
        Assert.Equal(2, ledger.Count);
    }

    [Fact]
    public void MismatchedTaskIdentityCannotConsumeAnExactRoot()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        Assert.Equal("no_match", ledger.BindTask(new NativeTaskObservation(
            "session-a", 1, "native.select", "task-a", "owner-a", "other-operand")).Status);
        Assert.Equal("no_match", ledger.BindTask(new NativeTaskObservation(
            "session-a", 1, "native.select", "task-b", "other-owner", "reward-a")).Status);
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public void SessionAndGenerationDriftFailClosedAcrossBindingAndCompletion()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        Assert.Equal("no_match", ledger.BindTask(new NativeTaskObservation(
            "other-session", 1, "native.select", "task-a", "owner-a", "reward-a")).Status);
        Assert.Equal("no_match", ledger.BindTask(new NativeTaskObservation(
            "session-a", 2, "native.select", "task-a", "owner-a", "reward-a")).Status);

        Assert.True(ledger.BindTask(new NativeTaskObservation(
            "session-a", 1, "native.select", "task-a", "owner-a", "reward-a")).IsMatched);
        Assert.Equal("no_match", ledger.CompleteTask(new NativeTaskCompletion(
            "other-session", 1, "completion-a", "task-a", true)).Status);
        Assert.Equal("no_match", ledger.CompleteTask(new NativeTaskCompletion(
            "session-a", 2, "completion-a", "task-a", true)).Status);
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public void FailedCompletionMatchesOnceButNeverClaimsSuccess()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        Assert.True(ledger.BindTask(new NativeTaskObservation(
            "session-a", 1, "native.select", "task-a", "owner-a", "reward-a")).IsMatched);
        NativePostCommitCompletionResolution result = ledger.CompleteTask(
            new NativeTaskCompletion("session-a", 1, "completion-a", "task-a", false));

        Assert.True(result.IsMatched);
        Assert.True(result.IsFailure);
        Assert.Equal(0, ledger.Count);
        Assert.Equal("no_match", ledger.CompleteTask(
            new NativeTaskCompletion("session-a", 1, "completion-b", "task-a", true)).Status);
    }

    [Fact]
    public void MalformedTaskCompletionCannotConsumeAnExactBinding()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        Assert.True(ledger.BindTask(new NativeTaskObservation(
            "session-a", 1, "native.select", "task-a", "owner-a", "reward-a")).IsMatched);
        Assert.Equal("no_match", ledger.CompleteTask(new NativeTaskCompletion(
            "session-a", 1, "", "task-a", true)).Status);
        Assert.Equal("no_match", ledger.CompleteTask(new NativeTaskCompletion(
            "session-a", 1, "completion-a", "", true)).Status);
        Assert.Equal(1, ledger.Count);
    }

    private static NativePostCommitCompletionRegistration Registration(
        string actionWitnessId,
        string operand,
        string owner,
        string sessionId = "session-a",
        long generation = 1) =>
        new(
            sessionId,
            generation,
            actionWitnessId,
            new NativePostCommitCompletionExpectation(
                "reward_claim",
                "native.select",
                NativeOwnerWitnessId: owner,
                NativeOperandWitnessId: operand));

}
