using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.NativeUi;

namespace STS2Connector.PlayerEnvironment.Witness;

public sealed record ProcessLocalSemanticAction(
    string Key,
    string Verb,
    string? SubjectReferentId,
    IReadOnlyDictionary<string, string> Arguments,
    string NativeLegalityBasis);

public sealed record ProcessLocalObservedSemanticAction(
    string NativeActionType,
    string? Key,
    string Status,
    int SemanticMatchCount,
    string Membership,
    string? Detail);

public sealed record ProcessLocalUiCatalogObservation(
    string SnapshotId,
    string SnapshotStatus,
    string InteractionKind,
    string BoundActionsStatus,
    int ActionCount,
    string CatalogDigest,
    string? ObservedMembership,
    int? ObservedMatchCount);

public sealed record ProcessLocalNativeSemanticCapture(
    string Schema,
    string Status,
    string Scope,
    string Phase,
    string? SemanticStateDigest,
    JsonNode? SemanticState,
    string SemanticCatalogDigest,
    IReadOnlyList<ProcessLocalSemanticAction> SemanticActions,
    ProcessLocalObservedSemanticAction? ObservedAction,
    ProcessLocalUiCatalogObservation UiCatalog,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> NonClaims,
    string? Detail);

/// <summary>
/// Read-only experiment seam for comparing current UI actionability with a
/// gameplay-semantic combat decision projected from STS2-owned state and
/// native validators. It has no mutation methods and is intentionally absent
/// from the public Player Environment transport.
/// </summary>
public static class PlayerEnvironmentNativeSemanticWitness
{
    public const string Schema =
        "sts2.player-environment/process-local-native-semantic-capture-1";

