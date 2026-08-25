using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

/// <summary>
/// Typed application boundary for recording controls used by the Platform UI.
/// It changes witness admission only; it never owns or invokes STS2 actions.
/// </summary>
public sealed class RecordingApplicationService
{
    public static RecordingApplicationService Instance { get; } = new();

    private RecordingApplicationService()
    {
    }

    public RecordingApplicationStatus GetStatus() => RecorderRuntime.GetRecordingApplicationStatus();

    public RecordingControlResult Pause() =>
        RecorderRuntime.ApplyRecordingControl(RecordingControlCommand.Pause);

    public RecordingControlResult Resume() =>
        RecorderRuntime.ApplyRecordingControl(RecordingControlCommand.Resume);

    public RecordingControlResult Close() =>
        RecorderRuntime.ApplyRecordingControl(RecordingControlCommand.Close);
}
