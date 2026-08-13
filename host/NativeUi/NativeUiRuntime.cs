
namespace STS2Connector.NativeUi;

/// <summary>
/// Process-local native UI identity shared by observation, reads and execution.
/// It publishes no affordances and grants no authority.
/// </summary>
internal static class NativeUiRuntime
{
    internal static NativeEntityRegistry Entities { get; } = 