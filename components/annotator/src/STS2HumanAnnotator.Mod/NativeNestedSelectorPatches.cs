using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rewards;
using STS2Connector.PlayerEnvironment.Witness;
using STS2HumanAnnotator.Core;
using STS2Platform.NativeFoundation;

namespace STS2HumanAnnotator.Mod;

/// <summary>
/// Exact process-local lineage for generic card-selection screens. Parent
/// scopes flow only through the exact native async invocation that owns the
/// selector factory. Screens are then keyed by their own native object. No
/// queue position, current overlay, timing, or most-recent root participates.
/// </summary>
internal static class NativeNestedSelectorBindings
{
    internal sealed record Parent(
        string ActionWitnessId,
        object NativeOwner,
        string Family,
        string NativeMechanism);

    internal sealed record Binding(
        string ActionWitnessId,
        object ParentOwner,
        string Family,
        string FactoryMechanism);

    private static readonly ExactAsyncOwnerBindingScope<object, Parent, Binding> Screens = new();

    internal static IDisposable EnterParent(
        string actionWitnessId,
        object nativeOwner,
        string family,
        string nativeMechanism)
    {
        return Screens.Enter(new Parent(
            actionWitnessId,
            nativeOwner,
            family,
            nativeMechanism));
    }

    internal static void Register(object? screen, MethodBase factory)
    {
        if (screen == null)
            return;
        if (Screens.TryBindCurrent(
                screen,
                parent => new Binding(
                    parent.ActionWitnessId,
                    parent.NativeOwner,
                    parent.Family,
                    $"{factory.DeclaringType?.FullName}.{factory.Name}")))
            return;
        NativePlayerChoiceLineage lineage = NativePlayerChoiceLineage.Capture();
        if (lineage.ParentAction is not GameAction action
            || !NativeUiCompletionRootBindings.TryGet(action, out string? actionWitnessId)
            || string.IsNullOrWhiteSpace(actionWitnessId))
        {
            return;
        }
        Screens.Set(
            screen,
            new Binding(
                actionWitnessId,
                action,
                FamilyFor(screen),
                $"{factory.DeclaringType?.FullName}.{factory.Name}"));
    }

    internal static bool TryTake(object screen, out Binding? binding)
    {
        return Screens.TryTake(screen, out binding);
    }

    internal static void Forget(object screen) => Screens.Forget(screen);

    private static string FamilyFor(object screen) => screen switch
    {
        NSimpleCardSelectScreen => "generic_simple_card_selector",
        NDeckCardSelectScreen or NDeckUpgradeSelectScreen
            or NDeckTransformSelectScreen or NDeckEnchantSelectScreen =>
            "generic_deck_card_selector",
        NCombatPileCardSelectScreen => "generic_combat_pile_selector",
        NChooseABundleSelectionScreen => "generic_card_bundle_selector",
        _ => "generic_card_selector"
    };
}

