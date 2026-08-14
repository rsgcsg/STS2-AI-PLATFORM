using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

/// <summary>
/// Exact-build single-player treasure-room adapter. The four UI stages remain
/// distinct because they expose different native controls and current owners.
/// </summary>
internal sealed class TreasureRoomSurfaceReader : ILiveSurfaceReader
{
    private const string SurfaceKind = "treasure_room";
    internal const string OpenChestDeliveryEvidence = "native_treasure_chest_clicked";
    internal const string ChooseRelicDeliveryEvidence = "native_treasure_relic_holder_clicked";
    internal const string SkipRelicDeliveryEvidence = "native_treasure_skip_button_clicked";
    internal const string ProceedDeliveryEvidence = "native_treasure_proceed_button_clicked";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? CollectionOpenField =
        typeof(NTreasureRoom).GetField("_isRelicCollectionOpen", Flags);
    private static readonly FieldInfo? ChestOpenedField =
        typeof(NTreasureRoom).GetField("_hasChestBeenOpened", Flags);

    public string Kind => SurfaceKind;

    public InputOwnerLayer Layer => InputOwnerLayer.Room;

    public LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState?.CurrentRoom is not TreasureRoom room)
            return null;

        string step = "scene_root";
        try
        {
        step = "screen_authority";
        NTreasureRoom? uiRoom = NRun.Instance?.TreasureRoom;
        if (uiRoom == null || !ConnectorMod.IsLiveNode(uiRoom))
            return null;
        if (ClassifyScreenHandoff(
                RunManager.Instance.IsInProgress,
                currentRoomIsTreasure: true,
                uiRoomIsLive: true,
                ActiveScreenContext.Instance.IsCurrent(uiRoom)))
        {
            return ScreenHandoff(game, runState);
        }

        step = "lifecycle_flags";
        if (!TryReadBool(ChestOpenedField, uiRoom, out bool chestOpened)
            || !TryReadBool(CollectionOpenField, uiRoom, out bool collectionOpen))
        {
            return BindingUnavailable(game, "Exact treasure lifecycle flags are unavailable.");
        }

        step = "exact_controls";
        NButton? chest = uiRoom.GetNodeOrNull<NButton>("%Chest");
        NTreasureRoomRelicCollection? collection =
            uiRoom.GetNodeOrNull<NTreasureRoomRelicCollection>("%RelicCollection");
        NProceedButton proceed = uiRoom.ProceedButton;
        Player? player = LocalContext.GetMe(runState);
        if (chest == null || collection == null || proceed == null || player == null)
            return BindingUnavailable(game, "Treasure controls, relic collection, or local player are unavailable.");

        step = "relic_collection";
        RelicModel[] currentRelics =
            RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics?.ToArray()
            ?? Array.Empty<RelicModel>();
        NTreasureRoomRelicHolder? holder = collection.SingleplayerRelicHolder;
        bool holderMatches = TreasureVisibilityFacts.CanReadSingleplayerRelic(
                                 collectionOpen,
                                 currentRelics.Length)
                             && holder != null
                             && TryReadHolderRelic(holder, out RelicModel? holderRelic)
                             && ReferenceEquals(holderRelic, currentRelics[0]);
        bool holderVisible = holderMatches
                             && ConnectorMod.IsLiveNode(holder!)
                             && ConnectorMod.IsNodeVisible(holder!)
                             && collectionOpen;
        bool holderActionable = holderVisible
                                && holder!.IsEnabled
                                && holder.MouseFilter != Control.MouseFilterEnum.Ignore;

        step = "surface_projection";
        string stage = TreasureLifecycleFacts.Stage(
            chestOpened,
            collectionOpen,
            currentRelics.Length,
            chest.IsEnabled && ConnectorMod.IsNodeVisible(chest));
        VisibleTreasureRelic[] visibleRelics = holderVisible
            ? new[] { BuildRelic(currentRelics[0], entities) }
            : Array.Empty<VisibleTreasureRelic>();
        bool canSkip = stage == "relic_choice"
                       && proceed.IsSkip
                       && proceed.IsEnabled
                       && ConnectorMod.IsNodeVisible(proceed);
        bool canProceed = stage == "completed"
                          && !proceed.IsSkip
                          && proceed.IsEnabled
                          && ConnectorMod.IsNodeVisible(proceed);

        string roomId = entities.GetId(uiRoom, "treasure_room");
        bool canOpenChest = stage == "closed"
                            && chest.IsEnabled
                            && ConnectorMod.IsNodeVisible(chest)
                            && chest.MouseFilter != Control.MouseFilterEnum.Ignore;

        var surface = new TreasureRoomSurface(
            SurfaceKind,
            stage,
            roomId,
            chestOpened,
            visibleRelics,
            stage == "relic_choice" && holderActionable,
            canSkip,
            canProceed);
        bool hasActionableControl = canOpenChest
                                    || stage == "relic_choice" && holderActionable
                                    || canSkip
                                    || canProceed;
        string readiness = hasActionableControl ? "ready" : "settling";
        var completeness = new StateCompleteness(
            "contract_complete_for_single_player_treasure_room_lifecycle",
            hasActionableControl
                ? "derived_from_same_exact_current_controls_as_execution"
                : "temporarily_empty_while_chest_or_relic_award_animation_settles",
            new[]
            {
                "TreasureRoom+NTreasureRoom exact room ownership",
                "NTreasureRoom._hasChestBeenOpened+_isRelicCollectionOpen exact-version bindings",
                "NTreasureRoomRelicCollection.CurrentRelics+SingleplayerRelicHolder",
                "RelicModel visible title+description+rarity+hover keywords",
                "NProceedButton.IsSkip+IsEnabled"
            },
            Array.Empty<string>());
        step = "state_signature";
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            context = new TreasureLiveContext("treasure"),
            surface
        });
        return new LiveObservation(
            signature,
            readiness,
            new TreasureLiveContext("treasure"),
            surface,
            completeness,
            game,
            Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return BindingUnavailable(
                game,
                $"Treasure projection failed at {step}: {ex.GetType().Name}.");
        }
    }

    internal static NativeInputResult StartOpen(
        NativeEntityRegistry entities,
        string expectedRoomId)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState?.CurrentRoom is not TreasureRoom room
            || NRun.Instance?.TreasureRoom is not { } uiRoom
            || !string.Equals(
                entities.GetId(uiRoom, "treasure_room"),
                expectedRoomId,
                StringComparison.Ordinal)
            || uiRoom.GetNodeOrNull<NButton>("%Chest") is not { } chest)
        {
            return NativeInputResult.Rejected(
                "treasure_chest_changed",
                "The exact treasure room or chest control is no longer current.");
        }
        return StartOpen(room, uiRoom, chest);
    }

    internal static NativeInputResult StartChoose(
        NativeEntityRegistry entities,
        string expectedRoomId,
        string expectedRelicId)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        RelicModel[] currentRelics =
            RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics?.ToArray()
            ?? Array.Empty<RelicModel>();
        if (runState?.CurrentRoom is not TreasureRoom room
            || LocalContext.GetMe(runState) == null
            || NRun.Instance?.TreasureRoom is not { } uiRoom
            || !string.Equals(
                entities.GetId(uiRoom, "treasure_room"),
                expectedRoomId,
                StringComparison.Ordinal)
            || !entities.TryResolve(expectedRelicId, out RelicModel? relic)
            || relic == null
            || currentRelics.Length != 1
            || !ReferenceEquals(currentRelics[0], relic)
            || uiRoom.GetNodeOrNull<NTreasureRoomRelicCollection>("%RelicCollection")
                is not { } collection
            || collection.SingleplayerRelicHolder is not { } holder)
        {
            return NativeInputResult.Rejected(
                "treasure_relic_changed",
                "The exact treasure room or relic entity is no longer current.");
        }
        return StartChoose(room, uiRoom, collection, holder, relic);
    }

    internal static NativeInputResult StartSkip(
        NativeEntityRegistry entities,
        string expectedRoomId)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState?.CurrentRoom is not TreasureRoom room
            || LocalContext.GetMe(runState) == null
            || NRun.Instance?.TreasureRoom is not { } uiRoom
            || !string.Equals(
                entities.GetId(uiRoom, "treasure_room"),
                expectedRoomId,
                StringComparison.Ordinal)
            || uiRoom.GetNodeOrNull<NTreasureRoomRelicCollection>("%RelicCollection")
                is not { } collection)
        {
            return NativeInputResult.Rejected(
                "treasure_skip_changed",
                "The exact treasure room or skip owner is no longer current.");
        }
        return StartSkip(room, uiRoom, collection, uiRoom.ProceedButton);
    }

    internal static NativeInputResult StartProceed(
        NativeEntityRegistry entities,
        string expectedRoomId)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not TreasureRoom room
            || NRun.Instance?.TreasureRoom is not { } uiRoom
            || !string.Equals(
                entities.GetId(uiRoom, "treasure_room"),
                expectedRoomId,
                StringComparison.Ordinal))
        {
            return NativeInputResult.Rejected(
                "treasure_proceed_changed",
                "The exact treasure room or proceed owner is no longer current.");
        }
        return StartProceed(room, uiRoom, uiRoom.ProceedButton);
    }

    private static VisibleTreasureRelic BuildRelic(
        RelicModel relic,
        NativeEntityRegistry entities) =>
        BuildVisibleRelic(relic, entities);

    private static VisibleTreasureRelic BuildVisibleRelic(
        RelicModel relic,
        NativeEntityRegistry entities)
    {
        string entityId = entities.GetId(relic, "treasure_relic");
        VisibleEntityFacts.HoverFacts hover =
            VisibleEntityFacts.BuildHoverFacts(relic.HoverTipsExcludingRelic, entityId);
        return new VisibleTreasureRelic(
            entityId,
            relic.Id.Entry,
            ConnectorMod.SafeGetText(() => relic.Title),
            ConnectorMod.SafeGetText(() => relic.DynamicDescription),
            relic.Rarity.ToString(),
            hover.Keywords,
            hover.CardPreviews);
    }

    private static NativeInputResult StartOpen(
        TreasureRoom expectedRoom,
        NTreasureRoom expectedUi,
        NButton expectedChest)
    {
        if (!IsCurrent(expectedRoom, expectedUi)
            || !TryReadBool(ChestOpenedField, expectedUi, out bool opened)
            || opened
            || !ReferenceEquals(expectedUi.GetNodeOrNull<NButton>("%Chest"), expectedChest)
            || !expectedChest.IsEnabled
            || !ConnectorMod.IsNodeVisible(expectedChest)
            || expectedChest.MouseFilter == Control.MouseFilterEnum.Ignore)
        {
            return NativeInputResult.Rejected(
                "treasure_chest_changed",
                "The advertised unopened treasure chest is no longer current and clickable.");
        }

        expectedChest.ForceClick();
        return NativeInputResult.Delivered(OpenChestDeliveryEvidence);
    }

    private static NativeInputResult StartChoose(
        TreasureRoom expectedRoom,
        NTreasureRoom expectedUi,
        NTreasureRoomRelicCollection expectedCollection,
        NTreasureRoomRelicHolder expectedHolder,
        RelicModel expectedRelic)
    {
        RelicModel[] current =
            RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics?.ToArray()
            ?? Array.Empty<RelicModel>();
        if (!IsCurrent(expectedRoom, expectedUi)
            || !TryReadBool(CollectionOpenField, expectedUi, out bool collectionOpen)
            || !collectionOpen
            || !ReferenceEquals(expectedUi.GetNodeOrNull<NTreasureRoomRelicCollection>("%RelicCollection"), expectedCollection)
            || !ReferenceEquals(expectedCollection.SingleplayerRelicHolder, expectedHolder)
            || current.Length != 1
            || !ReferenceEquals(current[0], expectedRelic)
            || !TryReadHolderRelic(expectedHolder, out RelicModel? holderRelic)
            || !ReferenceEquals(holderRelic, expectedRelic)
            || !expectedHolder.IsEnabled
            || !ConnectorMod.IsNodeVisible(expectedHolder)
            || expectedHolder.MouseFilter == Control.MouseFilterEnum.Ignore)
        {
            return NativeInputResult.Rejected(
                "treasure_relic_changed",
                "The advertised treasure relic is no longer the current selectable offer.");
        }

        expectedHolder.ForceClick();
        return NativeInputResult.Delivered(ChooseRelicDeliveryEvidence);
    }

    private static NativeInputResult StartSkip(
        TreasureRoom expectedRoom,
        NTreasureRoom expectedUi,
        NTreasureRoomRelicCollection expectedCollection,
        NProceedButton expectedProceed)
    {
        if (!IsCurrent(expectedRoom, expectedUi)
            || !TryReadBool(CollectionOpenField, expectedUi, out bool collectionOpen)
            || !collectionOpen
            || !ReferenceEquals(expectedUi.GetNodeOrNull<NTreasureRoomRelicCollection>("%RelicCollection"), expectedCollection)
            || !ReferenceEquals(expectedUi.ProceedButton, expectedProceed)
            || !expectedProceed.IsSkip
            || !expectedProceed.IsEnabled
            || !ConnectorMod.IsNodeVisible(expectedProceed))
        {
            return NativeInputResult.Rejected(
                "treasure_skip_changed",
                "The advertised treasure skip control is no longer current and enabled.");
        }

        expectedProceed.ForceClick();
        return NativeInputResult.Delivered(SkipRelicDeliveryEvidence);
    }

    private static NativeInputResult StartProceed(
        TreasureRoom expectedRoom,
        NTreasureRoom expectedUi,
        NProceedButton expectedProceed)
    {
        if (!IsCurrent(expectedRoom, expectedUi)
            || !ReferenceEquals(expectedUi.ProceedButton, expectedProceed)
            || expectedProceed.IsSkip
            || !expectedProceed.IsEnabled
            || !ConnectorMod.IsNodeVisible(expectedProceed))
        {
            return NativeInputResult.Rejected(
                "treasure_proceed_changed",
                "The advertised treasure proceed control is no longer current and enabled.");
        }

        expectedProceed.ForceClick();
        return NativeInputResult.Delivered(ProceedDeliveryEvidence);
    }

    private static bool IsCurrent(TreasureRoom expectedRoom, NTreasureRoom expectedUi) =>
        ReferenceEquals(RunManager.Instance.DebugOnlyGetState()?.CurrentRoom, expectedRoom)
        && ConnectorMod.IsLiveNode(expectedUi)
        && ActiveScreenContext.Instance.IsCurrent(expectedUi);

    private static bool TryReadBool(FieldInfo? field, object instance, out bool value)
    {
        value = false;
        if (field?.GetValue(instance) is not bool current)
            return false;
        value = current;
        return true;
    }

    private static bool TryReadHolderRelic(
        NTreasureRoomRelicHolder holder,
        out RelicModel? relic)
    {
        relic = null;
        try
        {
            relic = holder.Relic?.Model;
            return relic != null;
        }
        catch (InvalidOperationException)
        {
            // The synchronizer generates relics when the room is entered, but
            // the player-visible holder is not initialized until the chest is
            // opened. Treat that interval as non-visible, never as evidence.
            return false;
        }
    }

    private static LiveObservation BindingUnavailable(GameBuildIdentity game, string reason)
    {
        var context = new TreasureLiveContext("treasure");
        var surface = new UnsupportedSurface("unsupported", SurfaceKind, reason);
        var completeness = new StateCompleteness(
            "partial",
            "empty_fail_closed",
            new[] { "TreasureRoom+NTreasureRoom exact-version binding" },
            new[] { "treasure_stage", "visible_relics", "legal_actions" });
        string signature = StableIdentityHash.Object(new { game.Version, reason });
        return new LiveObservation(
            signature,
            "degraded",
            context,
            surface,
            completeness,
            game,
            new[] { "treasure_room_binding_unavailable" })
        {
            Diagnostics = new[]
            {
                HostDiagnostics.Create(
                    "host.surface.treasure_room.binding_unavailable",
                    "error",
                    "surface",
                    "actions_suppressed",
                    "update_host_adapter",
                    reason)
            }
        };
    }

    private static LiveObservation ScreenHandoff(GameBuildIdentity game, RunState runState)
    {
        var context = new TreasureLiveContext("treasure");
        var surface = new NoActionSurface(
            "no_action",
            "settling",
            "The treasure room node is still live while native screen ownership is handing off; no player input owner is current.");
        var completeness = new StateCompleteness(
            "complete_for_bounded_treasure_screen_handoff",
            "none_no_input_owner",
            new[]
            {
                "TreasureRoom exact current room",
                "NTreasureRoom live node",
                "ActiveScreenContext current-owner check"
            },
            Array.Empty<string>());
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            game.Commit,
            context,
            surface,
            runState.CurrentActIndex,
            runState.TotalFloor
        });
        return new LiveObservation(
            signature,
            "settling",
            context,
            surface,
            completeness,
            game,
            Array.Empty<string>())
        {
            InputOwnership = new InputOwnership(
                "none_fail_closed",
                null,
                "The treasure room exists but does not own the current native screen context; the Host polls without publishing actions."),
            Diagnostics = new[]
            {
                HostDiagnostics.Create(
                    "host.lifecycle.treasure_screen_handoff",
                    "info",
                    "runtime",
                    "none",
                    "settle")
            }
        };
    }

    internal static bool ClassifyScreenHandoff(
        bool runInProgress,
        bool currentRoomIsTreasure,
        bool uiRoomIsLive,
        bool ownsCurrentScreen) =>
        runInProgress
        && currentRoomIsTreasure
        && uiRoomIsLive
        && !ownsCurrentScreen;
}

internal static class TreasureVisibilityFacts
{
    public static bool CanReadSingleplayerRelic(bool collectionOpen, int currentRelicCount) =>
        collectionOpen && currentRelicCount == 1;
}

internal static class TreasureLifecycleFacts
{
    public static string Stage(
        bool chestOpened,
        bool collectionOpen,
        int currentRelicCount,
        bool chestActionable) =>
        !chestOpened
            ? chestActionable ? "closed" : "opening"
            : collectionOpen
                ? currentRelicCount > 0 ? "relic_choice" : "opening"
                : "completed";

}
