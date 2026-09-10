namespace STS2HumanAnnotator.Core;

/// <summary>
/// Session-local run identity and observation provenance. An in-progress poll
/// never substitutes for Launch, and a later Launch upgrades the same active
/// run's provenance without allocating a second run identity.
/// </summary>
public sealed class RecordingRunLifecycle
{
    private int _sequence;
    private bool _active;
    private bool _nativeStarted;
    private bool _nativeEnded;

    public string RunId { get; private set; } = "run-unassigned";

    public void Reset()
    {
        _sequence = 0;
        _active = false;
        _nativeStarted = false;
        _nativeEnded = false;
        RunId = "run-unassigned";
    }

    public RecordingRunObservation ObserveInProgress(bool inProgress)
    {
        if (inProgress)
        {
            // The terminal run may remain installed until native cleanup.
            if (_active || _nativeEnded)
                return RecordingRunObservation.None;
            BeginRun();
            return RecordingRunObservation.ObservedInProgress;
        }

        bool endedWithoutWitness = _active;
        _active = false;
        _nativeStarted = false;
        _nativeEnded = false;
        return endedWithoutWitness
            ? RecordingRunObservation.EndedUnproved
            : RecordingRunObservation.None;
    }

    public RecordingRunObservation ObserveNativeStarted()
    {
        if (_nativeStarted)
            return RecordingRunObservation.None;
        if (!_active)
            BeginRun();
        _nativeStarted = true;
        _nativeEnded = false;
        return RecordingRunObservation.StartedNative;
    }

    public RecordingRunObservation ObserveNativeEnded()
    {
        if (_nativeEnded)
            return RecordingRunObservation.None;
        _nativeEnded = true;
        _nativeStarted = false;
        _active = false;
        return RecordingRunObservation.EndedNative;
    }

    private void BeginRun()
    {
        RunId = $"run-{++_sequence:D4}";
        _active = true;
        _nativeStarted = false;
        _nativeEnded = false;
    }
}

public enum RecordingRunObservation
{
    None,
    ObservedInProgress,
    StartedNative,
    EndedNative,
    EndedUnproved,
}