/// <summary>
/// EventOption.Chosen is the exact option-owned outer Task. Its logical async
/// execution context owns any generic selector opened by that option.
/// </summary>
[HarmonyPatch]
internal static class NativeEventNestedSelectorParentPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(EventOption), nameof(EventOption.Chosen), Type.EmptyTypes)
        ?? throw new MissingMethodException(typeof(EventOption).FullName, nameof(EventOption.Chosen));

    private static void Prefix(EventOption __instance, out IDisposable? __state)
    {
        string? actionWitnessId =
            RecorderRuntime.CurrentSemanticActionWitnessId("NEventRoom.OptionButtonClicked");
        if (actionWitnessId == null)
            NativeUiCompletionRootBindings.TryGet(__instance, out actionWitnessId);
        __state = actionWitnessId == null
            ? null
            : NativeNestedSelectorBindings.EnterParent(
                actionWitnessId,
                __instance,
                "event_option.nested_selector",
                "EventOption.Chosen");
    }

    private static Exception? Finalizer(IDisposable? __state, Exception? __exception)
    {
        __state?.Dispose();
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeNestedSelectorFactoryPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return Required(typeof(NSimpleCardSelectScreen), "Create",
            typeof(IReadOnlyList<CardModel>), typeof(MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs));
        yield return Required(typeof(NSimpleCardSelectScreen), "Create",
            typeof(IReadOnlyList<CardCreationResult>),
            typeof(MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs));
        yield return Required(typeof(NCombatPileCardSelectScreen), "Create",
            typeof(CardPile),
            typeof(MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs),
            typeof(Func<CardModel, bool>));
        yield return Required(typeof(NDeckCardSelectScreen), "Create",
            typeof(IReadOnlyList<CardModel>), typeof(MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs));
        yield return Required(typeof(NDeckUpgradeSelectScreen), "ShowScreen",
            typeof(IReadOnlyList<CardModel>),
            typeof(MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs),
            typeof(MegaCrit.Sts2.Core.Runs.IRunState));
        yield return Required(typeof(NDeckTransformSelectScreen), "ShowScreen",
            typeof(IReadOnlyList<CardModel>),
            typeof(Func<CardModel, CardTransformation>),
            typeof(MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs));
        yield return Required(typeof(NDeckEnchantSelectScreen), "ShowScreen",
            typeof(IReadOnlyList<CardModel>),
            typeof(MegaCrit.Sts2.Core.Models.EnchantmentModel),
            typeof(int),
            typeof(MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs));
        yield return Required(typeof(NChooseABundleSelectionScreen), "ShowScreen",
            typeof(IReadOnlyList<IReadOnlyList<CardModel>>));
    }

    private static MethodBase Required(Type type, string name, params Type[] arguments) =>
        AccessTools.Method(type, name, arguments)
        ?? throw new MissingMethodException(type.FullName, name);

    private static void Postfix(object? __result, MethodBase __originalMethod)
    {
        try
        {
            NativeNestedSelectorBindings.Register(__result, __originalMethod);
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report(
                $"{__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name}",
                exception);
        }
    }
}

/// <summary>
/// Patches only native terminal selector callbacks. Preview/cancel-preview
/// methods are intentionally absent. CompletionSource state independently
/// confirms that the callback actually completed the exact child screen.
/// </summary>
[HarmonyPatch]
internal static class NativeNestedSelectorAcceptedPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return Required(typeof(NSimpleCardSelectScreen), "CompleteSelection", Type.EmptyTypes);
        yield return Required(typeof(NCombatPileCardSelectScreen), "CompleteSelection", Type.EmptyTypes);
        yield return Required(typeof(NDeckCardSelectScreen), "CloseSelection", typeof(NButton));
        yield return Required(typeof(NDeckCardSelectScreen), "ConfirmSelection", typeof(NButton));
        yield return Required(typeof(NDeckUpgradeSelectScreen), "CloseSelection", typeof(NButton));
        yield return Required(typeof(NDeckUpgradeSelectScreen), "ConfirmSelection", typeof(NButton));
        yield return Required(typeof(NDeckTransformSelectScreen), "CloseSelection", typeof(NButton));
        yield return Required(typeof(NDeckTransformSelectScreen), "CompleteSelection", typeof(NButton));
        yield return Required(typeof(NDeckEnchantSelectScreen), "CloseSelection", typeof(NButton));
        yield return Required(typeof(NDeckEnchantSelectScreen), "ConfirmSelection", typeof(NButton));
        yield return Required(typeof(NChooseABundleSelectionScreen), "ConfirmSelection", typeof(NButton));
    }

    private static MethodBase Required(Type type, string name, params Type[] arguments) =>
        AccessTools.Method(type, name, arguments)
        ?? throw new MissingMethodException(type.FullName, name);

    private static void Postfix(object __instance, MethodBase __originalMethod)
    {
        try
        {
            if (!TryReadCompletedSelection(__instance, out bool taskCancelled, out object[] selected)
                || !NativeNestedSelectorBindings.TryTake(
                    __instance,
                    out NativeNestedSelectorBindings.Binding? binding)
                || binding == null)
            {
                return;
            }

            bool explicitClose = string.Equals(
                __originalMethod.Name,
                "CloseSelection",
                StringComparison.Ordinal);
            var operands = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int index = 0; index < selected.Length; index++)
                operands[$"selected_{index}"] = selected[index];
            RecorderRuntime.ObserveAcceptedNestedHumanContinuation(
                binding.ActionWitnessId,
                binding.Family,
                explicitClose || taskCancelled ? "cancel" : "select",
                __instance,
                $"{__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name}",
                selected.FirstOrDefault(),
                operands,
                explicitClose || taskCancelled ? "cancelled" : "accepted");
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report(
                $"{__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name}",
                exception);
        }
    }

    private static bool TryReadCompletedSelection(
        object screen,
        out bool cancelled,
        out object[] selected)
    {
        cancelled = false;
        selected = Array.Empty<object>();
        FieldInfo? field = FindField(screen.GetType(), "_completionSource");
        object? source = field?.GetValue(screen);
        object? taskObject = source?.GetType().GetProperty("Task")?.GetValue(source);
        if (taskObject is not Task task || !task.IsCompleted)
            return false;
        cancelled = task.IsCanceled || task.IsFaulted;
        if (cancelled)
            return true;
        object? result = task.GetType().GetProperty("Result")?.GetValue(task);
        selected = Flatten(result).ToArray();
        return true;
    }

    private static IEnumerable<object> Flatten(object? value)
    {
        if (value is not IEnumerable values || value is string)
            yield break;
        foreach (object? item in values)
        {
            if (item is CardModel)
                yield return item;
            else if (item is IEnumerable nested && item is not string)
            {
                foreach (object nestedItem in Flatten(nested))
                    yield return nestedItem;
            }
        }
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            FieldInfo? field = AccessTools.Field(current, name);
            if (field != null)
                return field;
        }
        return null;
    }
}

