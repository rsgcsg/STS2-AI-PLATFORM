using STS2HumanAnnotator.Core;
using STS2PlatformLiveUi;
using Xunit;

namespace STS2PlatformLiveUiTests;

public sealed class PlatformLiveActionFeedTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.Parse("2026-09-01T00:00:00Z");

    [Fact]
    public void NativeOriginSelectorDoesNotDisplayAnInventedHumanParent()
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.DecisionRecorded, "input") with {
            Action = Action("bound", "Select", "card") with {
                Decision = new(2, "decision", "actual-hook", null, "selector", "hand", "native_selector", "owner",
                    new("actual-hook", "GenericHookGameAction", "HookPlayerChoiceContext", "SelectCards")) }});
        var item = feed.Recent(1)[0];
        Assert.Contains("Selector / native origin", PlatformLiveActionFeed.FormatEntry(item));
        Assert.Contains("Parent decision: none (native origin)", PlatformLiveActionFeed.FormatDetail(item));
        Assert.Contains("Native origin: GenericHookGameAction", PlatformLiveActionFeed.FormatDetail(item));
    }

    [Fact]
    public void CanonicalCountersDoNotUseLegacyProjectionOrFeedRetention()
    {
        string text = PlatformLiveActionFeed.FormatCounters(new(139, 83, 1397, 0,
            new(385, 34, 411, 8, 375, 34)));
        Assert.Contains("Canonical 409", text);
        Assert.Contains("Legacy records 139", text);
        Assert.Contains("Unresolved 8 (includes cancelled)", text);
        Assert.Contains("Decisions unavailable", PlatformLiveActionFeed.FormatCounters(new(139, 83, 0, 0)));
    }

    [Fact]
    public void SiblingDecisionsRemainDistinctAndExposeExactParentOnEveryPage()
    {
        var feed = new PlatformLiveActionAggregation();
        for (int i = 1; i <= 30; i++)
            feed.Apply(Event(i, RecordingEventKind.DecisionRecorded, "r-" + i) with {
                Action = Action("bound-" + i, "Select", "card") with {
                    Decision = new(1, "d-" + i, "root", "parent", "selector", "pile", "nested_selector", "owner"),
                    PreSnapshotId = "pre", SuccessorSnapshotId = "post", CandidateCount = 9, PileType = "draw"
                }});
        Assert.Equal(24, feed.Recent(24).Count);
        Assert.Equal(6, feed.Recent(24, 24).Count);
        string detail = PlatformLiveActionFeed.FormatDetail(feed.Recent(24, 24)[0]);
        Assert.Contains("Parent decision: parent", detail);
        Assert.Contains("Causal root: root", detail);
        Assert.Contains("Candidates: 9 · Pile: draw", detail);
        Assert.Equal(30, feed.Counts.Records);
    }

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
