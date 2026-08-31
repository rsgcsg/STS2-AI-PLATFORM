using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Platform.NativeFoundation;
using STS2RitsuLib;

namespace STS2Platform.Qualification.RitsuFirst;

/// <summary>
/// An independent Ritsu-first implementation of the existing Platform Combat
/// contract. Ritsu supplies effect/turn lifecycle plumbing; STS2 logical state
/// and validators still supply the complete semantic catalog.
/// </summary>
public static class RitsuFirstCombatProvider
{
    public static NativeCombatDecision Capture(INativeReferentIdentity identities)
    {
        try
        {
            RunState? run = RunManager.Instance.DebugOnlyGetState();
            if (run?.CurrentRoom is not CombatRoom || !CombatManager.Instance.IsInProgress)
                return Unavailable("not_combat", "No active combat exists.");

            Player? player = LocalContext.GetMe(run);
            PlayerCombatState? combat = player?.PlayerCombatState;
            if (player == null || combat == null)
                return Unavailable("combat_state_unavailable", "The local combat state is unavailable.");

            bool decisionOpen = combat.Phase == PlayerTurnPhase.Play
                && CombatManager.Instance.IsPartOfPlayerTurn(player)
                && !CombatManager.Instance.PlayerActionsDisabled
                && !CombatManager.Instance.IsPlayerReadyToEndTurn(player)
                && RunManager.Instance.ActionQueueSynchronizer.CombatState
                    == ActionSynchronizerCombatState.PlayPhase;

            IReadOnlyList<NativeSemanticAction> actions = decisionOpen
                ? BuildActions(player, combat, identities)
                : Array.Empty<NativeSemanticAction>();
            return new NativeCombatDecision(
                "captured",
                decisionOpen ? "combat_play_phase" : "combat_non_decision",
                decisionOpen,
                actions,
                new[]
                {
                    "Ritsu typed combat/effect lifecycle subscriptions",
                    "PlayerCombatState.Hand.Cards",
                    "CardModel.CanPlayTargeting",
                    "PotionModel.IsValidTarget+PassesCustomUsabilityCheck",
                    "Player.CanUseOrRemovePotions",
                    "CombatManager+ActionQueueSynchronizer"
                },
                null);
        }
        catch (Exception exception)
        {
            return Unavailable(
                "capture_failed",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    public static NativeObservedSemanticAction Describe(
        GameAction action,
        NativeCombatDecision decision,
        INativeReferentIdentity identities)
    {
        string? key = TryBuildObservedKey(action, identities);
        int count = key == null ? 0 : decision.Actions.Count(candidate => candidate.Key == key);
        return new NativeObservedSemanticAction(
            action.GetType().Name,
            key,
            key == null ? "not_described" : "described",
            count,
            key == null ? "unknown" : count == 1 ? "exact_once" : count == 0 ? "absent" : "ambiguous",
            key == null ? "The exact native operand no longer resolved." : null);
    }

    private static IReadOnlyList<NativeSemanticAction> BuildActions(
        Player player,
        PlayerCombatState combat,
        INativeReferentIdentity identities)
    {
        var actions = new List<NativeSemanticAction>();
        foreach (CardModel card in combat.Hand.Cards)
        {
            string cardId = identities.GetId(card, "card");
            foreach (Creature? target in CardTargets(card, player.Creature.CombatState!))
            {
                if (!card.CanPlayTargeting(target))
                    continue;
                actions.Add(CreateAction(
                    "play",
                    cardId,
                    card,
                    target,
                    identities,
                    "logical_hand+CardModel.CanPlayTargeting"));
            }
        }

        for (int slot = 0; slot < player.PotionSlots.Count; slot++)
        {
            PotionModel? potion = player.GetPotionAtSlotIndex(slot);
            if (potion == null
                || !player.CanUseOrRemovePotions
                || potion.Usage is not (PotionUsage.CombatOnly or PotionUsage.AnyTime)
                || potion.Owner.Creature.IsDead
                || !potion.PassesCustomUsabilityCheck)
                continue;

            string potionId = identities.GetId(potion, "potion");
            foreach (Creature? target in PotionTargets(potion, player))
            {
                if (!potion.IsValidTarget(target))
                    continue;
                actions.Add(CreateAction(
                    "use",
                    potionId,
                    potion,
                    target,
                    identities,
                    "current_potion_slot+native_potion_usability_and_target_validation"));
            }
        }

        actions.Add(new NativeSemanticAction(
            BuildActionKey("end_turn", null),
            "end_turn",
            null,
            player,
            Array.Empty<NativeSemanticOperand>(),
            "local_player_combat_play_phase"));
        return actions.OrderBy(action => action.Key, StringComparer.Ordinal).ToArray();
    }

    private static NativeSemanticAction CreateAction(
        string verb,
        string subjectId,
        object subject,
        Creature? target,
        INativeReferentIdentity identities,
        string legalityBasis)
    {
        IReadOnlyList<NativeSemanticOperand> operands = target == null
            ? Array.Empty<NativeSemanticOperand>()
            : new[]
            {
                new NativeSemanticOperand(
                    "target",
                    identities.GetId(target, TargetKind(target)),
                    target)
            };
        return new NativeSemanticAction(
            BuildActionKey(
                verb,
                subjectId,
                operands.ToDictionary(
                    value => value.Role,
                    value => value.ReferentId,
                    StringComparer.Ordinal)),
            verb,
            subjectId,
            subject,
            operands,
            legalityBasis);
    }

    private static IEnumerable<Creature?> CardTargets(CardModel card, ICombatState combat) =>
        card.TargetType switch
        {
            TargetType.AnyEnemy => combat.HittableEnemies.Cast<Creature?>(),
            TargetType.AnyAlly => combat.PlayerCreatures
                .Where(creature => creature.IsAlive)
                .Cast<Creature?>(),
            _ => new Creature?[] { null }
        };

    private static IEnumerable<Creature?> PotionTargets(PotionModel potion, Player player)
    {
        ICombatState combat = player.Creature.CombatState!;
        return potion.TargetType switch
        {
            TargetType.AnyEnemy => combat.HittableEnemies.Cast<Creature?>(),
            TargetType.AnyAlly or TargetType.AnyPlayer => combat.PlayerCreatures
                .Where(creature => creature.IsAlive)
                .Cast<Creature?>(),
            TargetType.Self => new Creature?[] { player.Creature },
            _ => new Creature?[] { null }
        };
    }

    private static string TargetKind(Creature target) =>
        target.IsPlayer
            ? "player"
            : target.CombatState?.PlayerCreatures.Contains(target) == true
                ? "companion"
                : "enemy";

    private static string? TryBuildObservedKey(
        GameAction action,
        INativeReferentIdentity identities)
    {
        if (action is PlayCardAction play)
        {
            CardModel? card = play.NetCombatCard.ToCardModelOrNull();
            return card == null
                ? null
                : ObservedKey("play", card, play.Target, identities);
        }
        if (action is UsePotionAction use)
        {
            PotionModel? potion = use.Player.GetPotionAtSlotIndex((int)use.PotionIndex);
            if (potion == null)
                return null;
            Creature? target = use.Player.Creature.CombatState?.GetCreature(use.TargetId);
            if (target == null && potion.TargetType is TargetType.Self or TargetType.AnyPlayer)
                target = use.Player.Creature;
            return ObservedKey("use", potion, target, identities);
        }
        return action is EndPlayerTurnAction
            ? BuildActionKey("end_turn", null)
            : null;
    }

    private static string ObservedKey(
        string verb,
        object subject,
        Creature? target,
        INativeReferentIdentity identities)
    {
        string subjectKind = verb == "play" ? "card" : "potion";
        IReadOnlyDictionary<string, string> arguments = target == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = identities.GetId(target, TargetKind(target))
            };
        return BuildActionKey(verb, identities.GetId(subject, subjectKind), arguments);
    }

    private static string BuildActionKey(
        string verb,
        string? subjectReferentId,
        IReadOnlyDictionary<string, string>? arguments = null)
    {
        string operands = arguments == null
            ? string.Empty
            : string.Join(",", arguments
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"));
        return $"{verb}|{subjectReferentId ?? "-"}|{operands}";
    }

    private static NativeCombatDecision Unavailable(string status, string detail) =>
        new(
            status,
            "unavailable",
            false,
            Array.Empty<NativeSemanticAction>(),
            Array.Empty<string>(),
            detail);
}

/// <summary>
/// Ritsu has no public parent/continuation API for arbitrary vanilla
/// PlayerChoice actions. The smallest correct Ritsu-first escape hatch remains
/// the exact STS2 ActionExecutor root.
/// </summary>
public static class RitsuFirstPlayerChoiceProvider
{
    public static NativePlayerChoiceLineage Capture()
    {
        try
        {
            GameAction? parent = RunManager.Instance.ActionExecutor.CurrentlyRunningAction;
            return parent == null
                ? new NativePlayerChoiceLineage("no_parent", null, null, null)
                : new NativePlayerChoiceLineage(
                    "parent_observed",
                    parent,
                    parent.GetType().Name,
                    parent.State.ToString().ToLowerInvariant());
        }
        catch (Exception exception)
        {
            return new NativePlayerChoiceLineage(
                "unavailable",
                null,
                null,
                exception.GetType().Name);
        }
    }
}

/// <summary>
/// Shows the boundary between reusable Ritsu effect events and the exact
/// vanilla root lifecycle that Platform evidence still has to observe.
/// </summary>
public sealed class RitsuFirstCombatLifecycleProbe : IDisposable
{
    private readonly Action<ExperimentalLifecycleObservation> _observer;
    private readonly List<IDisposable> _subscriptions = new();

    public RitsuFirstCombatLifecycleProbe(Action<ExperimentalLifecycleObservation> observer)
    {
        _observer = observer;
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CardPlayingEvent>(
            value => Observe("card_playing", value.CardPlay, false), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CardPlayedEvent>(
            value => Observe("card_played", value.CardPlay, true), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<PotionUsingEvent>(
            value => Observe("potion_using", value.Potion, false), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<PotionUsedEvent>(
            value => Observe("potion_used", value.Potion, true), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<SideTurnEndingEvent>(
            value => Observe("turn_ending", value.CombatState, false), false));
        _subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<SideTurnEndedEvent>(
            value => Observe("turn_ended", value.CombatState, true), false));
    }

    public IDisposable ObserveExactRoot(GameAction action) =>
        new ExactRootLifecycleLease(action, _observer);

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
    }

    private void Observe(string kind, object nativeSubject, bool isCommit) =>
        _observer(new ExperimentalLifecycleObservation(
            kind,
            nativeSubject,
            isCommit,
            HasExactRootAction: false,
            HasCancelOrAbortDisposition: false,
            "Ritsu typed lifecycle"));

    private sealed class ExactRootLifecycleLease : IDisposable
    {
        private readonly GameAction _action;
        private readonly Action<ExperimentalLifecycleObservation> _observer;
        private bool _disposed;

        public ExactRootLifecycleLease(
            GameAction action,
            Action<ExperimentalLifecycleObservation> observer)
        {
            _action = action;
            _observer = observer;
            _action.BeforeExecuted += OnStarted;
            _action.BeforePausedForPlayerChoice += OnPaused;
            _action.BeforeReadyToResumeAfterPlayerChoice += OnReady;
            _action.BeforeResumedAfterPlayerChoice += OnResumed;
            _action.BeforeCancelled += OnCancelled;
            _action.AfterFinished += OnFinished;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _action.BeforeExecuted -= OnStarted;
            _action.BeforePausedForPlayerChoice -= OnPaused;
            _action.BeforeReadyToResumeAfterPlayerChoice -= OnReady;
            _action.BeforeResumedAfterPlayerChoice -= OnResumed;
            _action.BeforeCancelled -= OnCancelled;
            _action.AfterFinished -= OnFinished;
        }

        private void OnStarted(GameAction value) => Observe("root_started", value, false, false);
        private void OnPaused(GameAction value) => Observe("root_paused", value, false, false);
        private void OnReady(GameAction value) => Observe("root_ready", value, false, false);
        private void OnResumed(GameAction value) => Observe("root_resumed", value, false, false);
        private void OnCancelled(GameAction value) => Observe("root_cancelled", value, false, true);
        private void OnFinished(GameAction value) => Observe("root_finished", value, true, true);

        private void Observe(
            string kind,
            GameAction value,
            bool isCommit,
            bool hasDisposition) =>
            _observer(new ExperimentalLifecycleObservation(
                kind,
                value,
                isCommit,
                HasExactRootAction: true,
                HasCancelOrAbortDisposition: hasDisposition,
                "direct STS2 GameAction escape hatch"));
    }
}