[HarmonyPatch]
internal static class NativeNestedSelectorExitPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return Required(typeof(NCardGridSelectionScreen), "_ExitTree");
        yield return Required(typeof(NChooseABundleSelectionScreen), "_ExitTree");
    }

    private static MethodBase Required(Type type, string name) =>
        AccessTools.Method(type, name, Type.EmptyTypes)
        ?? throw new MissingMethodException(type.FullName, name);

    private static void Finalizer(object __instance)
    {
        try
        {
            NativeNestedSelectorBindings.Forget(__instance);
        }
        catch (Exception exception)
        {
            NativeUiObservationSafety.Report(
                $"{__instance.GetType().FullName}._ExitTree",
                exception);
        }
    }
}

/// <summary>
/// Exact owner chain for the choices rendered on a card-reward screen.  The
/// screen is registered while CardReward.OnSelect is executing under the
/// reward-claim root; later callbacks use only that exact screen key and the
/// exact alternative index.  A reroll is special: the outer reward Task stays
/// open and therefore cannot be used as its Commit witness.
/// </summary>
internal static class NativeCardRewardAlternativeBindings
{
    private sealed record Parent(
        CardReward Reward,
        string RewardClaimWitnessId,
        Parent? Previous);

    internal sealed record ScreenBinding(
        CardReward Reward,
        string RewardClaimWitnessId,
        IReadOnlyList<CardRewardAlternative> Alternatives);

    internal sealed class RerollBinding
    {
        internal RerollBinding(
            string actionWitnessId,
            CardRewardAlternative alternative)
        {
            ActionWitnessId = actionWitnessId;
            Alternative = alternative;
        }

        internal string ActionWitnessId { get; }
        internal CardRewardAlternative Alternative { get; }
        internal bool Accepted { get; set; }
        internal bool Rerolled { get; set; }
        internal bool Committed { get; set; }
    }

    private sealed class ParentScope : IDisposable
    {
        private readonly Parent? _previous;
        private bool _disposed;

        internal ParentScope(Parent? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Current.Value = _previous;
        }
    }

    private sealed class TaskBinding
    {
        internal TaskBinding(Task<bool> task) => Task = task;

        internal Task<bool> Task { get; }
    }

    private static readonly AsyncLocal<Parent?> Current = new();
    private static readonly ConditionalWeakTable<NCardRewardSelectionScreen, ScreenBinding> Screens = new();
    private static readonly ConditionalWeakTable<CardReward, TaskBinding> Tasks = new();
    private static readonly ConditionalWeakTable<CardReward, RerollBinding> Rerolls = new();

