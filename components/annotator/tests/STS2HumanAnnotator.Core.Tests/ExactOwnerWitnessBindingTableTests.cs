using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class ExactOwnerWitnessBindingTableTests
{
    [Fact]
    public void NonGameOwnerCannotReplaceAnotherWitnessOnSameObject()
    {
        var table = new ExactOwnerWitnessBindingTable<object>();
        var owner = new object();
        Assert.True(table.TryBind(owner, "root-a"));
        Assert.False(table.TryBind(owner, "root-b"));
        Assert.True(table.TryGetWitness(owner, out string? witness));
        Assert.Equal("root-a", witness);
    }

    [Fact]
    public void SameWitnessCannotBindTwoAliveOwners()
    {
        var table = new ExactOwnerWitnessBindingTable<object>();
        var first = new object();
        var second = new object();
        Assert.True(table.TryBind(first, "root-a"));
        Assert.False(table.TryBind(second, "root-a"));
        Assert.True(table.TryGetOwner("root-a", out object? owner));
        Assert.Same(first, owner);
    }

    [Fact]
    public void ExactPairRemovalCannotConsumeAnotherCarrier()
    {
        var table = new ExactOwnerWitnessBindingTable<object>();
        var owner = new object();
        Assert.True(table.TryBind(owner, "root-a"));
        Assert.False(table.TakeIfMatches(owner, "root-b"));
        Assert.True(table.Contains(owner));
        Assert.True(table.TakeIfMatches(owner, "root-a"));
        Assert.False(table.TryGetOwner("root-a", out _));
    }
}
