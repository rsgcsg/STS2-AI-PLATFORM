using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;
using STS2Platform.NativeFoundation;

namespace STS2HumanAnnotator.Mod;

[HarmonyPatch]
internal static class NativeCardStartPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NPlayerHand), "StartCardPlay")
        ?? throw new MissingMethodException(typeof(NPlayerHand).FullName, "StartCardPlay");

    internal static void Prefix([HarmonyArgument(0)] NHandCardHolder holder)
    {
        if (holder.CardModel is { } card)
            RecorderRuntime.StageCardPlay(card);
    }
}

[HarmonyPatch]
internal static class NativeCardPlayPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NCardPlay), "TryPlayCard")
        ?? throw new MissingMethodException(typeof(NCardPlay).FullName, "TryPlayCard");

    private static void Prefix(
        NCardPlay __instance,
        [HarmonyArgument(0)] Creature? target,
        out NativeUiScopeEntry __state)
    {
        __state = __instance.Holder.CardModel is { } card
            ? RecorderRuntime.TryEnterCardScope(card, target)
            : default;
    }

    internal static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(NPotionHolder), nameof(NPotionHolder.UsePotion))]
internal static class NativePotionUseStartPatch
{
    internal static void Prefix(
        NPotionHolder __instance,
        out RecorderRuntime.PotionUseArmHandle? __state)
    {
        __state = RecorderRuntime.ArmPotionUse(__instance.Potion?.Model);
    }

    internal static void Postfix(
        RecorderRuntime.PotionUseArmHandle? __state,
        Task __result)
    {
        if (!__state.HasValue)
            return;
        if (__result == null)
        {
            RecorderRuntime.ClearPotionUseArm(__state);
            return;
        }
        _ = __result.ContinueWith(
            _ => RecorderRuntime.ClearPotionUseArm(__state),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.EnqueueManualUse))]
internal static class NativePotionEnqueuePatch
{
    internal static void Prefix(
        PotionModel __instance,
        [HarmonyArgument(0)] Creature? target,
        out NativeUiScopeEntry __state)
    {
        __state = RecorderRuntime.TryEnterPotionUseScope(__instance, target);
    }

    internal static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.CallReleaseLogic))]
internal static class NativeEndTurnPatch
{
    internal static void Prefix(out NativeUiScopeEntry __state)
    {
        __state = RecorderRuntime.TryEnterScope(
            "native_end_turn_ui",
            nameof(EndPlayerTurnAction),
            semanticSelection: new ProcessLocalObservedAction(
                "end_turn",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    internal static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.SecretEndTurnLogicViaFtue))]
internal static class NativeFtueEndTurnPatch
{
    internal static void Prefix(out NativeUiScopeEntry __state)
    {
        __state = RecorderRuntime.TryEnterScope(
            "native_ftue_end_turn_ui",
            nameof(EndPlayerTurnAction),
            semanticSelection: new ProcessLocalObservedAction(
                "end_turn",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    internal static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(GameAction), nameof(GameAction.OnEnqueued))]
internal static class AcceptedGameActionPatch
{
    // OnEnqueued has assigned the exact queue ID and state, while the caller has
    // not yet notified ActionExecutor. This is the earliest accepted-action seam
    // where no started/cancelled/finished lifecycle event can already be lost.
    internal static void Postfix(GameAction __instance) =>
        RecorderRuntime.ObserveAcceptedAction(__instance);
}

[HarmonyPatch(
    typeof(NCardPlayQueue),
    nameof(NCardPlayQueue.RemoveCardFromQueueForCancellation),
    new[] { typeof(PlayCardAction) })]
internal static class PlayCardExecutionAbortPatch
{
    // In exact v0.111.0 PlayCardAction returns without Cancel() when its card
    // is no longer in hand. This read-only seam distinguishes that native no-op
    // from a finished play; it does not change queue or card behavior.
    internal static void Prefix(PlayCardAction action)
    {
        if (action.State == MegaCrit.Sts2.Core.Entities.Actions.GameActionState.Executing)
            RecorderRuntime.ObservePlayCardExecutionAborted(action);
    }
}

[HarmonyPatch]
internal static class NativeGeneratedChoiceCardPatch
{
    private readonly record struct PatchState(
        NativeUiScopeEntry Scope,
        bool WasSelected,
        CardModel? Card);

    private static readonly AccessTools.FieldRef<NChooseACardSelectionScreen, bool>
        CardSelected = AccessTools.FieldRefAccess<NChooseACardSelectionScreen, bool>("_cardSelected");

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NChooseACardSelectionScreen), "SelectHolder")
        ?? throw new MissingMethodException(
            typeof(NChooseACardSelectionScreen).FullName,
            "SelectHolder");

    private static void Prefix(
        NChooseACardSelectionScreen __instance,
        [HarmonyArgument(0)] NCardHolder holder,
        out PatchState __state)
    {
        CardModel? card = holder.CardModel;
        __state = new PatchState(
            card != null ? RecorderRuntime.TryEnterGeneratedChoiceCardScope(__instance, holder) : default,
            CardSelected(__instance),
            card);
    }

