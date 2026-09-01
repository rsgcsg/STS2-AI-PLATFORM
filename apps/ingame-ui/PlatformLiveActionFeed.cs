using STS2HumanAnnotator.Core;

namespace STS2PlatformLiveUi;

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

    internal static string FormatEntry(RecordingEvent value) =>
        $"#{value.Sequence} {FormatCompactAction(value.Action)} · {FormatLifecycle(value.Kind)}";

    internal static string FormatDetail(RecordingEvent value)
    {
        RecordingActionProjection? action = value.Action;
        string actionKind = action?.Verb ?? "unavailable";
        string displayName = action?.Label ?? "unavailable (not present in canonical evidence)";
        string stableActionId = action?.BoundActionId ?? "unavailable";
        string stableSubjectId = action?.SubjectReferentId ?? "unavailable";
        string targets = action == null || action.Arguments.Count == 0
            ? "unavailable"
            : string.Join(", ", action.Arguments
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"));
        string effect = string.IsNullOrWhiteSpace(action?.EffectSummary)
            ? "unavailable (not present in canonical evidence)"
            : action.EffectSummary!;
        string detail = string.IsNullOrWhiteSpace(value.Detail)
            ? "none"
            : value.Detail!;
        return string.Join('\n', new[]
        {
            $"Action kind: {actionKind}",
            $"Card / choice display: {displayName}",
            $"Stable action ID: {stableActionId}",
            $"Stable subject/card ID: {stableSubjectId}",
            $"Target IDs: {targets}",
            $"Effect summary: {effect}",
            $"Lifecycle: {FormatLifecycle(value.Kind)}",
            $"Canonical detail: {detail}"
        });
    }

    private static string FormatAction(RecordingActionProjection? action)
    {
        if (action == null)
            return "Action unavailable (canonical evidence did not expose action facts)";

        string label = string.IsNullOrWhiteSpace(action.Label) ? "unavailable" : action.Label;
        string subject = string.IsNullOrWhiteSpace(action.SubjectReferentId)
            ? ""
            : $" [{action.SubjectReferentId}]";
        string targets = action.Arguments.Count == 0
            ? ""
            : " → " + string.Join(", ", action.Arguments
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key} [{pair.Value}]"));
        return $"{action.Verb} {label}{subject}{targets}".Trim();
    }

    private static string FormatCompactAction(RecordingActionProjection? action)
    {
        if (action == null)
            return "Action unavailable";
        string label = string.IsNullOrWhiteSpace(action.Label) ? action.Verb : action.Label;
        string targets = action.Arguments.Count == 0
            ? ""
            : " → " + string.Join(", ", action.Arguments
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value));
        return $"{action.Verb} {label}{targets}".Trim();
    }

    private static string FormatLifecycle(RecordingEventKind kind) => kind switch
    {
        RecordingEventKind.DecisionPending => "Observed",
        RecordingEventKind.DecisionRecorded => "Recorded",
        RecordingEventKind.DecisionInvalidated => "Invalidated",
        _ => "Canonical"
    };
}
