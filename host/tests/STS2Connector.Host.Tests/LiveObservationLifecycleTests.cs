using STS2Connector.LiveHost;

namespace STS2Connector.Tests;

public sealed class LiveObservationLifecycleTests
{
    [Fact]
    public void PersistentStateMountFailureSettlesOnlyInsideABoundedWindow()
    {
        var window = new BoundedSettlingWindow(TimeSpan.FromSeconds(20));
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-13T00:00:00Z");

        Assert.True(window.Observe(condition: true, start));
        Assert.True(window.Observe(condition: true, start.AddSeconds(20)));
        Assert.False(window.Observe(condition: true, start.AddSeconds(21)));
        Assert.False(window.Observe(condition: false, start.AddSeconds(22)));
        Assert.True(window.Observe(condition: true, start.AddSeconds(40)));
    }

    [Fact]
    public void CompletedEventPresentationIsASettlingStateWithoutAuthority()
    {
        Assert.True(LiveObservationReader.ClassifyEventNoInputTransition(
            runInProgress: true,
            currentRoomIsEvent: true,
            hasBlockingSurface: false,
            sourceType: "run_without_visible_overlay",
            eventRoomNodePresent: true,
            inDialogue: false));
    }

    [Theory]
    [InlineData(false, true, false, "run_without_visible_overlay", true, false)]
    [InlineData(true, false, false, "run_without_visible_overlay", true, false)]
    [InlineData(true, true, true, "run_without_visible_overlay", true, false)]
    [InlineData(true, true, false, "overlay", true, false)]
    [InlineData(true, true, false, "run_without_visible_overlay", false, false)]
    [InlineData(true, true, false, "run_without_visible_overlay", true, true)]
    public void EventTransitionDoesNotHideARealOrUnknownInputOwner(
        bool runInProgress,
        bool currentRoomIsEvent,
        bool hasBlockingSurface,
        string sourceType,
        bool eventRoomNodePresent,
        bool inDialogue)
    {
        Assert.False(LiveObservationReader.ClassifyEventNoInputTransition(
            runInProgress,
            currentRoomIsEvent,
            hasBlockingSurface,
            sourceType,
            eventRoomNodePresent,
            inDialogue));
    }

    [Fact]
    public void TreasureScreenOwnershipHandoffIsSettling()
    {
        Assert.True(TreasureRoomSurfaceReader.ClassifyScreenHandoff(
            runInProgress: true,
            currentRoomIsTreasure: true,
            uiRoomIsLive: true,
            ownsCurrentScreen: false));
    }

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void TreasureHandoffDoesNotMaskOtherStates(
        bool runInProgress,
        bool currentRoomIsTreasure,
        bool uiRoomIsLive,
        bool ownsCurrentScreen)
    {
        Assert.False(TreasureRoomSurfaceReader.ClassifyScreenHandoff(
            runInProgress,
            currentRoomIsTreasure,
            uiRoomIsLive,
            ownsCurrentScreen));
    }
}