    internal static IDisposable? Enter(CardReward reward, string? rewardClaimWitnessId)
    {
        if (string.IsNullOrWhiteSpace(rewardClaimWitnessId))
            return null;
        Parent? previous = Current.Value;
        Current.Value = new Parent(reward, rewardClaimWitnessId, previous);
        return new ParentScope(previous);
    }

    internal static void RegisterScreen(
        NCardRewardSelectionScreen? screen,
        IReadOnlyList<CardRewardAlternative> alternatives)
    {
        Parent? parent = Current.Value;
        if (screen == null || parent == null)
            return;
        Screens.Remove(screen);
        Screens.Add(
            screen,
            new ScreenBinding(
                parent.Reward,
                parent.RewardClaimWitnessId,
                alternatives.ToArray()));
    }

    internal static void RefreshScreen(
        NCardRewardSelectionScreen screen,
        IReadOnlyList<CardRewardAlternative> alternatives)
    {
        if (!Screens.TryGetValue(screen, out ScreenBinding? binding))
            return;
        Screens.Remove(screen);
        Screens.Add(screen, binding with { Alternatives = alternatives.ToArray() });
    }

    internal static bool TryGetAlternative(
        NCardRewardSelectionScreen screen,
        int index,
        out ScreenBinding? binding,
        out CardRewardAlternative? alternative)
    {
        alternative = null;
        if (!Screens.TryGetValue(screen, out binding)
            || index < 0
            || index >= binding.Alternatives.Count)
            return false;
        alternative = binding.Alternatives[index];
        return true;
    }

    internal static void RememberTask(CardReward reward, Task<bool> task)
    {
        Tasks.Remove(reward);
        Tasks.Add(reward, new TaskBinding(task));
    }

    internal static bool TryGetTask(CardReward reward, out Task<bool>? task)
    {
        task = null;
        return Tasks.TryGetValue(reward, out TaskBinding? binding)
            && (task = binding.Task) != null;
    }

    internal static RerollBinding BeginReroll(
        CardReward reward,
        string actionWitnessId,
        CardRewardAlternative alternative)
    {
        var binding = new RerollBinding(actionWitnessId, alternative);
        Rerolls.Remove(reward);
        Rerolls.Add(reward, binding);
        return binding;
    }

    internal static bool TryGetReroll(CardReward reward, out RerollBinding? binding) =>
        Rerolls.TryGetValue(reward, out binding);

    internal static void EndReroll(CardReward reward, RerollBinding binding)
    {
        if (Rerolls.TryGetValue(reward, out RerollBinding? current)
            && ReferenceEquals(current, binding))
            Rerolls.Remove(reward);
    }
}

[HarmonyPatch]
internal static class NativeCardRewardParentPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(CardReward), "OnSelect", Type.EmptyTypes)
        ?? throw new MissingMethodException(typeof(CardReward).FullName, "OnSelect");

    private static void Prefix(CardReward __instance, out IDisposable? __state)
    {
        string? root = RecorderRuntime.CurrentSemanticActionWitnessId("NRewardButton.OnRelease");
        if (root == null)
            NativeUiCompletionRootBindings.TryGet(__instance, out root);
        __state = NativeCardRewardAlternativeBindings.Enter(__instance, root);
    }

    private static Exception? Finalizer(IDisposable? __state, Exception? __exception)
    {
        __state?.Dispose();
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeCardRewardScreenBindingPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NCardRewardSelectionScreen),
            "ShowScreen",
            new[]
            {
                typeof(IReadOnlyList<CardCreationResult>),
                typeof(IReadOnlyList<CardRewardAlternative>)
            })
        ?? throw new MissingMethodException(
            typeof(NCardRewardSelectionScreen).FullName,
            "ShowScreen");

    private static void Postfix(
        [HarmonyArgument(1)] IReadOnlyList<CardRewardAlternative> alternatives,
        NCardRewardSelectionScreen? __result) =>
        NativeCardRewardAlternativeBindings.RegisterScreen(__result, alternatives);
}

