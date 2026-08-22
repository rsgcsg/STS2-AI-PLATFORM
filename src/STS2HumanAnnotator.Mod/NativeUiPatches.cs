using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace STS2HumanAnnotator.Mod;

[HarmonyPatch]
internal static class NativeCardPlayPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(NCardPlay), "TryPlayCard")
        ?? throw new MissingMethodException(typeof(NCardPlay).FullName, "TryPlayCard");

    internal static void Prefix(out bool __state) =>
        __state = RecorderRuntime.TryEnterScope("native_card_play_ui");

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
        __state = RecorderRuntime.TryEnterScope("native_end_turn_ui");

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
        __state = RecorderRuntime.TryEnterScope("native_ftue_end_turn_ui");

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
