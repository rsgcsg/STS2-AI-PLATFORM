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
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.PlayerEnvironment.Protocol;

namespace STS2Connector.NativeUi;

internal sealed record NativeDeckCardSelectionSurface(
    string Kind,
    string Stage,
    string ScreenEntityId,
    string? Prompt,
    int MinSelect,
    int MaxSelect,
    int SelectedCount,
    IReadOnlyList<string> SelectedCardEntityIds,
    IReadOnlyList<string> SelectableCardEntityIds,
    IReadOnlyList<string> DeselectableCardEntityIds,
    bool Cancelable,
    bool CanPreview,
    bool CanCancelSelection,
    bool CanCancelPreview,
    bool CanConfirm,
    IReadOnlyList<VisibleCard> Cards) : ILiveSurface;

/// <summary>
/// Source-free adapter for the native deck-card selector. It exposes only
/// controls the current player could use and delegates all consequences to the
/// game's own control callbacks.
/// </summary>
internal static class NativeDeckCardSelection
{
    internal const string SurfaceKind = "native_deck_card_selection";
    internal const string SelectOperation = "select_native_deck_card";
    internal const string DeselectOperation = "deselect_native_deck_card";
    internal const string PreviewOperation = "open_player_environment_deck_preview";
    internal const string CancelSelectionOperation = "cancel_player_environment_deck_selection";
    internal const string CancelPreviewOperation = "cancel_player_environment_deck_preview";
    internal const string ConfirmOperation = "confirm_player_environment_deck_selection";

