namespace STS2Connector.NativeUi;

/// <summary>
/// Process-local main-thread scheduling for trusted native UI infrastructure.
/// This seam is not exposed by REST/MCP and does not create gameplay authority.
/// </summary>
internal static class NativeUiMainThread
{
    internal static Task Run(Action action) => ConnectorMod.RunOnMainThread(action);
}
