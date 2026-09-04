using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// Projects the exact one-of-N relic choice owned by STS2's
/// <c>RelicSelectCmd.FromChooseARelicScreen</c>.  The screen is only the
/// presentation owner; PlayerChoiceSynchronizer remains the native choice
/// Commit owner.
/// </summary>
public static class NativeBossRelicDecisionProvider
{
    public const string SelectVerb = "select_boss_relic";
    public const string SkipVerb = "skip_boss_relic";
    public const string ScreenOwner = "NChooseARelicSelection";
    public const string ParentCommand = "RelicSelectCmd.FromChooseARelicScreen";
    public const string CompletionSeam = "NChooseARelicSelection.RelicsSelected";
    public const string CommitSeam = "PlayerChoiceSynchronizer.SyncLocalChoice";

    /// <summary>
    /// The next continuation is deliberately descriptive rather than a
    /// claimed successor.  The command resumes its parent after the choice;
    /// this provider must not infer a later room or act transition.
    /// </summary>
    public const string NextBoundary =
        "parent PlayerChoice continuation after SyncLocalChoice (not inferred)";

    public static NativeBossRelicDecision Capture(
        NChooseARelicSelection screen,
        IReadOnlyList<RelicModel> relics,
        INativeReferentIdentity identities)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(relics);
        ArgumentNullException.ThrowIfNull(identities);

        try
        {
            RelicModel[] exactRelics = relics
                .Where(relic => relic != null)
                .ToArray();
            NativePlayerChoiceLineage lineage = NativePlayerChoiceLineage.Capture();
            var actions = exactRelics
                .Select(relic => new NativeSemanticAction(
                    NativeSemanticActionCatalog.BuildKey(
                        SelectVerb,
                        identities.GetId(relic, "relic")),
                    SelectVerb,
                    identities.GetId(relic, "relic"),
                    relic,
                    Array.Empty<NativeSemanticOperand>(),
                    $"{ParentCommand}.relics+NChooseARelicSelection.SelectHolder"))
                .ToList();
            actions.Add(new NativeSemanticAction(
                NativeSemanticActionCatalog.BuildKey(
                    SkipVerb,
                    identities.GetId(screen, "screen")),
                SkipVerb,
                identities.GetId(screen, "screen"),
                screen,
                Array.Empty<NativeSemanticOperand>(),
                "NChooseARelicSelection.OnSkipButtonReleased"));

            return new NativeBossRelicDecision(
                "captured",
                "boss_relic_choice",
                exactRelics.Length > 0,
                exactRelics,
                actions.OrderBy(action => action.Key, StringComparer.Ordinal).ToArray(),
                lineage,
                ScreenOwner,
                ParentCommand,
                CompletionSeam,
                CommitSeam,
                NextBoundary,
                null);
        }
        catch (Exception exception)
        {
            return new NativeBossRelicDecision(
                "capture_failed",
                "boss_relic_choice",
                false,
                Array.Empty<RelicModel>(),
                Array.Empty<NativeSemanticAction>(),
                new NativePlayerChoiceLineage("unavailable", null, null, exception.GetType().Name),
                ScreenOwner,
                ParentCommand,
                CompletionSeam,
                CommitSeam,
                NextBoundary,
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}

public sealed record NativeBossRelicDecision(
    string Status,
    string Scope,
    bool IsDecisionOpen,
    IReadOnlyList<RelicModel> Relics,
    IReadOnlyList<NativeSemanticAction> Actions,
    NativePlayerChoiceLineage ParentLineage,
    string NativeScreenOwner,
    string ParentCommand,
    string CompletionSeam,
    string CommitSeam,
    string NextBoundary,
    string? Detail);