    private const BindingFlags Flags = BindingFlags.Instance
                                       | BindingFlags.Public
                                       | BindingFlags.NonPublic
                                       | BindingFlags.DeclaredOnly;
    private static readonly FieldInfo? ClickableField =
        typeof(NCardHolder).GetField("_isClickable", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static LiveObservation? TryBuild(
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        ActiveSurfaceSnapshot active = ActiveInputResolver.Capture();
        if (active.TopOverlay is not NDeckCardSelectScreen screen)
            return null;

        ILiveContext context = LiveContextReader.Build(entities);
        if (!BoundedCardSelectionFacts.TryRead(
                screen,
                out CardSelectorPrefs preferences,
                out IReadOnlyList<CardModel> selectedCards,
                out string? bindingError)
            || ClickableField == null)
        {
            return BindingUnavailable(
                game,
                context,
                bindingError ?? "The deck selector's current UI binding is unavailable.");
        }

        string? prompt = ReadNodeText(screen, "%InfoLabel")
                         ?? ReadNodeText(screen, "%BottomLabel");
        NGridCardHolder[] holders = ConnectorMod.FindAllSortedByPosition<NGridCardHolder>(screen)
            .Where(holder => ConnectorMod.IsNodeVisible(holder) && holder.CardModel != null)
            .ToArray();
        if (holders.Length == 0)
        {
            return BindingUnavailable(
                game,
                context,
                "The current deck selector has no visible card holders.");
        }

        HashSet<CardModel> selected = selectedCards.ToHashSet();
        var cardIds = new Dictionary<CardModel, string>();
        var cards = new List<VisibleCard>(holders.Length);
        foreach (NGridCardHolder holder in holders)
        {
            CardModel card = holder.CardModel;
            string cardId = entities.GetId(card, "card");
            cardIds[card] = cardId;
            cards.Add(LiveContextReader.BuildCard(
                card,
                cardId,
                selected.Contains(card),
                displayPile: PileType.Deck));
        }

        string[] selectedIds = selected
            .Select(card => cardIds.TryGetValue(card, out string? id) ? id : null)
            .Where(id => id != null)
            .Cast<string>()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (selectedIds.Length != selected.Count)
        {
            return BindingUnavailable(
                game,
                context,
                "A selected card is not present in the current visible deck-selector grid.");
        }

        string stage = IsPreviewVisible(screen) ? "preview" : "selecting";
        string[] selectableIds = stage == "selecting"
            ? holders.Where(holder =>
                    IsHolderClickable(holder)
                    && !selected.Contains(holder.CardModel)
                    && selected.Count < preferences.MaxSelect)
                .Select(holder => cardIds[holder.CardModel])
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        string[] deselectableIds = stage == "selecting"
            ? holders.Where(holder =>
                    IsHolderClickable(holder)
                    && selected.Contains(holder.CardModel))
                .Select(holder => cardIds[holder.CardModel])
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

        bool canPreview = stage == "selecting"
                          && IsVisibleEnabled(FindControl<NConfirmButton>(screen, "_confirmButton"));
        bool canCancelSelection = stage == "selecting"
                                  && preferences.Cancelable
                                  && IsVisibleEnabled(FindControl<NBackButton>(screen, "_closeButton"));
        bool canCancelPreview = stage == "preview"
                                && IsVisibleEnabled(FindControl<NBackButton>(screen, "_previewCancelButton"));
        bool canConfirm = stage == "preview"
                          && IsVisibleEnabled(FindControl<NConfirmButton>(screen, "_previewConfirmButton"));
        var surface = new NativeDeckCardSelectionSurface(
            SurfaceKind,
            stage,
            entities.GetId(screen, "screen"),
            prompt,
            preferences.MinSelect,
            preferences.MaxSelect,
            selected.Count,
            selectedIds,
            selectableIds,
            deselectableIds,
            preferences.Cancelable,
            canPreview,
            canCancelSelection,
            canCancelPreview,
            canConfirm,
            cards);
        bool actionable = DescribeCommands(surface).Count > 0;
        bool promptObserved = !string.IsNullOrWhiteSpace(prompt);
        return new LiveObservation(
            StableIdentityHash.Object(new { game.Version, context, surface }),
            actionable ? "ready" : "settling",
            context,
            surface,
            new StateCompleteness(
                promptObserved
                    ? "complete_current_structured_ui"
                    : "partial_current_structured_ui",
                actionable
                    ? "derived_from_current_visible_enabled_controls"
                    : "temporarily_empty_while_native_ui_settles",
                new[]
                {
                    "NDeckCardSelectScreen visible overlay and card grid",
                    "NDeckCardSelectScreen current selection preferences and controls"
                },
                promptObserved ? Array.Empty<string>() : new[] { "visible_prompt_text" }),
            Game: game,
            Warnings: new[]
            {
                "The opening business source and eventual effect are intentionally not inferred by Player Environment C."
            })
        {
            InputOwnership = new InputOwnership(
                "current_ui_owned",
                SurfaceKind,
                "The exact current native selector owns input; no business source grants authority.")
        };
    }

    internal static IReadOnlyList<NativeUiActionDescriptor> DescribeCommands(
        NativeDeckCardSelectionSurface surface)
    {
        if (surface.Kind != SurfaceKind)
            return Array.Empty<NativeUiActionDescriptor>();

        Dictionary<string, VisibleCard> cards = surface.Cards
            .ToDictionary(card => card.EntityId, StringComparer.Ordinal);
        var commands = new List<NativeUiActionDescriptor>();
        ActionEntityBinding screen = new("screen", surface.ScreenEntityId);
        foreach (string cardId in surface.SelectableCardEntityIds)
        {
            if (cards.TryGetValue(cardId, out VisibleCard? card))
                commands.Add(Descriptor(
                    $"select_native_deck_card:{surface.ScreenEntityId}:{cardId}",
                    SelectOperation,
                    "selection",
                    $"Select {card.Name ?? card.DefinitionId}",
                    new[] { screen, new ActionEntityBinding("card", cardId) }));
        }
        foreach (string cardId in surface.DeselectableCardEntityIds)
        {
            if (cards.TryGetValue(cardId, out VisibleCard? card))
                commands.Add(Descriptor(
                    $"deselect_native_deck_card:{surface.ScreenEntityId}:{cardId}",
                    DeselectOperation,
                    "selection",
                    $"Deselect {card.Name ?? card.DefinitionId}",
                    new[] { screen, new ActionEntityBinding("card", cardId) }));
        }
        if (surface.CanPreview)
            commands.Add(Descriptor(
                $"preview_player_environment_deck_selection:{surface.ScreenEntityId}",
                PreviewOperation,
                "preview",
                "Preview the selected cards",
                new[] { screen }));
        if (surface.CanCancelSelection)
            commands.Add(Descriptor(
                $"cancel_player_environment_deck_selection:{surface.ScreenEntityId}",
                CancelSelectionOperation,
                "cancel",
                "Cancel card selection",
                new[] { screen }));
        if (surface.CanCancelPreview)
            commands.Add(Descriptor(
                $"cancel_player_environment_deck_preview:{surface.ScreenEntityId}",
                CancelPreviewOperation,
                "cancel",
                "Return to card selection",
                new[] { screen }));
        if (surface.CanConfirm)
            commands.Add(Descriptor(
                $"confirm_player_environment_deck_selection:{surface.ScreenEntityId}",
                ConfirmOperation,
                "confirm",
                "Confirm the selected cards",
                new[] { screen }));
        return commands;
    }

    internal static NativeInputResult Start(
        NativeEntityRegistry entities,
        NativeDeckCardSelectionSurface surface,
        NativeUiBoundAction binding,
        IReadOnlyDictionary<string, string> parameters)
    {
        string? bindingError = null;
        if (!parameters.TryGetValue("screen_id", out string? screenId)
            || !string.Equals(screenId, surface.ScreenEntityId, StringComparison.Ordinal)
            || !entities.TryResolve(screenId, out NDeckCardSelectScreen? screen)
            || screen == null
            || !IsCurrent(screen)
            || !BoundedCardSelectionFacts.TryRead(
                screen,
                out CardSelectorPrefs preferences,
                out IReadOnlyList<CardModel> selectedCards,
                out bindingError))
        {
            return NativeInputResult.Rejected(
                "player_environment_owner_changed",
                bindingError ?? "The exact deck-card selector is no longer current.");
        }

        string operation = binding.Candidate.Operation;
        if (operation is SelectOperation or DeselectOperation
            && parameters.TryGetValue("card_id", out string? cardId)
            && entities.TryResolve(cardId, out CardModel? card)
            && card != null)
        {
            bool currentlySelected = selectedCards.Any(value => ReferenceEquals(value, card));
            bool advertisedSelect = surface.SelectableCardEntityIds.Contains(cardId, StringComparer.Ordinal);
            bool advertisedDeselect = surface.DeselectableCardEntityIds.Contains(cardId, StringComparer.Ordinal);
            if ((operation == SelectOperation && (currentlySelected || !advertisedSelect))
                || (operation == DeselectOperation && (!currentlySelected || !advertisedDeselect))
                || (!currentlySelected && selectedCards.Count >= preferences.MaxSelect))
            {
                return NativeInputResult.Rejected(
                    "player_environment_operand_changed",
                    "The card's current selected or selectable state changed.");
            }
            NGridCardHolder[] matchingHolders =
                ConnectorMod.FindAllSortedByPosition<NGridCardHolder>(screen)
                .Where(candidate =>
                    ReferenceEquals(candidate.CardModel, card)
                    && ConnectorMod.IsNodeVisible(candidate)
                    && IsHolderClickable(candidate))
                .Take(2)
                .ToArray();
            NCardGrid? grid = ConnectorMod.FindFirst<NCardGrid>(screen);
            if (matchingHolders.Length != 1 || grid == null || IsPreviewVisible(screen))
                return NotActionable("The exact current card holder is no longer actionable.");
            grid.EmitSignal(NCardGrid.SignalName.HolderPressed, matchingHolders[0]);
            return Delivered("native_card_grid_holder_pressed");
        }

        if (operation == PreviewOperation && !IsPreviewVisible(screen))
            return Click(FindControl<NConfirmButton>(screen, "_confirmButton"), "native_selection_preview_clicked");
        if (operation == CancelSelectionOperation && !IsPreviewVisible(screen) && preferences.Cancelable)
            return Click(FindControl<NBackButton>(screen, "_closeButton"), "native_selection_cancel_clicked");
        if (operation == CancelPreviewOperation && IsPreviewVisible(screen))
            return Click(FindControl<NBackButton>(screen, "_previewCancelButton"), "native_preview_cancel_clicked");
        if (operation == ConfirmOperation && IsPreviewVisible(screen))
            return Click(FindControl<NConfirmButton>(screen, "_previewConfirmButton"), "native_selection_confirm_clicked");

        return NotActionable("The exact advertised selector control is no longer current.");
    }

    private static NativeUiActionDescriptor Descriptor(
        string key,
        string operation,
        string phase,
        string label,
        IReadOnlyList<ActionEntityBinding> bindings) => new(
            key,
            operation,
            phase,
            label,
            $"NDeckCardSelectScreen current UI input delivery:{operation}",
            bindings);

    private static NativeInputResult Click(Control? control, string evidence)
    {
        if (!IsVisibleEnabled(control))
            return NotActionable("The exact native control is no longer visible and enabled.");
        switch (control)
        {
            case NConfirmButton confirm:
                confirm.ForceClick();
                break;
            case NBackButton back:
                back.ForceClick();
                break;
            default:
                return NotActionable("The exact native control type changed.");
        }
        return Delivered(evidence);
    }

    private static NativeInputResult Delivered(string evidence) =>
        NativeInputResult.Delivered(evidence);

    private static NativeInputResult NotActionable(string detail) =>
        NativeInputResult.Rejected("player_environment_target_not_actionable", detail);

    private static T? FindControl<T>(NDeckCardSelectScreen screen, string fieldName)
        where T : Control => ReadField(screen, fieldName) as T;

    private static object? ReadField(object source, string fieldName)
    {
        for (Type? type = source.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(fieldName, Flags);
            if (field != null)
                return field.GetValue(source);
        }
        return null;
    }

    private static bool IsPreviewVisible(NDeckCardSelectScreen screen) =>
        ReadField(screen, "_previewContainer") is Control preview
        && ConnectorMod.IsNodeVisible(preview);

    private static bool IsHolderClickable(NCardHolder holder) =>
        ClickableField?.GetValue(holder) is true;

    private static bool IsVisibleEnabled(Control? control) =>
        control is { } && ConnectorMod.IsNodeVisible(control) && ReadEnabled(control);

    private static bool ReadEnabled(Control control) => control switch
    {
        NConfirmButton confirm => confirm.IsEnabled,
        NBackButton back => back.IsEnabled,
        _ => false
    };

    private static bool IsCurrent(NDeckCardSelectScreen screen) =>
        ActiveInputResolver.IsVisibleActiveOverlay(screen)
        && ReferenceEquals(NOverlayStack.Instance?.Peek(), screen);

    private static string? ReadNodeText(Node screen, string path)
    {
        try
        {
            Node? node = screen.GetNodeOrNull(path);
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
        string detail) =>
        NativeUiFailClosedObservation.BindingUnavailable(
            game,
     