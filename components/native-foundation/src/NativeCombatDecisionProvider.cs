using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// Projects the current combat decision directly from STS2-owned logical
/// state and native validators. It observes legality but never executes input.
/// </summary>
public static class NativeCombatDecisionProvider
{
    public static NativeCombatDecision Capture(INativeReferentIdentity identities)
    {
        try
        {
            RunState? run = RunManager.Instance.DebugOnlyGetState();
            if (run?.CurrentRoom is not CombatRoom || !CombatManager.Instance.IsInProgress)
                return Unavailable("not_combat", "No active combat exists.");

            Player? player = LocalContext.GetMe(run);
            PlayerCombatState? combat = player?.PlayerCombatState;
            if (player == null || combat == null)
                return Unavailable("combat_state_unavailable", "The local combat state is unavailable.");

            bool decisionOpen = IsSemanticPlayPhase(player, combat);
            IReadOnlyList<NativeSemanticAction> actions = decisionOpen
                ? BuildActions(player, combat, identities)
                : Array.Empty<NativeSemanticAction>();
            return new NativeCombatDecision(
                "captured",
                decisionOpen ? "combat_play_phase" : "combat_non_decision",
                decisionOpen,
                actions,
                new[]
                {
                    "PlayerCombatState.Hand.Cards",
                    "CardModel.CanPlayTargeting",
                    "PotionModel.IsValidTarget+PassesCustomUsabilityCheck",
                    "Player.CanUseOrRemovePotions",
                    "CombatManager+ActionQueueSynchronizer"
                },
                null);
        }
        catch (Exception exception)
        {
            return Unavailable(
                "capture_failed",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    public static NativeObservedSemanticAction Describe(
        GameAction action,
        NativeCombatDecision decision,
        INativeReferentIdentity identities)
    {
        string? key = TryBuildObservedKey(action, identities);
        int count = key == null ? 0 : decision.Actions.Count(candidate => candidate.Key == key);
        return new NativeObservedSemanticAction(
            action.GetType().Name,
            key,
            key == null ? "not_described" : "described",
            count,
            key == null ? "unknown" : count == 1 ? "exact_once" : count == 0 ? "absent" : "ambiguous",
            key == null ? "The exact native operand no longer resolved." : null);
    }

    private static NativeCombatDecision Unavailable(string status, string detail) =>
        new(
            status,
            "unavailable",
            false,
            Array.Empty<NativeSemanticAction>(),
            Array.Empty<string>(),
            detail);

    private static bool IsSemanticPlayPhase(Player player, PlayerCombatState combat) =>
        combat.Phase == PlayerTurnPhase.Play
        && CombatManager.Instance.IsPartOfPlayerTurn(player)
        && !CombatManager.Instance.PlayerActionsDisabled
        && !CombatManager.Instance.IsPlayerReadyToEndTurn(player)
        && RunManager.Instance.ActionQueueSynchronizer.CombatState
            == ActionSynchronizerCombatState.PlayPhase;

    private static IReadOnlyList<NativeSemanticAction> BuildActions(
        Player player,
        PlayerCombatState combat,
        INativeReferentIdentity identities)
    {
        var actions = new List<NativeSemanticAction>();
        foreach (CardModel card in combat.Hand.Cards)
        {
            string cardId = identities.GetId(card, "card");
            foreach (Creature? target in CardTargets(card, player.Creature.CombatState!))
            {
                if (!card.CanPlayTargeting(target))
                    continue;
                actions.Add(CreateAction(
                    "play",
                    cardId,
                    card,
                    target,
                    identities,
                    "logical_hand+CardModel.CanPlayTargeting"));
            }
        }

        for (int slot = 0; slot < player.PotionSlots.Count; slot++)
        {
            PotionModel? potion = player.GetPotionAtSlotIndex(slot);
            if (!CanUsePotionSemantically(player, potion))
                continue;
            string potionId = identities.GetId(potion!, "potion");
            foreach (Creature? target in PotionTargets(potion!, player))
            {
                if (!potion!.IsValidTarget(target))
                    continue;
                actions.Add(CreateAction(
                    "use",
                    potionId,
                    potion,
                    target,
                    identities,
                    "current_potion_slot+native_potion_usability_and_target_validation"));
            }
        }

        actions.Add(new NativeSemanticAction(
            NativeSemanticActionCatalog.BuildKey("end_turn", null),
            "end_turn",
            null,
            player,
            Array.Empty<NativeSemanticOperand>(),
            "local_player_combat_play_phase"));
        return actions.OrderBy(action => action.Key, StringComparer.Ordinal).ToArray();
    }

    private static NativeSemanticAction CreateAction(
        string verb,
        string subjectId,
        object subject,
        Creature? target,
        INativeReferentIdentity identities,
        string legalityBasis)
    {
        IReadOnlyList<NativeSemanticOperand> operands = target == null
            ? Array.Empty<NativeSemanticOperand>()
            : new[]
            {
                new NativeSemanticOperand(
                    "target",
                    identities.GetId(target, TargetKind(target)),
                    target)
            };
        return new NativeSemanticAction(
            NativeSemanticActionCatalog.BuildKey(
                verb,
                subjectId,
                operands.ToDictionary(value => value.Role, value => value.ReferentId, StringComparer.Ordinal)),
            verb,
            subjectId,
            subject,
            operands,
            legalityBasis);
    }

    private static IEnumerable<Creature?> CardTargets(CardModel card, ICombatState combat) =>
        card.TargetType switch
        {
            TargetType.AnyEnemy => combat.HittableEnemies.Cast<Creature?>(),
            TargetType.AnyAlly =>
                combat.PlayerCreatures.Where(creature => creature.IsAlive).Cast<Creature?>(),
            // CardModel.IsValidTarget intentionally differs from potion
            // targeting: every non-enemy/non-ally card is played with null.
            _ => new Creature?[] { null }
        };

    private static IEnumerable<Creature?> PotionTargets(PotionModel potion, Player player)
    {
        ICombatState combat = player.Creature.CombatState!;
        return potion.TargetType switch
        {
            TargetType.AnyEnemy => combat.HittableEnemies.Cast<Creature?>(),
            TargetType.AnyAlly or TargetType.AnyPlayer =>
                combat.PlayerCreatures.Where(creature => creature.IsAlive).Cast<Creature?>(),
            TargetType.Self => new Creature?[] { player.Creature },
            _ => new Creature?[] { null }
        };
    }

    private static bool CanUsePotionSemantically(Player player, PotionModel? potion) =>
        potion != null
        && player.CanUseOrRemovePotions
        && potion.Usage is PotionUsage.CombatOnly or PotionUsage.AnyTime
        && !potion.Owner.Creature.IsDead
        && potion.PassesCustomUsabilityCheck;

    private static string? TryBuildObservedKey(
        GameAction action,
        INativeReferentIdentity identities)
    {
        if (action is PlayCardAction play)
        {
            CardModel? card = play.NetCombatCard.ToCardModelOrNull();
            return card == null
                ? null
                : ObservedKey("play", card, play.Target, identities);
        }
        if (action is UsePotionAction use)
        {
            PotionModel? potion = use.Player.GetPotionAtSlotIndex((int)use.PotionIndex);
            if (potion == null)
                return null;
            Creature? target = use.Player.Creature.CombatState?.GetCreature(use.TargetId);
            if (target == null && potion.TargetType is TargetType.Self or TargetType.AnyPlayer)
                target = use.Player.Creature;
            return ObservedKey("use", potion, target, identities);
        }
        return action is EndPlayerTurnAction
            ? NativeSemanticActionCatalog.BuildKey("end_turn", null)
            : null;
    }

    private static string ObservedKey(
        string verb,
        object subject,
        Creature? target,
        INativeReferentIdentity identities)
    {
        string subjectKind = verb == "play" ? "card" : "potion";
        IReadOnlyDictionary<string, string> arguments = target == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = identities.GetId(target, TargetKind(target))
            };
        return NativeSemanticActionCatalog.BuildKey(
            verb,
            identities.GetId(subject, subjectKind),
            arguments);
    }

    private static string TargetKind(Creature target) =>
        target.IsPlayer
            ? "player"
            : target.CombatState?.PlayerCreatures.Contains(target) == true
                ? "companion"
                : "enemy";
}
