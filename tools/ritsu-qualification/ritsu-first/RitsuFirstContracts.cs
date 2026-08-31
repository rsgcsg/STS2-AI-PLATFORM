using System;
using System.Collections.Generic;
using System.Linq;

namespace STS2Platform.Qualification.RitsuFirst;

public enum ExperimentalShopStage
{
    Unavailable,
    Room,
    Inventory,
    Resolving
}

public enum ExperimentalShopEntryKind
{
    Card,
    Relic,
    Potion,
    CardRemoval
}

public sealed record ExperimentalShopEntry(
    string ReferentId,
    string ItemId,
    ExperimentalShopEntryKind Kind,
    object NativeEntry,
    int Cost,
    bool IsStocked,
    bool IsAffordable,
    bool NativeCapacityAllowsPurchase,
    string NativeLegalityBasis);

public sealed record ExperimentalShopAction(
    string Key,
    string Verb,
    string? SubjectReferentId,
    object? NativeSubject,
    string NativeLegalityBasis);

public sealed record ExperimentalShopDecision(
    string Status,
    ExperimentalShopStage Stage,
    string? OwnerReferentId,
    int Gold,
    IReadOnlyList<ExperimentalShopEntry> Inventory,
    IReadOnlyList<ExperimentalShopAction> Actions,
    IReadOnlyList<string> Evidence,
    string? Detail);

public sealed record ExperimentalShopProjection(
    string OwnerReferentId,
    object Owner,
    object InventoryOwner,
    ExperimentalShopStage Stage,
    int Gold,
    IReadOnlyList<ExperimentalShopEntry> Entries,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Platform-owned Shop semantics shared by both experimental integration
/// lanes. It consumes already captured STS2 facts and never discovers owners,
/// reads private state, invokes hooks, or performs input.
/// </summary>
public static class ExperimentalShopContract
{
    public static ExperimentalShopDecision Project(ExperimentalShopProjection projection)
    {
        var actions = new List<ExperimentalShopAction>();
        if (projection.Stage == ExperimentalShopStage.Room)
        {
            actions.Add(Action(
                "open",
                projection.OwnerReferentId,
                projection.Owner,
                "current MerchantRoom owner and closed inventory"));
            actions.Add(Action(
                "proceed",
                projection.OwnerReferentId,
                projection.Owner,
                "current MerchantRoom owner and native proceed path"));
        }
        else if (projection.Stage == ExperimentalShopStage.Inventory)
        {
            foreach (ExperimentalShopEntry entry in projection.Entries)
            {
                if (!entry.IsStocked
                    || !entry.IsAffordable
                    || !entry.NativeCapacityAllowsPurchase)
                    continue;
                actions.Add(Action(
                    entry.Kind == ExperimentalShopEntryKind.CardRemoval
                        ? "remove_card"
                        : "purchase",
                    entry.ReferentId,
                    entry.NativeEntry,
                    entry.NativeLegalityBasis));
            }

            actions.Add(Action(
                "close",
                projection.OwnerReferentId,
                projection.InventoryOwner,
                "current open MerchantInventory and native close path"));
        }

        return new ExperimentalShopDecision(
            "captured",
            projection.Stage,
            projection.OwnerReferentId,
            projection.Gold,
            projection.Entries
                .OrderBy(entry => entry.ReferentId, StringComparer.Ordinal)
                .ToArray(),
            actions
                .OrderBy(action => action.Key, StringComparer.Ordinal)
                .ToArray(),
            projection.Evidence,
            actions.Count == 0
                ? $"No Human Shop decision is open during {projection.Stage}."
                : null);
    }

    public static ExperimentalShopDecision Unavailable(string status, string detail) =>
        new(
            status,
            ExperimentalShopStage.Unavailable,
            null,
            0,
            Array.Empty<ExperimentalShopEntry>(),
            Array.Empty<ExperimentalShopAction>(),
            Array.Empty<string>(),
            detail);

    public static string BuildActionKey(string verb, string? subjectReferentId) =>
        $"{verb}|{subjectReferentId ?? "-"}";

    private static ExperimentalShopAction Action(
        string verb,
        string? subjectReferentId,
        object nativeSubject,
        string basis) =>
        new(
            BuildActionKey(verb, subjectReferentId),
            verb,
            subjectReferentId,
            nativeSubject,
            basis);
}

public sealed record ExperimentalLifecycleObservation(
    string Kind,
    object NativeSubject,
    bool IsCommit,
    bool HasExactRootAction,
    bool HasCancelOrAbortDisposition,
    string Source);
