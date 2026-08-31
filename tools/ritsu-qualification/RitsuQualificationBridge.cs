using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.GameActions;
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

namespace STS2Platform.Qualification.Ritsu;

/// <summary>
/// Exact dependency identity for the bounded qualification spike. This is not
/// a production provider selector or a fallback contract.
/// </summary>
public static class RitsuQualificationIdentity
{
    public const string StableVersion = "0.5.18";
    public const string StableCommit = "f224961a9392e010335da092240b90ee8235317f";
    public const string DevelopmentCommit = "c466809004f8ecd801956fea2bc3fef83a5d7ad5";
}

/// <summary>
/// Ritsu does not expose a complete combat decision catalog. A Ritsu-backed
/// substrate must therefore retain the Platform-owned semantic provider and
/// its STS2-owned validators unchanged.
/// </summary>
public static class RitsuBackedCombatProvider
{
    public static NativeCombatDecision Capture(INativeReferentIdentity identities) =>
        NativeCombatDecisionProvider.Capture(identities);
}

/// <summary>
/// Ritsu lifecycle notifications carry effect-level CardPlay and
/// PlayerChoiceContext values, but not the exact currently executing root
/// GameAction. Parent lineage therefore remains the direct STS2 owner seam.
/// </summary>
public static class RitsuBackedPlayerChoiceProvider
{
    public static NativePlayerChoiceLineage Capture() => NativePlayerChoiceLineage.Capture();
}

public sealed record RitsuLifecycleObservation(
    string Kind,
    object? NativeSubject,
    bool HasExactGameActionIdentity,
    bool HasCancelOrAbortDisposition,
    bool HasRootParentLineage);

/// <summary>
/// Captures the strongest public Ritsu lifecycle facts relevant to the sampled
/// domains. These observations are useful diagnostics, but never replace the
/// exact GameAction lifecycle used as Platform semantic authority.
/// </summary>
public sealed class RitsuLifecycleProbe : IDisposable
{
    private readonly Action<RitsuLifecycleObservation> _observer;
    private readonly List<IDisposable> _subscriptions = new();

    public RitsuLifecycleProbe(Action<RitsuLifecycleObservation> observer)
    {
        _observer = observer;
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CardPlayingEvent>(
            value => Observe("card_playing", value.CardPlay), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CardPlayedEvent>(
            value => Observe("card_played", value.CardPlay), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<PotionUsingEvent>(
            value => Observe("potion_using", value.Potion), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<PotionUsedEvent>(
            value => Observe("potion_used", value.Potion), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<SideTurnEndingEvent>(
            value => Observe("side_turn_ending", value.CombatState), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<SideTurnEndedEvent>(
            value => Observe("side_turn_ended", value.CombatState), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RewardTakenEvent>(
            value => Observe("reward_taken", value.Reward), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RewardsScreenContinuingEvent>(
            value => Observe("rewards_screen_continuing", value.RunManager), false));
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
    }

    /// <summary>
    /// The exact lifecycle contract still comes directly from GameAction. The
    /// wrapper is explicit so qualification metrics cannot count it as Ritsu
    /// integration deletion.
    /// </summary>
    public static NativeActionLifecycleObserver ObserveExactAction(
        GameAction action,
        Action<GameAction, string> observer) =>
        new(action, observer);

    private void Observe(string kind, object nativeSubject) =>
        _observer(new RitsuLifecycleObservation(
            kind,
            nativeSubject,
            HasExactGameActionIdentity: false,
            HasCancelOrAbortDisposition: false,
            HasRootParentLineage: false));
}

/// <summary>
/// Ritsu PrivateAccess makes missing members fail explicitly and shortens field
/// access syntax. It does not remove the exact-version field names or turn UI
/// lifecycle flags into a general semantic owner.
/// </summary>
public static class RitsuTreasureLifecycleReader
{
    private static readonly HarmonyLib.AccessTools.FieldRef<NTreasureRoom, bool> CollectionOpen =
        PrivateAccess.FieldRef<NTreasureRoom, bool>("_isRelicCollectionOpen");
    private static readonly HarmonyLib.AccessTools.FieldRef<NTreasureRoom, bool> ChestOpened =
        PrivateAccess.FieldRef<NTreasureRoom, bool>("_hasChestBeenOpened");

    public static (bool ChestOpened, bool CollectionOpen) Capture(NTreasureRoom room) =>
        (ChestOpened(room), CollectionOpen(room));
}

/// <summary>
/// The Ritsu patcher replaces Platform's small Harmony registration helper,
/// but the same two exact STS2 targets and Platform owner callbacks remain.
/// </summary>
public static class RitsuTreasurePatchSet
{
    public static bool Initialize(Action disableQualification)
    {
        ModPatcher patcher = RitsuLibFramework.CreatePatcher(
            "STS2_PLATFORM",
            "ritsu-qualification-treasure",
            "Platform Ritsu qualification treasure seams");
        patcher.RegisterPatch<TreasureOwnerPatch>();
        patcher.RegisterPatch<TreasureChestPatch>();
        return RitsuLibFramework.ApplyRequiredPatcher(
            patcher,
            disableQualification,
            "The required Ritsu qualification targets were unavailable.");
    }
}

public sealed class TreasureOwnerPatch : IPatchMethod
{
    public static string PatchId => "platform_qualification_treasure_owner";
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
            NativeTreasureDecisionProvider.Register(__result, room, runState);
    }
}

public sealed class TreasureChestPatch : IPatchMethod
{
    public static string PatchId => "platform_qualification_treasure_chest";
    public static string Description => "Observe accepted treasure chest opening";

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NTreasureRoom>("OnChestButtonReleased", typeof(NButton))
    ];

    public static void Postfix(NTreasureRoom __instance) =>
        NativeTreasureDecisionProvider.ObserveChestOpening(__instance);
}
