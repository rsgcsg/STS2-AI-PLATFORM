using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

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

[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.CallReleaseLogic))]
internal static class NativeEndTurnPatch
{
    internal static void Prefix(out NativeUiScopeEntry __state) =>
        __state = RecorderRuntime.TryEnterScope(
            "native_end_turn_ui",
            nameof(EndPlayerTurnAction));

    internal static Exception? Finalizer(NativeUiScopeEntry __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.SecretEndTurnLogicViaFtue))]
internal static class NativeFtueEndTurnPatch
{
    internal static void Prefix(out NativeUiScopeEntry __state) =>
        __state = RecorderRuntime.TryEnterScope(
            "native_ftue_end_turn_ui",
            nameof(EndPlayerTurnAction));

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
            card != null ? RecorderRuntime.TryEnterGeneratedChoiceCardScope(card) : default,
            CardSelected(__instance),
            card);
    }

    private static void Postfix(
        NChooseACardSelectionScreen __instance,
        PatchState __state)
    {
        if (!__state.WasSelected && CardSelected(__instance)
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
        out PatchState __state) =>
        __state = new PatchState(
            RecorderRuntime.TryEnterGeneratedChoiceSkipScope(),
            ScreenComplete(__instance));

    private static void Postfix(NChooseACardSelectionScreen __instance, PatchState __state)
    {
        if (!__state.WasComplete && ScreenComplete(__instance))
            RecorderRuntime.ObserveGeneratedChoiceSkip();
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}
