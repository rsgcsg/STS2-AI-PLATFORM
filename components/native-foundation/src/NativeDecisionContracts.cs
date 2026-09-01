using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// Supplies process-local opaque identities without making Native Foundation
/// own a public entity registry or transport format.
/// </summary>
public interface INativeReferentIdentity
{
    string GetId(object value, string kind);
}

public sealed record NativeSemanticOperand(
    string Role,
    string ReferentId,
    object NativeValue);

/// <summary>
/// A game-owned action that is legal at a native semantic decision boundary.
/// Native operands stay process-local; consumers project only opaque referents.
/// </summary>
public sealed record NativeSemanticAction(
    string Key,
    string Verb,
    string? SubjectReferentId,
    object? NativeSubject,
    IReadOnlyList<NativeSemanticOperand> Operands,
    string NativeLegalityBasis);

public sealed record NativeObservedSemanticAction(
    string NativeActionType,
    string? Key,
    string Status,
    int MatchCount,
    string Membership,
    string? Detail);

public sealed record NativeCombatDecision(
    string Status,
    string Scope,
    bool IsDecisionOpen,
    IReadOnlyList<NativeSemanticAction> Actions,
    IReadOnlyList<string> Evidence,
    string? Detail)
{
    public NativeObservedSemanticAction Describe(
        GameAction action,
        INativeReferentIdentity identities) =>
        NativeCombatDecisionProvider.Describe(action, this, identities);
}

public sealed record NativeMapDecision(
    string Status,
    string Scope,
    bool IsDecisionOpen,
    IReadOnlyList<NativeSemanticAction> Actions,
    IReadOnlyList<string> Evidence,
    string? Detail);

public sealed record NativeRewardDecision(
    string Status,
    string Scope,
    bool IsDecisionOpen,
    bool IsTerminal,
    IReadOnlyList<Reward> Rewards,
    IReadOnlyList<NativeSemanticAction> Actions,
    IReadOnlyList<string> Evidence,
    string? Detail);

public sealed record NativeCardRewardDecision(
    string Status,
    string Scope,
    bool IsDecisionOpen,
    IReadOnlyList<NativeSemanticAction> Actions,
    IReadOnlyList<string> Evidence,
    string? Detail);

public sealed record NativeTreasureDecision(
    string Status,
    string Scope,
    string Stage,
    bool ChestOpened,
    bool IsDecisionOpen,
    IReadOnlyList<RelicModel> Relics,
    IReadOnlyList<NativeSemanticAction> Actions,
    IReadOnlyList<string> Evidence,
    string? Detail);

public static class NativeDecisionProjection
{
    public static IReadOnlyList<NativeSemanticAction> VisibleSubjects(
        NativeCombatDecision decision,
        string verb,
        IEnumerable<object> visibleSubjects) =>
        VisibleSubjects(decision.Actions, verb, visibleSubjects);

    public static IReadOnlyList<NativeSemanticAction> VisibleSubjects(
        IEnumerable<NativeSemanticAction> actions,
        string verb,
        IEnumerable<object> visibleSubjects)
    {
        var visible = new HashSet<object>(
            visibleSubjects,
            ReferenceEqualityComparer.Instance);
        return actions
            .Where(action => action.Verb == verb
                && action.NativeSubject != null
                && visible.Contains(action.NativeSubject))
            .OrderBy(action => action.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool HasExactReferenceBijection<T>(
        IEnumerable<T> semanticSubjects,
        IEnumerable<T> presentationSubjects)
        where T : class
    {
        var counts = new Dictionary<T, int>(ReferenceEqualityComparer.Instance);
        foreach (T subject in semanticSubjects)
        {
            if (!counts.TryAdd(subject, 1))
                return false;
        }
        foreach (T subject in presentationSubjects)
        {
            if (!counts.TryGetValue(subject, out int count) || count != 1)
                return false;
            counts[subject] = 0;
        }
        return counts.Values.All(count => count == 0);
    }
}

/// <summary>
/// Mechanical operations over a native semantic action catalog. This helper
/// neither discovers legality nor binds presentation controls.
/// </summary>
public static class NativeSemanticActionCatalog
{
    public static string BuildKey(
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

    public static bool ContainsExactlyOnce(
        IEnumerable<NativeSemanticAction> actions,
        string verb) =>
        actions.Count(action => action.Verb == verb) == 1;

    public static bool ContainsExactlyOnce(
        IEnumerable<NativeSemanticAction> actions,
        string verb,
        object subject) =>
        actions.Count(action =>
            action.Verb == verb && ReferenceEquals(action.NativeSubject, subject)) == 1;

    public static IReadOnlyList<T> Subjects<T>(
        IEnumerable<NativeSemanticAction> actions,
        string verb)
        where T : class =>
        actions
            .Where(action => action.Verb == verb)
            .Select(action => action.NativeSubject)
            .OfType<T>()
            .ToArray();
}

public sealed record NativePlayerChoiceLineage(
    string Status,
    GameAction? ParentAction,
    string? ParentActionType,
    string? ParentState)
{
    public static NativePlayerChoiceLineage Capture()
    {
        try
        {
            GameAction? parent = MegaCrit.Sts2.Core.Runs.RunManager.Instance
                .ActionExecutor.CurrentlyRunningAction;
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
