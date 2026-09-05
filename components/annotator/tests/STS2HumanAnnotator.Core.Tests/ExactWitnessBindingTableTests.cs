using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class ExactWitnessBindingTableTests
{
    [Fact]
    public void SameWitnessCannotReplaceAnAliveDifferentOwner()
    {
        var table = new ExactWitnessBindingTable<object>();
        var first = new object();
        var second = new object();

        Assert.True(table.TryBind("root-a", first));
        Assert.False(table.TryBind("root-a", second));
        Assert.True(table.TryGet("root-a", out object? actual));
        Assert.Same(first, actual);
    }

    [Fact]
    public void RebindingSameOwnerIsIdempotentAndExactRemovalIsScoped()
    {
        var table = new ExactWitnessBindingTable<object>();
        var owner = new object();
        var other = new object();

        Assert.True(table.TryBind("root-a", owner));
        Assert.True(table.TryBind("root-a", owner));
        Assert.False(table.Remove("root-a", other));
        Assert.True(table.TryGet("root-a", out object? actual));
        Assert.Same(owner, actual);
        Assert.True(table.Remove("root-a", owner));
        Assert.False(table.TryGet("root-a", out _));
    }
}
