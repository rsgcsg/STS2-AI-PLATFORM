namespace STS2HumanAnnotator.Core;

/// <summary>
/// Admits exactly one expected game-owned root action for one native UI scope.
/// Downstream actions enqueued by that root action are not human decisions.
/// </summary>
public sealed class AcceptedRootActionGate
{
    private readonly string _expectedNativeActionType;
    private int _claimed;

    public AcceptedRootActionGate(string expectedNativeActionType)
    {
        if (string.IsNullOrWhiteSpace(expectedNativeActionType))
            throw new ArgumentException("Expected native action type is required.", nameof(expectedNativeActionType));
        _expectedNativeActionType = expectedNativeActionType;
    }

    public bool Accepts(string nativeActionType) =>
        string.Equals(nativeActionType, _expectedNativeActionType, StringComparison.Ordinal);

    public bool TryClaim(string nativeActionType) =>
        Accepts(nativeActionType)
        && Interlocked.CompareExchange(ref _claimed, 1, 0) == 0;

    public bool IsClaimed => Volatile.Read(ref _claimed) != 0;
}
