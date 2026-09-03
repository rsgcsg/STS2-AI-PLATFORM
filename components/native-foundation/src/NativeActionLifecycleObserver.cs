using System;
using MegaCrit.Sts2.Core.GameActions;

namespace STS2Platform.NativeFoundation;

public static class NativeActionLifecyclePhase
{
    public const string Started = "started";
    public const string PausedForPlayerChoice = "paused_for_player_choice";
    public const string ReadyToResume = "ready_to_resume";
    public const string Resumed = "resumed";
    public const string Cancelled = "cancelled";
    public const string Finished = "finished";
}

/// <summary>
/// Read-only adapter over the exact GameAction lifecycle. It neither enqueues
/// nor mutates actions and deliberately carries no evidence-store concerns.
/// </summary>
public sealed class NativeActionLifecycleObserver : IDisposable
{
    private readonly GameAction _action;
    private readonly Action<GameAction, string> _observer;
    private bool _disposed;

    public NativeActionLifecycleObserver(
        GameAction action,
        Action<GameAction, string> observer)
    {
        _action = action;
        _observer = observer;
        _action.BeforeExecuted += OnStarted;
        _action.BeforePausedForPlayerChoice += OnPaused;
        _action.BeforeReadyToResumeAfterPlayerChoice += OnReadyToResume;
        _action.BeforeResumedAfterPlayerChoice += OnResumed;
        _action.BeforeCancelled += OnCancelled;
        _action.AfterFinished += OnFinished;
    }

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

    private void OnStarted(GameAction action) =>
        _observer(action, NativeActionLifecyclePhase.Started);

    private void OnPaused(GameAction action) =>
        _observer(action, NativeActionLifecyclePhase.PausedForPlayerChoice);

    private void OnReadyToResume(GameAction action) =>
        _observer(action, NativeActionLifecyclePhase.ReadyToResume);

    private void OnResumed(GameAction action) =>
        _observer(action, NativeActionLifecyclePhase.Resumed);

    private void OnCancelled(GameAction action) =>
        _observer(action, NativeActionLifecyclePhase.Cancelled);

    private void OnFinished(GameAction action) =>
        _observer(action, NativeActionLifecyclePhase.Finished);
}
