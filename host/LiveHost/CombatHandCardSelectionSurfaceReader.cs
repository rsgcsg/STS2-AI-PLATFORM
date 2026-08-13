using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

/// <summary>
/// Exact-build adapter for the in-hand selector used by combat card effects.
/// It remains distinct from pile/grid selectors because replacement, selected
/// card rendering, peek mode, and completion are owned by NPlayerHand.
/// </summary>
internal sealed class CombatHandCardSelectionSurfaceReader : ILiveSurfaceReader
{
    internal const string ConfirmDeliveryEvidence = "native_combat_hand_confirm_clicked";

    private const string SurfaceKind = "combat_hand_card_selection";
    private const string ReflectionEvidence =
        "sts2-v0.109.0:cached_reflection:NPlayerHand._prefs+_selectedCards";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo? PreferencesField =
        typeof(NPlayerHand).GetField("_prefs", Flags);
    private static readonly FieldInfo? SelectedCardsField =
        typeof(NPlayerHand).GetField("_selectedCards", Flags);
    private static readonly FieldInfo? ClickableField =
        typeof(NCardHolder).GetField("_isClickable", Flags);

    public string Kind => SurfaceKind;

    public InputOwnerLayer Layer => InputOwnerLayer.Room;

    public LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        NPlayerHand? hand = NPlayerHand.Instance;
        if (hand?.IsInCardSelection != true)
            return null;

