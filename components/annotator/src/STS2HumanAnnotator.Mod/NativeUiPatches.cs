using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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
        out bool __state)
    {
        __state = __instance.Holder.CardModel is { } card
            && RecorderRuntime.TryEnterCardScope(card, target);
    }

    internal static Exception? Finalizer(bool __state, Exception? __exception)
    {
        if (__state)
            HumanActionScope.Exit();
        return __exception;
    }
}

[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.CallReleaseLogic))]
internal static class NativeEndTurnPatch
{
    internal static void Prefix(out bool __state) =>
        __state = RecorderRuntime.TryEnterScope(
            "native_end_turn_ui",
            nameof(EndPlayerTurnAction));

    internal static Exception? Finalizer(bool __state, Exception? __exception)
    {
        if (__state)
            HumanActionScope.Exit();
        return __exception;
    }
}

[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.SecretEndTurnLogicViaFtue))]
internal static class NativeFtueEndTurnPatch
{
    internal static void Prefix(out bool __state) =>
        __state = RecorderRuntime.TryEnterScope(
            "native_ftue_end_turn_ui",
            nameof(EndPlayerTurnAction));

    internal static Exception? Finalizer(bool __state, Exception? __exception)
    {
        if (__state)
            HumanActionScope.Exit();
        return __exception;
    }
}

[HarmonyPatch(typeof(ActionQueueSynchronizer), nameof(ActionQueueSynchronizer.RequestEnqueue))]
internal static class AcceptedGameActionPatch
{
    internal static void Postfix([HarmonyArgument(0)] GameAction action) =>
        RecorderRuntime.ObserveAcceptedAction(action);
}

[HarmonyPatch]
internal static class NativeGeneratedChoiceCardPatch
{
    private readonly record struct PatchState(bool Entered, bool WasSelected, CardModel? Card);

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
            card != null && RecorderRuntime.TryEnterGeneratedChoiceCardScope(card),
            CardSelected(__instance),
            card);
    }

    private static void Postfix(
        NChooseACardSelectionScreen __instance,
        PatchState __state)
    {
        if (__state.Entered && !__state.WasSelected && CardSelected(__instance)
            && __state.Card is { } card)
            RecorderRuntime.ObserveGeneratedChoiceCard(card);
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        if (__state.Entered)
            HumanActionScope.Exit();
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeGeneratedChoiceSkipPatch
{
    private readonly record struct PatchState(bool Entered, bool WasComplete);

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
        if (__state.Entered && !__state.WasComplete && ScreenComplete(__instance))
            RecorderRuntime.ObserveGeneratedChoiceSkip();
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        if (__state.Entered)
            HumanActionScope.Exit();
        return __exception;
    }
}
