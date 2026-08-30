using MegaCrit.Sts2.Core.GameActions;
using STS2HumanAnnotator.Core;
using STS2Platform.NativeFoundation;

namespace STS2HumanAnnotator.Mod;

internal sealed class NativeActionLifecycleSubscription : IDisposable
{
    private readonly GameAction _action;
    private readonly NativeActionLifecycleObserver _nativeObserver;
    private readonly Action<NativeActionLifecycleSubscription, string> _observer;
    private bool _disposed;

    internal NativeActionLifecycleSubscription(
        GameAction action,
        string actionWitnessId,
        long actionSequence,
        string recordId,
        Action<NativeActionLifecycleSubscription, string> observer)
    {
        _action = action;
        ActionWitnessId = actionWitnessId;
        ActionSequence = actionSequence;
        RecordId = recordId;
        _observer = observer;
        _nativeObserver = new NativeActionLifecycleObserver(
            action,
            (_, phase) => _observer(this, phase));
    }

    internal GameAction Action => _action;
    internal string ActionWitnessId { get; }
    internal long ActionSequence { get; }
    internal string RecordId { get; }
    internal string NativeActionType => _action.GetType().Name;
    internal uint? NativeQueueId => _action.Id;
    internal string NativeState => _action.State.ToString().ToLowerInvariant();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _nativeObserver.Dispose();
    }
}
