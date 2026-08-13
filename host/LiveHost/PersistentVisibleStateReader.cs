using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

internal sealed record PersistentVisibleStateBuildResult(
    bool RunActive,
    PersistentVisibleState? State,
    HostDiagnostic? Failure);

/// <summary>
/// Projects strategy-relevant facts rendered by the persistent single-player
/// run HUD. It does not inspect optional overlays and cannot publish actions.
/// </summary>
internal static class PersistentVisibleStateReader
{
    public static PersistentVisibleStateBuildResult Build(NativeEntityRegistry entities)
    {
        bool runActive;
        try
        {
            runActive = RunManager.Instance.IsInProgress;
        }
        catch (Exception ex)
        {
            return Failed(true, "run_activity_read_failed", ex);
        }

        if (!runActive)
            return new PersistentVisibleStateBuildResult(false, null, null);
        if (ConnectorMod.IsMultiplayerRun())
            return Failed(true, "multiplayer_shared_state_not_implemented", new NotSupportedException());

        try
        {
            RunState run = RunManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Run state is unavailable while a run is active.");
            Player player = LocalContext.GetMe(run)
                ?? throw new InvalidOperationException("Local player is unavailable while a run is active.");

            VisibleBoss[] bosses = new[] { run.Act.BossEncounter, run.Act.SecondBossEncounter }
                .Where(boss => boss != null)
                .Select((boss, index) => new VisibleBoss(
                    boss!.Id.Entry,
                    ConnectorMod.SafeGetText(() => boss.Title),
                    index))
                .ToArray();
            VisibleRunModifier[] modifiers = run.Modifiers.Select(modifier =>
            {
                string ownerEntityId = entities.GetId(modifier, "run_modifier");
                VisibleEntityFacts.HoverFacts hover =
                    VisibleEntityFacts.BuildHoverFacts(modifier.HoverTips, ownerEntityId);
                return new VisibleRunModifier(
                    modifier.Id.Entry,
                    ConnectorMod.SafeGetText(() => modifier.Title),
                    ConnectorMod.SafeGetText(() => modifier.Description),
                    hover.Keywords,
                    hover.CardPreviews);
            }).ToArray();
            VisibleRelic[] relics = player.Relics
                .Select(relic => VisibleEntityFacts.BuildRelic(relic, entities))
                .ToArray();
            var potions = new List<VisibleOwnedPotion>();
            for (int slot = 0; slot < player.PotionSlots.Count; slot++)
            {
                PotionModel? potion = player.GetPotionAtSlotIndex(slot);
                if (potion != null)
                    potions.Add(VisibleEntityFacts.BuildOwnedPotion(potion, slot, entities));
            }

            var state = new PersistentVisibleState(
                "active_single_player_run",
                new VisibleRunHud(
                    run.CurrentActIndex + 1,
                    run.Act.Id.Entry,
                    ConnectorMod.SafeGetText(() => run.Act.Title),
                    run.TotalFloor,
                    run.AscensionLevel,
                    bosses,
                    modifiers),
                new VisiblePlayerHud(
                    entities.GetId(player.Creature, "player"),
                    player.Character.Id.Entry,
                    ConnectorMod.SafeGetText(() => player.Character.Title),
                    player.Creature.CurrentHp,
                    player.Creature.MaxHp,
                    player.Gold,
                    relics,
                    potions,
                    player.MaxPotionCount),
                new PersistentStateCompleteness(
                    "complete_for_strategy_relevant_persistent_single_player_hud",
                    new[]
                    {
                        "RunState.CurrentActIndex+Act+TotalFloor+AscensionLevel+Modifiers",
                        "NTopBar+NTopBarBossIcon+NTopBarFloorIcon+NTopBarHp+NTopBarGold",
                        "NRelicInventory+NPotionContainer+LocalContext.GetMe"
                    },
                    Array.Empty<string>()));
            return new PersistentVisibleStateBuildResult(true, state, null);
        }
        catch (Exception ex)
        {
            return Failed(true, "shared_visible_state_build_failed", ex);
        }
    }

    private static PersistentVisibleStateBuildResult Failed(
        bool runActive,
        string code,
        Exception exception) => new(
            runActive,
            null,
            HostDiagnostics.Create(
                $"host.shared_state.{code}",
                "error",
                "