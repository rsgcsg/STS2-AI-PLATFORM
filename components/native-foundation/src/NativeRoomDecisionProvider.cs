using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// Projects the ordinary non-combat room decisions that are already owned by
/// STS2.  This is deliberately a read-only semantic catalog: Connector still
/// validates and delivers public actions and Annotator only witnesses them.
/// </summary>
public static class NativeRoomDecisionProvider
{
    public static NativeRoomDecision Capture(INativeReferentIdentity identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        try
        {
            RunState? run = RunManager.Instance.DebugOnlyGetState();
            if (run?.CurrentRoom is EventRoom eventRoom)
                return CaptureEvent(eventRoom, identities);
            if (run?.CurrentRoom is MerchantRoom merchantRoom)
                return CaptureShop(merchantRoom, identities);
            if (run?.CurrentRoom is RestSiteRoom restSiteRoom)
                return CaptureRest(restSiteRoom, identities);
            return Unavailable("no_supported_room", "No supported non-combat room is current.");
        }
        catch (Exception exception)
        {
            return Unavailable(
                "capture_failed",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static NativeRoomDecision CaptureEvent(
        EventRoom room,
        INativeReferentIdentity identities)
    {
        var actions = room.LocalMutableEvent.CurrentOptions
            .Where(option => !option.IsLocked)
            .Select(option =>
            {
                string verb = option.IsProceed ? "proceed_event" : "choose_event_option";
                string id = identities.GetId(option, "event_option");
                return new NativeSemanticAction(
                    NativeSemanticActionCatalog.BuildKey(verb, id),
                    verb,
                    id,
                    option,
                    Array.Empty<NativeSemanticOperand>(),
                    "EventModel.CurrentOptions+EventOption.IsLocked");
            })
            .OrderBy(action => action.Key, StringComparer.Ordinal)
            .ToArray();
        return Result(
            "event_option",
            actions,
            "EventRoom.LocalMutableEvent.CurrentOptions+EventOption.Chosen");
    }

    private static NativeRoomDecision CaptureRest(
        RestSiteRoom room,
        INativeReferentIdentity identities)
    {
        var actions = room.Options
            .Where(option => option.IsEnabled)
            .Select(option =>
            {
                string id = identities.GetId(option, "rest_option");
                return new NativeSemanticAction(
                    NativeSemanticActionCatalog.BuildKey("choose_rest_option", id),
                    "choose_rest_option",
                    id,
                    option,
                    Array.Empty<NativeSemanticOperand>(),
                    "RestSiteRoom.Options+RestSiteOption.IsEnabled");
            })
            .ToList();
        // A rest option remains in the native collection after selection but
        // becomes disabled while the room exposes its proceed control.  Use
        // the same enabled-option projection as the live UI rather than
        // assuming the collection is physically empty.
        if (actions.Count == 0 && NRestSiteRoom.Instance is { } uiRoom
            && uiRoom.ProceedButton.IsEnabled)
        {
            actions.Add(new NativeSemanticAction(
                NativeSemanticActionCatalog.BuildKey("proceed_rest_site", null),
                "proceed_rest_site",
                null,
                room,
                Array.Empty<NativeSemanticOperand>(),
                "NRestSiteRoom.ProceedButton+RestSiteRoom.Options"));
        }
        return Result(
            "rest_site",
            actions.OrderBy(action => action.Key, StringComparer.Ordinal).ToArray(),
            "RestSiteSynchronizer.GetLocalOptions+RestSiteOption.OnSelect");
    }

    private static NativeRoomDecision CaptureShop(
        MerchantRoom room,
        INativeReferentIdentity identities)
    {
        NMerchantRoom? uiRoom = NMerchantRoom.Instance;
        if (uiRoom?.Inventory is { IsOpen: false }
            && uiRoom.MerchantButton.IsEnabled)
        {
            var roomActions = new List<NativeSemanticAction>
            {
                new(
                    NativeSemanticActionCatalog.BuildKey(
                        "open_shop_inventory",
                        identities.GetId(uiRoom, "room")),
                    "open_shop_inventory",
                    identities.GetId(uiRoom, "room"),
                    uiRoom,
                    Array.Empty<NativeSemanticOperand>(),
                    "NMerchantRoom.MerchantButton+NMerchantRoom.OpenInventory")
            };
            if (uiRoom.ProceedButton.IsEnabled)
            {
                roomActions.Add(new NativeSemanticAction(
                    NativeSemanticActionCatalog.BuildKey(
                        "proceed_shop",
                        identities.GetId(uiRoom, "room")),
                    "proceed_shop",
                    identities.GetId(uiRoom, "room"),
                    uiRoom,
                    Array.Empty<NativeSemanticOperand>(),
                    "NMerchantRoom.ProceedButton+NMapScreen.Open"));
            }
            return Result(
                "shop_room",
                roomActions,
                "NMerchantRoom.MerchantButton+ProceedButton");
        }

        MerchantInventory inventory = room.GetLocalInventory();
        NativeSemanticAction[] actions = inventory.AllEntries
            .Where(entry => entry.IsStocked && entry.EnoughGold)
            .Select(entry =>
            {
                string operation = entry switch
                {
                    MerchantCardEntry => "purchase_shop_card",
                    MerchantRelicEntry => "purchase_shop_relic",
                    MerchantPotionEntry => "purchase_shop_potion",
                    MerchantCardRemovalEntry => "open_shop_card_removal",
                    _ => "purchase_shop_entry"
                };
                string id = identities.GetId(entry, "shop_offer");
                return new NativeSemanticAction(
                    NativeSemanticActionCatalog.BuildKey(operation, id),
                    operation,
                    id,
                    entry,
                    Array.Empty<NativeSemanticOperand>(),
                    "MerchantInventory.AllEntries+MerchantEntry.IsStocked+EnoughGold");
            })
            .Where(action => action.Verb != "purchase_shop_entry")
            .OrderBy(action => action.Key, StringComparer.Ordinal)
            .ToArray();
        if (uiRoom?.Inventory is { IsOpen: true } inventoryUi
            && inventoryUi.IsOpen
            && inventoryUi.GetAllSlots() is not null)
        {
            var inventoryActions = actions.ToList();
            inventoryActions.Add(new NativeSemanticAction(
                NativeSemanticActionCatalog.BuildKey(
                    "close_shop_inventory",
                    identities.GetId(inventoryUi, "screen")),
                "close_shop_inventory",
                identities.GetId(inventoryUi, "screen"),
                inventoryUi,
                Array.Empty<NativeSemanticOperand>(),
                "NMerchantInventory.BackButton+NMerchantInventory.Close"));
            actions = inventoryActions
                .OrderBy(action => action.Key, StringComparer.Ordinal)
                .ToArray();
        }
        return Result(
            "shop_inventory",
            actions,
            "MerchantRoom.GetLocalInventory+MerchantEntry.OnTryPurchaseWrapper");
    }

    private static NativeRoomDecision Result(
        string interactionKind,
        IReadOnlyList<NativeSemanticAction> actions,
        string evidence) =>
        new(
            "captured",
            interactionKind,
            interactionKind,
            actions.Count > 0,
            actions,
            new[] { evidence },
            actions.Count == 0 ? "No native decision is currently open." : null);

    private static NativeRoomDecision Unavailable(string status, string detail) =>
        new(
            status,
            "unavailable",
            "unavailable",
            false,
            Array.Empty<NativeSemanticAction>(),
            Array.Empty<string>(),
            detail);
}
