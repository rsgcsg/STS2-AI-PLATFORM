using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
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
    private const BindingFlags NativeMethodFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly object Gate = new();
    private static readonly List<PendingChoice> PendingChoices = new();

    public const string SelectVerb = "select_boss_relic";
    public const string SkipVerb = "skip_boss_relic";
    public const string ScreenOwner = "NChooseARelicSelection";
    public const string ParentCommand = "RelicSelectCmd.FromChooseARelicScreen";
    public const string CompletionSeam = "NChooseARelicSelection.RelicsSelected";
    public const string CommitSeam = "PlayerChoiceSynchronizer.SyncLocalChoice";
    public const string SkipSeam = "NChooseARelicSelection.OnSkipButtonReleased";

    public static bool HasExactSkipPath =>
        typeof(NChooseARelicSelection).GetMethod(
            "OnSkipButtonReleased",
            NativeMethodFlags) != null;

    /// <summary>
    /// The next continuation is deliberately descriptive rather than a
    /// claimed successor.  The command resumes its parent after the choice;
    /// this provider must not infer a later room or act transition.
    /// </summary>
    public const string NextBoundary =
        "parent PlayerChoice continuation after SyncLocalChoice (not inferred)";

    /// <summary>
    /// Called by the exact-version composition patch before the native command
    /// enters its async body. The argument list is the command's own option
    /// list; a screen's visible holders are never used as semantic discovery.
    /// </summary>
    public static void RegisterFromChooseARelicScreen(
        Player player,
        IReadOnlyList<RelicModel> relics)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(relics);

        if (!LocalContext.IsMe(player))
            return;

        NativePlayerChoiceLineage lineage = NativePlayerChoiceLineage.Capture();
        var pending = new PendingChoice(
            new WeakReference<IReadOnlyList<RelicModel>>(relics),
            relics.ToArray(),
            player,
            lineage);
        lock (Gate)
        {
            RemoveCollectedChoices();
            PendingChoices.RemoveAll(candidate =>
                candidate.Relics.TryGetTarget(out IReadOnlyList<RelicModel>? value)
                && ReferenceEquals(value, relics));
            PendingChoices.Add(pending);
            if (PendingChoices.Count > 16)
                PendingChoices.RemoveRange(0, PendingChoices.Count - 16);
        }
    }

    public static NativeBossRelicDecision Capture(
        NChooseARelicSelection screen,
        IReadOnlyList<RelicModel> nativeRelics,
        INativeReferentIdentity identities)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(nativeRelics);
        ArgumentNullException.ThrowIfNull(identities);

        try
        {
            if (!TryGetCurrentChoice(nativeRelics, out PendingChoice? pending, out string detail))
                return Failed(detail);

            PendingChoice request = pending!;
            RelicModel[] exactRelics = request.Options;
            if (exactRelics.Length == 0
                || exactRelics.Any(relic => relic == null)
                || exactRelics.Distinct(ReferenceEqualityComparer.Instance).Count()
                    != exactRelics.Length)
            {
                return Failed(
                    "The command supplied an empty, null, or duplicate native relic option set.");
            }

            NativePlayerChoiceLineage lineage = request.Lineage;
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
            if (HasExactSkipPath)
            {
                actions.Add(new NativeSemanticAction(
                    NativeSemanticActionCatalog.BuildKey(
                        SkipVerb,
                        identities.GetId(screen, "screen")),
                    SkipVerb,
                    identities.GetId(screen, "screen"),
                    screen,
                    Array.Empty<NativeSemanticOperand>(),
                    SkipSeam));
            }

            return new NativeBossRelicDecision(
                "captured",
                "boss_relic_choice",
                exactRelics.Length > 0,
                exactRelics,
                actions.OrderBy(action => action.Key, StringComparer.Ordinal).ToArray(),
                lineage,
                HasExactSkipPath,
                ScreenOwner,
                ParentCommand,
                CompletionSeam,
                CommitSeam,
                NextBoundary,
                null);
        }
        catch (Exception exception)
        {
            return Failed($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>
    /// Revalidates the command-owned option list and parent action immediately
    /// before a UI control is clicked. The caller must pass the current native
    /// screen list, not a previously projected presentation list.
    /// </summary>
    public static bool ValidateCurrentExecution(
        IReadOnlyList<RelicModel> nativeRelics,
        RelicModel? expectedRelic,
        bool requireSkip,
        out string detail)
    {
        if (!TryGetCurrentChoice(nativeRelics, out PendingChoice? pending, out detail))
            return false;
        if (requireSkip && !HasExactSkipPath)
        {
            detail = "The exact native skip handler is unavailable.";
            return false;
        }
        PendingChoice request = pending!;
        if (expectedRelic != null
            && request.Options.Count(relic => ReferenceEquals(relic, expectedRelic)) != 1)
        {
            detail = "The requested relic is not an exact current command option.";
            return false;
        }

        detail = "exact native relic membership and PlayerChoice parent are current";
        return true;
    }

    private static bool TryGetCurrentChoice(
        IReadOnlyList<RelicModel> nativeRelics,
        out PendingChoice? pending,
        out string detail)
    {
        pending = null;
        lock (Gate)
        {
            RemoveCollectedChoices();
            pending = PendingChoices.FirstOrDefault(candidate =>
                candidate.Relics.TryGetTarget(out IReadOnlyList<RelicModel>? value)
                && ReferenceEquals(value, nativeRelics));
        }
        if (pending == null)
        {
            detail = "The current screen list was not registered by RelicSelectCmd.FromChooseARelicScreen.";
            return false;
        }
        PendingChoice request = pending;
        if (request.Lineage.Status != "parent_observed"
            || request.Lineage.ParentAction == null)
        {
            detail = "The exact RelicSelectCmd PlayerChoice parent is unavailable.";
            return false;
        }
        try
        {
            if (!LocalContext.IsMe(request.Player)
                || request.Player.RunState.Players.Count != 1)
            {
                detail = "The registered relic command is not the local single-player choice.";
                return false;
            }
        }
        catch (Exception exception)
        {
            detail = $"The local single-player command owner is unavailable: {exception.GetType().Name}.";
            return false;
        }

        NativePlayerChoiceLineage current = NativePlayerChoiceLineage.Capture();
        if (current.ParentAction == null
            || !ReferenceEquals(current.ParentAction, request.Lineage.ParentAction))
        {
            detail = "The current PlayerChoice parent no longer matches RelicSelectCmd.";
            return false;
        }
        if (request.Options.Length != nativeRelics.Count
            || request.Options.Where((relic, index) =>
                !ReferenceEquals(relic, nativeRelics[index])).Any())
        {
            detail = "The command-owned relic option list changed while the screen was open.";
            return false;
        }
        detail = string.Empty;
        return true;
    }

    private static NativeBossRelicDecision Failed(string detail) =>
        new(
            "capture_failed",
            "boss_relic_choice",
            false,
            Array.Empty<RelicModel>(),
            Array.Empty<NativeSemanticAction>(),
            new NativePlayerChoiceLineage("unavailable", null, null, detail),
            false,
            ScreenOwner,
            ParentCommand,
            CompletionSeam,
            CommitSeam,
            NextBoundary,
            detail);

    private static void RemoveCollectedChoices() =>
        PendingChoices.RemoveAll(candidate => !candidate.Relics.TryGetTarget(out _));

    private sealed record PendingChoice(
        WeakReference<IReadOnlyList<RelicModel>> Relics,
        RelicModel[] Options,
        Player Player,
        NativePlayerChoiceLineage Lineage);
}

public sealed record NativeBossRelicDecision(
    string Status,
    string Scope,
    bool IsDecisionOpen,
    IReadOnlyList<RelicModel> Relics,
    IReadOnlyList<NativeSemanticAction> Actions,
    NativePlayerChoiceLineage ParentLineage,
    bool SkipPathProven,
    string NativeScreenOwner,
    string ParentCommand,
    string CompletionSeam,
    string CommitSeam,
    string NextBoundary,
    string? Detail);
