using STS2HumanAnnotator.Core;
using STS2PlatformLiveUi;
using Xunit;

namespace STS2PlatformLiveUiTests;

public sealed class PlatformLiveActionFeedTests
{
    private static readonly DateTimeOffset T0 =
        DateTimeOffset.Parse("2026-09-01T00:00:00Z");

    [Fact]
    public void NativeInputKeepsDecisionCorrelationWithoutInventingBoundActionId()
    {
        var feed = new PlatformLiveActionAggregation();
        var projection = Action("unused", "Play card", null) with { BoundActionId = null, NativeActionKey = "play|card-1|" };
        feed.Apply(Event(1, RecordingEventKind.RootPending, "record-native") with { Action = projection });
        feed.Apply(Event(2, RecordingEventKind.DecisionRecorded, "record-native") with { Action = projection });
        var item = Assert.Single(feed.Recent(5));
        Assert.True(item.HasReliableCorrelation);
        Assert.Contains("Native input key: play|card-1|", PlatformLiveActionFeed.FormatDetail(item));
        Assert.Contains("Action ID: unavailable", PlatformLiveActionFeed.FormatDetail(item));
        Assert.Equal(1, feed.Counts.Records);
    }

    [Theory]
    [InlineData("MoveToMapCoordAction")]
    [InlineData("ReadyToBeginEnemyTurnAction")]
    public void NativeDiagnosticDoesNotImpersonateHumanRoot(string nativeType)
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.DecisionInvalidated, null) with {
            Action = Action("", nativeType, null) with { Verb = "activate", SubjectReferentId = null, IsDiagnostic = true } });
        var item = feed.Recent(1)[0];
        Assert.Contains("Diagnostic", PlatformLiveActionFeed.FormatEntry(item));
        Assert.DoesNotContain("Root / legacy", PlatformLiveActionFeed.FormatEntry(item));
        Assert.Contains("not an additional Human decision", PlatformLiveActionFeed.FormatDetail(item));
        Assert.DoesNotContain("Invalidated", PlatformLiveActionFeed.FormatEntry(item));
        Assert.DoesNotContain("Status: ✕ Invalidated", PlatformLiveActionFeed.FormatDetail(item));
        Assert.False(PlatformLiveActionFeed.IsFailure(item));
        Assert.Equal(0, feed.Counts.Invalidated);
        Assert.Equal(0, feed.Counts.Records);
        Assert.Equal(1, feed.Counts.Diagnostics);
        Assert.True(feed.Counts.Exact);
        Assert.Contains(nativeType, PlatformLiveActionFeed.FormatEntry(item));
        feed.Apply(Event(2, RecordingEventKind.DecisionInvalidated, "failed-human"));
        Assert.Equal(1, feed.Counts.Invalidated);
        Assert.Equal(1, feed.Counts.Diagnostics);
        Assert.True(PlatformLiveActionFeed.IsFailure(feed.Recent(1)[0]));
    }

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
        Assert.Contains("Unresolved 8 · Cancelled 0 · Aborted 0", text);
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
        Assert.Contains("Status: ✕ Failed closed", detail, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("failed_closed", true)]
    [InlineData("unresolved", true)]
    [InlineData("cancelled", false)]
    [InlineData("aborted", false)]
    [InlineData("diagnostic", false)]
    [InlineData("unsupported", false)]
    [InlineData(null, false)]
    public void RealFailureUsesOnlyTheAuthoritativeDisposition(string? disposition, bool failed)
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.DecisionInvalidated, "input", Action("b", "Play Strike", null) with {
            Disposition = disposition
        }));
        var row = Assert.Single(feed.Recent(3));
        Assert.Equal(failed, PlatformLiveActionFeed.IsFailure(row));
        if (disposition is "cancelled" or "aborted")
            Assert.DoesNotContain("Invalidated", PlatformLiveActionFeed.FormatEntry(row));
    }

    [Theory]
    [InlineData(RecordingEventKind.DecisionCancelled, "cancelled")]
    [InlineData(RecordingEventKind.DecisionAborted, "aborted")]
    public void CancelAndAbortSettleOnePendingRowWithoutFailure(RecordingEventKind kind, string disposition)
    {
        var feed = new PlatformLiveActionAggregation();
        feed.Apply(Event(1, RecordingEventKind.RootPending, "input"));
        feed.Apply(Event(2, kind, "input", Action("bound-input", "Play Strike", null) with {
            Disposition = disposition
        }));
        var row = Assert.Single(feed.Recent(3));
        Assert.Equal(kind, row.Kind);
        Assert.Equal(0, feed.Counts.Pending);
        Assert.Equal(0, feed.Counts.Records);
        Assert.Equal(0, feed.Counts.Invalidated);
        Assert.DoesNotContain("Unresolved", PlatformLiveActionFeed.FormatDetail(row));
    }

    [Fact]
    public void CompactTotalsNeverUseRetainedFeedOrTreatAbsentDispositionAsZero()
    {
        var counters = new RecordingCounters(7, 99, 10, 0,
            new(100, 20, 110, 2, 90, 20, Cancelled: 4, Aborted: 1,
                CaptureFailures: 3, PersistenceFailures: 1, DispositionVersion: 1, FailedDecisions: 6));
        Assert.Equal("已录入 110 / 真实失败 6", PlatformLiveActionFeed.FormatCompactCounters(counters));
        Assert.Equal("已录入 110 / 真实失败 —", PlatformLiveActionFeed.FormatCompactCounters(counters with {
            Decisions = counters.Decisions! with { DispositionVersion = 0 }
        }));
        Assert.Equal("已录入 — / 真实失败 —", PlatformLiveActionFeed.FormatCompactCounters(new(0, 0, 0, 0)));
    }

    [Fact]
    public void CompactLatestThreeExcludeDiagnosticsAndDoNotDuplicateTheVerb()
    {
        var feed = new PlatformLiveActionAggregation();
        for (int index = 1; index <= 5; index++)
            feed.Apply(Event(index, RecordingEventKind.DecisionRecorded, "r-" + index,
                Action("b-" + index, "Play Strike", null) with { Disposition = "recorded" }));
        feed.Apply(Event(6, RecordingEventKind.DecisionInvalidated, null,
            Action("", "Native carrier", null) with { Disposition = "diagnostic" }));
        var recent = feed.RecentDecisions(3);
        Assert.Equal(new long[] { 5, 4, 3 }, recent.Select(item => item.FirstSequence));
        string text = PlatformLiveActionFeed.FormatCompactRecent(recent);
        Assert.Equal(3, text.Split('\n').Length);
        Assert.DoesNotContain("Native carrier", text);
        Assert.DoesNotContain("Play Play", text);
        Assert.DoesNotContain("Parent:", text);
    }

    [Fact]
    public void LongSessionsRetainABoundedViewAndRejectReplayedOldEvents()
    {
        var feed = new PlatformLiveActionAggregation();
        for (int index = 1; index <= 2048; index++)
            Assert.True(feed.Apply(Event(index, RecordingEventKind.DecisionRecorded, "r-" + index)));
        Assert.Equal(PlatformLiveActionAggregation.RetainedLimit, feed.Count);
        Assert.False(feed.Counts.Exact);
        Assert.False(feed.Apply(Event(1, RecordingEventKind.DecisionRecorded, "r-1")));
        Assert.Equal(2048, feed.Recent(1)[0].FirstSequence);
        feed.Reset();
        Assert.True(feed.Apply(Event(1, RecordingEventKind.RootPending, "next-session")));
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
                    : Action($"bound-{recordId}", "Thunderclap", "enemy-all") with {
                        Disposition = kind switch {
                            RecordingEventKind.DecisionRecorded => "recorded",
                            RecordingEventKind.RootPending => "pending",
                            RecordingEventKind.DecisionInvalidated => "failed_closed",
                            RecordingEventKind.DecisionUnresolved => "unresolved",
                            _ => null
                        }
                    }));

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
