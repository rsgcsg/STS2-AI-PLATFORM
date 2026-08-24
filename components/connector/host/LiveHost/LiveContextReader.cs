using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

internal static class LiveContextReader
{
    public static ILiveContext Build(NativeEntityRegistry entities)
    {
        try
        {
            RunState? runState = RunManager.Instance.DebugOnlyGetState();
            if (runState?.CurrentRoom is EventRoom eventRoom)
                return BuildEvent(eventRoom);
            if (runState?.CurrentRoom is RestSiteRoom)
                return new RestLiveContext("rest");
            if (runState?.CurrentRoom is MerchantRoom merchantRoom)
                return BuildShop(merchantRoom, entities);
            if (runState?.CurrentRoom is TreasureRoom)
                return new TreasureLiveContext("treasure");
            if (runState?.CurrentRoom is CombatRoom combatRoom && CombatManager.Instance.IsInProgress)
                return BuildCombat(runState, combatRoom, entities);

            return new UnknownLiveContext(
                "unknown",
                runState?.CurrentRoom?.GetType().Name ?? "no_active_run_context",
                "This context has not yet received a complete player-visible projection.");
        }
        catch (Exception ex)
        {
            return new UnknownLiveContext(
                "unknown",
                "context_read_failed",
                $"Context projection failed closed: {ex.GetType().Name}");
        }
    }

    public static EventLiveContext BuildEvent(EventRoom room)
    {
        EventModel model = room.LocalMutableEvent ?? room.CanonicalEvent;
        NEventRoom? uiRoom = NEventRoom.Instance;
        bool ancient = model is AncientEventModel;
        bool inDialogue = false;
        if (ancient && uiRoom != null)
        {
            NAncientEventLayout? layout = ConnectorMod.FindFirst<NAncientEventLayout>(uiRoom);
            NClickableControl? hitbox = layout?.GetNodeOrNull<NClickableControl>("%DialogueHitbox");
            inDialogue = hitbox is { IsEnabled: true } && ConnectorMod.IsNodeVisible(hitbox);
        }

        return new EventLiveContext(
            "event",
            model.Id.Entry,
            ReadNodeText(uiRoom, "%Title") ?? ConnectorMod.SafeGetText(() => model.Title),
            ancient,
            inDialogue,
            ReadNodeText(uiRoom, "%EventDescription") ?? ConnectorMod.SafeGetText(() => model.Description));
    }

    public static ShopLiveContext BuildShop(MerchantRoom room, NativeEntityRegistry entities)
    {
        // Persistent player facts are owned by top-level shared_state. Shop
        // context identifies only the semantic room; the Surface owns offers.
        return new ShopLiveContext("shop");
    }

    public static CombatLiveContext BuildCombat(
        RunState runState,
        CombatRoom room,
        NativeEntityRegistry entities)
    {
        CombatState combat = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is unavailable.");
        Player player = LocalContext.GetMe(runState)
            ?? throw new InvalidOperationException("Local player is unavailable.");
        PlayerCombatState playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");

        VisibleCombatPlayer visiblePlayer = BuildPlayer(player, playerCombat, entities);
        VisibleEnemy[] enemies = combat.Enemies
            .Where(enemy => enemy.IsAlive)
            .Select(enemy => BuildEnemy(enemy, entities))
            .ToArray();
        bool playPhase = playerCombat.Phase == PlayerTurnPhase.Play
                         && CombatManager.Instance.IsPartOfPlayerTurn(player)
                         && !CombatManager.Instance.PlayerActionsDisabled;

        return new CombatLiveContext(
            "combat",
            room.RoomType switch
            {
                RoomType.Monster => "normal",
                RoomType.Elite => "elite",
                RoomType.Boss => "boss",
                _ => "unknown"
            },
            combat.RoundNumber,
            combat.CurrentSide.ToString().ToLowerInvariant(),
            playPhase,
            visiblePlayer,
            enemies);
    }

