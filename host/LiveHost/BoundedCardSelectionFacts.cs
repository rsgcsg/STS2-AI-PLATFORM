using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;

namespace STS2Connector.LiveHost;

/// <summary>
/// Reads the small structural state shared by exact deck-card selectors. This
/// helper carries no action authority and deliberately does not interpret the
/// selector's business purpose, eligibility, commit effect, or completion.
/// </summary>
internal static class BoundedCardSelectionFacts
{
    public static bool TryRead(
        object screen,
        out CardSelectorPrefs preferences,
        out IReadOnlyList<CardModel> selectedCards,
        out string? error)
    {
        preferences = default;
        selectedCards = Array.Empty<CardModel>();
        error = null;

        object? prefsValue = ReadField(screen, "_prefs");
        object? selectedValue = ReadField(screen, "_selectedCards");
        if (prefsValue is not CardSelectorPrefs prefs)
            error = "Missing or incompatible _prefs binding.";
        else if (selectedValue is not IEnumerable<CardModel> selected)
            error = "Missing or incompatible _selectedCards binding.";
        else
        {
            preferences = prefs;
            selectedCards = selected.ToArray();
        }

        return error == null;
    }

    public static IReadOnlyList<CardModel> ReadSelectedCards(object screen) =>
        ReadField(screen, "_selectedCards") is IEnumerable<CardModel> cards
            ? cards.ToArray()
            : Array.Empty<CardModel>();

    public static bool IsSelected(object screen, CardModel card) =>
        ReadSelectedCards(screen).Any(selected => ReferenceEquals(selected, card));

    private static object? ReadField(object source, string fieldName)
    {
        const BindingFlags flags = BindingFlags.Instance
                                   | BindingFlags.Public
                                   | BindingFlags.NonPublic
                                   | BindingFlags.DeclaredOnly;
        for (Type? type = source.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(fieldName, flags);
            if (field != null)
                return field.GetValue(source);
        }

        return null;
    }
}
