using System;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// One process-local STS2 fact that a new player decision owner has reached an
/// exact native-ready seam. This observation does not claim that Connector can
/// publish a complete fair-player frame; consumers must capture and validate
/// that frame independently at the same synchronous seam.
/// </summary>
public sealed record NativeDecisionOwnerReadyObservation(
    string Domain,
    object NativeOwner,
    string NativeOwnerType,
    string NativeMechanism);

public static class NativeDecisionOwnerReadyProvider
{
    public const string CombatTurnDomain = "combat_turn";
    public const string CombatTurnMechanism =
        "CombatManager.TurnStarted->NEndTurnButton.OnTurnStarted.postfix";

    public static event Action<NativeDecisionOwnerReadyObservation>? Observed;

    /// <summary>
    /// Called only by the exact-version composition patch after STS2 has run
    /// its player-turn input-owner callback. The semantic checks reject enemy,
    /// stale and non-play-phase callbacks before publishing an observation.
    /// </summary>
    internal static bool ObservePlayerCombatTurnReady(CombatState state)
    {
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (run?.CurrentRoom is not CombatRoom
            || !ReferenceEquals(state.RunState, run)
            || !ReferenceEquals(CombatManager.Instance.DebugOnlyGetState(), state)
            || state.CurrentSide != CombatSide.Player
            || !CombatManager.Instance.IsInProgress)
        {
            return false;
        }

        Player? player = LocalContext.GetMe(state);
        PlayerCombatState? combat = player?.PlayerCombatState;
        if (player == null
            || combat == null
            || !NativeCombatDecisionProvider.IsSemanticPlayPhase(player, combat))
        {
            return false;
        }

        Observed?.Invoke(new NativeDecisionOwnerReadyObservation(
            CombatTurnDomain,
            state,
            state.GetType().FullName ?? state.GetType().Name,
            CombatTurnMechanism));
        return true;
    }
}
