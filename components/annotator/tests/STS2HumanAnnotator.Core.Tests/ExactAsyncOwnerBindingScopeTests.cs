using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class ExactAsyncOwnerBindingScopeTests
{
    private sealed class Key;
    private sealed record Context(string Root);
    private sealed record Binding(string Root);

    [Fact]
    public void FactoryWithoutExactParentScopeCannotBind()
    {
        var scope = new ExactAsyncOwnerBindingScope<Key, Context, Binding>();

        Assert.False(scope.TryBindCurrent(new Key(), context => new Binding(context.Root)));
    }

    [Fact]
    public async Task CapturedContinuationRetainsImmutableParentAfterCallerDisposesScope()
    {
        var scope = new ExactAsyncOwnerBindingScope<Key, Context, Binding>();
        var key = new Key();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task child;
        using (scope.Enter(new Context("parent-a")))
        {
            child = Task.Run(async () =>
            {
                await release.Task;
                Assert.True(scope.TryBindCurrent(
                    key,
                    context => new Binding(context.Root)));
            });
        }

        release.SetResult();
        await child;

        Assert.True(scope.TryTake(key, out Binding? binding));
        Assert.Equal("parent-a", binding!.Root);
        Assert.False(scope.TryBindCurrent(new Key(), context => new Binding(context.Root)));
    }

    [Fact]
    public async Task ConcurrentScopesDoNotCollideOrUseLatestParent()
    {
        var scope = new ExactAsyncOwnerBindingScope<Key, Context, Binding>();
        var keyA = new Key();
        var keyB = new Key();
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task Bind(string root, Key key)
        {
            using (scope.Enter(new Context(root)))
            {
                await barrier.Task;
                Assert.True(scope.TryBindCurrent(
                    key,
                    context => new Binding(context.Root)));
            }
        }

        Task first = Task.Run(() => Bind("parent-a", keyA));
        Task second = Task.Run(() => Bind("parent-b", keyB));
        barrier.SetResult();
        await Task.WhenAll(first, second);

        Assert.True(scope.TryGet(keyA, out Binding? bindingA));
        Assert.True(scope.TryGet(keyB, out Binding? bindingB));
        Assert.Equal("parent-a", bindingA!.Root);
        Assert.Equal("parent-b", bindingB!.Root);
    }

    [Fact]
    public void ExactKeyCollisionIsIdempotentForSameBindingAndRejectsDifferentOwner()
    {
        var scope = new ExactAsyncOwnerBindingScope<Key, Context, Binding>();
        var replaced = new Key();
        var untouched = new Key();
        Assert.True(scope.TrySet(replaced, new Binding("old")));
        Assert.True(scope.TrySet(untouched, new Binding("other")));

        Assert.True(scope.TrySet(replaced, new Binding("old")));
        Assert.False(scope.TrySet(replaced, new Binding("new")));

        Assert.True(scope.TryGet(replaced, out Binding? replacement));
        Assert.True(scope.TryGet(untouched, out Binding? other));
        Assert.Equal("old", replacement!.Root);
        Assert.Equal("other", other!.Root);
    }
}
