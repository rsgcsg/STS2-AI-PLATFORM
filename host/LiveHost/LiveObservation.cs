using System;
using System.Collections.Generic;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

internal sealed record LiveObservation(
    string Signature,
    string Readiness,
    ILiveContext Context,
    ILiveSurface Surface,
    StateCompleteness Completeness,
    GameBuildIdentity Game,
    IReadOnlyList<string> Warnings)
{
    public InputOwnership InputOwnership { get; init; } = new(
        "current_ui_owned",
        Surface.Kind,
        "The exact current native UI owns this interaction.");

    public IReadOnlyList<HostDiagnostic> Diagnostics { get; init; } =
        Array.Empty<HostDiagnostic>();
}