    public static VisibleCard BuildCard(
        CardModel card,
        string entityId,
        bool selected = false,
        bool includeCombatLegality = false,
        PileType displayPile = PileType.Hand)
    {
        string cost = card.EnergyCost.CostsX ? "X" : card.EnergyCost.GetAmountToSpend().ToString();
        string? starCost = card.HasStarCostX
            ? "X"
            : card.CurrentStarCost >= 0 ? card.GetStarCostWithModifiers().ToString() : null;
        string? description;
        try
        {
            description = ConnectorMod.StripRichTextTags(card.GetDescriptionForPile(displayPile)).Replace("\n", " ");
        }
        catch
        {
            description = ConnectorMod.SafeGetText(() => card.Description)?.Replace("\n", " ");
        }

        VisibleEnchantment? existing = card.Enchantment == null
            ? null
            : new VisibleEnchantment(
                card.Enchantment.Id.Entry,
                ConnectorMod.SafeGetText(() => card.Enchantment.Title),
                ConnectorMod.SafeGetText(() => card.Enchantment.DynamicDescription),
                card.Enchantment.Amount,
                "card_hover_semantics");

        bool? canPlay = null;
        string? unplayableReason = null;
        if (includeCombatLegality)
        {
            card.CanPlay(out UnplayableReason reason, out _);
            canPlay = reason == UnplayableReason.None;
            unplayableReason = reason == UnplayableReason.None ? null : reason.ToString();
        }

        return new VisibleCard(
            entityId,
            card.Id.Entry,
            ConnectorMod.SafeGetText(() => card.Title),
            card.Type.ToString(),
            cost,
            starCost,
            description,
            card.Rarity.ToString(),
            card.IsUpgraded,
            selected,
            existing,
            includeCombatLegality ? card.TargetType.ToString() : null,
            canPlay,
            unplayableReason);
    }

    public static IReadOnlyList<VisibleStatus> BuildStatuses(Creature creature)
    {
        var result = new List<VisibleStatus>();
        foreach (PowerModel power in creature.Powers)
        {
            if (!power.IsVisible)
                continue;
            try
            {
                HoverTip hoverTip = power.HoverTips
                    .OfType<HoverTip>()
                    .FirstOrDefault(tip => tip.Id == power.Id.ToString());
                string? description = string.IsNullOrWhiteSpace(hoverTip.Description)
                    ? ConnectorMod.StripRichTextTags(power.DumbHoverTip.Description)
                    : ConnectorMod.StripRichTextTags(hoverTip.Description);
                result.Add(new VisibleStatus(
                    power.Id.Entry,
                    ConnectorMod.SafeGetText(() => power.Title),
                    power.DisplayAmount,
                    power.Type.ToString(),
                    description));
            }
            catch
            {
                // An individual transitioning power is omitted; completeness is
                // evaluated by the owning surface rather than fabricating text.
            }
        }
        return result;
    }

    public static IReadOnlyList<VisibleCombatPotionState> BuildPotionStates(
        Player player,
        NativeEntityRegistry entities,
        bool playPhase)
    {
        var result = new List<VisibleCombatPotionState>();
        for (int slot = 0; slot < player.PotionSlots.Count; slot++)
        {
            PotionModel? potion = player.GetPotionAtSlotIndex(slot);
            if (potion == null)
                continue;
            bool automatic = potion.Usage == PotionUsage.Automatic;
            bool canUse = playPhase
                          && !automatic
                          && !potion.IsQueued
                          && !potion.Owner.Creature.IsDead
                          && potion.PassesCustomUsabilityCheck
                          && HasVisiblePotionTarget(potion, player);
            result.Add(new VisibleCombatPotionState(
                entities.GetId(potion, "potion"),
                potion.TargetType.ToString(),
                canUse,
                automatic));
        }
        return result;
    }

    private static bool HasVisiblePotionTarget(PotionModel potion, Player player)
    {
        if (potion.TargetType == TargetType.AnyEnemy)
            return player.Creature.CombatState?.HittableEnemies.Any(potion.IsValidTarget) == true;
        if (potion.TargetType == TargetType.AnyAlly)
            return player.Creature.CombatState?.PlayerCreatures.Any(potion.IsValidTarget) == true;
        Creature? target = potion.TargetType is TargetType.Self or TargetType.AnyPlayer
            ? player.Creature
            : null;
        return potion.IsValidTarget(target);
    }

