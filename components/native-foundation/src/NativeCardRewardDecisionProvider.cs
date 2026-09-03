using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// Tracks the exact native option lists supplied to the shipped card reward
/// selector. UI holders and buttons remain delivery bindings only.
/// </summary>
public static class NativeCardRewardDecisionProvider
{
    private sealed record Owner(
        IReadOnlyList<CardCreationResult> Options,
        IReadOnlyList<CardRewardAlternative> Alternatives);

    private static readonly ConditionalWeakTable<NCardRewardSelectionScreen, Owner> Owners = new();

    public static void Register(
        NCardRewardSelectionScreen screen,
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> alternatives)
    {
        Owners.Remove(screen);
        Owners.Add(screen, new Owner(options.ToArray(), alternatives.ToArray()));
    }

    public static NativeCardRewardDecision Capture(
        NCardRewardSelectionScreen screen,
        INativeReferentIdentity identities)
    {
        if (!Owners.TryGetValue(screen, out Owner? owner))
        {
            return Unavailable(
                "owner_not_registered",
                "The exact card reward options were not observed at ShowScreen or RefreshOptions.");
        }

        try
        {
            var actions = new List<NativeSemanticAction>();
            foreach (CardCreationResult option in owner.Options)
            {
                string id = identities.GetId(option.Card, "card");
                actions.Add(new NativeSemanticAction(
                    NativeSemanticActionCatalog.BuildKey("select", id),
                    "select",
                    id,
                    option.Card,
                    Array.Empty<NativeSemanticOperand>(),
                    "NCardRewardSelectionScreen.ShowScreen/RefreshOptions native card options"));
            }
            foreach (CardRewardAlternative alternative in owner.Alternatives)
            {
                string id = identities.GetId(alternative, "card_reward_alternative");
                actions.Add(new NativeSemanticAction(
                    NativeSemanticActionCatalog.BuildKey("activate", id),
                    "activate",
                    id,
                    alternative,
                    Array.Empty<NativeSemanticOperand>(),
                    "NCardRewardSelectionScreen.ShowScreen/RefreshOptions native alternatives"));
            }

            return new NativeCardRewardDecision(
                "captured",
                "card_reward",
                actions.Count > 0,
                actions.ToArray(),
                new[]
                {
                    "NCardRewardSelectionScreen.ShowScreen native options",
                    "NCardRewardSelectionScreen.RefreshOptions native options",
                    "CardCreationResult.Card",
                    "CardRewardAlternative"
                },
                actions.Count == 0 ? "The active card reward has no native option." : null);
        }
        catch (Exception exception)
        {
            return Unavailable(
                "capture_failed",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static NativeCardRewardDecision Unavailable(string status, string detail) =>
        new(
            status,
            "unavailable",
            false,
            Array.Empty<NativeSemanticAction>(),
            Array.Empty<string>(),
            detail);
}
