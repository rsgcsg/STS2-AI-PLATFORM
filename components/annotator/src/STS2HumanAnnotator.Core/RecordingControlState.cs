namespace STS2HumanAnnotator.Core;

public enum RecordingControlState
{
    Stopped,
    Recording,
    Paused,
    Closed
}

public enum RecordingControlCommand
{
    Start,
    Pause,
    Resume,
    Close
}

public sealed record RecordingControlSnapshot(
    RecordingControlState State,
    string? SessionId,
    DateTimeOffset ChangedAt,
    string Detail)
{
    public static RecordingControlSnapshot Initial(DateTimeOffset changedAt) =>
        new(RecordingControlState.Stopped, null, changedAt, "Recording has not been started.");
}

public sealed record RecordingControlResult(
    bool Accepted,
    string Code,
    string Detail,
    RecordingControlSnapshot Snapshot);

public sealed record RecordingApplicationStatus(
    RecordingControlSnapshot Control,
    string RuntimeState,
    string Detail,
    RecorderEnvironmentIdentity? Environment,
    string? CurrentSnapshotId,
    IReadOnlyList<string> Blockers);

public static class RecordingControlStateMachine
{
    public static RecordingControlSnapshot Start(
        string sessionId,
        DateTimeOffset changedAt) =>
        new(
            RecordingControlState.Recording,
            sessionId,
            changedAt,
            "Recording is accepting eligible native-human decisions.");

    public static RecordingControlResult Apply(
        RecordingControlSnapshot current,
        RecordingControlCommand command,
        string sessionId,
        DateTimeOffset changedAt)
    {
        RecordingControlState next;
        string detail;

        switch (command)
        {
            case RecordingControlCommand.Start when current.State == RecordingControlState.Stopped:
                next = RecordingControlState.Recording;
                detail = "Recording is accepting eligible native-human decisions.";
                break;
            case RecordingControlCommand.Pause when current.State == RecordingControlState.Recording:
                next = RecordingControlState.Paused;
                detail = "Recording is paused; no new decision will be admitted.";
                break;
            case RecordingControlCommand.Resume when current.State == RecordingControlState.Paused:
                next = RecordingControlState.Recording;
                detail = "Recording has resumed and is accepting eligible decisions.";
                break;
            case RecordingControlCommand.Close when current.State != RecordingControlState.Closed:
                next = RecordingControlState.Closed;
                detail = "Recording is closed and cannot be resumed.";
                break;
            default:
                return new RecordingControlResult(
                    false,
                    "invalid_transition",
                    $"Cannot {command.ToString().ToLowerInvariant()} while recording is {current.State.ToString().ToLowerInvariant()}.",
                    current);
        }

        RecordingControlSnapshot snapshot = new(next, sessionId, changedAt, detail);
        return new RecordingControlResult(true, command.ToString().ToLowerInvariant(), detail, snapshot);
    }
}
