using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class NativeSynchronousOwnerHandoffTests
{
    [Theory]
    [InlineData("complete", true)]
    [InlineData("not_owned", false)]
    [InlineData("other_scope", false)]
    [InlineData("no_scope", false)]
    [InlineData("already_open", false)]
    [InlineData("still_closed", false)]
    [InlineData("replacement_owner", false)]
    [InlineData("missing_owner", false)]
    [InlineData("async_pending", false)]
    [InlineData("cancelled", false)]
    [InlineData("faulted", false)]
    public void OnlyTheSameSynchronousHumanInvocationCanObserveItsNewOwner(string condition, bool expected)
    {
        object map = new();
        var pending = new TaskCompletionSource();
        Task completion = condition switch
        {
            "async_pending" => pending.Task,
            "cancelled" => Task.FromCanceled(new CancellationToken(true)),
            "faulted" => Task.FromException(new InvalidOperationException("native failed")),
            _ => Task.CompletedTask
        };

        Assert.Equal(expected, NativeSynchronousOwnerHandoff.Matches(
            completion,
            condition == "missing_owner" ? null : map,
            condition == "already_open",
            condition == "replacement_owner" ? new object() : map,
            condition != "still_closed",
            condition == "not_owned" ? null : "event-root",
            condition == "no_scope" ? null : condition == "other_scope" ? "other-root" : "event-root"));

        // Matching is read-only and never waits for or completes a native task.
        Assert.False(pending.Task.IsCompleted);
    }
}
