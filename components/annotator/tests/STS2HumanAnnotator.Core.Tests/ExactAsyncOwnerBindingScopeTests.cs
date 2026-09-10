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

        Assert.True(scope.TryGet(key, out Binding? binding));
        Assert.Equal("parent-a", binding!.Root);
        Assert.True(scope.TryReserve(key, out Binding? reserved));
        Assert.Same(binding, reserved);
        Assert.True(scope.TryConsume(key, binding));
        Assert.False(scope.TryGet(key, out _));
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

    [Fact]
    public void TerminalConsumeIsCompareAndRemoveAfterSuccessfulPersistence()
    {
        var scope = new ExactAsyncOwnerBindingScope<Key, Context, Binding>();
        var key = new Key();
        var exact = new Binding("root-a");
        var stale = new Binding("root-b");

        Assert.True(scope.TrySet(key, exact));
        Assert.True(scope.TryGet(key, out Binding? observed));

        // A failed durable append leaves the carrier available for the exact
        // native callback to report/retry; merely reading never consumes it.
        Assert.Same(exact, observed);
        Assert.True(scope.TryGet(key, out _));

        Assert.True(scope.TryReserve(key, out Binding? reserved));
        Assert.Same(exact, reserved);
        Assert.False(scope.TryConsume(key, stale));
        Assert.True(scope.TryGet(key, out _));
        Assert.True(scope.TryRelease(key, exact));
        Assert.True(scope.TryReserve(key, out reserved));
        Assert.True(scope.TryConsume(key, exact));
        Assert.False(scope.TryConsume(key, exact));
        Assert.False(scope.TryGet(key, out _));
    }

    [Fact]
    public void ConcurrentTerminalReservationsAllowOnlyOneWriter()
    {
        var scope = new ExactAsyncOwnerBindingScope<Key, Context, Binding>();
        var key = new Key();
        var binding = new Binding("root-a");
        Assert.True(scope.TrySet(key, binding));

        Assert.True(scope.TryReserve(key, out Binding? first));
        Assert.False(scope.TryReserve(key, out _));
        Assert.True(scope.TryRelease(key, first!));
        Assert.True(scope.TryReserve(key, out Binding? second));
        Assert.Same(binding, second);
        Assert.True(scope.TryConsume(key, second!));
    }

    [Fact]
    public void TerminalCallbackReservationPreventsExitTreeFromStealingCarrier()
    {
        var scope = new ExactAsyncOwnerBindingScope<Key, Context, Binding>();
        var key = new Key();
        var binding = new Binding("root-a");
        Assert.True(scope.TrySet(key, binding));

        // The terminal callback reserves in its prefix, before native code can
        // synchronously queue/free the screen and run _ExitTree. Teardown must
        // not append an unavailable disposition for the same carrier.
        Assert.True(scope.TryReserve(key, out Binding? terminalIntent));
        Assert.False(scope.TryReserve(key, out _));
        Assert.True(scope.TryConsume(key, terminalIntent!));
        Assert.False(scope.TryGet(key, out _));
    }
    [Fact]
    public async Task OptionPrefixBindingSurvivesSynchronousFactoryAndTaskCarrierConsumption()
    {
        var owners = new ExactOwnerWitnessBindingTable<object>();
        var selectors = new ExactAsyncOwnerBindingScope<Key, Context, Binding>();
        var option = new object();
        var screen = new Key();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(owners.TryBind(option, "event-root")); // UI Prefix
        Assert.True(owners.TryGetWitness(option, out var root)); // Chosen Prefix
        Task choice;
        using (selectors.Enter(new Context(root!)))
            choice = OpenAndWait(); // factory runs before UI Postfix
        Assert.True(owners.TakeIfMatches(option, "event-root")); // exact Task takes over
        Assert.True(selectors.TryGet(screen, out var binding));
        Assert.Equal("event-root", binding!.Root);
        Assert.False(owners.TryGetWitness(new object(), out _));
        completed.SetResult();
        await choice;

        async Task OpenAndWait()
        {
            Assert.True(selectors.TryBindCurrent(screen, context => new Binding(context.Root)));
            await completed.Task;
            Assert.True(selectors.TryGet(screen, out var exact));
            Assert.Equal("event-root", exact!.Root);
        }
    }
}