    public static ProcessLocalNativeSemanticCapture Capture(
        string phase,
        GameAction? observedAction = null,
        ProcessLocalNativeWitnessFrame? uiFrame = null)
    {
        uiFrame ??= PlayerEnvironmentNativeWitness.Capture();
        ProcessLocalUiCatalogObservation ui = BuildUiCatalog(uiFrame, observedAction);
        try
        {
            RunState? run = RunManager.Instance.DebugOnlyGetState();
            if (run?.CurrentRoom is not CombatRoom combatRoom
                || !CombatManager.Instance.IsInProgress)
            {
                return Unavailable(
                    phase,
                    "not_combat",
                    observedAction,
                    ui,
                    "No active combat semantic state exists.");
            }

            Player? player = LocalContext.GetMe(run);
            PlayerCombatState? playerCombat = player?.PlayerCombatState;
            if (player == null || playerCombat == null)
            {
                return Unavailable(
                    phase,
                    "combat_state_unavailable",
                    observedAction,
                    ui,
                    "The local player combat state is unavailable.");
            }

            NativeEntityRegistry entities = NativeUiRuntime.Entities;
            CombatLiveContext context = LiveContextReader.BuildCombat(run, combatRoom, entities);
            bool localPlayPhase = playerCombat.Phase == PlayerTurnPhase.Play
                                  && CombatManager.Instance.IsPartOfPlayerTurn(player)
                                  && !CombatManager.Instance.PlayerActionsDisabled
                                  && !CombatManager.Instance.IsPlayerReadyToEndTurn(player)
                                  && RunManager.Instance.ActionQueueSynchronizer.CombatState
                                  == ActionSynchronizerCombatState.PlayPhase;
            IReadOnlyList<VisibleCombatPotionState> semanticPotions =
                BuildSemanticPotionStates(player, entities, localPlayPhase);
            context = context with
            {
                Player = context.Player with { PotionStates = semanticPotions }
            };

            var state = new
            {
                encounter_type = context.EncounterType,
                round = context.Round,
                turn_owner = context.TurnOwner,
                player = new
                {
                    entity_id = context.Player.PlayerEntityId,
                    hp = player.Creature.CurrentHp,
                    max_hp = player.Creature.MaxHp,
                    context.Player.Block,
                    context.Player.Energy,
                    context.Player.MaxEnergy,
                    context.Player.Stars,
                    hand = context.Player.Hand.Select(card => new
                    {
                        card.EntityId,
                        card.DefinitionId,
                        card.Type,
                        card.Cost,
                        card.StarCost,
                        card.IsUpgraded,
                        card.TargetType,
                        card.CanPlay,
                        card.UnplayableReason
                    }).ToArray(),
                    context.Player.DrawPileCount,
                    context.Player.DiscardPileCount,
                    context.Player.ExhaustPileCount,
                    statuses = context.Player.Statuses.Select(status => new
                    {
                        status.DefinitionId,
                        status.Amount,
                        status.Type
                    }).ToArray(),
                    companions = context.Player.Companions.Select(companion => new
                    {
                        companion.EntityId,
                        companion.DefinitionId,
                        companion.IsAlive,
                        companion.HealthBarVisible,
                        companion.Hp,
                        companion.MaxHp,
                        companion.Block,
                        statuses = companion.Statuses.Select(status => new
                        {
                            status.DefinitionId,
                            status.Amount,
                            status.Type
                        }).ToArray()
                    }).ToArray(),
                    potions = semanticPotions,
                    orbs = context.Player.Orbs.Select(orb => new
                    {
                        orb.EntityId,
                        orb.DefinitionId,
                        orb.PassiveValue,
                        orb.EvokeValue,
                        orb.QueueIndex,
                        orb.IsNextToEvoke
                    }).ToArray(),
                    context.Player.OrbSlots
                },
                enemies = context.Enemies.Select(enemy => new
                {
                    enemy.EntityId,
                    enemy.CombatId,
                    enemy.DefinitionId,
                    enemy.Hp,
                    enemy.MaxHp,
                    enemy.Block,
                    statuses = enemy.Statuses.Select(status => new
                    {
                        status.DefinitionId,
                        status.Amount,
                        status.Type
                    }).ToArray(),
                    intents = enemy.Intents.Select(intent => intent.Type).ToArray()
                }).ToArray(),
                player_turn_number = playerCombat.TurnNumber,
                player_phase = playerCombat.Phase.ToString(),
                player_actions_disabled = CombatManager.Instance.PlayerActionsDisabled,
                player_ready_to_end_turn = CombatManager.Instance.IsPlayerReadyToEndTurn(player),
                action_synchronizer_state =
                    RunManager.Instance.ActionQueueSynchronizer.CombatState.ToString(),
                semantic_input_open = localPlayPhase
            };
            IReadOnlyList<ProcessLocalSemanticAction> actions = localPlayPhase
                ? BuildActions(player, playerCombat, entities)
                : Array.Empty<ProcessLocalSemanticAction>();
            ProcessLocalObservedSemanticAction? observed = observedAction == null
                ? null
                : DescribeObserved(observedAction, player, entities, actions);
            string scope = observedAction?.State.ToString() == "ReadyToResumeExecuting"
                ? "player_choice_continuation"
                : localPlayPhase ? "combat_play_phase" : "combat_non_decision";

            return new ProcessLocalNativeSemanticCapture(
                Schema,
                "captured",
                scope,
                phase,
                StableIdentityHash.Object(state),
                JsonSerializer.SerializeToNode(state, ConnectorMod._jsonOptions),
                CatalogDigest(actions),
                actions,
                observed,
                ui,
                new[]
                {
                    "exact_sts2_logical_hand",
                    "CardModel.CanPlayTargeting",
                    "PotionModel.IsValidTarget+PassesCustomUsabilityCheck",
                    "Player.CanUseOrRemovePotions",
                    "CombatManager+ActionQueueSynchronizer"
                },
                new[]
                {
                    "read_only_experiment_not_action_authority",
                    "not_a_public_player_environment_contract",
                    "compact_state_omits_persistent_run_hud_and_on_demand_reads",
                    "end_turn_finished_does_not_alone_prove_turn_commit",
                    "player_choice_options_remain_owned_by_the_current_selector"
                },
                null);
        }
        catch (Exception exception)
        {
            return Unavailable(
                phase,
                "capture_failed",
                observedAction,
                ui,
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    public static string BuildActionKey(
        string verb,
        string? subjectReferentId,
        IReadOnlyDictionary<string, string>? arguments = null)
    {
        string operands = arguments == null
            ? string.Empty
            : string.Join(
                ",",
                arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value}"));
        return $"{verb}|{subjectReferentId ?? "-"}|{operands}";
    }

    private static ProcessLocalNativeSemanticCapture Unavailable(
        string phase,
        string status,
        GameAction? observedAction,
        ProcessLocalUiCatalogObservation ui,
        string detail) =>
        new(
            Schema,
            status,
            "unavailable",
            phase,
            null,
            null,
            CatalogDigest(Array.Empty<ProcessLocalSemanticAction>()),
            Array.Empty<ProcessLocalSemanticAction>(),
            observedAction == null
                ? null
                : new ProcessLocalObservedSemanticAction(
                    observedAction.GetType().Name,
                    null,
                    "not_described",
                    0,
                    "unknown",
                    detail),
            ui,
            Array.Empty<string>(),
            new[] { "semantic_state_not_captured" },
            detail);

    private static ProcessLocalUiCatalogObservation BuildUiCatalog(
        ProcessLocalNativeWitnessFrame frame,
        GameAction? observedAction)
    {
        ProcessLocalObservedAction? observed = TryDescribeForUi(observedAction);
        ProcessLocalNativeMatch? match = observed == null ? null : frame.Resolve(observed);
        var projection = frame.Snapshot.BoundActions;
        return new ProcessLocalUiCatalogObservation(
            frame.Snapshot.SnapshotId,
            frame.Snapshot.Status,
            frame.Snapshot.Interaction.Kind,
            projection.Status,
            projection.Actions.Count,
            StableIdentityHash.Object(projection.Actions.Select(action => new
            {
                action.Verb,
                action.SubjectReferentId,
                arguments = action.Arguments
                    .OrderBy(argument => argument.Role, StringComparer.Ordinal)
                    .Select(argument => new { argument.Role, argument.ReferentId })
                    .ToArray()
            }).ToArray()),
            match?.Status,
            match?.MatchCount);
    }

    private static IReadOnlyList<ProcessLocalSemanticAction> BuildActions(
        Player player,
        PlayerCombatState combat,
        NativeEntityRegistry entities)
    {
        var actions = new List<ProcessLocalSemanticAction>();
        foreach (CardModel card in combat.Hand.Cards)
        {
            string cardId = entities.GetId(card, "card");
            foreach (Creature? target in CardTargets(card, player.Creature.CombatState!))
            {
                if (!card.CanPlayTargeting(target))
                    continue;
                IReadOnlyDictionary<string, string> arguments = target == null
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["target"] = entities.GetId(target, TargetKind(target))
                    };
                actions.Add(new ProcessLocalSemanticAction(
                    BuildActionKey("play", cardId, arguments),
                    "play",
                    cardId,
                    arguments,
                    "logical_hand+CardModel.CanPlayTargeting"));
            }
        }

        for (int slot = 0; slot < player.PotionSlots.Count; slot++)
        {
            PotionModel? potion = player.GetPotionAtSlotIndex(slot);
            if (!CanUsePotionSemantically(player, potion))
                continue;
            string potionId = entities.GetId(potion!, "potion");
            foreach (Creature? target in PotionTargets(potion!, player))
            {
                if (!potion!.IsValidTarget(target))
                    continue;
                IReadOnlyDictionary<string, string> arguments = target == null
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["target"] = entities.GetId(target, TargetKind(target))
                    };
                actions.Add(new ProcessLocalSemanticAction(
                    BuildActionKey("use", potionId, arguments),
                    "use",
                    potionId,
                    arguments,
                    "current_potion_slot+native_potion_usability_and_target_validation"));
            }
        }

        actions.Add(new ProcessLocalSemanticAction(
            BuildActionKey("end_turn", null),
            "end_turn",
            null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            "local_player_combat_play_phase"));
        return actions.OrderBy(action => action.Key, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<Creature?> CardTargets(
        CardModel card,
        ICombatState combat) => card.TargetType switch
    {
        TargetType.AnyEnemy => combat.HittableEnemies.Cast<Creature?>(),
        TargetType.AnyAlly => combat.PlayerCreatures.Where(creature => creature.IsAlive).Cast<Creature?>(),
        _ => new Creature?[] { null }
    };

    private static IEnumerable<Creature?> PotionTargets(PotionModel potion, Player player)
    {
        ICombatState combat = player.Creature.CombatState!;
        return potion.TargetType switch
        {
            TargetType.AnyEnemy => combat.HittableEnemies.Cast<Creature?>(),
            TargetType.AnyAlly => combat.PlayerCreatures.Where(creature => creature.IsAlive).Cast<Creature?>(),
            TargetType.AnyPlayer => combat.PlayerCreatures.Where(creature => creature.IsAlive).Cast<Creature?>(),
            TargetType.Self => new Creature?[] { player.Creature },
            _ => new Creature?[] { null }
        };
    }

    private static IReadOnlyList<VisibleCombatPotionState> BuildSemanticPotionStates(
        Player player,
        NativeEntityRegistry entities,
        bool localPlayPhase)
    {
        var result = new List<VisibleCombatPotionState>();
        for (int slot = 0; slot < player.PotionSlots.Count; slot++)
        {
            PotionModel? potion = player.GetPotionAtSlotIndex(slot);
            if (potion == null)
                continue;
            bool automatic = potion.Usage == PotionUsage.Automatic;
            result.Add(new VisibleCombatPotionState(
                entities.GetId(potion, "potion"),
                potion.TargetType.ToString(),
                localPlayPhase && CanUsePotionSemantically(player, potion)
                               && PotionTargets(potion, player).Any(potion.IsValidTarget),
                automatic));
        }
        return result;
    }

    private static bool CanUsePotionSemantically(Player player, PotionModel? potion) =>
        potion != null
        && player.CanUseOrRemovePotions
        && potion.Usage is PotionUsage.CombatOnly or PotionUsage.AnyTime
        && !potion.Owner.Creature.IsDead
        && potion.PassesCustomUsabilityCheck;

    private static ProcessLocalObservedSemanticAction DescribeObserved(
        GameAction action,
        Player player,
        NativeEntityRegistry entities,
        IReadOnlyList<ProcessLocalSemanticAction> actions)
    {
        string? key = TryBuildObservedKey(action, player, entities);
        int count = key == null ? 0 : actions.Count(candidate => candidate.Key == key);
        return new ProcessLocalObservedSemanticAction(
            action.GetType().Name,
            key,
            key == null ? "not_described" : "described",
            count,
            key == null ? "unknown" : count == 1 ? "exact_once" : count == 0 ? "absent" : "ambiguous",
            key == null ? "The exact native operand no longer resolved." : null);
    }

    private static string? TryBuildObservedKey(
        GameAction action,
        Player player,
        NativeEntityRegistry entities)
    {
        if (action is PlayCardAction play)
        {
            CardModel? card = play.NetCombatCard.ToCardModelOrNull();
            if (card == null)
                return null;
            Creature? target = play.Target;
            IReadOnlyDictionary<string, string> arguments = target == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = entities.GetId(target, TargetKind(target))
                };
            return BuildActionKey("play", entities.GetId(card, "card"), arguments);
        }
        if (action is UsePotionAction use)
        {
            PotionModel? potion = player.GetPotionAtSlotIndex((int)use.PotionIndex);
            if (potion == null)
                return null;
            Creature? target = player.Creature.CombatState?.GetCreature(use.TargetId);
            if (target == null && potion.TargetType is TargetType.Self or TargetType.AnyPlayer)
                target = player.Creature;
            IReadOnlyDictionary<string, string> arguments = target == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = entities.GetId(target, TargetKind(target))
                };
            return BuildActionKey("use", entities.GetId(potion, "potion"), arguments);
        }
        return action is EndPlayerTurnAction
            ? BuildActionKey("end_turn", null)
            : null;
    }

    private static ProcessLocalObservedAction? TryDescribeForUi(GameAction? action)
    {
        if (action is PlayCardAction play)
        {
            CardModel? card = play.NetCombatCard.ToCardModelOrNull();
            if (card == null)
                return null;
            IReadOnlyDictionary<string, object> arguments = play.Target == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal) { ["target"] = play.Target };
            return new ProcessLocalObservedAction("play", card, arguments);
        }
        if (action is UsePotionAction use)
        {
            PotionModel? potion = use.Player.GetPotionAtSlotIndex((int)use.PotionIndex);
            if (potion == null)
                return null;
            Creature? target = use.Player.Creature.CombatState?.GetCreature(use.TargetId);
            if (target == null && potion.TargetType is TargetType.Self or TargetType.AnyPlayer)
                target = use.Player.Creature;
            IReadOnlyDictionary<string, object> arguments = target == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal) { ["target"] = target };
            return new ProcessLocalObservedAction("use", potion, arguments);
        }
        return action is EndPlayerTurnAction
            ? new ProcessLocalObservedAction(
                "end_turn",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal))
            : null;
    }

    private static string CatalogDigest(IReadOnlyList<ProcessLocalSemanticAction> actions) =>
        StableIdentityHash.Object(actions.Select(action => action.Key).Order(StringComparer.Ordinal).ToArray());

    private static string TargetKind(Creature target) => target.IsPlayer ? "player" : "creature";
}
