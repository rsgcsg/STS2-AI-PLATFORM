using STS2Connector.PlayerEnvironment.Witness;

namespace STS2HumanAnnotator.Mod;

/// <summary>
/// The small, process-local admission state machine shared by every native
/// accepted seam. It deliberately owns no transport or ledger: staging remains in
/// <see cref="HumanActionScope"/> and asynchronous completion remains in the
/// existing native completion correlator.
/// </summary>
internal static class AcceptedDecisionObserver
{
    internal enum OutcomeKind
    {
        NoScope,
        DeferredFailure,
        NativeTypeMismatch,
        MappingFailure,
        Duplicate,
        Accepted
    }

    internal readonly record struct Outcome(
        OutcomeKind Kind,
        HumanActionContext? Context = null,
        DeferredHumanActionFailure? DeferredFailure = null,
        ProcessLocalNativeMatch? Match = null);

    /// <summary>
    /// Classifies one already-accepted native callback. The callback is the
    /// only place where the scope gate is claimed; Prefix staging never claims
    /// it. A failed mapping claims the same gate so a duplicate callback is
    /// harmless, while an unowned callback remains a non-root.
    /// </summary>
    internal static Outcome Observe(
        string nativeActionType,
        HumanActionContext? context,
        ProcessLocalNativeMatch? match,
        bool hasMapping)
    {
        if (context == null)
        {
            DeferredHumanActionFailure? failure = HumanActionScope.CurrentDeferredFailure;
            return failure != null
                ? new Outcome(OutcomeKind.DeferredFailure, null, failure)
                : new Outcome(OutcomeKind.NoScope);
        }

        if (!context.AcceptsRootAction(nativeActionType))
        {
            return context.TryClaimRejectedAcceptedIngress()
                ? new Outcome(OutcomeKind.NativeTypeMismatch, context)
                : new Outcome(OutcomeKind.Duplicate, context);
        }

        if (!hasMapping || match == null || !IsExact(match))
        {
            return context.TryClaimRootAction(nativeActionType)
                ? new Outcome(OutcomeKind.MappingFailure, context, null, match)
                : new Outcome(OutcomeKind.Duplicate, context, null, match);
        }

        return context.TryClaimRootAction(nativeActionType)
            ? new Outcome(OutcomeKind.Accepted, context, null, match)
            : new Outcome(OutcomeKind.Duplicate, context, null, match);
    }

    private static bool IsExact(ProcessLocalNativeMatch match) =>
        string.Equals(match.Status, "exact_unique", StringComparison.Ordinal)
        && match.MatchCount == 1
        && match.BoundAction != null;
}
