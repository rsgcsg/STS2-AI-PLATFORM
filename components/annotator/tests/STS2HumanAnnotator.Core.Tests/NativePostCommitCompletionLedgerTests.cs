using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class NativePostCommitCompletionLedgerTests
{
    [Fact]
    public void CompletionUsesExactRootAndNativeIdentityRatherThanRegistrationOrder()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));
        Assert.True(ledger.Register(Registration("root-b", "reward-b", "owner-b")));

        NativePostCommitCompletionResolution result = ledger.Complete(
            Completion("root-b", "reward-b", "owner-b"));

        Assert.True(result.IsMatched);
        Assert.Equal("root-b", result.Registration!.ActionWitnessId);
        Assert.Equal(1, ledger.Count);
        Assert.Equal("no_match", ledger.Complete(
            Completion("root-b", "reward-b", "owner-b")).Status);
    }

    [Fact]
    public void MismatchedOperandOrOwnerCannotSettleAnExactRoot()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        NativePostCommitCompletionResolution operandMismatch = ledger.Complete(
            Completion("root-a", "reward-a-other", "owner-a"));
        Assert.Equal("no_match", operandMismatch.Status);
        Assert.Equal(1, ledger.Count);

        NativePostCommitCompletionResolution ownerMismatch = ledger.Complete(
            Completion("root-a", "reward-a", "owner-other"));
        Assert.Equal("no_match", ownerMismatch.Status);
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public void SessionAndGenerationDriftFailClosedAndDoNotConsumeRegistration()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        Assert.Equal("no_match", ledger.Complete(
            Completion("root-a", "reward-a", "owner-a", sessionId: "other-session")).Status);
        Assert.Equal("no_match", ledger.Complete(
            Completion("root-a", "reward-a", "owner-a", generation: 2)).Status);
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public void MissingRootIdentityCannotMatchEvenWhenNativeFactsAreUnique()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward", "owner")));
        Assert.True(ledger.Register(Registration("root-b", "reward", "owner")));

        NativePostCommitCompletionResolution result = ledger.Complete(
            Completion(null, "reward", "owner"));

        Assert.Equal("no_match", result.Status);
        Assert.Equal(2, ledger.Count);
    }

    [Fact]
    public void FailedCompletionMatchesOnceButNeverClaimsSuccess()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        NativePostCommitCompletionResolution result = ledger.Complete(
            Completion("root-a", "reward-a", "owner-a", succeeded: false));

        Assert.True(result.IsMatched);
        Assert.True(result.IsFailure);
        Assert.Equal(0, ledger.Count);
        Assert.Equal("no_match", ledger.Complete(
            Completion("root-a", "reward-a", "owner-a")).Status);
    }

    [Fact]
    public void MalformedCompletionCannotConsumeAnExactRoot()
    {
        var ledger = new NativePostCommitCompletionLedger();
        Assert.True(ledger.Register(Registration("root-a", "reward-a", "owner-a")));

        NativePostCommitCompletion malformed = Completion(
            "root-a",
            "reward-a",
            "owner-a") with
        {
            CompletionId = "",
            TaskWitnessId = ""
        };

        Assert.Equal("no_match", ledger.Complete(malformed).Status);
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

    private static NativePostCommitCompletion Completion(
        string? actionWitnessId,
        string operand,
        string owner,
        string sessionId = "session-a",
        long generation = 1,
        bool succeeded = true) =>
        new(
            sessionId,
            generation,
            $"completion-{Guid.NewGuid():N}",
            "reward_claim",
            "native.select",
            "task-a",
            succeeded,
            actionWitnessId,
            owner,
            operand);
}
