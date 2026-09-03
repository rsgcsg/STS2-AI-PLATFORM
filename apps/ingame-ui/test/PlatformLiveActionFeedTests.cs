using STS2HumanAnnotator.Core;
using STS2PlatformLiveUi;
using Xunit;

namespace STS2PlatformLiveUiTests;

public sealed class PlatformLiveActionFeedTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.Parse("2026-09-01T00:00:00Z");

    [Fact]
    public void ObservedThenRecordedUpdatesOneActionRow()
    {
        var feed = new PlatformLiveActionAggregation();

        Assert.True(feed.Apply(Event(1, RecordingEventKind.RootPending, "record-1")));
        Assert.True(feed.Apply(Event(2, RecordingEventKind.DecisionRecorded, "record-1")));

        PlatformLiveActionItem row = Assert.Single(feed.Recent(24));
        Assert.Equal(1, row.FirstSequence);
        Assert.Equal(2, row.LatestSequence);
        Assert.Equal(RecordingEventKind.DecisionRecorded, row.Kind);
        Assert.Equal("record:record-1", row.CorrelationIdentity);
    }

    [Fact]
    public void ObservedIsPendingAndNeverCountsAsRecorded()
    {
        var feed = new PlatformLiveActionAggregation();

        feed.Apply(Event(1, RecordingEventKind.RootPending, "record-1"));

        Assert.Equal(new PlatformLiveActionCounts(0, 1, 0, true), feed.Counts);
        Assert.Contains("waiting for canonical settlement", PlatformLiveActionFeed.FormatDetail(
            Assert.Single(feed.Recent(24))), StringComparison.Ordinal);
    }

    [Fact]
    public void RecordedSettlementMovesTheSameActionFromPendingToRecords()
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.RootPending, "record-1"));

        feed.Apply(Event(2, RecordingEventKind.DecisionRecorded, "record-1"));

        Assert.Equal(new PlatformLiveActionCounts(1, 0, 0, true), feed.Counts);
        Assert.Contains("Status: ✓ Recorded", PlatformLiveActionFeed.FormatDetail(
            Assert.Single(feed.Recent(24))), StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidatedSettlementMovesTheSameActionWithoutIncreasingRecords()
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.RootPending, "record-1"));

        feed.Apply(Event(
            2,
            RecordingEventKind.DecisionInvalidated,
            "record-1",
            detail: "successor_not_stable"));

        Assert.Equal(new PlatformLiveActionCounts(0, 0, 1, true), feed.Counts);
        string detail = PlatformLiveActionFeed.FormatDetail(Assert.Single(feed.Recent(24)));
        Assert.Contains("Status: ✕ Invalidated", detail, StringComparison.Ordinal);
        Assert.Contains("Reason: successor_not_stable", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentStableActionRootsRemainDifferentRowsWithIdenticalMetadata()
    {
        var feed = new PlatformLiveActionAggregation();
        RecordingActionProjection action = Action("bound-shared", "Strike", "enemy-1");

        feed.Apply(Event(1, RecordingEventKind.RootPending, "record-1", action));
        feed.Apply(Event(2, RecordingEventKind.RootPending, "record-2", action));

        Assert.Equal(2, feed.Count);
        Assert.Equal(2, feed.Counts.Pending);
    }

    [Fact]
    public void RepeatedLifecycleEventsForOneStableRootNeverDuplicateTheRow()
    {
        var feed = new PlatformLiveActionAggregation();

        feed.Apply(Event(1, RecordingEventKind.RootPending, "record-1"));
        feed.Apply(Event(2, RecordingEventKind.RootPending, "record-1"));
        feed.Apply(Event(3, RecordingEventKind.DecisionRecorded, "record-1"));

        Assert.Single(feed.Recent(24));
        Assert.Equal(1, feed.Counts.Records);
    }

    [Fact]
    public void BoundActionIdentityNeverSubstitutesForTheMissingActionRoot()
    {
        var feed = new PlatformLiveActionAggregation();
        RecordingActionProjection action = Action("bound-1", "Defend", null);

        feed.Apply(Event(1, RecordingEventKind.RootPending, null, action));
        feed.Apply(Event(2, RecordingEventKind.DecisionRecorded, null, action));

        Assert.Equal(2, feed.Count);
        Assert.False(feed.Counts.Exact);
        Assert.All(feed.Recent(24), row => Assert.StartsWith(
            "event:", row.CorrelationIdentity, StringComparison.Ordinal));
        Assert.All(feed.Recent(24), row => Assert.False(row.HasReliableCorrelation));
    }

    [Fact]
    public void MissingMetadataIsExplicitAndNeverProducesAnEmptyRow()
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.RootPending, "record-1", omitAction: true));

        PlatformLiveActionItem row = Assert.Single(feed.Recent(24));
        string entry = PlatformLiveActionFeed.FormatEntry(row);
        string detail = PlatformLiveActionFeed.FormatDetail(row);

        Assert.False(string.IsNullOrWhiteSpace(entry));
        Assert.Contains("Action unavailable", entry, StringComparison.Ordinal);
        Assert.Contains("Action ID: unavailable", detail, StringComparison.Ordinal);
        Assert.Contains("Subject/card ID: unavailable", detail, StringComparison.Ordinal);
        Assert.Contains("Target IDs: unavailable", detail, StringComparison.Ordinal);
        Assert.Contains("Effect: unavailable", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void UncorrelatedEventsStayEventLevelAndMakeCountsFailExplicit()
    {
        var feed = new PlatformLiveActionAggregation();

        feed.Apply(Event(1, RecordingEventKind.DecisionInvalidated, null, action: null));
        feed.Apply(Event(2, RecordingEventKind.DecisionInvalidated, null, action: null));

        Assert.Equal(2, feed.Count);
        Assert.False(feed.Counts.Exact);
        Assert.All(feed.Recent(24), row => Assert.False(row.HasReliableCorrelation));
        Assert.All(feed.Recent(24), row => Assert.Contains(
            "Correlation: unavailable",
            PlatformLiveActionFeed.FormatDetail(row),
            StringComparison.Ordinal));
    }

    [Fact]
    public void RecentRowsAndCanonicalRecordCountAreIntentionallyDifferentMeasures()
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.DecisionRecorded, "record-1"));
        feed.Apply(Event(2, RecordingEventKind.RootPending, "record-2"));
        feed.Apply(Event(3, RecordingEventKind.DecisionInvalidated, "record-3"));

        Assert.Equal(3, feed.Count);
        Assert.Equal(new PlatformLiveActionCounts(1, 1, 1, true), feed.Counts);
    }

    [Fact]
    public void LifecycleUpdatesDoNotReorderHumanActionHistory()
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.RootPending, "record-1"));
        feed.Apply(Event(2, RecordingEventKind.RootPending, "record-2"));
        feed.Apply(Event(3, RecordingEventKind.DecisionRecorded, "record-1"));

        IReadOnlyList<PlatformLiveActionItem> rows = feed.Recent(24);
        Assert.Equal("record:record-2", rows[0].CorrelationIdentity);
        Assert.Equal("record:record-1", rows[1].CorrelationIdentity);
    }

    private static RecordingEvent Event(
        long sequence,
        RecordingEventKind kind,
        string? recordId,
        RecordingActionProjection? action = null,
        string? detail = null,
        bool omitAction = false) => new(
            sequence,
            $"event-{sequence}",
            kind,
            T0.AddSeconds(sequence),
            "session-1",
            "run-1",
            recordId,
            detail,
            omitAction
                ? null
                : action ?? (recordId == null
                    ? null
                    : Action($"bound-{recordId}", "Thunderclap", "enemy-all")));

    private static RecordingActionProjection Action(
        string boundActionId,
        string label,
        string? target) => new(
            "play",
            boundActionId,
            $"card-{label.ToLowerInvariant()}",
            target == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal) { ["target"] = target },
            label);
}
