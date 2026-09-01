using STS2HumanAnnotator.Core;

namespace STS2PlatformLiveUi;

internal sealed record PlatformLiveActionItem(
    string CorrelationIdentity,
    bool HasReliableCorrelation,
    string? CorrelationIssue,
    long FirstSequence,
    long LatestSequence,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset LatestObservedAt,
    RecordingEventKind Kind,
    string? RecordId,
    string? Detail,
    RecordingActionProjection? Action);

internal sealed record PlatformLiveActionCounts(
    int Records,
    int Pending,
    int Invalidated,
    bool Exact);

/// <summary>
/// Read-only, session-local projection of canonical Recorder application events.
/// RecordId is the required Human action root identity. BoundActionId remains
/// presentation metadata for the state-bound candidate and is never promoted to
/// a lifecycle correlation root. Events without RecordId remain event-level and
/// make aggregate disposition counts explicitly inexact.
/// This class cannot authorize, commit, record or deliver gameplay actions.
/// </summary>
internal sealed class PlatformLiveActionAggregation
{
    private readonly Dictionary<string, PlatformLiveActionItem> _items =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenEventIds = new(StringComparer.Ordinal);
    private bool _sourceComplete = true;

    internal int Count => _items.Count;

    internal IReadOnlyList<PlatformLiveActionItem> Recent(int limit) => _items.Values
        .OrderByDescending(value => value.FirstSequence)
        .Take(Math.Max(0, limit))
        .ToArray();

    internal PlatformLiveActionCounts Counts
    {
        get
        {
            bool exact = _sourceComplete && _items.Values.All(value => value.HasReliableCorrelation);
            return new PlatformLiveActionCounts(
                _items.Values.Count(value => value.Kind == RecordingEventKind.DecisionRecorded),
                _items.Values.Count(value => value.Kind == RecordingEventKind.DecisionPending),
                _items.Values.Count(value => value.Kind == RecordingEventKind.DecisionInvalidated),
                exact);
        }
    }

    internal void Reset()
    {
        _items.Clear();
        _seenEventIds.Clear();
        _sourceComplete = true;
    }

    internal void MarkSourceIncomplete() => _sourceComplete = false;

    internal bool Apply(RecordingEvent value)
    {
        if (!PlatformLiveActionFeed.IsActionEvent(value.Kind)
            || !_seenEventIds.Add(value.EventId))
            return false;

        (string key, bool reliable, string? issue) = Correlation(value);
        if (_items.TryGetValue(key, out PlatformLiveActionItem? existing))
        {
            string? existingBoundActionId = StableBoundActionId(existing.Action);
            string? incomingBoundActionId = StableBoundActionId(value.Action);
            if (existingBoundActionId != null
                && incomingBoundActionId != null
                && !string.Equals(existingBoundActionId, incomingBoundActionId, StringComparison.Ordinal))
            {
                // A shared root paired with conflicting bound action identities is
                // not safe to merge. Preserve the incoming event as its own explicit
                // evidence row and make aggregate disposition counts unavailable.
                key = $"conflict:{value.EventId}";
                reliable = false;
                issue = "conflicting stable action identity";
                _sourceComplete = false;
            }
            else
            {
                RecordingEventKind kind = value.Sequence >= existing.LatestSequence
                    ? value.Kind
                    : existing.Kind;
                string? detail = value.Sequence >= existing.LatestSequence
                    ? value.Detail
                    : existing.Detail;
                RecordingActionProjection? action = value.Action ?? existing.Action;
                _items[key] = existing with
                {
                    LatestSequence = Math.Max(existing.LatestSequence, value.Sequence),
                    LatestObservedAt = value.ObservedAt > existing.LatestObservedAt
                        ? value.ObservedAt
                        : existing.LatestObservedAt,
                    Kind = kind,
                    RecordId = value.RecordId ?? existing.RecordId,
                    Detail = detail,
                    Action = action
                };
                return true;
            }
        }

        _items[key] = new PlatformLiveActionItem(
            key,
            reliable,
            issue,
            value.Sequence,
            value.Sequence,
            value.ObservedAt,
            value.ObservedAt,
            value.Kind,
            value.RecordId,
            value.Detail,
            value.Action);
        if (!reliable)
            _sourceComplete = false;
        return true;
    }

    private static (string Key, bool Reliable, string? Issue) Correlation(RecordingEvent value)
    {
        if (!string.IsNullOrWhiteSpace(value.RecordId))
            return ($"record:{value.RecordId}", true, null);
        return (
            $"event:{value.EventId}",
            false,
            "RecordId action root unavailable; event retained without aggregation");
    }

