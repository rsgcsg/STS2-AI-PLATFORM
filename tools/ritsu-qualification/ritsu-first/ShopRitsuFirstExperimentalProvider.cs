using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Platform.NativeFoundation;
using STS2RitsuLib;
using STS2RitsuLib.Patching;

namespace STS2Platform.Qualification.RitsuFirst;

/// <summary>
/// Ritsu-first greenfield implementation of the same Shop contract. It starts
/// from Ritsu lifecycle/private-access facilities and uses direct STS2 model
/// APIs only where Ritsu has no semantic owner or catalog facade.
/// </summary>
public static class ShopRitsuFirstExperimentalProvider
{
    private static readonly AccessTools.FieldRef<NMerchantInventory, bool> InputBlocked =
        PrivateAccess.FieldRef<NMerchantInventory, bool>("_isInputBlocked");

    public static ExperimentalShopDecision ProjectForConformance(
        ExperimentalShopProjection projection) =>
        ExperimentalShopContract.Project(projection);

    public static ExperimentalShopDecision Capture(
        NMerchantRoom screen,
        INativeReferentIdentity identities)
    {
        try
        {
            if (!ReferenceEquals(NMerchantRoom.Instance, screen)
                || RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not MerchantRoom room
                || !ReferenceEquals(room, screen.Room)
                || screen.Inventory?.Inventory is not MerchantInventory inventory)
            {
                return ExperimentalShopContract.Unavailable(
                    "owner_not_current",
                    "The exact Merchant room/inventory owner is not current.");
            }

            bool inputBlocked = InputBlocked(screen.Inventory);
            string ownerId = identities.GetId(room, "merchant_room");
            ExperimentalShopStage stage = inputBlocked
                ? ExperimentalShopStage.Resolving
                : screen.Inventory.IsOpen
                    ? ExperimentalShopStage.Inventory
                    : ExperimentalShopStage.Room;
            return ExperimentalShopContract.Project(new ExperimentalShopProjection(
                ownerId,
                room,
                screen.Inventory,
                stage,
                inventory.Player.Gold,
                CaptureEntries(inventory, identities),
                new[]
                {
                    "Ritsu ItemPurchasedEvent lifecycle",
                    "Ritsu PrivateAccess typed input-blocked field",
                    "NMerchantRoom.Instance+Room+Inventory exact owner escape hatch",
                    "MerchantInventory.AllEntries exact semantic escape hatch",
                    "MerchantEntry.IsStocked+Cost+EnoughGold exact semantic escape hatch",
                    "Player.HasOpenPotionSlots+Hook.ShouldProcurePotion exact semantic escape hatch"
                }));
        }
        catch (Exception exception)
        {
            return ExperimentalShopContract.Unavailable(
                "capture_failed",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static IReadOnlyList<ExperimentalShopEntry> CaptureEntries(
        MerchantInventory inventory,
        INativeReferentIdentity identities) =>
        inventory.AllEntries
            .Select(entry => CaptureEntry(inventory, entry, identities))
            .OrderBy(entry => entry.ReferentId, StringComparer.Ordinal)
            .ToArray();

    private static ExperimentalShopEntry CaptureEntry(
        MerchantInventory inventory,
        MerchantEntry entry,
        INativeReferentIdentity identities)
    {
        (ExperimentalShopEntryKind kind, string itemId, bool capacityAllows) =
            DescribeEntry(inventory, entry);
        return new ExperimentalShopEntry(
            identities.GetId(entry, "merchant_entry"),
            itemId,
            kind,
            entry,
            entry.Cost,
            entry.IsStocked,
            entry.EnoughGold,
            capacityAllows,
            kind == ExperimentalShopEntryKind.Potion
                ? "stock+gold+open potion slot+Hook.ShouldProcurePotion"
                : "MerchantEntry stock+gold and type-specific native wrapper");
    }

    private static (ExperimentalShopEntryKind Kind, string ItemId, bool CapacityAllows)
        DescribeEntry(MerchantInventory inventory, MerchantEntry entry) =>
        entry switch
        {
            MerchantCardEntry card => (
                ExperimentalShopEntryKind.Card,
                card.CreationResult?.Card.Id.ToString() ?? "sold",
                true),
            MerchantRelicEntry relic => (
                ExperimentalShopEntryKind.Relic,
                relic.Model?.Id.ToString() ?? "sold",
                true),
            MerchantPotionEntry potion => (
                ExperimentalShopEntryKind.Potion,
                potion.Model?.Id.ToString() ?? "sold",
                PotionCapacityAllows(inventory, potion)),
            MerchantCardRemovalEntry => (
                ExperimentalShopEntryKind.CardRemoval,
                "card_removal",
                true),
            _ => throw new NotSupportedException(
                $"Unknown MerchantEntry type {entry.GetType().FullName}.")
        };

    private static bool PotionCapacityAllows(
        MerchantInventory inventory,
        MerchantPotionEntry entry) =>
        entry.Model != null
        && inventory.Player.HasOpenPotionSlots
        && Hook.ShouldProcurePotion(
            inventory.Player.RunState,
            inventory.Player.Creature.CombatState,
            entry.Model,
            inventory.Player);
}

/// <summary>
/// Ritsu centralizes the successful purchase hook/task bridge. Its public
/// event does not expose failed attempts or the UI input root.
/// </summary>
public sealed class ShopRitsuFirstLifecycleProbe : IDisposable
{
    private readonly Action<ExperimentalLifecycleObservation> _observer;
    private readonly IDisposable _subscription;

    public ShopRitsuFirstLifecycleProbe(Action<ExperimentalLifecycleObservation> observer)
    {
        _observer = observer;
        _subscription = RitsuLibFramework.SubscribeLifecycle<ItemPurchasedEvent>(
            OnPurchased,
            replayCurrentState: false);
    }

    public void Dispose() => _subscription.Dispose();

    private void OnPurchased(ItemPurchasedEvent value) =>
        _observer(new ExperimentalLifecycleObservation(
            "shop_purchase_completed",
            value.ItemPurchased,
            IsCommit: true,
            HasExactRootAction: false,
            HasCancelOrAbortDisposition: false,
            "Ritsu ItemPurchasedEvent"));
}