    private static VisibleCombatPlayer BuildPlayer(
        Player player,
        PlayerCombatState combat,
        NativeEntityRegistry entities)
    {
        bool playPhase = combat.Phase == PlayerTurnPhase.Play
                         && CombatManager.Instance.IsPartOfPlayerTurn(player)
                         && !CombatManager.Instance.PlayerActionsDisabled;
        VisibleOrb[] orbs = combat.OrbQueue?.Orbs.Select((orb, index) => new VisibleOrb(
            entities.GetId(orb, "orb"),
            orb.Id.Entry,
            ConnectorMod.SafeGetText(() => orb.Title),
            BuildOrbDescription(orb),
            orb.PassiveVal,
            orb.EvokeVal,
            index,
            index == 0)).ToArray() ?? Array.Empty<VisibleOrb>();

        return new VisibleCombatPlayer(
            entities.GetId(player.Creature, "player"),
            player.Creature.Block,
            combat.Energy,
            combat.MaxEnergy,
            player.Character.ShouldAlwaysShowStarCounter || combat.Stars > 0 ? combat.Stars : null,
            combat.Hand.Cards.Select(card => BuildCard(card, entities.GetId(card, "card"), includeCombatLegality: true)).ToArray(),
            combat.DrawPile.Cards.Count,
            combat.DiscardPile.Cards.Count,
            combat.ExhaustPile.Cards.Count,
            BuildStatuses(player.Creature),
            BuildCompanions(combat, entities),
            BuildPotionStates(player, entities, playPhase),
            orbs,
            combat.OrbQueue?.Capacity);
    }

    private static string? BuildOrbDescription(OrbModel orb)
    {
        return ConnectorMod.SafeGetText(() =>
        {
            // This mirrors OrbModel.HoverTips. Reading Description or
            // SmartDescription without these variables emits a localization
            // error and can make a coherent combat observation noisy or fail.
            var description = orb.SmartDescription;
            description.Add("energyPrefix", orb.Owner.Character.CardPool.Title);
            description.Add("Passive", orb.PassiveVal);
            description.Add("Evoke", orb.EvokeVal);
            return description;
        })?.Replace("\n", " ");
    }

    private static IReadOnlyList<VisibleCombatCompanion> BuildCompanions(
        PlayerCombatState combat,
        NativeEntityRegistry entities)
    {
        return combat.Pets.Select(companion =>
        {
            MonsterModel model = companion.Monster
                ?? throw new InvalidOperationException("A player combat pet has no monster model.");
            bool healthBarVisible = model.IsHealthBarVisible;
            return new VisibleCombatCompanion(
                entities.GetId(companion, "companion"),
                model.Id.Entry,
                ConnectorMod.SafeGetText(() => model.Title),
                companion.IsAlive,
                healthBarVisible,
                healthBarVisible ? companion.CurrentHp : null,
                healthBarVisible ? companion.MaxHp : null,
                companion.IsAlive ? companion.Block : 0m,
                companion.IsAlive ? BuildStatuses(companion) : Array.Empty<VisibleStatus>());
        }).ToArray();
    }

    private static VisibleEnemy BuildEnemy(Creature creature, NativeEntityRegistry entities)
    {
        var intents = new List<VisibleIntent>();
        if (creature.Monster?.NextMove is MoveState move)
        {
            foreach (var intent in move.Intents)
            {
                string? label = null;
                string? title = null;
                string? description = null;
                try
                {
                    var targets = creature.CombatState?.PlayerCreatures;
                    if (targets != null)
                    {
                        label = ConnectorMod.StripRichTextTags(intent.GetIntentLabel(targets, creature).GetFormattedText());
                        var tip = intent.GetHoverTip(targets, creature);
                        title = tip.Title == null ? null : ConnectorMod.StripRichTextTags(tip.Title);
                        description = tip.Description == null ? null : ConnectorMod.StripRichTextTags(tip.Description);
                    }
                }
                catch
                {
                    // Intent type remains visible even if transient localization fails.
                }
                intents.Add(new VisibleIntent(intent.IntentType.ToString(), label, title, description));
            }
        }

        return new VisibleEnemy(
            entities.GetId(creature, "enemy"),
            creature.CombatId,
            creature.Monster?.Id.Entry ?? "unknown",
            ConnectorMod.SafeGetText(() => creature.Monster?.Title),
            creature.CurrentHp,
            creature.MaxHp,
            creature.Block,
            BuildStatuses(creature),
            intents);
    }

    private static string? ReadNodeText(Node? owner, string path)
    {
        try
        {
            Node? node = owner?.GetNodeOrNull(path);
            if (node == null && owner != null)
            {
                string nodeName = path.StartsWith("%", StringComparison.Ordinal)
                    ? path[1..]
                    : path;
                // The concrete event layout can own this unique-name label
                // below NEventRoom. Prefer already-rendered player-visible
                // text over unbound localization model variables.
                node = owner.FindChild(nodeName, recursive: true, owned: false);
            }
            if (node == null)
                return null;
            Variant text = node.Get("text");
            return text.VariantType == Variant.Type.Nil
                ? null
                : ConnectorMod.StripRichTextTags(text.AsString()).Replace("\n", " ");
        }
        catch
        {
            return null;
        }
    }
}
