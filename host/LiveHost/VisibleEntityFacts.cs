using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

/// <summary>
/// Shared, read-only projection mechanics for entities that the normal UI
/// renders in more than one semantic surface. This helper grants no authority.
/// </summary>
internal static class VisibleEntityFacts
{
    internal sealed record HoverFacts(
        IReadOnlyList<VisibleKeyword> Keywords,
        IReadOnlyList<VisibleCard> CardPreviews);

    public static VisibleRelic BuildRelic(RelicModel relic, NativeEntityRegistry entities)
    {
        string entityId = entities.GetId(relic, "relic");
        HoverFacts hover = BuildHoverFacts(relic.HoverTipsExcludingRelic, entityId);
        return new VisibleRelic(
            entityId,
            relic.Id.Entry,
            ConnectorMod.SafeGetText(() => relic.Title),
            ConnectorMod.SafeGetText(() => relic.DynamicDescription),
            relic.ShowCounter ? relic.DisplayAmount : null,
            hover.Keywords,
            hover.CardPreviews);
    }

    public static VisibleOwnedPotion BuildOwnedPotion(
        PotionModel potion,
        int slot,
        NativeEntityRegistry entities)
    {
        string entityId = entities.GetId(potion, "potion");
        HoverFacts hover = BuildHoverFacts(potion.ExtraHoverTips, entityId);
        return new VisibleOwnedPotion(
            entityId,
            potion.Id.Entry,
            ConnectorMod.SafeGetText(() => potion.Title),
            ConnectorMod.SafeGetText(() => potion.DynamicDescription),
            slot,
            hover.Keywords,
            hover.CardPreviews);
    }

    public static HoverFacts BuildHoverFacts(
        IEnumerable<IHoverTip> tips,
        string ownerEntityId)
    {
        var keywords = new List<VisibleKeyword>();
        var cardPreviews = new List<VisibleCard>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int cardOrdinal = 0;
        foreach (IHoverTip tip in IHoverTip.RemoveDupes(tips))
        {
            switch (tip)
            {
                case HoverTip hoverTip:
                {
                    string? name = hoverTip.Title == null
                        ? null
                        : ConnectorMod.StripRichTextTags(hoverTip.Title);
                    string? description = ConnectorMod.StripRichTextTags(hoverTip.Description);
                    string? key = name ?? description;
                    if (key != null && seen.Add($"text:{key}"))
                        keywords.Add(new VisibleKeyword(name ?? "Unnamed", description));
                    break;
                }
                case CardHoverTip cardTip:
                {
                    CardModel card = cardTip.Card;
                    if (seen.Add($"card:{cardTip.Id}"))
                    {
                        cardPreviews.Add(LiveContextReader.BuildCard(
                            card,
                            BuildTooltipCardEntityId(ownerEntityId, card.Id.Entry, cardOrdinal++),
                            displayPile: PileType.None));
                    }
                    break;
                }
                default:
                    throw new NotSupportedException($"Unsupported visible hover-tip type: {tip.GetType().Name}");
            }
        }
        return new HoverFacts(keywords, cardPreviews);
    }

    public static IReadOnlyList<VisibleKeyword> BuildKeywords(
        IEnumerable<IHoverTip> tips,
        string ownerEntityId) => BuildHoverFacts(tips, ownerEntityId).Keywords;

    internal static string BuildTooltipCardEntityId(
        string ownerEntityId,
        string cardDefinitionId,
        int ordinal)
    {
        string digest = StableIdentityHash.Text($"{