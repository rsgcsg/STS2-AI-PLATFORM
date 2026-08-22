using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

internal sealed class HumanActionContext
{
    private readonly AcceptedRootActionGate _rootActionGate;

    internal HumanActionContext(
        string origin,
        string expectedNativeActionType,
        ProcessLocalNativeWitnessFrame frame,
        DateTimeOffset enteredAt)
    {
        Origin = origin;
        Frame = frame;
        EnteredAt = enteredAt;
        _rootActionGate = new AcceptedRootActionGate(expectedNativeActionType);
    }

    internal string Origin { get; }

    internal ProcessLocalNativeWitnessFrame Frame { get; }

    internal DateTimeOffset EnteredAt { get; }

    internal bool TryClaimRootAction(string nativeActionType) =>
        _rootActionGate.TryClaim(nativeActionType);
}

internal static class HumanActionScope
{
    [ThreadStatic]
    private static Stack<HumanActionContext>? _stack;

    internal static HumanActionContext? Current =>
        _stack is { Count: > 0 } ? _stack.Peek() : null;

    internal static void Enter(
        string origin,
        string expectedNativeActionType,
        ProcessLocalNativeWitnessFrame frame)
    {
        _stack ??= new Stack<HumanActionContext>();
        _stack.Push(new HumanActionContext(
            origin,
            expectedNativeActionType,
            frame,
            DateTimeOffset.UtcNow));
    }

    internal static void Exit()
    {
        if (_stack is { Count: > 0 })
            _stack.Pop();
    }
}