        return Build(hand, entities, game);
    }

    private static LiveObservation Build(
        NPlayerHand hand,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        ILiveContext context = LiveContextReader.Build(entities);
        if (context is not CombatLiveContext)
        {
            return BindingUnavailable(
                game,
                context,
                "The combat-hand selector is visible without a current combat context.",
                new[] { "combat_context", "legal_actions" });
        }

        if (!TryReadBinding(hand, out Binding? binding, out string? bindingError)
            || ClickableField == null)
        {
            return BindingUnavailable(
                game,
                context,
                bindingError ?? "The exact combat-hand selection binding is unavailable.",
                new[] { "selection_constraints", "selected_cards", "card_selectability", "legal_actions" });
        }

        Binding exact = binding!;
        if (exact.Preferences.MinSelect < 0
            || exact.Preferences.MaxSelect < exact.Preferences.MinSelect)
        {
            return BindingUnavailable(
                game,
                context,
                "The combat-hand selection constraints are invalid.",
                new[] { "selection_constraints", "legal_actions" });
        }

        string? prompt = ReadNodeText(hand, "%SelectionHeader");
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return BindingUnavailable(
                game,
                context,
                "The player-visible combat-hand selection prompt is unavailable.",
                new[] { "prompt", "legal_actions" });
        }

        if (!SelectedCardsMatchVisibleSelection(hand, exact.Mode, exact.SelectedCards))
        {
            return BindingUnavailable(
                game,
                context,
                "Selected combat-hand cards do not match the visible selected-card UI.",
                new[] { "selected_cards", "legal_actions" });
        }

        IReadOnlyList<NHandCardHolder> activeHolders = hand.ActiveHolders
            .Where(holder => holder.CardModel != null && ConnectorMod.IsNodeVisible(holder))
            .ToArray();
        var cards = new List<VisibleCard>(activeHolders.Count + exact.SelectedCards.Count);
        var cardIds = new Dictionary<CardModel, string>();
        foreach (NHandCardHolder holder in activeHolders)
        {
            CardModel card = holder.CardModel!;
            string id = entities.GetId(card, "card");
            cardIds[card] = id;
            cards.Add(LiveContextReader.BuildCard(card, id));
        }
        foreach (CardModel card in exact.SelectedCards)
        {
            string id = entities.GetId(card, "card");
            cardIds[card] = id;
            cards.Add(LiveContextReader.BuildCard(card, id, selected: true));
        }

        if (cards.Select(card => card.EntityId).Distinct(StringComparer.Ordinal).Count() != cards.Count)
        {
            return BindingUnavailable(
                game,
                context,
                "A combat-hand card appeared in both selectable and selected UI regions.",
                new[] { "card_membership", "legal_actions" });
        }

        string[] selectedIds = exact.SelectedCards
            .Select(card => cardIds[card])
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        NConfirmButton? confirm = hand.GetNodeOrNull<NConfirmButton>("%SelectModeConfirmButton");
        bool requireManualConfirmation = ResolveManualConfirmationRequirement(
            exact.Preferences.RequireManualConfirmation,
            confirm != null && ConnectorMod.IsNodeVisible(confirm));
        bool isPeeking = hand.PeekButton.IsPeeking;
        string[] selectableCardIds = isPeeking
            ? Array.Empty<string>()
            : activeHolders
                .Where(holder => IsHolderClickable(holder)
                                 && !IsSelected(hand, holder.CardModel!))
                .Select(holder => cardIds[holder.CardModel!])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        NSelectedHandCardContainer? selectedContainer = exact.Mode == NPlayerHand.Mode.SimpleSelect
            ? hand.GetNodeOrNull<NSelectedHandCardContainer>("%SelectedHandCardContainer")
            : null;
        string[] deselectableCardIds = isPeeking
            ? Array.Empty<string>()
            : selectedContainer?.Holders
                .Where(holder => IsHolderClickable(holder) && holder.CardModel != null)
                .Select(holder => cardIds[holder.CardModel!])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray()
              ?? Array.Empty<string>();
        bool canConfirm = !isPeeking
                          && confirm is { IsEnabled: true }
                          && ConnectorMod.IsNodeVisible(confirm);
        bool canClosePeek = isPeeking
                            && hand.PeekButton.IsEnabled
                            && ConnectorMod.IsNodeVisible(hand.PeekButton);
        var surface = new CombatHandCardSelectionSurface(
            SurfaceKind,
            entities.GetId(hand, "hand"),
            prompt,
            ModeName(exact.Mode),
            exact.Preferences.MinSelect,
            exact.Preferences.MaxSelect,
            exact.SelectedCards.Count,
            selectedIds,
            requireManualConfirmation,
            isPeeking,
            selectableCardIds,
            deselectableCardIds,
            canConfirm,
            canClosePeek,
            cards);
        bool hasCurrentCommand = selectableCardIds.Length > 0
                                 || deselectableCardIds.Length > 0
                                 || canConfirm
                                 || canClosePeek;
        string readiness = hasCurrentCommand ? "ready" : "settling";
        var completeness = new StateCompleteness(
            "contract_complete_for_combat_hand_card_selection",
            hasCurrentCommand
                ? "derived_from_exact_visible_hand_selection_and_current_controls"
                : "temporarily_empty_while_selection_completes_or_settles",
            new[]
            {
                "NPlayerHand.IsInCardSelection+CurrentMode+ActiveHolders",
                "NPlayerHand.%SelectionHeader+%SelectModeConfirmButton",
                "NSelectedHandCardContainer or NUpgradePreview selected-card representation",
                "NCardHolder._isClickable exact-version binding",
                ReflectionEvidence
            },
            Array.Empty<string>());
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            surface
        });

        return new LiveObservation(
            signature,
            readiness,
            context,
            surface,
            completeness,
            game,
            new[]
            {
                "Private-field bindings are exact-version scoped and expose only semantics already visible in the hand-selection UI."
            });
    }

    internal static bool ResolveManualConfirmationRequirement(
        bool preferenceRequiresManualConfirmation,
        bool visibleConfirmControl)
    {
        return preferenceRequiresManualConfirmation || visibleConfirmControl;
    }

    internal static string SelectionLabel(
        string cardName,
        NPlayerHand.Mode mode,
        int selectedCount,
        int maxSelect)
    {
        bool replacesExistingSelection = mode == NPlayerHand.Mode.UpgradeSelect
            ? selectedCount > 0
            : selectedCount >= maxSelect;
        return replacesExistingSelection
            ? $"Replace current selection with {cardName}"
            : $"Select {cardName}";
    }

    private static NativeInputResult StartSelect(
        NPlayerHand expectedHand,
        NPlayerHand.Mode expectedMode,
        CardModel expectedCard)
    {
        if (!IsCurrent(expectedHand, expectedMode))
            return NativeInputResult.Rejected("selection_changed", "Combat-hand selection is no longer current.");
        NHandCardHolder? holder = expectedHand.ActiveHolders.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.CardModel, expectedCard)
            && ConnectorMod.IsNodeVisible(candidate)
            && IsHolderClickable(candidate));
        if (holder == null || IsSelected(expectedHand, expectedCard))
        {
            return NativeInputResult.Rejected(
                "card_not_actionable",
                "The advertised hand card or its selected state changed before execution.");
        }

        holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
        return NativeInputResult.Delivered("native_combat_hand_card_pressed");
    }

    private static NativeInputResult StartDeselect(NPlayerHand expectedHand, CardModel expectedCard)
    {
        if (!IsCurrent(expectedHand, NPlayerHand.Mode.SimpleSelect))
            return NativeInputResult.Rejected("selection_changed", "Simple hand selection is no longer current.");
        NSelectedHandCardContainer? container =
            expectedHand.GetNodeOrNull<NSelectedHandCardContainer>("%SelectedHandCardContainer");
        NSelectedHandCardHolder? holder = container?.Holders.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.CardModel, expectedCard)
            && ConnectorMod.IsNodeVisible(candidate)
            && IsHolderClickable(candidate));
        if (holder == null || !IsSelected(expectedHand, expectedCard))
        {
            return NativeInputResult.Rejected(
                "card_not_actionable",
                "The advertised selected hand card changed before execution.");
        }

        holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
        return NativeInputResult.Delivered("native_selected_hand_card_pressed");
    }

    private static NativeInputResult StartConfirm(NPlayerHand expectedHand)
    {
        if (!IsCurrentSelection(expectedHand))
            return NativeInputResult.Rejected("selection_changed", "Combat-hand selection is no longer current.");
        NConfirmButton? confirm = expectedHand.GetNodeOrNull<NConfirmButton>("%SelectModeConfirmButton");
        if (confirm is not { IsEnabled: true } || !ConnectorMod.IsNodeVisible(confirm))
            return NativeInputResult.Rejected("confirm_not_available", "The hand-selection confirm control is no longer enabled.");

        confirm.ForceClick();
        return NativeInputResult.Delivered(ConfirmDeliveryEvidence);
    }

    private static NativeInputResult StartClosePeek(NPlayerHand expectedHand)
    {
        if (!IsCurrentSelection(expectedHand) || !expectedHand.PeekButton.IsPeeking)
            return NativeInputResult.Rejected("peek_changed", "The combat-hand peek view is no longer current.");
        if (!expectedHand.PeekButton.IsEnabled || !ConnectorMod.IsNodeVisible(expectedHand.PeekButton))
            return NativeInputResult.Rejected("peek_close_not_available", "The peek control is no longer enabled.");

        expectedHand.PeekButton.ForceClick();
        return NativeInputResult.Delivered("native_combat_hand_peek_button_clicked");
    }

    internal static NativeInputResult StartSelect(
        NativeEntityRegistry entities,
        string handEntityId,
        string cardEntityId)
    {
        if (!entities.TryResolve(handEntityId, out NPlayerHand? hand)
            || hand == null
            || !entities.TryResolve(cardEntityId, out CardModel? card)
            || card == null
            || !TryReadBinding(hand, out Binding? binding, out _)
            || binding == null)
        {
            return NativeInputResult.Rejected(
                "selection_changed",
                "The exact combat-hand owner, mode, or card is no longer current.");
        }
        return StartSelect(hand, binding.Mode, card);
    }

    internal static NativeInputResult StartDeselect(
        NativeEntityRegistry entities,
        string handEntityId,
        string cardEntityId)
    {
        if (!entities.TryResolve(handEntityId, out NPlayerHand? hand)
            || hand == null
            || !entities.TryResolve(cardEntityId, out CardModel? card)
            || card == null)
        {
            return NativeInputResult.Rejected(
                "selection_changed",
                "The exact combat-hand owner or selected card is no longer current.");
        }
        return StartDeselect(hand, card);
    }

    internal static NativeInputResult StartConfirm(
        NativeEntityRegistry entities,
        string handEntityId) =>
        entities.TryResolve(handEntityId, out NPlayerHand? hand) && hand != null
            ? StartConfirm(hand)
            : NativeInputResult.Rejected(
                "selection_changed",
                "The exact combat-hand owner is no longer current.");

    internal static NativeInputResult StartClosePeek(
        NativeEntityRegistry entities,
        string handEntityId) =>
        entities.TryResolve(handEntityId, out NPlayerHand? hand) && hand != null
            ? StartClosePeek(hand)
            : NativeInputResult.Rejected(
                "selection_changed",
                "The exact combat-hand owner is no longer current.");

    private static bool TryReadBinding(NPlayerHand hand, out Binding? binding, out string? error)
    {
        binding = null;
        error = null;
        if (PreferencesField?.GetValue(hand) is not CardSelectorPrefs preferences)
            error = "Missing or incompatible NPlayerHand._prefs binding.";
        else if (SelectedCardsField?.GetValue(hand) is not IEnumerable<CardModel> selected)
            error = "Missing or incompatible NPlayerHand._selectedCards binding.";
        else if (hand.CurrentMode is not (NPlayerHand.Mode.SimpleSelect or NPlayerHand.Mode.UpgradeSelect))
            error = $"Unsupported NPlayerHand selection mode {hand.CurrentMode}.";
        else
            binding = new Binding(preferences, selected.ToArray(), hand.CurrentMode);
        return binding != null;
    }

    private static bool SelectedCardsMatchVisibleSelection(
        NPlayerHand hand,
        NPlayerHand.Mode mode,
        IReadOnlyList<CardModel> selected)
    {
        if (mode == NPlayerHand.Mode.SimpleSelect)
        {
            NSelectedHandCardContainer? container =
                hand.GetNodeOrNull<NSelectedHandCardContainer>("%SelectedHandCardContainer");
            return container != null
                   && SetEqualsByReference(
                       container.Holders.Select(holder => holder.CardModel).Where(card => card != null).Cast<CardModel>(),
                       selected);
        }

        NUpgradePreview? preview = hand.GetNodeOrNull<NUpgradePreview>("%UpgradePreview");
        return selected.Count switch
        {
            0 => preview?.Card == null,
            1 => preview?.Card != null && ReferenceEquals(preview.Card, selected[0]),
            _ => false
        };
    }

    private static bool SetEqualsByReference(IEnumerable<CardModel> left, IReadOnlyList<CardModel> right)
    {
        CardModel[] leftArray = left.ToArray();
        return leftArray.Length == right.Count
               && leftArray.All(card => right.Any(other => ReferenceEquals(card, other)));
    }

    private static bool IsSelected(NPlayerHand hand, CardModel card) =>
        SelectedCardsField?.GetValue(hand) is IEnumerable<CardModel> selected
        && selected.Any(candidate => ReferenceEquals(candidate, card));

    private static bool IsHolderClickable(NCardHolder holder) =>
        ClickableField?.GetValue(holder) is true;

    private static bool IsCurrent(NPlayerHand hand, NPlayerHand.Mode mode) =>
        IsCurrentSelection(hand) && hand.CurrentMode == mode;

    private static bool IsCurrentSelection(NPlayerHand hand) =>
        ReferenceEquals(NPlayerHand.Instance, hand)
        && hand.IsInCardSelection
        && ConnectorMod.IsLiveNode(hand);

    private static string ModeName(NPlayerHand.Mode mode) => mode switch
    {
        NPlayerHand.Mode.SimpleSelect => "simple_select",
        NPlayerHand.Mode.UpgradeSelect => "upgrade_select",
        _ => "unsupported"
    };

    private static string? ReadNodeText(Node root, string path)
    {
        try
        {
            Node? node = root.GetNodeOrNull(path);
            if (node == null)
                return null;
            Variant value = node.Get("text");
            return value.VariantType == Variant.Type.Nil
                ? null
                : ConnectorMod.StripRichTextTags(value.AsString()).Replace("\n", " ");
        }
        catch
        {
            return null;
        }
    }

    private static LiveObservation BindingUnavailable(
        GameBuildIdentity game,
        ILiveContext context,
        string reason,
        IReadOnlyList<string> missing)
        => NativeUiFailClosedObservation.BindingUnavailable(
            game,
            context,
            nameof(NPlayerHand),
            reason,
            new[] { "NPlay