    private static void Postfix(
        NChooseACardSelectionScreen __instance,
        PatchState __state)
    {
        if ((__state.Scope.Entered || __state.Scope.DeferredFailure)
            && !__state.WasSelected && CardSelected(__instance)
            && __state.Card is { } card)
            RecorderRuntime.ObserveGeneratedChoiceCard(card);
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeGeneratedChoiceSkipPatch
{
    private readonly record struct PatchState(NativeUiScopeEntry Scope, bool WasComplete);

    private static readonly AccessTools.FieldRef<NChooseACardSelectionScreen, bool>
        ScreenComplete = AccessTools.FieldRefAccess<NChooseACardSelectionScreen, bool>("_screenComplete");

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NChooseACardSelectionScreen), "OnSkipButtonReleased")
        ?? throw new MissingMethodException(
            typeof(NChooseACardSelectionScreen).FullName,
            "OnSkipButtonReleased");

    private static void Prefix(
        NChooseACardSelectionScreen __instance,
        out PatchState __state)
    {
        __state = new PatchState(
            RecorderRuntime.TryEnterGeneratedChoiceSkipScope(__instance),
            ScreenComplete(__instance));
    }

    private static void Postfix(NChooseACardSelectionScreen __instance, PatchState __state)
    {
        if ((__state.Scope.Entered || __state.Scope.DeferredFailure)
            && !__state.WasComplete && ScreenComplete(__instance))
            RecorderRuntime.ObserveGeneratedChoiceSkip();
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeCombatHandSelectPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NPlayerHand), "SelectCardInSimpleMode")
            ?? throw new MissingMethodException(typeof(NPlayerHand).FullName, "SelectCardInSimpleMode");
        yield return AccessTools.Method(typeof(NPlayerHand), "SelectCardInUpgradeMode")
            ?? throw new MissingMethodException(typeof(NPlayerHand).FullName, "SelectCardInUpgradeMode");
    }

    private static void Prefix(
        MethodBase __originalMethod,
        [HarmonyArgument(0)] NHandCardHolder holder,
        out NativeUiScopeEntry __state)
    {
        string nativeActionType = $"NPlayerHand.{__originalMethod.Name}";
        __state = holder.CardModel is { } card
            ? RecorderRuntime.TryEnterSemanticScope(
                "native_combat_hand_selection_ui",
                nativeActionType,
                new ProcessLocalObservedAction(
                    "select",
                    card,
                    new Dictionary<string, object>(StringComparer.Ordinal)))
            : default;
    }

    private static void Postfix(
        MethodBase __originalMethod,
        [HarmonyArgument(0)] NHandCardHolder holder,
        NativeUiScopeEntry __state)
    {
        if ((!__state.Entered && !__state.DeferredFailure) || holder.CardModel is not { } card)
            return;
        string nativeActionType = $"NPlayerHand.{__originalMethod.Name}";
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            nativeActionType,
            new ProcessLocalObservedAction(
                "select",
                card,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_combat_hand_selection_ui",
                nativeActionType,
                NativeWitnessIdentity.Get(card, "card"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeCombatHandDeselectPatch
{
    private const string NativeActionType = "NSelectedHandCardContainer.DeselectHolder";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NSelectedHandCardContainer), "DeselectHolder")
        ?? throw new MissingMethodException(
            typeof(NSelectedHandCardContainer).FullName,
            "DeselectHolder");

    private static void Prefix(
        [HarmonyArgument(0)] NCardHolder holder,
        out NativeUiScopeEntry __state)
    {
        __state = holder.CardModel is { } card
            ? RecorderRuntime.TryEnterSemanticScope(
                "native_combat_hand_deselect_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "deselect",
                    card,
                    new Dictionary<string, object>(StringComparer.Ordinal)))
            : default;
    }

    private static void Postfix(
        [HarmonyArgument(0)] NCardHolder holder,
        NativeUiScopeEntry __state)
    {
        if ((!__state.Entered && !__state.DeferredFailure) || holder.CardModel is not { } card)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "deselect",
                card,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_combat_hand_deselect_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(card, "card"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeCombatHandConfirmPatch
{
    private const string NativeActionType = "NPlayerHand.OnSelectModeConfirmButtonPressed";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NPlayerHand), "OnSelectModeConfirmButtonPressed")
        ?? throw new MissingMethodException(
            typeof(NPlayerHand).FullName,
            "OnSelectModeConfirmButtonPressed");

    private static void Prefix(out NativeUiScopeEntry __state)
    {
        __state = RecorderRuntime.TryEnterSemanticScope(
            "native_combat_hand_confirm_ui",
            NativeActionType,
            new ProcessLocalObservedAction(
                "confirm",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static void Postfix(NativeUiScopeEntry __state)
    {
        if (!__state.Entered && !__state.DeferredFailure)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "confirm",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_combat_hand_confirm_ui",
                NativeActionType,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.OnMapPointSelectedLocally))]
internal static class NativeMapChoicePatch
{
    private static void Prefix(
        [HarmonyArgument(0)] NMapPoint point,
        out NativeUiScopeEntry __state)
    {
        __state = RecorderRuntime.TryEnterSemanticScope(
            "native_map_choice_ui",
            nameof(VoteForMapCoordAction),
            new ProcessLocalObservedAction(
                "activate",
                point.Point,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            nativeSemanticSelection: new ProcessLocalObservedAction(
                "travel",
                point.Point,
                new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

internal static class NativeTreasureUiContext
{
    // These observed actions must match the public Player Environment
    // projection. The native provider keeps TreasureRoom as its semantic
    // owner, but the public projection hides room-owner bindings from the
    // player action and maps choose/proceed operations to "activate".
    internal static TreasureRoom? CurrentRoom() =>
        RunManager.Instance.DebugOnlyGetState()?.CurrentRoom as TreasureRoom;

    internal static NTreasureRoom? CurrentUi() => NRun.Instance?.TreasureRoom;
}

internal static class NativeRewardUiContext
{
    internal static RewardsSet? CurrentRewardsSet() =>
        NOverlayStack.Instance?.Peek() is NRewardsScreen screen
            ? NativeRewardDecisionProvider.ObserveOwner(screen)?.RewardsSet
            : null;
}

/// <summary>
/// Carries the exact Human root through a native UI callback to the Task
/// returned by that callback's game-owned operation. The key is the exact
/// native owner object, never a queue position, timestamp, or ambient current
/// root. Entries are weak and consumed once the operation is bound.
/// </summary>
internal static class NativeUiCompletionRootBindings
{
    private sealed class RootBinding
    {
        internal RootBinding(string actionWitnessId) => ActionWitnessId = actionWitnessId;

        internal string ActionWitnessId { get; }
    }

    private static readonly ConditionalWeakTable<object, RootBinding> Bindings = new();

    internal static void Remember(object? owner, string? actionWitnessId)
    {
        if (owner == null || string.IsNullOrWhiteSpace(actionWitnessId))
            return;
        Bindings.Remove(owner);
        Bindings.Add(owner, new RootBinding(actionWitnessId));
    }

    internal static string? Take(object? owner)
    {
        if (owner == null || !Bindings.TryGetValue(owner, out RootBinding? binding))
            return null;
        Bindings.Remove(owner);
        return binding.ActionWitnessId;
    }

    internal static string? TakeCurrentRewardOrTreasure()
    {
        string? overlayRoot = Take(NOverlayStack.Instance?.Peek());
        return overlayRoot ?? Take(NativeTreasureUiContext.CurrentUi());
    }
}

[HarmonyPatch]
internal static class NativeTreasureChestChoicePatch
{
    private const string NativeActionType = "NTreasureRoom.OnChestButtonReleased";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NTreasureRoom),
            "OnChestButtonReleased",
            new[] { typeof(NButton) })
        ?? throw new MissingMethodException(
            typeof(NTreasureRoom).FullName,
            "OnChestButtonReleased");

    private static void Prefix(out NativeUiScopeEntry __state)
    {
        TreasureRoom? room = NativeTreasureUiContext.CurrentRoom();
        __state = room == null
            ? default
            : RecorderRuntime.TryEnterSemanticScope(
                "native_treasure_chest_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "open",
                    null,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                new NativePostCommitCompletionExpectation(
                    "treasure_open",
                    "OneOffSynchronizer.DoLocalTreasureRoomRewards",
                    NativeOperandWitnessId: NativeWitnessIdentity.Get(room, "native_operand")),
                nativeSemanticSelection: new ProcessLocalObservedAction(
                    "open",
                    room,
                    new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static void Postfix(NativeUiScopeEntry __state)
    {
        TreasureRoom? room = NativeTreasureUiContext.CurrentRoom();
        if ((!__state.Entered && !__state.DeferredFailure) || room == null)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "open",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_treasure_chest_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(room, "treasure_room"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: __state.ActionWitnessId);
        NativeUiCompletionRootBindings.Remember(NativeTreasureUiContext.CurrentUi(), __state.ActionWitnessId);
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeTreasureRelicChoicePatch
{
    private const string NativeActionType = nameof(PickRelicAction);

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NTreasureRoomRelicCollection),
            "PickRelic",
            new[] { typeof(NTreasureRoomRelicHolder) })
        ?? throw new MissingMethodException(
            typeof(NTreasureRoomRelicCollection).FullName,
            "PickRelic");

    private readonly record struct PatchState(
        NativeUiScopeEntry Scope,
        RelicModel? Relic);

    private static void Prefix(
        [HarmonyArgument(0)] NTreasureRoomRelicHolder holder,
        out PatchState __state)
    {
        RelicModel? relic = holder.Relic?.Model;
        __state = new PatchState(
            relic == null
                ? default
                : RecorderRuntime.TryEnterSemanticScope(
                    "native_treasure_relic_ui",
                    NativeActionType,
                    new ProcessLocalObservedAction(
                        "activate",
                        relic,
                        new Dictionary<string, object>(StringComparer.Ordinal)),
                    nativeSemanticSelection: new ProcessLocalObservedAction(
                        "select",
                        relic,
                        new Dictionary<string, object>(StringComparer.Ordinal))),
            relic);
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeTreasureProceedPatch
{
    private const string NativeActionType = "NTreasureRoom.OnProceedButtonPressed";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NTreasureRoom),
            "OnProceedButtonPressed",
            new[] { typeof(NButton) })
        ?? throw new MissingMethodException(
            typeof(NTreasureRoom).FullName,
            "OnProceedButtonPressed");

    private readonly record struct PatchState(
        NativeUiScopeEntry Scope,
        TreasureRoom? Room,
        string? Verb,
        bool IsGameAction);

    private static void Prefix(
        NTreasureRoom __instance,
        [HarmonyArgument(0)] NButton button,
        out PatchState __state)
    {
        TreasureRoom? room = NativeTreasureUiContext.CurrentRoom();
        NTreasureRoom? uiRoom = NativeTreasureUiContext.CurrentUi();
        if (room == null || uiRoom == null || !ReferenceEquals(uiRoom, __instance)
            || button is not NProceedButton proceed)
        {
            __state = default;
            return;
        }

        bool isGameAction = proceed.IsSkip;
        string verb = isGameAction ? "skip" : "activate";
        string expectedNativeActionType = isGameAction
            ? nameof(PickRelicAction)
            : NativeActionType;
        __state = new PatchState(
            RecorderRuntime.TryEnterSemanticScope(
                "native_treasure_proceed_ui",
                expectedNativeActionType,
                new ProcessLocalObservedAction(
                    verb,
                    null,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                isGameAction
                    ? null
                    : new NativePostCommitCompletionExpectation(
                        "treasure_proceed",
                        "RunManager.ProceedFromTerminalRewardsScreen",
                        NativeOperandWitnessId: NativeWitnessIdentity.Get(room, "native_operand")),
                nativeSemanticSelection: new ProcessLocalObservedAction(
                    isGameAction ? "skip" : "proceed",
                    room,
                    new Dictionary<string, object>(StringComparer.Ordinal))),
            room,
            verb,
            isGameAction);
    }

    private static void Postfix(
        NTreasureRoom __instance,
        PatchState __state)
    {
        if ((!__state.Scope.Entered && !__state.Scope.DeferredFailure)
            || __state.Room is not { } room
            || __state.Verb is not { } verb
            || __state.IsGameAction)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                verb,
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_treasure_proceed_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(__instance.ProceedButton, "proceed_button"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: __state.Scope.ActionWitnessId);
        NativeUiCompletionRootBindings.Remember(__instance, __state.Scope.ActionWitnessId);
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeTreasureNormalRewardsPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(OneOffSynchronizer),
            "DoLocalTreasureRoomRewards")
        ?? throw new MissingMethodException(
            typeof(OneOffSynchronizer).FullName,
            "DoLocalTreasureRoomRewards");

    private static void Postfix(OneOffSynchronizer __instance, Task<int> __result) =>
        RecorderRuntime.QueueNativePostCommitBoundary(
            __result,
            "OneOffSynchronizer.DoLocalTreasureRoomRewards",
            nativeOwner: __instance,
            nativeOperand: NativeTreasureUiContext.CurrentRoom(),
            expectedActionWitnessId: NativeUiCompletionRootBindings.Take(
                NativeTreasureUiContext.CurrentUi()));
}

[HarmonyPatch]
internal static class NativeTreasureProceedCompletionPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(RunManager),
            "ProceedFromTerminalRewardsScreen")
        ?? throw new MissingMethodException(
            typeof(RunManager).FullName,
            "ProceedFromTerminalRewardsScreen");

    private static void Postfix(RunManager __instance, Task __result) =>
        RecorderRuntime.QueueNativePostCommitBoundary(
            __result,
            "RunManager.ProceedFromTerminalRewardsScreen",
            nativeOwner: __instance,
            nativeOperand: NOverlayStack.Instance?.Peek() is NRewardsScreen
                ? NativeRewardUiContext.CurrentRewardsSet()
                : NativeTreasureUiContext.CurrentRoom(),
            expectedActionWitnessId: NativeUiCompletionRootBindings.TakeCurrentRewardOrTreasure());
}

[HarmonyPatch]
internal static class NativeRewardClaimStartPatch
{
    private const string NativeActionType = "NRewardButton.OnRelease";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NRewardButton), "OnRelease")
        ?? throw new MissingMethodException(typeof(NRewardButton).FullName, "OnRelease");

    private static void Prefix(NRewardButton __instance, out NativeUiScopeEntry __state)
    {
        __state = __instance.Reward == null
            ? default
            : RecorderRuntime.TryEnterSemanticScope(
                "native_reward_claim_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "activate",
                    __instance.Reward,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                RewardClaimCompletion(__instance.Reward),
                new ProcessLocalObservedAction(
                    "claim",
                    __instance.Reward,
                    new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static NativePostCommitCompletionExpectation RewardClaimCompletion(Reward reward) =>
        new(
            "reward_claim",
            reward is CardReward
                ? "NCardRewardSelectionScreen.ShowScreen"
                : "RewardsSetSynchronizer.SelectLocalReward",
            NativeOperandWitnessId: NativeWitnessIdentity.Get(reward, "native_operand"));

    private static void Postfix(NRewardButton __instance, NativeUiScopeEntry __state)
    {
        if ((!__state.Entered && !__state.DeferredFailure) || __instance.Reward == null)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                __instance.Reward,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_reward_claim_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(__instance, "reward_button"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: __state.ActionWitnessId);
        NativeUiCompletionRootBindings.Remember(__instance.Reward, __state.ActionWitnessId);
        if (__instance.Reward is CardReward reward
            && __state.ActionWitnessId is { } actionWitnessId
            && NOverlayStack.Instance?.Peek() is NCardRewardSelectionScreen screen)
        {
            RecorderRuntime.ObserveSemanticUiNativeCommit(
                actionWitnessId,
                "reward_claim",
                "NCardRewardSelectionScreen.ShowScreen",
                nativeOwner: screen,
                nativeOperand: reward);
        }
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeRewardProceedPatch
{
    private const string NativeActionType = "NRewardsScreen.OnProceedButtonPressed";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NRewardsScreen), "OnProceedButtonPressed")
        ?? throw new MissingMethodException(typeof(NRewardsScreen).FullName, "OnProceedButtonPressed");

    private static void Prefix(NRewardsScreen __instance, out NativeUiScopeEntry __state)
    {
        NativeRewardDecisionOwner? owner =
            NativeRewardDecisionProvider.ObserveOwner(__instance);
        __state = RecorderRuntime.TryEnterSemanticScope(
            "native_reward_proceed_ui",
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativePostCommitCompletionExpectation(
                "reward_proceed",
                "RunManager.ProceedFromTerminalRewardsScreen",
                NativeOperandWitnessId: owner == null
                    ? null
                    : NativeWitnessIdentity.Get(owner.RewardsSet, "native_operand"),
                AlternativeKinds: new[]
                {
                    "RewardsSetSynchronizer.SkipLocalRewardsSet"
                }),
            nativeSemanticSelection: owner == null
                ? null
                : new ProcessLocalObservedAction(
                    "proceed",
                    owner.RewardsSet,
                    new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static void Postfix(NRewardsScreen __instance, NativeUiScopeEntry __state)
    {
        if (!__state.Entered && !__state.DeferredFailure)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_reward_proceed_ui",
                NativeActionType,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: __state.ActionWitnessId);
        NativeUiCompletionRootBindings.Remember(__instance, __state.ActionWitnessId);
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(RewardsSetSynchronizer), nameof(RewardsSetSynchronizer.SkipLocalRewardsSet))]
internal static class NativeRewardSkipCommitPatch
{
    private static void Postfix(RewardsSetSynchronizer __instance) =>
        RecorderRuntime.ObserveSemanticUiNativeCommit(
            "reward_proceed",
            "RewardsSetSynchronizer.SkipLocalRewardsSet",
            nativeOwner: __instance,
            nativeOperand: NativeRewardUiContext.CurrentRewardsSet(),
            expectedActionWitnessId: NativeUiCompletionRootBindings.Take(
                NOverlayStack.Instance?.Peek()));
}

[HarmonyPatch]
internal static class NativeCardRewardSelectionPatch
{
    private const string NativeActionType = "NCardRewardSelectionScreen.SelectCard";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NCardRewardSelectionScreen), "SelectCard")
        ?? throw new MissingMethodException(
            typeof(NCardRewardSelectionScreen).FullName,
            "SelectCard");

    private static void Prefix(
        NCardRewardSelectionScreen __instance,
        [HarmonyArgument(0)] NCardHolder cardHolder,
        out NativeUiScopeEntry __state)
    {
        __state = cardHolder.CardModel is { } card
            ? RecorderRuntime.TryEnterSemanticScope(
                "native_card_reward_selection_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "select",
                    card,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                new NativePostCommitCompletionExpectation(
                    "card_reward_select",
                    "NCardRewardSelectionScreen.SelectCard",
                    NativeOwnerWitnessId: NativeWitnessIdentity.Get(
                        __instance,
                        "native_owner"),
                    NativeOperandWitnessId: NativeWitnessIdentity.Get(card, "native_operand")))
            : default;
    }

    private static void Postfix(
        NCardRewardSelectionScreen __instance,
        [HarmonyArgument(0)] NCardHolder cardHolder,
        NativeUiScopeEntry __state)
    {
        if ((!__state.Entered && !__state.DeferredFailure) || cardHolder.CardModel is not { } card)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "select",
                card,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_card_reward_selection_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(card, "card"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: __state.ActionWitnessId);
        if (__state.ActionWitnessId is { } actionWitnessId)
        {
            RecorderRuntime.ObserveSemanticUiNativeCommit(
                actionWitnessId,
                "card_reward_select",
                "NCardRewardSelectionScreen.SelectCard",
                nativeOwner: __instance,
                nativeOperand: card);
        }
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeRewardClaimCompletionPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(RewardsSetSynchronizer),
            "SelectLocalReward",
            new[] { typeof(Reward) })
        ?? throw new MissingMethodException(
            typeof(RewardsSetSynchronizer).FullName,
            "SelectLocalReward");

    private static void Postfix(
        RewardsSetSynchronizer __instance,
        [HarmonyArgument(0)] Reward reward,
        Task<bool> __result)
    {
        // CardReward opens a nested native decision before SelectLocalReward's
        // Task can complete. That exact ShowScreen owner is the claim Commit;
        // the Task remains the later business outcome and must not block the
        // child Human decision.
        if (reward is CardReward)
            return;
        RecorderRuntime.QueueNativePostCommitBoundary(
            __result,
            "RewardsSetSynchronizer.SelectLocalReward",
            nativeOwner: __instance,
            nativeOperand: reward,
            expectedActionWitnessId: NativeUiCompletionRootBindings.Take(reward));
    }
}

[HarmonyPatch]
internal static class NativeEventOptionPatch
{
    private const string NativeActionType = "NEventRoom.OptionButtonClicked";

    private readonly record struct PatchState(
        NativeUiScopeEntry Scope,
        EventOption? Option,
        string? Verb,
        NEventRoom? Room);

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NEventRoom),
            "OptionButtonClicked",
            new[] { typeof(EventOption), typeof(int) })
        ?? throw new MissingMethodException(
            typeof(NEventRoom).FullName,
            "OptionButtonClicked");

    private static void Prefix(
        NEventRoom __instance,
        [HarmonyArgument(0)] EventOption option,
        out PatchState __state)
    {
        if (option == null || option.IsLocked)
        {
            __state = default;
            return;
        }

        string verb = option.IsProceed ? "proceed_event" : "choose_event_option";
        __state = new PatchState(
            RecorderRuntime.TryEnterSemanticScope(
                "native_event_option_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "activate",
                    option,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                new NativePostCommitCompletionExpectation(
                    "event_option",
                    "EventOption.Chosen",
                    NativeOperandWitnessId: NativeWitnessIdentity.Get(option, "native_operand")),
                new ProcessLocalObservedAction(
                    verb,
                    option,
                    new Dictionary<string, object>(StringComparer.Ordinal))),
            option,
            verb,
            __instance);
    }

    private static void Postfix(PatchState __state)
    {
        if ((!__state.Scope.Entered && !__state.Scope.DeferredFailure)
            || __state.Option is not { } option
            || __state.Verb is not { } verb
            || __state.Room is not { } room)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                option,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_event_option_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(option, "event_option"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: __state.Scope.ActionWitnessId);
        NativeUiCompletionRootBindings.Remember(option, __state.Scope.ActionWitnessId);
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeEventOptionCompletionPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(EventOption), nameof(EventOption.Chosen))
        ?? throw new MissingMethodException(typeof(EventOption).FullName, nameof(EventOption.Chosen));

    private static void Postfix(EventOption __instance, Task __result)
    {
        if (__result != null)
        {
            RecorderRuntime.QueueNativePostCommitBoundary(
                __result,
                "EventOption.Chosen",
                nativeOperand: __instance,
                expectedActionWitnessId: NativeUiCompletionRootBindings.Take(__instance));
        }
    }
}

[HarmonyPatch]
internal static class NativeRestSiteOptionPatch
{
    private const string NativeActionType = "RestSiteSynchronizer.ChooseLocalOption";

    private readonly record struct PatchState(
        NativeUiScopeEntry Scope,
        RestSiteOption? Option,
        NRestSiteRoom? Room,
        string? InheritedActionWitnessId);

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(RestSiteSynchronizer),
            nameof(RestSiteSynchronizer.ChooseLocalOption),
            new[] { typeof(int) })
        ?? throw new MissingMethodException(
            typeof(RestSiteSynchronizer).FullName,
            nameof(RestSiteSynchronizer.ChooseLocalOption));

    private static void Prefix(
        RestSiteSynchronizer __instance,
        [HarmonyArgument(0)] int index,
        out PatchState __state)
    {
        NRestSiteRoom? room = NRestSiteRoom.Instance;
        RestSiteOption[] options = __instance.GetLocalOptions().ToArray();
        if (room == null || index < 0 || index >= options.Length)
        {
            __state = default;
            return;
        }
        RestSiteOption option = options[index];
        string? inheritedActionWitnessId =
            RecorderRuntime.CurrentSemanticActionWitnessId(NativeActionType);
        __state = new PatchState(
            inheritedActionWitnessId == null
                ? RecorderRuntime.TryEnterSemanticScope(
                    "native_rest_site_option_ui",
                    NativeActionType,
                    new ProcessLocalObservedAction(
                        "activate",
                        option,
                        new Dictionary<string, object>(StringComparer.Ordinal)),
                    new NativePostCommitCompletionExpectation(
                        "rest_site",
                        NativeActionType,
                        NativeOperandWitnessId: NativeWitnessIdentity.Get(option, "native_operand")),
                    new ProcessLocalObservedAction(
                        "choose_rest_option",
                        option,
                        new Dictionary<string, object>(StringComparer.Ordinal)))
                : default,
            option,
            room,
            inheritedActionWitnessId);
    }

    private static void Postfix(
        RestSiteSynchronizer __instance,
        PatchState __state,
        Task<bool> __result)
    {
        if (__state.Option is not { } option
            || __state.Room is not { } room)
            return;
        string? actionWitnessId = __state.Scope.ActionWitnessId
            ?? __state.InheritedActionWitnessId;
        if (actionWitnessId == null
            || !__state.Scope.Entered && !__state.Scope.DeferredFailure
                && __state.InheritedActionWitnessId == null)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                option,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_rest_site_option_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(option, "rest_option"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: actionWitnessId);
        if (__result != null)
        {
            RecorderRuntime.QueueNativePostCommitBoundary(
                __result,
                NativeActionType,
                nativeOwner: __instance,
                nativeOperand: option,
                expectedActionWitnessId: actionWitnessId);
        }
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}

/// <summary>
/// NRestSiteButton disables its visible options before it invokes the
/// asynchronous RestSiteSynchronizer operation. Capture the exact interactive
/// frame at the button boundary, then let the nested synchronizer callback
/// publish the accepted root and bind the returned Task to that same root.
/// </summary>
[HarmonyPatch]
internal static class NativeRestSiteButtonPatch
{
    private const string NativeActionType = "RestSiteSynchronizer.ChooseLocalOption";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NRestSiteButton), "OnRelease")
        ?? throw new MissingMethodException(
            typeof(NRestSiteButton).FullName,
            "OnRelease");

    private static void Prefix(
        NRestSiteButton __instance,
        out NativeUiScopeEntry __state)
    {
        RestSiteOption? option = __instance.Option;
        RestSiteRoom? room = RunManager.Instance.DebugOnlyGetState()?.CurrentRoom as RestSiteRoom;
        if (option == null
            || room == null
            || NRestSiteRoom.Instance == null
            || !room.Options.Any(candidate => ReferenceEquals(candidate, option)))
        {
            __state = default;
            return;
        }
        __state = RecorderRuntime.TryEnterSemanticScope(
            "native_rest_site_option_ui",
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                option,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativePostCommitCompletionExpectation(
                "rest_site",
                NativeActionType,
                NativeOperandWitnessId: NativeWitnessIdentity.Get(option, "native_operand")),
            new ProcessLocalObservedAction(
                "choose_rest_option",
                option,
                new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static Exception? Finalizer(
        NativeUiScopeEntry __state,
        Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeRestSiteProceedPatch
{
    private const string NativeActionType = "NRestSiteRoom.OnProceedButtonReleased";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NRestSiteRoom),
            "OnProceedButtonReleased",
            new[] { typeof(NButton) })
        ?? throw new MissingMethodException(
            typeof(NRestSiteRoom).FullName,
            "OnProceedButtonReleased");

    private static void Prefix(
        NRestSiteRoom __instance,
        out NativeUiScopeEntry __state)
    {
        RestSiteRoom? room = RunManager.Instance.DebugOnlyGetState()?.CurrentRoom as RestSiteRoom;
        __state = room == null
            ? default
            : RecorderRuntime.TryEnterSemanticScope(
                "native_rest_site_proceed_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "activate",
                    null,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                nativeSemanticSelection: new ProcessLocalObservedAction(
                    "proceed_rest_site",
                    room,
                    new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static void Postfix(
        NRestSiteRoom __instance,
        NativeUiScopeEntry __state)
    {
        if (!__state.Entered && !__state.DeferredFailure)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_rest_site_proceed_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(__instance, "proceed_button"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeShopPurchasePatch
{
    private const string NativeActionType = "MerchantEntry.OnTryPurchaseWrapper";

    private readonly record struct PatchState(
        NativeUiScopeEntry Scope,
        MerchantEntry? Entry,
        NMerchantInventory? Inventory);

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(MerchantEntry),
            nameof(MerchantEntry.OnTryPurchaseWrapper),
            new[] { typeof(MerchantInventory), typeof(bool) })
        ?? throw new MissingMethodException(
            typeof(MerchantEntry).FullName,
            nameof(MerchantEntry.OnTryPurchaseWrapper));

    private static void Prefix(
        MerchantEntry __instance,
        [HarmonyArgument(0)] MerchantInventory? inventory,
        out PatchState __state)
    {
        NMerchantInventory? ui = NMerchantRoom.Instance?.Inventory;
        if (ui == null || inventory == null || !ReferenceEquals(ui.Inventory, inventory))
        {
            __state = default;
            return;
        }
        string operation = __instance switch
        {
            MerchantCardEntry => "purchase_shop_card",
            MerchantRelicEntry => "purchase_shop_relic",
            MerchantPotionEntry => "purchase_shop_potion",
            MerchantCardRemovalEntry => "open_shop_card_removal",
            _ => string.Empty
        };
        if (operation.Length == 0)
        {
            __state = default;
            return;
        }
        __state = new PatchState(
            RecorderRuntime.TryEnterSemanticScope(
                "native_shop_purchase_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "activate",
                    __instance,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                new NativePostCommitCompletionExpectation(
                    "shop_inventory",
                    NativeActionType,
                    NativeOperandWitnessId: NativeWitnessIdentity.Get(__instance, "native_operand")),
                new ProcessLocalObservedAction(
                    operation,
                    __instance,
                    new Dictionary<string, object>(StringComparer.Ordinal))),
            __instance,
            ui);
    }

    private static void Postfix(
        MerchantEntry __instance,
        PatchState __state,
        Task<bool> __result)
    {
        if ((!__state.Scope.Entered && !__state.Scope.DeferredFailure)
            || __state.Entry is not { } entry
            || __state.Inventory is not { } inventory)
            return;
        string operation = entry switch
        {
            MerchantCardEntry => "purchase_shop_card",
            MerchantRelicEntry => "purchase_shop_relic",
            MerchantPotionEntry => "purchase_shop_potion",
            MerchantCardRemovalEntry => "open_shop_card_removal",
            _ => string.Empty
        };
        if (operation.Length == 0)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                entry,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_shop_purchase_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(entry, "shop_offer"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: __state.Scope.ActionWitnessId);
        if (__result != null)
        {
            RecorderRuntime.QueueNativePostCommitBoundary(
                __result,
                NativeActionType,
                nativeOperand: entry,
                expectedActionWitnessId: __state.Scope.ActionWitnessId);
        }
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeShopRoomOpenPatch
{
    private const string NativeActionType = "NMerchantRoom.OpenInventory";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NMerchantRoom),
            nameof(NMerchantRoom.OpenInventory),
            Type.EmptyTypes)
        ?? throw new MissingMethodException(
            typeof(NMerchantRoom).FullName,
            nameof(NMerchantRoom.OpenInventory));

    private static void Prefix(
        NMerchantRoom __instance,
        out NativeUiScopeEntry __state)
    {
        MerchantRoom? room = RunManager.Instance.DebugOnlyGetState()?.CurrentRoom as MerchantRoom;
        __state = room == null || __instance.Inventory.IsOpen
            ? default
            : RecorderRuntime.TryEnterSemanticScope(
                "native_shop_room_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "open",
                    null,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                nativeSemanticSelection: new ProcessLocalObservedAction(
                    "open_shop_inventory",
                    __instance,
                    new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static void Postfix(
        NMerchantRoom __instance,
        NativeUiScopeEntry __state)
    {
        if (!__state.Entered && !__state.DeferredFailure)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "open",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_shop_room_open_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(__instance, "shop_room"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeShopRoomProceedPatch
{
    private const string NativeActionType = "NMerchantRoom.HideScreen";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NMerchantRoom),
            "HideScreen",
            new[] { typeof(NButton) })
        ?? throw new MissingMethodException(
            typeof(NMerchantRoom).FullName,
            "HideScreen");

    private static void Prefix(
        NMerchantRoom __instance,
        out NativeUiScopeEntry __state)
    {
        MerchantRoom? room = RunManager.Instance.DebugOnlyGetState()?.CurrentRoom as MerchantRoom;
        __state = room == null || __instance.Inventory.IsOpen
            ? default
            : RecorderRuntime.TryEnterSemanticScope(
                "native_shop_room_proceed_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "activate",
                    null,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                nativeSemanticSelection: new ProcessLocalObservedAction(
                    "proceed_shop",
                    __instance,
                    new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static void Postfix(
        NMerchantRoom __instance,
        NativeUiScopeEntry __state)
    {
        if (!__state.Entered && !__state.DeferredFailure)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_shop_room_proceed_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(__instance, "shop_room"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeShopInventoryClosePatch
{
    private const string NativeActionType = "NMerchantInventory.Close";

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NMerchantInventory),
            "Close",
            Type.EmptyTypes)
        ?? throw new MissingMethodException(
            typeof(NMerchantInventory).FullName,
            "Close");

    private static void Prefix(
        NMerchantInventory __instance,
        out NativeUiScopeEntry __state)
    {
        NMerchantRoom? room = NMerchantRoom.Instance;
        __state = room?.Inventory is not { IsOpen: true } current
                  || !ReferenceEquals(current, __instance)
            ? default
            : RecorderRuntime.TryEnterSemanticScope(
                "native_shop_inventory_close_ui",
                NativeActionType,
                new ProcessLocalObservedAction(
                    "cancel",
                    null,
                    new Dictionary<string, object>(StringComparer.Ordinal)),
                nativeSemanticSelection: new ProcessLocalObservedAction(
                    "close_shop_inventory",
                    __instance,
                    new Dictionary<string, object>(StringComparer.Ordinal)));
    }

    private static void Postfix(
        NMerchantInventory __instance,
        NativeUiScopeEntry __state)
    {
        if (!__state.Entered && !__state.DeferredFailure)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "cancel",
                null,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_shop_inventory_close_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(__instance, "shop_inventory"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow));
    }

    private static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

/// <summary>
/// Captures the exact STS2 run-start seam. RunManager.Launch is called after
/// the native RunState has been initialized; it is distinct from a recorder
/// joining an already-running session, which remains an observed-in-progress
/// marker.
/// </summary>
[HarmonyPatch]
internal static class NativeRunStartedPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(RunManager), "Launch")
        ?? throw new MissingMethodException(
            typeof(RunManager).FullName,
            "Launch");

    private static void Postfix() => RecorderRuntime.ObserveNativeRunStarted();
}

/// <summary>
/// Captures the exact STS2 terminal seam.  RunManager.OnEnded is invoked by
/// both native victory and defeat paths; the recorder only projects this
/// observation and never manufactures a successor or settles an action.
/// </summary>
[HarmonyPatch]
internal static class NativeRunEndedPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(RunManager),
            "OnEnded",
            new[] { typeof(bool) })
        ?? throw new MissingMethodException(
            typeof(RunManager).FullName,
            "OnEnded");

    private static void Postfix([HarmonyArgument(0)] bool isVictory) =>
        RecorderRuntime.ObserveNativeRunEnded(isVictory);
}
