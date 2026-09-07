namespace STS2HumanAnnotator.Core;

public sealed record NativeTerminalTaskDisposition(
    bool IsTerminal,
    bool IsCancelled,
    string? UnavailableReason)
{
    public static NativeTerminalTaskDisposition Classify(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (!task.IsCompleted)
            return new(false, false, "completion_task_not_terminal_after_callback");
        if (task.IsFaulted)
            return new(true, false, "completion_task_faulted");
        return new(true, task.IsCanceled, null);
    }
}
