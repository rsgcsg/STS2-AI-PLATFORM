using STS2Connector.PlayerEnvironment.Witness;

namespace STS2HumanAnnotator.Mod;

internal sealed record HumanActionContext(
    string Origin,
    ProcessLocalNativeWitnessFrame Frame,
    DateTimeOffset EnteredAt);

internal static class HumanActionScope
{
    [ThreadStatic]
    private static Stack<HumanActionContext>? _stack;

    internal static HumanActionContext? Current =>
        _stack is { Count: > 0 } ? _stack.Peek() : null;

    internal static void Enter(string origin)
    {
        _stack ??= new Stack<HumanActionContext>();
        _stack.Push(new HumanActionContext(
            origin,
            PlayerEnvironmentNativeWitness.Capture(),
            DateTimeOffset.UtcNow));
    }

    internal static void Exit()
    {
        if (_stack is { Count: > 0 })
            _stack.Pop();
    }
}
