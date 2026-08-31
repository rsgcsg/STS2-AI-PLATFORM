using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Platform.NativeFoundation;
using STS2RitsuLib;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace STS2Platform.Qualification.RitsuFirst;

/// <summary>
/// Independent Treasure implementation built from Ritsu public integration
/// facilities. It does not call the Direct Treasure provider.
/// </summary>
public static class RitsuFirstTreasureProvider
{
    private static readonly AccessTools.FieldRef<NTreasureRoom, bool> CollectionOpen =
        PrivateAccess.FieldRef<NTreasureRoom, bool>("_isRelicCollectionOpen");
    private static readonly AccessTools.FieldRef<NTreasureRoom, bool> ChestOpened =
        PrivateAccess.FieldRef<NTreasureRoom, bool>("_hasChestBeenOpened");

    private sealed class Owner(TreasureRoom room, IRunState runState)
    {
        public TreasureRoom Room { get; } = room;
        public IRunState RunState { get; } = runState;
        public bool ChestOpeningObserved { get; set; }
    }

    private static readonly ConditionalWeakTable<NTreasureRoom, Owner> Owners = new();

    public static void Register(NTreasureRoom screen, TreasureRoom room, IRunState runState)
    {
        Owners.Remove(screen);
        Owners.Add(screen, new Owner(room, runState));
    }

    public static void ObserveChestOpening(NTreasureRoom screen)
    {
        if (Owners.TryGetValue(screen, out Owner? owner))
            owner.ChestOpeningObserved = true;
    }

    public static NativeTreasureDecision Capture(
        NTreasureRoom screen,
        INativeReferentIdentity identities)
    {
        if (!Owners.TryGetValue(screen, out Owner? owner))
        {
            return Unavailable(
                "owner_not_registered",
                "Ritsu-first owner patch did not observe NTreasureRoom.Create.");
        }

        try
        {
            RunState? currentRun = RunManager.Instance.DebugOnlyGetState();
            if (currentRun == null
                || !ReferenceEquals(currentRun.CurrentRoom, owner.Room)
                || owner.RunState.Players.Count != 1)
            {
                return Unavailable(
                    "owner_not_current",
                    "The registered single-player Treasure owner is no longer current.");
            }

            bool chestOpened = ChestOpened(screen);
            bool collectionOpen = CollectionOpen(screen);
            IReadOnlyList<RelicModel>? currentRelics =
                RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics;
            bool localVoteReceived = collectionOpen
                && currentRelics != null
                && RunManager.Instance.TreasureRoomRelicSynchronizer
                    .GetPlayerVote(owner.RunState.Players[0])
                    .voteReceived;

            string stage = ClassifyStage(
                chestOpened,
                collectionOpen,
                owner.ChestOpeningObserved,
                currentRelics,
                localVoteReceived);
            RelicModel[] relics = currentRelics?.ToArray() ?? Array.Empty<RelicModel>();
            string roomId = identities.GetId(owner.Room, "treasure_room");
            var actions = new List<NativeSemanticAction>();
            if (stage == "closed")
            {
                actions.Add(Action(
                    "open",
                    roomId,
                    owner.Room,
                    "Ritsu owner patch+typed private chest lifecycle"));
            }
            else if (stage == "relic_choice")
            {
                RelicModel relic = relics[0];
                actions.Add(Action(
                    "select",
                    identities.GetId(relic, "treasure_relic"),
                    relic,
                    "TreasureRoomRelicSynchronizer current relic+vote"));
                actions.Add(Action(
                    "skip",
                    roomId,
                    owner.Room,
                    "TreasureRoomRelicSynchronizer current relic+vote"));
            }
            else if (stage == "completed")
            {
                actions.Add(Action(
                    "proceed",
                    roomId,
                    owner.Room,
                    "typed private completed lifecycle+native proceed"));
            }

            return new NativeTreasureDecision(
                "captured",
                "treasure",
                stage,
                chestOpened,
                actions.Count > 0,
                relics,
                actions.OrderBy(action => action.Key, StringComparer.Ordinal).ToArray(),
                new[]
                {
                    "Ritsu IPatchMethod+required patcher owner callbacks",
                    "Ritsu PrivateAccess typed field refs",
                    "TreasureRoomRelicSynchronizer.CurrentRelics+GetPlayerVote"
                },
                actions.Count == 0
                    ? $"The Treasure lifecycle is {stage}; no Human decision is open."
                    : null);
        }
        catch (Exception exception)
        {
            return Unavailable(
                "capture_failed",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    public static bool InitializePatches(Action disableQualification)
    {
        ModPatcher patcher = RitsuLibFramework.CreatePatcher(
            "STS2_PLATFORM",
            "ritsu-first-treasure",
            "Platform Ritsu-first Treasure experiment");
        patcher.RegisterPatch<RitsuFirstTreasureOwnerPatch>();
        patcher.RegisterPatch<RitsuFirstTreasureChestPatch>();
        return RitsuLibFramework.ApplyRequiredPatcher(
            patcher,
            disableQualification,
            "Required Ritsu-first Treasure targets were unavailable.");
    }

    internal static string ClassifyStage(
        bool chestOpened,
        bool collectionOpen,
        bool chestOpeningObserved,
        IReadOnlyList<RelicModel>? currentRelics,
        bool localVoteReceived)
    {
        if (!chestOpened)
            return chestOpeningObserved || currentRelics != null ? "opening" : "closed";
        if (!collectionOpen)
            return "completed";
        return currentRelics is { Count: 1 } && !localVoteReceived
            ? "relic_choice"
            : "resolving";
    }

    private static NativeSemanticAction Action(
        string verb,
        string referentId,
        object subject,
        string basis) =>
        new(
            $"{verb}|{referentId}|",
            verb,
            referentId,
            subject,
            Array.Empty<NativeSemanticOperand>(),
            basis);

    private static NativeTreasureDecision Unavailable(string status, string detail) =>
        new(
            status,
            "unavailable",
            "unknown",
            false,
            false,
            Array.Empty<RelicModel>(),
            Array.Empty<NativeSemanticAction>(),
            Array.Empty<string>(),
            detail);
}

public sealed class RitsuFirstTreasureOwnerPatch : IPatchMethod
{
    public static string PatchId => "platform_ritsu_first_treasure_owner";
    public static string Description => "Observe exact TreasureRoom and run owner";

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NTreasureRoom>(
            nameof(NTreasureRoom.Create),
            typeof(TreasureRoom),
            typeof(IRunState))
    ];

    public static void Postfix(
        TreasureRoom room,
        IRunState runState,
        NTreasureRoom? __result)
    {
        if (__result != null)
            RitsuFirstTreasureProvider.Register(__result, room, runState);
    }
}

public sealed class RitsuFirstTreasureChestPatch : IPatchMethod
{
    public static string PatchId => "platform_ritsu_first_treasure_chest";
    public static string Description => "Observe accepted Treasure chest opening";

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NTreasureRoom>("OnChestButtonReleased", typeof(NButton))
    ];

    public static void Postfix(NTreasureRoom __instance) =>
        RitsuFirstTreasureProvider.ObserveChestOpening(__instance);
}
