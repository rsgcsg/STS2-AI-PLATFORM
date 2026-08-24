using System;
using System.Collections.Generic;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.NativeUi;

namespace STS2Connector.LiveHost;

/// <summary>
/// Builds a non-authorizing observation when a provider recognizes the active
/// UI owner but cannot prove its exact semantic binding. This helper never
/// creates actions or legacy fallback authority.
/// </summary>
internal static class NativeUiFailClosedObservation
{
    internal static LiveObservation BindingUnavailable(
        GameBuildIdentity game,
        ILiveContext context,
        string sourceType,
        string reason,
        IReadOnlyList<string> sources,
        IReadOnlyList<string> missing,
        string warning,
        string diagnosticCode,
        string authorityReason)
    {
        var surface = new UnsupportedSurface("unsupported", sourceType, reason);
        var completeness = new StateCompleteness(
            "degraded",
            "empty_fail_closed",
            sources,
            missing);
        string signature = StableIdentityHash.Object(new { game.Version, context, surface, missing });
        return new LiveObservation(
            signature,
            "degraded",
            context,
            surface,
            completeness,
            game,
            new[] { warning })
        {
            InputOwnership = new InputOwnership(
                "none_fail_closed",
                null,
                authorityReason),
            Diagnostics = new[]
            {
                HostDiagnostics.Create(
                    diagnosticCode,
                    "error",
                    "surface",
                    "actions_suppressed",
                    "update_host_adapter",
                    reason)
            }
        };
    }
}
