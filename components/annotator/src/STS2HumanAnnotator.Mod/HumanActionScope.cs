using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

internal readonly record struct NativeUiScopeEntry(bool Entered, bool DeferredFailure);

internal sealed class HumanActionContext
{
    private readonly AcceptedRootActionGate _rootActionGate;

    internal HumanActionContext(
        string origin,
        string expectedNativeActionType,
        ProcessLocalObservedAction? expectedAction,
        ProcessLocalNativeWitnessFrame frame,
        DateTimeOffset enteredAt)
    {
        Origin = origin;
        ExpectedAction = expectedAction;
        Frame = frame;
        EnteredAt = enteredAt;
        _rootActionGate = new AcceptedRootActionGate(expectedNativeActionType);
    }

    internal string Origin { get; }

    internal ProcessLocalObservedAction? ExpectedAction { get; }

    internal ProcessLocalNativeWitnessFrame Frame { get; }

    internal DateTimeOffset EnteredAt { get; }

    internal bool AcceptsRootAction(string nativeActionType) =>
        _rootActionGate.Accepts(nativeActionType);

    internal bool TryClaimRootAction(string nativeActionType) =>
        _rootActionGate.TryClaim(nativeActionType);
}

internal sealed class DeferredHumanActionFailure
{
    private readonly AcceptedRootActionGate _rootActionGate;

    internal DeferredHumanActionFailure(
        string expectedNativeActionType,
        string reasonCode,
        string detail,
        string? snapshotId,
        string evidenceLevel)
    {
        _rootActionGate = new AcceptedRootActionGate(expectedNativeActionType);
        ReasonCode = reasonCode;
        Detail = detail;
        SnapshotId = snapshotId;
        EvidenceLevel = evidenceLevel;
    }

    internal string ReasonCode { get; }

    internal string Detail { get; }

    internal string? SnapshotId { get; }

    internal string EvidenceLevel { get; }

    internal bool TryClaim(string nativeActionType) =>
        _rootActionGate.TryClaim(nativeActionType);
}

internal static class HumanActionScope
{
    [ThreadStatic]
    private static Stack<HumanActionContext>? _stack;

    [ThreadStatic]
    private static Stack<DeferredHumanActionFailure>? _deferredFailures;

    internal static HumanActionContext? Current =>
        _stack is { Count: > 0 } ? _stack.Peek() : null;

    internal static DeferredHumanActionFailure? CurrentDeferredFailure =>
        _deferredFailures is { Count: > 0 } ? _deferredFailures.Peek() : null;

    internal static void Enter(
        string origin,
        string expectedNativeActionType,
        ProcessLocalObservedAction? expectedAction,
        ProcessLocalNativeWitnessFrame frame)
    {
        _stack ??= new Stack<HumanActionContext>();
        _stack.Push(new HumanActionContext(
            origin,
            expectedNativeActionType,
            expectedAction,
            frame,
            DateTimeOffset.UtcNow));
    }

    internal static void Exit()
    {
        if (_stack is { Count: > 0 })
            _stack.Pop();
    }

    internal static void EnterDeferredFailure(
        string expectedNativeActionType,
        string reasonCode,
        string detail,
        string? snapshotId,
        string evidenceLevel)
    {
        _deferredFailures ??= new Stack<DeferredHumanActionFailure>();
        _deferredFailures.Push(new DeferredHumanActionFailure(
            expectedNativeActionType,
            reasonCode,
            detail,
            snapshotId,
            evidenceLevel));
    }

    internal static void ExitDeferredFailure()
    {
        if (_deferredFailures is { Count: > 0 })
            _deferredFailures.Pop();
    }
}