[HarmonyPatch]
internal static class NativeCardRewardScreenRefreshBindingPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NCardRewardSelectionScreen),
            nameof(NCardRewardSelectionScreen.RefreshOptions),
            new[]
            {
                typeof(IReadOnlyList<CardCreationResult>),
                typeof(IReadOnlyList<CardRewardAlternative>)
            })
        ?? throw new MissingMethodException(
            typeof(NCardRewardSelectionScreen).FullName,
            nameof(NCardRewardSelectionScreen.RefreshOptions));

    private static void Postfix(
        NCardRewardSelectionScreen __instance,
        [HarmonyArgument(1)] IReadOnlyList<CardRewardAlternative> alternatives) =>
        NativeCardRewardAlternativeBindings.RefreshScreen(__instance, alternatives);
}

[HarmonyPatch]
internal static class NativeRewardSelectTaskBindingPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(Reward), nameof(Reward.SelectUnsynchronized), Type.EmptyTypes)
        ?? throw new MissingMethodException(
            typeof(Reward).FullName,
            nameof(Reward.SelectUnsynchronized));

    private static void Postfix(Reward __instance, Task<bool> __result)
    {
        if (__instance is CardReward reward && __result != null)
            NativeCardRewardAlternativeBindings.RememberTask(reward, __result);
    }
}

[HarmonyPatch]
internal static class NativeCardRewardAlternativePatch
{
    private const string NativeActionType =
        "NCardRewardSelectionScreen.OnAlternateRewardSelected";

    private readonly record struct PatchState(
        NativeUiScopeEntry Scope,
        NativeCardRewardAlternativeBindings.ScreenBinding? Binding,
        CardRewardAlternative? Alternative,
        NativeCardRewardAlternativeBindings.RerollBinding? Reroll);

    internal static MethodBase TargetMethod() =>
        AccessTools.Method(
            typeof(NCardRewardSelectionScreen),
            "OnAlternateRewardSelected",
            new[] { typeof(int) })
        ?? throw new MissingMethodException(
            typeof(NCardRewardSelectionScreen).FullName,
            "OnAlternateRewardSelected");

    private static void Prefix(
        NCardRewardSelectionScreen __instance,
        [HarmonyArgument(0)] int index,
        out PatchState __state)
    {
        if (!NativeCardRewardAlternativeBindings.TryGetAlternative(
                __instance,
                index,
                out NativeCardRewardAlternativeBindings.ScreenBinding? binding,
                out CardRewardAlternative? alternative)
            || binding == null
            || alternative == null)
        {
            __state = default;
            return;
        }
        bool reroll = string.Equals(alternative.OptionId, "REROLL", StringComparison.Ordinal);
        NativeUiScopeEntry scope = RecorderRuntime.TryEnterSemanticScope(
            "native_card_reward_alternative_ui",
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                alternative,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativePostCommitCompletionExpectation(
                "card_reward_alternative",
                reroll ? "CardReward.Reroll" : "Reward.SelectUnsynchronized",
                NativeOwnerWitnessId: reroll
                    ? NativeWitnessIdentity.Get(binding.Reward, "native_owner")
                    : null,
                NativeOperandWitnessId: NativeWitnessIdentity.Get(
                    alternative,
                    "native_operand")),
            new ProcessLocalObservedAction(
                "activate",
                alternative,
                new Dictionary<string, object>(StringComparer.Ordinal)));
        NativeCardRewardAlternativeBindings.RerollBinding? rerollBinding =
            reroll && scope.ActionWitnessId is { } root
                ? NativeCardRewardAlternativeBindings.BeginReroll(
                    binding.Reward,
                    root,
                    alternative)
                : null;
        __state = new PatchState(scope, binding, alternative, rerollBinding);
    }

