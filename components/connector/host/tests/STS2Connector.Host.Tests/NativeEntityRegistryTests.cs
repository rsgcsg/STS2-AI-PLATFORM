using STS2Connector.NativeUi;

namespace STS2Connector.Tests;

public sealed class NativeEntityRegistryTests
{
    [Fact]
    public void ExactObjectCanBeResolvedByItsStableEntityId()
    {
        var registry = new NativeEntityRegistry();
        var entity = new object();

        string id = registry.GetId(entity, "card");

        Assert.True(registry.TryResolve<object>(id, out object? resolved));
        Assert.Same(entity, resolved);
        Assert.Equal(id, registry.GetId(entity, "card"));
    }

    [Fact]
    public void UnknownOrWrongTypedEntityFailsClosed()
    {
        var registry = new NativeEntityRegistry();
        var entity = new object();
        string id = registry.GetId(entity, "card");

        Assert.False(registry.TryResolve<string>(id, out _));
        Assert.True(registry.TryResolve<object>(id, out object? resolved));
        Assert.Same(entity, resolved);
        Assert.False(registry.TryResolve<object>("card_missing_1", out _));
    }

    [Fact]
    public void PruningRetainsLiveReferencesAndRemovesDeadEntries()
    {
        var registry = new NativeEntityRegistry();
        object live = new();
        string liveId = registry.GetId(live, "card");
        int before = registry.TrackedReferenceCount;

        string deadId = AddShortLivedEntity(registry);
        ForceCollection();

        Assert.True(registry.TryResolve<object>(liveId, out object? resolved));
        Assert.Same(live, resolved);
        Assert.True(registry.PruneDeadEntries() >= 1);
        Assert.False(registry.TryResolve<object>(deadId, out _));
        Assert.Equal(before, registry.TrackedReferenceCount);
    }

    private static string AddShortLivedEntity(NativeEntityRegistry registry)
    {
        object entity = new();
        return registry.GetId(entity, "temporary");
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
