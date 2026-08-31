using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// Projects the single-player treasure decision from STS2's room coordinator
/// and relic synchronizer. Native controls remain delivery bindings only.
/// </summary>
public static class NativeTreasureDecisionProvider
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? CollectionOpenField =
        typeof(NTreasureRoom).GetField("_isRelicCollectionOpen", Flags);
    private static readonly FieldInfo? ChestOpenedField =
        typeof(NTreasureRoom).GetField("_hasChestBeenOpened", Flags);

    private sealed class Owner(TreasureRoom room, IRunState runState)
    {
        public TreasureRoom Room { get; } = room;
        public IRunState RunState { get; } = runState;
        public bool ChestOpeningObserved { get; set; }
    }

    private static readonly ConditionalWeakTable<NTreasureRoom, Owner> Owners = new();

    public static void Register(
        NTreasureRoom screen,
        TreasureRoom room,
        IRunState runState)
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
                "The exact treasure room owner was not observed at NTreasureRoom.Create.");
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
                    "The registered single-player treasure owner is no longer current.");
            }
            if (!TryReadBool(ChestOpenedField, screen, out bool chestOpened)
                || !TryReadBool(CollectionOpenField, screen, out bool collectionOpen))
            {
                return Unavailable(
                    "lifecycle_binding_unavailable",
                    "The exact treasure lifecycle flags are unavailable.");
            }

            IReadOnlyList<RelicModel>? currentRelics =
                RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics;
            bool localVoteReceived = false;
            if (collectionOpen && currentRelics != null)
            {
                localVoteReceived = RunManager.Instance.TreasureRoomRelicSynchronizer
                    .GetPlayerVote(owner.RunState.Players[0])
                    .voteReceived;
            }

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
                    "NTreasureRoom exact owner+chest lifecycle"));
            }
            else if (stage == "relic_choice")
            {
                RelicModel relic = relics[0];
                string relicId = identities.GetId(relic, "treasure_relic");
                actions.Add(Action(
                    "select",
                    relicId,
                    relic,
                    "TreasureRoomRelicSynchronizer.CurrentRelics+local vote state"));
                actions.Add(Action(
                    "skip",
                    roomId,
                    owner.Room,
                    "TreasureRoomRelicSynchronizer.CurrentRelics+local vote state"));
            }
            else if (stage == "completed")
            {
                actions.Add(Action(
                    "proceed",
                    roomId,
                    owner.Room,
                    "NTreasureRoom completed lifecycle+ProceedFromTerminalRewardsScreen"));
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
                    "NTreasureRoom.Create exact TreasureRoom+IRunState owner",
                    "NTreasureRoom._hasChestBeenOpened+_isRelicCollectionOpen",
                    "NTreasureRoom.OnChestButtonReleased accepted callback",
                    "TreasureRoomRelicSynchronizer.CurrentRelics+GetPlayerVote"
                },
                actions.Count == 0
                    ? $"The treasure lifecycle is {stage}; no Human decision is open."
                    : null);
        }
        catch (Exception exception)
        {
            return Unavailable(
                "capture_failed",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    internal static string ClassifyStage(
        bool chestOpened,
        bool collectionOpen,
        bool chestOpeningObserved,
        IReadOnlyList<RelicModel>? currentRelics,
        bool localVoteReceived)
    {
        if (!chestOpened)
        {
            return chestOpeningObserved || currentRelics != null
                ? "opening"
                : "closed";
        }
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
            NativeSemanticActionCatalog.BuildKey(verb, referentId),
            verb,
            referentId,
            subject,
            Array.Empty<NativeSemanticOperand>(),
            basis);

    private static bool TryReadBool(FieldInfo? field, object instance, out bool value)
    {
        value = false;
        if (field?.GetValue(instance) is not bool current)
            return false;
        value = current;
        return true;
    }

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
