using MegaCrit.Sts2.Core.GameActions;
using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

internal sealed class NativeActionLifecycleSubscription : IDisposable
{
    private readonly GameAction _action;
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
        _action.BeforeExecuted += OnStarted;
        _action.BeforePausedForPlayerChoice += OnPaused;
        _action.BeforeReadyToResumeAfterPlayerChoice += OnReadyToResume;
        _action.BeforeResumedAfterPlayerChoice += OnResumed;
        _action.BeforeCancelled += OnCancelled;
        _action.AfterFinished += OnFinished;
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
        _action.BeforeExecuted -= OnStarted;
        _action.BeforePausedForPlayerChoice -= OnPaused;
        _action.BeforeReadyToResumeAfterPlayerChoice -= OnReadyToResume;
        _action.BeforeResumedAfterPlayerChoice -= OnResumed;
        _action.BeforeCancelled -= OnCancelled;
        _action.AfterFinished -= OnFinished;
    }

    private void OnStarted(GameAction _) =>
        _observer(this, NativeActionLifecycleKinds.Started);

    private void OnPaused(GameAction _) =>
        _observer(this, NativeActionLifecycleKinds.PausedForPlayerChoice);

    private void OnReadyToResume(GameAction _) =>
        _observer(this, NativeActionLifecycleKinds.ReadyToResume);

    private void OnResumed(GameAction _) =>
        _observer(this, NativeActionLifecycleKinds.Resumed);

    private void OnCancelled(GameAction _) =>
        _observer(this, NativeActionLifecycleKinds.Cancelled);

    private void OnFinished(GameAction _) =>
        _observer(this, NativeActionLifecycleKinds.Finished);
}
