using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class NativeTerminalTaskDispositionTests
{
    [Fact]
    public void FaultedTaskIsExplicitlyUnavailableAndNeverCancelled()
    {
        Task task = Task.FromException(new InvalidOperationException("native failure"));

        NativeTerminalTaskDisposition result = NativeTerminalTaskDisposition.Classify(task);

        Assert.True(result.IsTerminal);
        Assert.False(result.IsCancelled);
        Assert.Equal("completion_task_faulted", result.UnavailableReason);
    }

    [Fact]
    public void CancelledTaskIsAnExplicitCancelDisposition()
    {
        Task task = Task.FromCanceled(new CancellationToken(canceled: true));

        NativeTerminalTaskDisposition result = NativeTerminalTaskDisposition.Classify(task);

        Assert.True(result.IsTerminal);
        Assert.True(result.IsCancelled);
        Assert.Null(result.UnavailableReason);
    }

    [Fact]
    public void IncompleteTaskCannotBePromotedToTerminal()
    {
        var source = new TaskCompletionSource();

        NativeTerminalTaskDisposition result = NativeTerminalTaskDisposition.Classify(source.Task);

        Assert.False(result.IsTerminal);
        Assert.False(result.IsCancelled);
        Assert.Equal("completion_task_not_terminal_after_callback", result.UnavailableReason);
    }
}
