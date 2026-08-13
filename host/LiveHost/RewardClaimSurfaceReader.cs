using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

/// <summary>
/// Owns the outer rewards screen only. A card reward claim completes when the
/// UI changes to its separate card-selection surface; it is never flattened
/// into a generic reward index.
/// </summary>
internal sealed class RewardClaimSurfaceReader : ILiveSurfaceReader
{
    private const string SurfaceKind = "reward_claim";

    public string Kind => SurfaceKind;

    public InputOwnerLayer Layer => InputOwnerLayer.Overlay;

    public LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        if (snapshot.TopOverlay is not NRewardsScreen screen)
            return null;
        return Build(screen, entities, game);
    }

    private static LiveObservation Build(
        NRewardsScreen screen,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        NRewardButton[] buttons = ConnectorMod.FindAll<NRewardButton>(screen)
            .Where(button => ConnectorMod.IsNodeVisible(button) && button.Reward != null)
            .OrderBy(button => button.Position.Y)
            .ThenBy(button => button.Position.X)
            .ToArray();
        NLinkedRewardSet[] linkedSets = ConnectorMod.FindAll<NLinkedRewardSet>(screen)
            .Where(ConnectorMod.IsNodeVisible)
            .ToArray();
        NProceedButton? proceedButton = ConnectorMod.FindFirst<NProceedButton>(screen);
        Player? player = RunManager.Instance.DebugOnlyGetState() is { } runState
            ? LocalContext.GetMe(runState)
            : null;

        // Linked reward sets have their own UI protocol. Never omit them and
        // claim that the remaining ordinary buttons form a complete surface.
        if (linkedSets.Length > 0 || proceedButton == null || player == null)
        {
            return BindingUnavailable(
                game,
                linkedSets.Length > 0
                    ? "A visible linked reward set needs its own selection contract."
                    : proceedButton == null
                        ? "The visible rewards screen has no exact proceed-button binding."
                        : "The local player is unavailable while rewards are visible.",
                linkedSets.Length > 0
                    ? new[] { "surface.linked_reward_set", "legal_actions" }
                    : proceedButton == null
                        ? new[] { "surface.proceed_button", "legal_actions" }
                        : new[] { "local_player", "legal_actions" });
        }

        Player exactPlayer = player;
        IReadOnlyList<(int Slot, PotionModel Potion)> occupiedPotions = OccupiedPotions(exactPlayer);
        bool potionSlotsFull = ArePotionSlotsFull(exactPlayer, occupiedPotions.Count);
        bool hasPotionReward = buttons.Any(button => button.Reward is PotionReward);
        VisibleReward[] rewards = buttons.Select(button =>
            BuildReward(button, entities, !IsBlockedPotionReward(button.Reward!, potionSlotsFull))).ToArray();
        VisibleCombatPotion[] discardablePotions = hasPotionReward && potionSlotsFull
            ? occupiedPotions.Select(entry => BuildDiscardablePotion(entry.Slot, entry.Potion, entities)).ToArray()
            : Array.Empty<VisibleCombatPotion>();
        bool hasVisibleControls = buttons.Length > 0 || proceedButton.IsEnabled;
        bool hasCurrentCommand = rewards.Any(reward => reward.Enabled)
                                 || discardablePotions.Length > 0
                                    && exactPlayer.CanUseOrRemovePotions
                                 || proceedButton.IsEnabled;
        string readiness = hasCurrentCommand ? "ready" : hasVisibleControls ? "settling" : "degraded";
        var missing = hasVisibleControls ? Array.Empty<string>() : new[] { "surface.rewards_or_enabled_proceed" };
        var surface = new RewardClaimSurface(
            SurfaceKind,
            entities.GetId(screen, "screen"),
            rewards,
            potionSlotsFull,
            discardablePotions,
            proceedButton.IsEnabled,
            proceedButton.IsSkip);
        var completeness = new StateCompleteness(
            hasVisibleControls ? "contract_complete_for_reward_claim" : "partial",
            hasCurrentCommand
                ? "derived_from_same_current_ui_controls_as_execution"
                : "temporarily_empty_while_ui_settles",
            new[]
            {
                "NRewardsScreen._rewardButtons rendered as NRewardButton",
                "NRewardButton.Reward",
                "NRewardButton.Reward.Description",
                "PotionReward.OnSelect+PotionProcureFailureReason.TooFull",
                "NPotionPopup.OnDiscardButtonPressed+DiscardPotionGameAction",
                "NRewardsScreen.ProceedButton"
            },
            missing);
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            surface
        });
        return new LiveObservation(
            signature,
            readiness,
            new RewardFlowLiveContext("reward_flow", "room_rewards"),
            surface,
            completeness,
            game,
            Array.Empty<string>());
    }

    private static VisibleReward BuildReward(
        NRewardButton button,
        NativeEntityRegistry entities,
        bool claimable)
    {
        Reward reward = button.Reward!;
        string label = ConnectorMod.SafeGetText(() => reward.Description) ?? reward.GetType().Name;
        string description = reward switch
        {
            RelicReward { Relic: { } relic } =>
                ConnectorMod.SafeGetText(() => relic.DynamicDescription) ?? label,
            PotionReward { Potion: { } potion } =>
                ConnectorMod.SafeGetText(() => potion.DynamicDescription) ?? label,
            _ => label
        };
        return new VisibleReward(
            entities.GetId(button, "reward"),
            RewardKind(reward),
            label,
            description,
            button.IsEnabled && claimable);
    }

    private static VisibleCombatPotion BuildDiscardablePotion(
        int slot,
        PotionModel potion,
        NativeEntityRegistry entities) =>
        new(
            entities.GetId(potion, "potion"),
            potion.Id.Entry,
            ConnectorMod.SafeGetText(() => potion.Title),
            ConnectorMod.SafeGetText(() => potion.DynamicDescription),
            slot,
            potion.TargetType.ToString(),
            CanUse: false,
            Automatic: potion.Usage == PotionUsage.Automatic);

    private static IReadOnlyList<(int Slot, PotionModel Potion)> OccupiedPotions(Player player)
    {
        var result = new List<(int, PotionModel)>();
        for (int slot = 0; slot < player.PotionSlots.Count; slot++)
        {
            PotionModel? potion = player.GetPotionAtSlotIndex(slot);
            if (potion != null)
                result.Add((slot, potion));
        }
        return result;
    }

    private static bool IsBlockedPotionReward(Reward reward, bool potionSlotsFull) =>
        reward is PotionReward && potionSlotsFull;

    private static bool ArePotionSlotsFull(Player player, int? occupiedCount = null) =>
        (occupiedCount ?? OccupiedPotions(player).Count) >= player.PotionSlots.Count;

    internal static NativeInputResult StartClaim(
        NativeEntityRegistry entities,
        string expectedScreenId,
        string expectedRewardId)
    {
        if (!entities.TryResolve(expectedScreenId, out NRewardsScreen? screen)
            || screen == null
            || !entities.TryResolve(expectedRewardId, out NRewardButton? button)
            || button?.Reward == null
            || RunManager.Instance.DebugOnlyGetState() is not { } runState
            || LocalContext.GetMe(runState) is not { } player)
        {
            return NativeInputResult.Rejected(
                "reward_binding_changed",
                "The exact rewards screen, reward, or local player is no longer available.");
        }

        return StartClaim(screen, player, button, button.Reward);
    }

    internal static NativeInputResult StartProceed(
        NativeEntityRegistry entities,
        string expectedScreenId)
    {
        if (!entities.TryResolve(expectedScreenId, out NRewardsScreen? screen)
            || screen == null
            || ConnectorMod.FindFirst<NProceedButton>(screen) is not { } proceed)
        {
            return NativeInputResult.Rejected(
                "reward_proceed_binding_changed",
                "The exact rewards screen or proceed control is no longer available.");
        }

        return StartProceed(screen, proceed);
    }

    internal static NativeInputResult StartDiscardPotion(
        NativeEntityRegistry entities,
        string expectedScreenId,
        string expectedPotionId)
    {
        if (!entities.TryResolve(expectedScreenId, out NRewardsScreen? screen)
            || screen == null
            || !entities.TryResolve(expectedPotionId, out PotionModel? potion)
            || potion?.Owner is not Player player)
        {
            return NativeInputResult.Rejected(
                "potion_capacity_binding_changed",
                "The exact rewards screen, potion, or potion owner is no longer available.");
        }

        int slot = Enumerable.Range(0, player.PotionSlots.Count)
            .FirstOrDefault(
                index => ReferenceEquals(player.GetPotionAtSlotIndex(index), potion),
                -1);
        return slot < 0
            ? NativeInputResult.Rejected(
                "potion_slot_changed",
                "The exact potion is no longer in the player's belt.")
            : StartDiscardPotion(screen, player, potion, slot);
    }

    private static NativeInputResult StartClaim(
        NRewardsScreen expectedScreen,
        Player expectedPlayer,
        NRewardButton expectedButton,
        Reward expectedReward)
    {
        if (!IsCurrent(expectedScreen)
            || !ConnectorMod.FindAll<NRewardButton>(expectedScreen).Any(button => ReferenceEquals(button, expectedButton))
            || !ReferenceEquals(expectedButton.Reward, expectedReward)
            || !ConnectorMod.IsNodeVisible(expectedButton)
            || !expectedButton.IsEnabled
            || IsBlockedPotionReward(expectedReward, ArePotionSlotsFull(expectedPlayer)))
        {
            return NativeInputResult.Rejected(
                "reward_claim_changed",
                "The advertised reward is no longer claimable.");
        }

        expectedButton.ForceClick();
        return NativeInputResult.Delivered("native_reward_button_clicked");
    }

    private static NativeInputResult StartProceed(
        NRewardsScreen expectedScreen,
        NProceedButton expectedButton)
    {
        if (!IsCurrent(expectedScreen)
            || ConnectorMod.FindFirst<NProceedButton>(expectedScreen) is not { } currentButton
            || !ReferenceEquals(currentButton, expectedButton)
            || !ConnectorMod.IsNodeVisible(expectedButton)
            || !expectedButton.IsEnabled)
        {
            return NativeInputResult.Rejected(
                "reward_proceed_changed",
                "The advertised rewards proceed control is no longer enabled.");
        }

        expectedButton.ForceClick();
        return NativeInputResult.Delivered("native_rewards_proceed_button_clicked");
    }

    private static NativeInputResult StartDiscardPotion(
        NRewardsScreen expectedScreen,
        Player expectedPlayer,
        PotionModel expectedPotion,
        int expectedSlot)
    {
        if (!IsCurrent(expectedScreen)
            || !expectedPlayer.CanUseOrRemovePotions
            || !ArePotionSlotsFull(expectedPlayer)
            || !ReferenceEquals(expectedPlayer.GetPotionAtSlotIndex(expectedSlot), expectedPotion)
            || !ConnectorMod.FindAll<NRewardButton>(expectedScreen).Any(button =>
                ConnectorMod.IsNodeVisible(button) && button.Reward is PotionReward))
        {
            return NativeInputResult.Rejected(
                "potion_capacity_changed",
                "The advertised potion slot or full-potion reward state changed before execution.");
        }

        var action = new DiscardPotionGameAction(
            expectedPlayer,
            (uint)expectedSlot,
            CombatManager.Instance.IsInProgress);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
        return NativeInputResult.Delivered("native_discard_potion_action_enqueued");
    }

    private static bool IsCurrent(NRewardsScreen screen) =>
        ActiveInputResolver.IsVisibleActiveOverlay(screen)
        && ReferenceEquals(NOverlayStack.Instance?.Peek(), screen);

    // An ordinary terminal reward can expose the map before the old overlay
    // leaves the stack. Map visibility is player-visible completion evidence,
    // unlike a merely disabled Proceed button or a hidden first-time tutorial.
    private static bool IsVisibleMapAfterRewards()
    {
        NMapScreen? map = NMapScreen.Instance;
        return map != null && (map.IsOpen || ConnectorMod.IsNodeVisible(map));
    }

    private static string RewardKind(Reward reward) => reward switch
    {
        GoldReward => "gold",
        PotionReward => "potion",
        RelicReward => "relic",
        CardReward => "card",
        _ => "other_visible_reward"
    };

    private static LiveObservation BindingUnavailable(
        GameBuildIdentity game,
        string reason,
        IReadOnlyList<string> missing)
        => NativeUiFailClosedObservation.BindingUnavailable(
            game,
            new RewardFlowLiveContext("reward_flow"