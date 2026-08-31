using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Platform.NativeFoundation;

namespace STS2Platform.Qualification.RitsuFirst;

/// <summary>
/// Clean Direct greenfield implementation of the experimental Shop contract.
/// Public STS2 domain state is preferred; one private input-blocked flag is
/// read fail-closed because it distinguishes a decision from purchase resolve.
/// </summary>
public static class ShopDirectExperimentalProvider
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? InputBlockedField =
        typeof(NMerchantInventory).GetField("_isInputBlocked", PrivateInstance);

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

            if (InputBlockedField?.GetValue(screen.Inventory) is not bool inputBlocked)
            {
                return ExperimentalShopContract.Unavailable(
                    "lifecycle_binding_unavailable",
                    "NMerchantInventory._isInputBlocked was unavailable.");
            }

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
                    "NMerchantRoom.Instance+Room+Inventory exact owner",
                    "MerchantInventory.AllEntries",
                    "MerchantEntry.IsStocked+Cost+EnoughGold",
                    "Player.HasOpenPotionSlots+Hook.ShouldProcurePotion",
                    "NMerchantInventory._isInputBlocked"
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
/// Direct public MerchantEntry events expose both successful commit and failed
/// attempts without a Harmony patch.
/// </summary>
public sealed class ShopDirectLifecycleProbe : IDisposable
{
    private readonly Action<ExperimentalLifecycleObservation> _observer;
    private readonly MerchantEntry[] _entries;

    public ShopDirectLifecycleProbe(
        MerchantInventory inventory,
        Action<ExperimentalLifecycleObservation> observer)
    {
        _observer = observer;
        _entries = inventory.AllEntries.ToArray();
        foreach (MerchantEntry entry in _entries)
        {
            entry.PurchaseCompleted += OnCompleted;
            entry.PurchaseFailed += OnFailed;
        }
    }

    public void Dispose()
    {
        foreach (MerchantEntry entry in _entries)
        {
            entry.PurchaseCompleted -= OnCompleted;
            entry.PurchaseFailed -= OnFailed;
        }
    }

    private void OnCompleted(PurchaseStatus _, MerchantEntry entry) =>
        _observer(new ExperimentalLifecycleObservation(
            "shop_purchase_completed",
            entry,
            IsCommit: true,
            HasExactRootAction: false,
            HasCancelOrAbortDisposition: false,
            "MerchantEntry.PurchaseCompleted"));

    private void OnFailed(PurchaseStatus status) =>
        _observer(new ExperimentalLifecycleObservation(
            $"shop_purchase_failed:{status}",
            status,
            IsCommit: false,
            HasExactRootAction: false,
            HasCancelOrAbortDisposition: true,
            "MerchantEntry.PurchaseFailed"));
}