    private static void Postfix(
        NCardRewardSelectionScreen __instance,
        [HarmonyArgument(0)] int index,
        PatchState __state)
    {
        if ((!__state.Scope.Entered && !__state.Scope.DeferredFailure)
            || __state.Binding is not { } binding
            || __state.Alternative is not { } alternative)
            return;
        RecorderRuntime.ObserveAcceptedSemanticUiAction(
            NativeActionType,
            new ProcessLocalObservedAction(
                "activate",
                alternative,
                new Dictionary<string, object>(StringComparer.Ordinal)),
            new NativeWitnessEvidence(
                "native_card_reward_alternative_ui",
                NativeActionType,
                NativeWitnessIdentity.Get(alternative, "card_reward_alternative"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["alternative_index"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["reward_claim_root"] = binding.RewardClaimWitnessId
                },
                DateTimeOffset.UtcNow),
            captureImmediatePostCommitBoundary: false,
            actionWitnessId: __state.Scope.ActionWitnessId);

        if (__state.Reroll is { } reroll)
        {
            reroll.Accepted = true;
            TryCommitReroll(binding.Reward, alternative, reroll);
        }
        else if (__state.Scope.ActionWitnessId is { } root
                 && NativeCardRewardAlternativeBindings.TryGetTask(
                     binding.Reward,
                     out Task<bool>? task)
                 && task != null)
        {
            RecorderRuntime.QueueNativePostCommitBoundary(
                (Task)task,
                "Reward.SelectUnsynchronized",
                nativeOperand: alternative,
                expectedActionWitnessId: root);
        }
    }

    internal static void TryCommitReroll(
        CardReward reward,
        CardRewardAlternative alternative,
        NativeCardRewardAlternativeBindings.RerollBinding binding)
    {
        if (!binding.Accepted || !binding.Rerolled || binding.Committed)
            return;
        binding.Committed = true;
        RecorderRuntime.ObserveSemanticUiNativeCommit(
            binding.ActionWitnessId,
            "card_reward_alternative",
            "CardReward.Reroll",
            nativeOwner: reward,
            nativeOperand: alternative);
        NativeCardRewardAlternativeBindings.EndReroll(reward, binding);
    }

    private static Exception? Finalizer(PatchState __state, Exception? __exception)
    {
        if (__state.Binding is { } binding
            && __state.Reroll is { } reroll
            && (__exception != null || !reroll.Accepted))
            NativeCardRewardAlternativeBindings.EndReroll(binding.Reward, reroll);
        RecorderRuntime.ExitNativeUiScope(__state.Scope);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NativeCardRewardRerollCompletionPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(CardReward), nameof(CardReward.Reroll), Type.EmptyTypes)
        ?? throw new MissingMethodException(typeof(CardReward).FullName, nameof(CardReward.Reroll));

    private static void Postfix(CardReward __instance)
    {
        if (!NativeCardRewardAlternativeBindings.TryGetReroll(
                __instance,
                out NativeCardRewardAlternativeBindings.RerollBinding? binding)
            || binding == null)
            return;
        binding.Rerolled = true;
        // If TaskCompletionSource continuations ran inline, acceptance is
        // published by the enclosing callback immediately afterward.  If
        // they ran asynchronously, this call publishes the exact Commit now.
        // The shared exact-object carrier makes either ordering equivalent.
        if (binding.Accepted)
        {
            // The exact alternative is still recoverable from the active
            // screen binding; use the stable REROLL option identity only.
            NativeCardRewardAlternativePatch.TryCommitReroll(
                __instance,
                binding.Alternative,
                binding);
        }
    }
}

[HarmonyPatch]
internal static class NativeCardRemovalRewardNestedSelectorPatch
{
    internal static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(CardRemovalReward), "OnSelect", Type.EmptyTypes)
        ?? throw new MissingMethodException(typeof(CardRemovalReward).FullName, "OnSelect");

    private static void Prefix(CardRemovalReward __instance, out IDisposable? __state)
    {
        string? root = RecorderRuntime.CurrentSemanticActionWitnessId("NRewardButton.OnRelease");
        if (root == null)
            NativeUiCompletionRootBindings.TryGet(__instance, out root);
        __state = root == null
            ? null
            : NativeNestedSelectorBindings.EnterParent(
                root,
                __instance,
                "reward_card_removal.nested_selector",
                "CardRemovalReward.OnSelect");
    }

    private static Exception? Finalizer(IDisposable? __state, Exception? __exception)
    {
        __state?.Dispose();
        return __exception;
    }
}
