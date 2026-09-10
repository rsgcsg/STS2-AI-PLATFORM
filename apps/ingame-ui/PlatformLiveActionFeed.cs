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
/// RecordId identifies a decision occurrence, not its enclosing causal root. BoundActionId remains
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

    internal IReadOnlyList<PlatformLiveActionItem> Recent(int limit, int offset = 0) => _items.Values
        .OrderByDescending(value => value.FirstSequence)
        .Skip(Math.Max(0, offset))
        .Take(Math.Max(0, limit))
        .ToArray();

    internal PlatformLiveActionCounts Counts
    {
        get
        {
            bool exact = _sourceComplete && _items.Values.All(value => value.HasReliableCorrelation);
            return new PlatformLiveActionCounts(
                _items.Values.Count(value => value.Kind == RecordingEventKind.DecisionRecorded),
                _items.Values.Count(value => value.Kind == RecordingEventKind.RootPending),
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

    internal static string FormatCounters(RecordingCounters counters)
    {
        if (counters.Decisions is not { } d)
            return $"Decisions unavailable · Legacy records {counters.Records} · Invalidations {counters.Invalidations}";
        return $"Accepted {d.Accepted} ({d.AcceptedRoots} roots/entries + {d.AcceptedChildren} children)"
            + $" · Proved {d.Proved} · Canonical {d.Canonical} ({d.CanonicalRoots} roots/entries + {d.CanonicalChildren} children)"
            + $"\nPending {d.Pending} · Unresolved {d.Unresolved} (includes cancelled) · Invalidations {counters.Invalidations} · Legacy records {counters.Records}";
    }


    internal static bool IsActionEvent(RecordingEventKind kind) =>
        kind is RecordingEventKind.RootPending
            or RecordingEventKind.DecisionRecorded
            or RecordingEventKind.DecisionInvalidated
            or RecordingEventKind.DecisionUnresolved
            or RecordingEventKind.DecisionProjectionOmitted;

    internal static string FormatEntry(PlatformLiveActionItem value) =>
        $"#{value.FirstSequence}  {(value.Action?.IsDiagnostic == true ? "Diagnostic" : value.Action?.FailedOccurrence != null ? "Human input / failed capture" : value.Kind == RecordingEventKind.DecisionInvalidated ? "Capture failure" : value.Action?.Decision?.DecisionKind == "nested_selector" ? "↳ Selector" : value.Action?.Decision?.DecisionKind == "native_selector" ? "Selector / native origin" : value.Action?.Decision?.DecisionKind == "root" ? "Root" : "Legacy / unclassified")}  {FormatCompactAction(value.Action)}  {FormatLifecycle(value.Kind)}"
        + (value.Action?.Decision?.ParentDecisionId is { } parent ? $"\nParent: {parent}" : "");

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
        if (value.Kind is RecordingEventKind.DecisionInvalidated or RecordingEventKind.DecisionUnresolved or RecordingEventKind.DecisionProjectionOmitted)
            lines.Add($"Reason: {Explicit(value.Detail, "unavailable (canonical reason not exposed)")}");
        if (!value.HasReliableCorrelation)
            lines.Add($"Correlation: unavailable ({value.CorrelationIssue ?? "stable identity not exposed"})");
        if (action?.FailedOccurrence is { } occurrence)
        {
            lines.Add($"Human occurrence: {occurrence.OccurrenceId}");
            lines.Add($"Native input: {occurrence.NativeActionType} · {occurrence.NativeMechanism}");
            lines.Add($"Capture disposition: {occurrence.Disposition}");
        }
        if (action?.IsDiagnostic == true)
            lines.Add("Internal native diagnostic; not an additional Human decision.");
        if (action?.Decision is { } decision)
        {
            lines.Add($"Decision: {decision.DecisionId} ({decision.DecisionKind})");
            if (decision.NativeOrigin is { } origin)
                lines.Add($"Native origin: {origin.NativeActionType} · {origin.ChoiceContextType}");
            lines.Add($"Causal root: {decision.CausalRootId}");
            lines.Add($"Parent decision: {decision.ParentDecisionId ?? (decision.NativeOrigin == null ? "none (root)" : "none (native origin)")}");
            lines.Add($"Surface: {decision.Surface} · Family: {decision.Family}");
            lines.Add($"Native selector owner: {decision.NativeOwnerWitnessId ?? "unavailable"}");
        }
        else lines.Add("Decision lineage: unavailable (legacy or diagnostic event)");
        lines.Add($"Pre-state: {action?.PreSnapshotId ?? "unavailable"}");
        lines.Add($"Successor: {action?.SuccessorSnapshotId ?? "unavailable"}");
        lines.Add($"Candidates: {action?.CandidateCount?.ToString() ?? "unavailable"} · Pile: {action?.PileType ?? "not exposed"}");
        lines.Add($"Action ID: {stableActionId}");
        lines.Add($"Subject/card ID: {stableSubjectId}");
        lines.Add($"Target IDs: {targets}");
        lines.Add($"Effect: {effect}");
        return string.Join('\n', lines);
    }

    internal static string FormatLifecycle(RecordingEventKind kind) => kind switch
    {
        RecordingEventKind.RootPending => "… Observed",
        RecordingEventKind.DecisionRecorded => "✓ Recorded",
        RecordingEventKind.DecisionInvalidated => "✕ Invalidated",
        RecordingEventKind.DecisionUnresolved => "? Unresolved",
        RecordingEventKind.DecisionProjectionOmitted => "Proved / not canonical",
        _ => "unavailable"
    };

    private static string FormatLifecycleDetail(PlatformLiveActionItem value) => value.Kind switch
    {
        RecordingEventKind.RootPending =>
            "Status: … Observed · waiting for canonical settlement",
        RecordingEventKind.DecisionRecorded => "Status: ✓ Recorded",
        RecordingEventKind.DecisionInvalidated => "Status: ✕ Invalidated",
        RecordingEventKind.DecisionUnresolved => "Status: ? Unresolved",
        RecordingEventKind.DecisionProjectionOmitted => "Status: Proved / not canonical",
        _ => "Status: unavailable"
    };

    private static string FormatCompactAction(RecordingActionProjection? action)
    {
        if (action == null)
            return "Action unavailable (event has no action projection)";
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
