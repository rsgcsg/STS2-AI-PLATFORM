using STS2Connector.NativeUi;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

internal interface ILiveSurfaceReader
{
    string Kind { get; }

    InputOwnerLayer Layer { get; }

    LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIden