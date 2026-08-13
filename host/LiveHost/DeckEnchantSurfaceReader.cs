using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

internal sealed class DeckEnchantSurfaceReader : ILiveSurfaceReader
{
    internal const string NativeBindingEvidence =
        "NDeckEnchantSelectScreen+current-visible-grid+exact-enchantment-binding";
    internal const string ToggleDeliveryEvidence = "native_enchant_card_holder_pressed";
    internal const string PreviewDeliveryEvidence = "native_enchant_preview_button_clicked";
    internal const string ConfirmDeliveryEvidence = "native_enchant_confirm_button_clicked";
    internal const string CancelPreviewDeliveryEvidence = "native_enchant_preview_cancel_clicked";
    internal const string CloseDeliveryEvidence = "native_enchant_close_clicked";

    public string Kind => "deck_enchant_selection";

    public InputOwnerLayer Layer => InputOwnerLayer.Overlay;

    public LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        if (snapshot.TopOverlay is not NDeckEnchantSelectScreen screen)
            return null;
        return Build(screen, entities, game);
    }

    private static LiveObservation Build(
        NDeckEnchantSelectScreen screen,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        if (!TryReadBinding(screen, out Binding? binding, out string? bindingError))
        {
            var degradedSurface = new UnsupportedSurface(
                "deck_enchant_selection",
                nameof(NDeckEnchantSelectScreen),
                bindingError ?? "Required screen binding is unavailable.");
            var degradedCompleteness = new StateCompleteness(
                "degraded",
                "empty_fail_closed",
                new[] { "public_scene_tree" },
                new[] { "selection_constraints", "selected_cards", "enchantment_semantics", "legal_actions" });
            string degradedSignature = StableIdentityHash.Object(new
            {
                game.Version,
                degradedSurface,
                actionKeys = Array.Empty<string>()
            });

            return new LiveObservation(
                degradedSignature,
                "degraded",
                LiveContextReader.Build(entities),
                degradedSurface,
                degradedCompleteness,
                game,
                new[] { "deck_enchant_binding_unavailable", bindingError ?? "unknown_binding_error" });
        }

        Binding exactBinding = binding!;

        string stage = IsPreviewVisible(screen) ? "preview" : "selecting";
        string screenEntityId = entities.GetId(screen, "screen");
        HashSet<CardModel> selectedCards = exactBinding.SelectedCards.ToHashSet();
        IReadOnlyList<NGridCardHolder> holders = ConnectorMod.FindAllSortedByPosition<NGridCardHolder>(screen)
            .Where(holder => ConnectorMod.IsNodeVisible(holder) && holder.CardModel != null)
            .ToArray();

        var cards = new List<VisibleCard>(holders.Count);
        var cardIds = new Dictionary<CardModel, string>();
        foreach (NGridCardHolder holder in holders)
        {
            CardModel card = holder.CardModel;
            string entityId = entities.GetId(card, "card");
            cardIds[card] = entityId;
            cards.Add(LiveContextReader.BuildCard(
                card,
                entityId,
                selectedCards.Contains(card),
                displayPile: PileType.Deck));
        }

        string[] selectedIds = selectedCards
            .Select(card => cardIds.TryGetValue(card, out string? id) ? id : entities.GetId(card, "card"))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] selectableIds = stage == "selecting"
            ? holders.Where(holder => !selectedCards.Contains(holder.CardModel)
                                      && selectedCards.Count < exactBinding.Preferences.MaxSelect
                                      && exactBinding.Enchantment.CanEnchant(holder.CardModel))
                .Select(holder => cardIds[holder.CardModel])
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        string[] deselectableIds = stage == "selecting"
            ? holders.Where(holder => selectedCards.Contains(holder.CardModel))
                .Select(holder => cardIds[holder.CardModel])
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        NConfirmButton? mainConfirm = screen.GetNodeOrNull<NConfirmButton>("Confirm")
                                      ?? screen.GetNodeOrNull<NConfirmButton>("%Confirm");
        NBackButton? close = screen.GetNodeOrNull<NBackButton>("%Close");
        Control? preview = GetVisiblePreview(screen);
        NConfirmButton? previewConfirm = preview?.GetNodeOrNull<NConfirmButton>("Confirm");
        NBackButton? previewCancel = preview?.GetNodeOrNull<NBackButton>("Cancel");

        VisibleEnchantment enchantment = BuildEnchantment(screen, exactBinding.Enchantment, exactBinding.EnchantmentAmount);
        var surface = new DeckEnchantSelectionSurface(
            "deck_enchant_selection",
            stage,
            screenEntityId,
            ReadNodeText(screen, "%BottomLabel"),
            exactBinding.Preferences.MinSelect,
            exactBinding.Preferences.MaxSelect,
            selectedCards.Count,
            selectedIds,
            exactBinding.Preferences.Cancelable,
            enchantment,
            cards)
        {
            SelectableCardEntityIds = selectableIds,
            DeselectableCardEntityIds = deselectableIds,
            CanPreview = stage == "selecting"
                         && mainConfirm is { IsEnabled: true }
                         && ConnectorMod.IsNodeVisible(mainConfirm),
            CanCloseSelection = stage == "selecting"
                                && exactBinding.Preferences.Cancelable
                                && close is { IsEnabled: true }
                                && ConnectorMod.IsNodeVisible(close),
            CanConfirm = stage == "preview"
                         && previewConfirm is { IsEnabled: true }
                         && ConnectorMod.IsNodeVisible(previewConfirm),
            CanCancelPreview = stage == "preview"
                               && previewCancel is { IsEnabled: true }
                               && ConnectorMod.IsNodeVisible(previewCancel)
        };

        var missing = new List<string>();
        if (surface.Enchantment.Name == null)
            missing.Add("enchantment.name");
        if (surface.Enchantment.Description == null)
            missing.Add("enchantment.description");
        if (surface.Prompt == null)
            missing.Add("prompt");

        var completeness = new StateCompleteness(
            missing.Count == 0 ? "contract_complete_for_supported_surface" : "partial",
            "derived_from_same_validator_as_execution",
            new[]
            {
                "public_scene_tree",
                "localized_visible_ui_text",
                "card_models_rendered_by_grid",
                NativeBindingEvidence
            },
            missing);
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            surface
        });

        return new LiveObservation(
            signature,
            missing.Count == 0 ? "ready" : "degraded",
            LiveContextReader.Build(entities),
            surface,
            completeness,
            game,
            new[]
            {
                "Private-field bindings are exact-version scoped and fail closed outside the tested game identity."
            });
    }

    private static NativeInputResult StartToggleCard(
        NDeckEnchantSelectScreen expectedScreen,
        CardModel expectedCard,
        EnchantmentModel enchantment)
    {
        if (!IsCurrentScreen(expectedScreen) || IsPreviewVisible(expectedScreen))
            return NativeInputResult.Rejected("screen_stage_changed", "Enchant selection is no longer in selecting stage.");

        NGridCardHolder? holder = ConnectorMod.FindAllSortedByPosition<NGridCardHolder>(expectedScreen)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.CardModel, expectedCard) && ConnectorMod.IsNodeVisible(candidate));
        NCardGrid? grid = ConnectorMod.FindFirst<NCardGrid>(expectedScreen);
        if (holder == null || grid == null)
            return NativeInputResult.Rejected("card_not_actionable", "The selected card is no longer present in the active grid.");
        if (!enchantment.CanEnchant(expectedCard))
            return NativeInputResult.Rejected("card_not_enchantable", "The game model no longer permits this enchantment on the card.");

        grid.EmitSignal(NCardGrid.SignalName.HolderPressed, holder);
        return NativeInputResult.Delivered(ToggleDeliveryEvidence);
    }

    private static NativeInputResult StartMainPreview(NDeckEnchantSelectScreen expectedScreen)
    {
        if (!IsCurrentScreen(expectedScreen) || IsPreviewVisible(expectedScreen))
            return NativeInputResult.Rejected("screen_stage_changed", "Enchant selection is no longer in selecting stage.");

        NConfirmButton? confirm = expectedScreen.GetNodeOrNull<NConfirmButton>("Confirm")
                                  ?? expectedScreen.GetNodeOrNull<NConfirmButton>("%Confirm");
        if (confirm is not { IsEnabled: true } || !ConnectorMod.IsNodeVisible(confirm))
            return NativeInputResult.Rejected("preview_not_available", "The preview button is no longer enabled.");

        confirm.ForceClick();
        return NativeInputResult.Delivered(PreviewDeliveryEvidence);
    }

    private static NativeInputResult StartPreviewConfirm(
        NDeckEnchantSelectScreen expectedScreen,
        IReadOnlyList<CardModel> expectedCards,
        string expectedEnchantmentId,
        int expectedEnchantmentAmount)
    {
        if (!IsCurrentScreen(expectedScreen) || !IsPreviewVisible(expectedScreen))
            return NativeInputResult.Rejected("screen_stage_changed", "Enchant selection is no longer in preview stage.");

        if (!TryReadBinding(expectedScreen, out Binding? currentBinding, out string? bindingError))
            return NativeInputResult.Rejected(
                "enchantment_binding_changed",
                bindingError ?? "Enchant selection binding is unavailable at commit time.");

        Binding exactBinding = currentBinding!;
        IReadOnlyList<CardModel> currentCards = exactBinding.SelectedCards;
        if (!string.Equals(exactBinding.Enchantment.Id.Entry, expectedEnchantmentId, StringComparison.Ordinal)
            || exactBinding.EnchantmentAmount != expectedEnchantmentAmount
            || currentCards.Count != expectedCards.Count
            || currentCards.Count < exactBinding.Preferences.MinSelect
            || currentCards.Count > exactBinding.Preferences.MaxSelect
            || expectedCards.Any(expected => !currentCards.Any(current => ReferenceEquals(current, expected))))
        {
            return NativeInputResult.Rejected(
                "enchantment_commit_state_changed",
                "The exact selected cards or target enchantment changed before confirmation.");
        }

        if (expectedCards.Any(card => !exactBinding.Enchantment.CanEnchant(card)))
            return NativeInputResult.Rejected(
                "card_not_enchantable",
                "At least one selected card is no longer eligible for the target enchantment.");

        NConfirmButton? confirm = GetVisiblePreview(expectedScreen)?.GetNodeOrNull<NConfirmButton>("Confirm");
        if (confirm is not { IsEnabled: true } || !ConnectorMod.IsNodeVisible(confirm))
            return NativeInputResult.Rejected("confirm_not_available", "The preview confirm button is no longer enabled.");

        confirm.ForceClick();
        return NativeInputResult.Delivered(ConfirmDeliveryEvidence);
    }

    private static NativeInputResult StartPreviewCancel(NDeckEnchantSelectScreen expectedScreen)
    {
        if (!IsCurrentScreen(expectedScreen))
            return NativeInputResult.Rejected("screen_changed", "Enchant selection is no longer the current screen.");

        NBackButton? cancel = GetVisiblePreview(expectedScreen)?.GetNodeOrNull<NBackButton>("Cancel");
        if (cancel is not { IsEnabled: true } || !ConnectorMod.IsNodeVisible(cancel))
            return NativeInputResult.Rejected("cancel_not_available", "The preview cancel button is no longer enabled.");

        cancel.ForceClick();
        return NativeInputResult.Delivered(CancelPreviewDeliveryEvidence);
    }

    private static NativeInputResult StartClose(NDeckEnchantSelectScreen expectedScreen)
    {
        if (!IsCurrentScreen(expectedScreen) || IsPreviewVisible(expectedScreen))
            return NativeInputResult.Rejected("screen_stage_changed", "Enchant selection is no longer closable from the selecting stage.");

        NBackButton? close = expectedScreen.GetNodeOrNull<NBackButton>("%Close");
        if (close is not { IsEnabled: true } || !ConnectorMod.IsNodeVisible(close))
            return NativeInputResult.Rejected("close_not_available", "The close button is no longer enabled.");

        close.ForceClick();
        return NativeInputResult.Delivered(CloseDeliveryEvidence);
    }

    internal static NativeInputResult StartToggleCard(
        NativeEntityRegistry entities,
        string expectedScreenId,
        string expectedCardId)
    {
        if (!TryResolveCurrentScreen(entities, expectedScreenId, out NDeckEnchantSelectScreen? screen)
            || !entities.TryResolve(expectedCardId, out CardModel? card)
            || card == null)
        {
            return NativeInputResult.Rejected(
                "enchantment_binding_changed",
                "The exact enchant screen or card is no longer current.");
        }
        if (!TryReadBinding(screen!, out Binding? binding, out string? error))
        {
            return NativeInputResult.Rejected(
                "enchantment_binding_changed",
                error ?? "The exact enchant screen binding is no longer current.");
        }

        return StartToggleCard(screen!, card, binding!.Enchantment);
    }

    internal static NativeInputResult StartMainPreview(
        NativeEntityRegistry entities,
        string expectedScreenId) =>
        TryResolveCurrentScreen(entities, expectedScreenId, out NDeckEnchantSelectScreen? screen)
            ? StartMainPreview(screen!)
            : NativeInputResult.Rejected(
                "screen_changed",
                "The exact enchant screen is no longer current.");

    internal static NativeInputResult StartPreviewConfirm(
        NativeEntityRegistry entities,
        string expectedScreenId)
    {
        if (!TryResolveCurrentScreen(entities, expectedScreenId, out NDeckEnchantSelectScreen? screen))
        {
            return NativeInputResult.Rejected(
                "enchantment_binding_changed",
                "The exact enchant screen is no longer current.");
        }
        if (!TryReadBinding(screen!, out Binding? binding, out string? error))
        {
            return NativeInputResult.Rejected(
                "enchantment_binding_changed",
                error ?? "The exact enchant screen binding is no longer current.");
        }

        return StartPreviewConfirm(
            screen!,
            binding!.SelectedCards.ToArray(),
            binding.Enchantment.Id.Entry,
            binding.EnchantmentAmount);
    }

    internal static NativeInputResult StartPreviewCancel(
        NativeEntityRegistry entities,
        string expectedScreenId) =>
        TryResolveCurrentScreen(entities, expectedScreenId, out NDeckEnchantSelectScreen? screen)
            ? StartPreviewCancel(screen!)
            : NativeInputResult.Rejected(
                "screen_changed",
                "The exact enchant screen is no longer current.");

    internal static NativeInputResult StartClose(
        NativeEntityRegistry entities,
        string expectedScreenId) =>
        TryResolveCurrentScreen(entities, expectedScreenId, out NDeckEnchantSelectScreen? screen)
            ? StartClose(screen!)
            : NativeInputResult.Rejected(
                "screen_changed",
                "The exact enchant screen is no longer current.");

    private static bool TryResolveCurrentScreen(
        NativeEntityRegistry entities,
        string expectedScreenId,
        out NDeckEnchantSelectScreen? screen)
    {
        if (!entities.TryResolve(expectedScreenId, out screen)
            || screen == null
            || !IsCurrentScreen(screen)
            || !string.Equals(
                entities.GetId(screen, "screen"),
                expectedScreenId,
                StringComparison.Ordinal))
        {
            screen = null;
            return false;
        }
        return true;
    }

    private static bool TryReadBinding(
        NDeckEnchantSelectScreen screen,
        out Binding? binding,
        out string? error)
    {
        binding = null;
        error = null;

        if (!BoundedCardSelectionFacts.TryRead(
                screen,
                out CardSelectorPrefs prefs,
                out IReadOnlyList<CardModel> selected,
                out string? selectionError))
        {
            error = selectionError;
            return false;
        }

        object? enchantmentValue = ReadField(screen, "_enchantment");
        object? amountValue = ReadField(screen, "_enchantmentAmount");

        if (enchantmentValue is not EnchantmentModel enchantment)
            error = "Missing or incompatible _enchantment binding.";
        else if (amountValue is not int amount)
            error = "Missing or incompatible _enchantmentAmount binding.";
        else
            binding = new Binding(prefs, selected, enchantment, amount);

        return binding != null;
    }

    private static object? ReadField(object source, string fieldName)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type? type = source.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(fieldName, Flags);
            if (field != null)
                return field.GetValue(source);
        }
        return null;
    }

    private static bool IsCardSelected(NDeckEnchantSelectScreen screen, CardModel card) =>
        BoundedCardSelectionFacts.IsSelected(screen, card);

    private static IReadOnlyList<CardModel> ReadSelectedCards(NDeckEnchantSelectScreen screen) =>
        BoundedCardSelectionFacts.ReadSelectedCards(screen);

    private static VisibleEnchantment BuildEnchantment(
        NDeckEnchantSelectScreen screen,
        EnchantmentModel enchantment,
        int amount)
    {
        string? name = ReadNodeText(screen, "%EnchantmentTitle")
                       ?? ConnectorMod.SafeGetText(() => enchantment.Title);
        string? description = ReadNodeText(screen, "%EnchantmentDescription");

        if (description == null)
        {
            try
            {
                EnchantmentModel display = enchantment.ToMutable();
                display.Amount = amount;
                display.RecalculateValues();
                description = ConnectorMod.SafeGetText(() => display.DynamicDescription);
            }
            catch
            {
                description = ConnectorMod.SafeGetText(() => enchantment.DynamicDescription);
            }
        }

        return new VisibleEnchantment(
            enchantment.Id.Entry,
            name,
            description,
            amount,
            "localized_visible_ui_text+model_identity");
    }

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

    private static bool IsCurrentScreen(NDeckEnchantSelectScreen screen) =>
        ActiveInputResolver.IsVisibleActiveOverlay(screen)
        && ReferenceEquals(NOverlayStack.Instance?.Peek(), screen);

    private static bool IsPreviewVisible(NDeckEnchantSelectScreen screen) =>
        GetVisiblePreview(screen) != null;

    private static Control? GetVisiblePreview(NDeckEnchantSelectScreen screen)
    {
        Control? single = screen.GetNodeOrNull<Control>("%Ench