    private static string? StableBoundActionId(RecordingActionProjection? action) =>
        string.IsNullOrWhiteSpace(action?.BoundActionId) ? null : action.BoundActionId;
}

/// <summary>
/// Deterministic, read-only formatting for the recorder's canonical event
/// projection. No gameplay state is inferred from input, timing or frames.
/// </summary>
internal static class PlatformLiveActionFeed
{
    internal const int MaxEntries = 24;

    internal static bool IsActionEvent(RecordingEventKind kind) =>
        kind is RecordingEventKind.DecisionPending
            or RecordingEventKind.DecisionRecorded
            or RecordingEventKind.DecisionInvalidated;

    internal static string FormatEntry(PlatformLiveActionItem value) =>
        $"#{value.FirstSequence}  {FormatCompactAction(value.Action)}  {FormatLifecycle(value.Kind)}";

    internal static string FormatDetail(PlatformLiveActionItem value)
    {
        RecordingActionProjection? action = value.Action;
        string stableActionId = string.IsNullOrWhiteSpace(action?.BoundActionId)
            ? "unavailable"
            : action.BoundActionId;
        string stableSubjectId = string.IsNullOrWhiteSpace(action?.SubjectReferentId)
            ? "unavailable"
            : action.SubjectReferentId;
        string targets = FormatTargets(action, includeKeys: true, unavailableWhenEmpty: true);
        string effect = string.IsNullOrWhiteSpace(action?.EffectSummary)
            ? "unavailable (not present in canonical evidence)"
            : action.EffectSummary!;
        var lines = new List<string>
        {
            FormatCompactAction(action),
            FormatLifecycleDetail(value)
        };
        if (value.Kind == RecordingEventKind.DecisionRecorded
            && !string.IsNullOrWhiteSpace(value.RecordId))
            lines.Add($"Record: {value.RecordId}");
        if (value.Kind == RecordingEventKind.DecisionInvalidated)
            lines.Add($"Reason: {Explicit(value.Detail, "unavailable (canonical reason not exposed)")}");
        if (!value.HasReliableCorrelation)
            lines.Add($"Correlation: unavailable ({value.CorrelationIssue ?? "stable identity not exposed"})");
        lines.Add($"Action ID: {stableActionId}");
        lines.Add($"Subject/card ID: {stableSubjectId}");
        lines.Add($"Target IDs: {targets}");
        lines.Add($"Effect: {effect}");
        return string.Join('\n', lines);
    }

    internal static string FormatLifecycle(RecordingEventKind kind) => kind switch
    {
        RecordingEventKind.DecisionPending => "… Observed",
        RecordingEventKind.DecisionRecorded => "✓ Recorded",
        RecordingEventKind.DecisionInvalidated => "✕ Invalidated",
        _ => "unavailable"
    };

    private static string FormatLifecycleDetail(PlatformLiveActionItem value) => value.Kind switch
    {
        RecordingEventKind.DecisionPending =>
            "Status: … Observed · waiting for canonical settlement",
        RecordingEventKind.DecisionRecorded => "Status: ✓ Recorded",
        RecordingEventKind.DecisionInvalidated => "Status: ✕ Invalidated",
        _ => "Status: unavailable"
    };

    private static string FormatCompactAction(RecordingActionProjection? action)
    {
        if (action == null)
            return "Action unavailable (canonical evidence omitted action facts)";
        string verb = Humanize(action.Verb, "Action unavailable");
        string label = string.IsNullOrWhiteSpace(action.Label) ? "unavailable" : action.Label.Trim();
        string actionText = string.Equals(verb, label, StringComparison.OrdinalIgnoreCase)
            ? verb
            : $"{verb} {label}";
        string targets = FormatTargets(action, includeKeys: false, unavailableWhenEmpty: false);
        return targets.Length == 0 ? actionText : $"{actionText} → {targets}";
    }

    private static string FormatTargets(
        RecordingActionProjection? action,
        bool includeKeys,
        bool unavailableWhenEmpty)
    {
        if (action == null || action.Arguments.Count == 0)
            return unavailableWhenEmpty ? "unavailable" : "";
        string value = string.Join(", ", action.Arguments
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => includeKeys ? $"{pair.Key}={Explicit(pair.Value)}" : Explicit(pair.Value)));
        return value.Length == 0 && unavailableWhenEmpty ? "unavailable" : value;
    }

    private static string Humanize(string? value, string unavailable)
    {
        if (string.IsNullOrWhiteSpace(value))
            return unavailable;
        string normalized = value.Replace('_', ' ').Trim();
        return string.Join(' ', normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string Explicit(string? value, string unavailable = "unavailable") =>
        string.IsNullOrWhiteSpace(value) ? unavailable : value.Trim();
}
