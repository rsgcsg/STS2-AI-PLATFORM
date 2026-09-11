using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class ExactNativeInputBindingTableTests
{
    private sealed record Input(string Session, string Snapshot, string? Failure, AcceptedRootActionGate Gate);

    [Fact]
    public void DeferredNativeRequestRetainsFrozenInputAfterAmbientScopeEnds()
    {
        var table = new ExactNativeInputBindingTable<object, Input>();
        object action = new();
        var input = new Input("session-a", "human-before-enemy-turn", null, new("UsePotionAction"));
        Input? ambient = input;
        Assert.True(table.TryBind(action, ambient));
        Assert.False(input.Gate.IsClaimed); // a request is not OnEnqueued
        ambient = null;
        Assert.True(table.TryGet(action, out var restored));
        Assert.Same(input, restored);
        Assert.Equal("human-before-enemy-turn", restored!.Snapshot);
        Assert.True(restored.Gate.TryClaim("UsePotionAction"));
        Assert.True(table.TryBind(action, input)); // native deferred re-request
        Assert.False(restored.Gate.TryClaim("UsePotionAction"));
        Assert.Null(ambient);
    }

    [Fact]
    public void DelayedPreFrameFailureSurvivesAndIsClaimedOnlyOnceAtAcceptance()
    {
        var table = new ExactNativeInputBindingTable<object, Input>();
        object action = new();
        var failure = new Input("session-a", "partial-H", "potion_pre_frame_capture_failed", new("UsePotionAction"));
        table.TryBind(action, failure);
        Assert.True(table.TryGet(action, out var restored));
        Assert.Equal("potion_pre_frame_capture_failed", restored!.Failure);
        Assert.False(restored.Gate.TryClaim("ReadyToBeginEnemyTurnAction"));
        Assert.True(restored.Gate.TryClaim("UsePotionAction"));
        Assert.False(restored.Gate.TryClaim("UsePotionAction"));
    }

    [Fact]
    public void NativeObjectAndSessionCannotBeReplacedByLatestInput()
    {
        var table = new ExactNativeInputBindingTable<object, Input>();
        object first = new(), second = new();
        var old = new Input("closed-session", "old-H", null, new("UsePotionAction"));
        var fresh = new Input("new-session", "new-H", null, new("UsePotionAction"));
        Assert.True(table.TryBind(first, old));
        Assert.False(table.TryBind(first, fresh));
        Assert.False(table.TryGet(second, out _));
        Assert.True(table.TryBind(second, fresh));
        Assert.True(table.TryGet(first, out var retained));
        Assert.Same(old, retained);
        Assert.NotEqual(fresh.Session, retained!.Session);
    }
}
