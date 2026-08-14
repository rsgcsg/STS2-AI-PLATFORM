using System;
using System.Collections.Generic;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

internal static class HostDiagnostics
{
    public static IReadOnlyList<HostDiagnostic> ForObservation(LiveObservation draft)
    {
        var result = new List<HostDiagnostic>(draft.Diagnostics);
        bool requiredForAction = string.Equals(
            draft.Completeness.InteractionDiscovery,
            "empty_fail_closed",
            StringComparison.Ordinal);
        foreach (string path in draft.Completeness.Missing)
        {
            result.Add(new HostDiagnostic(
                "host.completeness.missing_field",
                requiredForAction ? "error" : "warning",
                "completeness",
                requiredForAction ? "actions_suppressed" : "field_omitted",
                requiredForAction ? "change_surface" : "unknown",
                Path: path,
                RequiredForAction: requiredForAction,
                SafeDetail: "A player-visible field required by this bounded interaction is absent."));
        }

        foreach (string warning in draft.Warnings)
        {
            result.Add(new HostDiagnostic(
                "host.observation.warning",
                "warning",
                "compatibility",
                "none",
                "unknown",
                SafeDetail: Bound(warning)));
        }
        return result;
    }

    public static HostDiagnostic Create(
        string code,
        string severity,
        string category,
        string effect,
        string recoverability,
        string? detail = null) =>
        new(code, severity, category, effect, recoverability, SafeDetail: Bound(detail));

    private static string? Bound(string? value) =>
        value == null || value.Length <= 500 ? value : value[..500];
}
