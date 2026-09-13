using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Platform.NativeFoundation;

/// <summary>Execution-time discard operands from the native potion belt.
/// Popup lifetime and underlying room overlays do not own this queued action.</summary>
public static class NativePotionDiscardDecisionProvider
{
    public static NativePotionDiscardDecision Capture(INativeReferentIdentity identities)
    {
        var run = RunManager.Instance.DebugOnlyGetState();
        var player = run == null ? null : LocalContext.GetMe(run);
        if (player == null)
            return new("player_unavailable", "unavailable", false,
                Array.Empty<NativeSemanticAction>(), Array.Empty<string>(), "No exact local player.");
        var actions = CaptureSlots(player.PotionSlots, identities);
        return new("captured", "potion_belt_discard", actions.Count > 0, actions,
            new[] { "DiscardPotionGameAction.ExecuteAction:current_PotionSlots_non_null", "PotionCmd.Discard" }, null);
    }

    internal static IReadOnlyList<NativeSemanticAction> CaptureSlots(
        IReadOnlyList<PotionModel?> slots, INativeReferentIdentity identities) =>
        slots.Where(potion => potion != null).Select(potion => {
            string id = identities.GetId(potion!, "potion");
            return new NativeSemanticAction(NativeSemanticActionCatalog.BuildKey("discard", id),
                "discard", id, potion, Array.Empty<NativeSemanticOperand>(),
                "DiscardPotionGameAction.ExecuteAction:current_PotionSlots_non_null");
        }).ToArray();
}

public sealed record NativePotionDiscardDecision(
    string Status, string Scope, bool IsDecisionOpen,
    IReadOnlyList<NativeSemanticAction> Actions, IReadOnlyList<string> Evidence, string? Detail);
