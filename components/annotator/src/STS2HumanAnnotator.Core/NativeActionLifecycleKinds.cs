namespace STS2HumanAnnotator.Core;

/// <summary>
/// Current runtime vocabulary for native action lifecycle observations. This
/// is a shared event vocabulary only; it is not a ledger or admission
/// authority. Current causal settlement remains owned by
/// <see cref="SemanticBoundaryTracker"/>.
/// </summary>
public static class NativeActionLifecycleKinds
{
    public const string Accepted = "accepted";
    public const string Started = "started";
    public const string PausedForPlayerChoice = "paused_for_player_choice";
    public const string ReadyToResume = "ready_to_resume";
    public const string Resumed = "resumed";
    public const string Cancelled = "cancelled";
    public const string Finished = "finished";
    public const string StrictTransitionInvalidated = "strict_transition_invalidated";
    public const string StrictTransitionAdmitted = "strict_transition_admitted";

    public static bool IsTerminal(string kind) =>
        kind is Cancelled or Finished;
}
