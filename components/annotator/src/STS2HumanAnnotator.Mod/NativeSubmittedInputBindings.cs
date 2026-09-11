using MegaCrit.Sts2.Core.GameActions;
using STS2HumanAnnotator.Core;

namespace STS2HumanAnnotator.Mod;

internal sealed record SubmittedHumanInput(
    string SessionId, string TimelineId, string RunId,
    HumanActionContext? Context, DeferredHumanActionFailure? Failure);

internal static partial class RecorderRuntime
{
    private static readonly ExactNativeInputBindingTable<GameAction, SubmittedHumanInput>
        SubmittedInputs = new();

    internal static void BindSubmittedHumanInput(GameAction action)
    {
        try { BindSubmittedHumanInputCore(action); }
        catch (Exception exception)
        {
            // Never allow an unrecorded real effect to leave later green
            // transitions if the exact carrier itself could not be retained.
            DisableSemanticBoundaryTrace(exception);
        }
    }

    private static void BindSubmittedHumanInputCore(GameAction action)
    {
        // Native retries of a deferred request are not new Human inputs.
        if (SubmittedInputs.TryGet(action, out _)) return;
        if (!AcceptingNewWitnesses() || SessionId == null || TimelineId == null) return;
        string type = action.GetType().Name;
        HumanActionContext? context = HumanActionScope.Current;
        DeferredHumanActionFailure? failure = HumanActionScope.CurrentDeferredFailure;
        if (context?.ExpectedNativeActionType != type) context = null;
        if (failure?.ExpectedNativeActionType != type) failure = null;
        if (failure != null) context = null; // the exact failed input outranks an enclosing scope
        if (context == null && failure == null) return; // no manufactured Human origin
        var input = new SubmittedHumanInput(SessionId, TimelineId, _currentRunId, context, failure);
        if (!SubmittedInputs.TryBind(action, input))
            throw new InvalidOperationException("Exact native action already carries another Human input.");
    }
}
