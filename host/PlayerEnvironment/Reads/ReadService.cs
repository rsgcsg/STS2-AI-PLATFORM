using STS2Connector.Authority;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.PlayerEnvironment.Protocol;
using STS2Connector.NativeUi;

namespace STS2Connector.PlayerEnvironment;

internal static partial class PlayerEnvironmentService
{
    public static PlayerEnvironmentReadResult Read(
        string readId,
        string expectedSnapshotId)
    {
        SnapshotBuildResult snapshot =
            BuildSnapshot();
        PlayerEnvironmentSnapshot observation = snapshot.Snapshot;
        if (!string.Equals(observation.SnapshotId, expectedSnapshotId, StringComparison.Ordinal))
        {
            return new PlayerEnvironmentReadResult(
                null,
                "stale_state",
                "The expected snapshot is no longer current; obtain a fresh Player Environment observation.");
        }
        PlayerEnvironmentReadOpportunity? opportunity = observation.Reads.SingleOrDefault(entry =>
            string.Equals(entry.ReadId, readId, StringComparison.Ordinal));
        if (opportunity == null)
        {
            return new PlayerEnvironmentReadResult(
                null,
                "read_not_available",
                "This read is not in the current player-visible read catalog.");
        }

        if (opportunity.TargetReferentId != null)
            return ReadLinkedDetail(snapshot, opportunity, expectedSnapshotId);

        PlayerReadBuildResult built = PlayerVisibleReadBuilder.Build(
            opportunity.Kind,
            snapshot.HostObservation.Context,
            Entities);
        if (built.Draft == null)
            return new PlayerEnvironmentReadResult(null, built.ErrorCode, built.Detail);

        PlayerReadDraft draft = built.Draft;
        return new PlayerEnvironmentReadResult(
            new PlayerEnvironmentReadResponse(
                PlayerEnvironmentContract.ProtocolVersion,
                PlayerEnvironmentContract.ReadSchema,
                readId,
                expectedSnapshotId,
                observation.SnapshotId,
                DateTimeOffset.UtcNow,
                draft.Kind,
                null,
                opportunity.VisibilityBasis,
                draft.OrderingSemantics,
                PlayerEnvironmentContract.ReadContentSchema(draft.Kind),
                JsonSerializer.SerializeToNode(draft.Content, draft.Content.GetType(), ConnectorMod._jsonOptions) ?? new JsonObject(),
                ToCompleteness(draft.Completeness, opportunity.HiddenByPolicy),
                observation.Session,
                observation.InformationPolicy),
            null,
            null);
    }

    private static PlayerEnvironmentReadResult ReadLinkedDetail(
        SnapshotBuildResult snapshot,
        PlayerEnvironmentReadOpportunity opportunity,
        string expectedSnapshotId)
    {
        PlayerEnvironmentSnapshot observation = snapshot.Snapshot;
        if (!string.Equals(opportunity.Kind, "surface_card", StringComparison.Ordinal)
            || opportunity.TargetReferentId == null)
        {
            return new PlayerEnvironmentReadResult(
                null,
                "read_kind_not_implemented",
                "This current read kind has no bounded Live host implementation.");
        }
        string referentId = opportunity.TargetReferentId;
        VisibleCard? card = VisibleSurfaceCards(snapshot.HostObservation.Surface)
            .FirstOrDefault(value => string.Equals(
                value.EntityId,
                referentId,
                StringComparison.Ordinal));
        if (card == null)
        {
            return new PlayerEnvironmentReadResult(
                null,
                "read_binding_failed",
                "The current card detail could not be rebuilt from the same UI Surface.");
        }

        return new PlayerEnvironmentReadResult(
            new PlayerEnvironmentReadResponse(
                PlayerEnvironmentContract.ProtocolVersion,
                PlayerEnvironmentContract.ReadSchema,
                opportunity.ReadId,
                expectedSnapshotId,
                observation.SnapshotId,
                DateTimeOffset.UtcNow,
                opportunity.Kind,
                referentId,
                opportunity.VisibilityBasis,
                opportunity.OrderingSemantics,
                opportunity.ContentSchema,
                JsonSerializer.SerializeToNode(card, ConnectorMod._jsonOptions) ?? new JsonObject(),
                new PlayerEnvironmentCompleteness(
                    "complete",
                    "complete_current_player_visible_card_detail",
                    "read_only",
                    Array.Empty<string>(),
                    Array.Empty<string>()),
                observation.Session,
                observation.InformationPolicy),
            null,
            null);
    }

    internal static IReadOnlyList<PlayerEnvironmentLinkedDetailCatalogEntry>
        BuildLinkedDetailCatalog(ILiveSurface surface) =>
        VisibleSurfaceCards(surface)
            .GroupBy(card => card.EntityId, StringComparer.Ordinal)
            .Select(group => new PlayerEnvironmentLinkedDetailCatalogEntry(
                "surface_card",
                group.Key,
                "normal_player_visible_surface_card"))
            .OrderBy(entry => entry.EntityId, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<VisibleCard> VisibleSurfaceCards(ILiveSurface surface) =>
        surface switch
        {
            NativeDeckCardSelectionSurface value => value.Cards,
            NativeCombatPileSelectionSurface value => value.Cards,
            NativeGeneratedCardChoiceSurface value => value.Cards,
            NativeSimpleCardSelectionSurface value => value.Cards,
            DeckEnchantSelectionSurface value => value.Cards,
            DeckTransformSelectionSurface value => value.Cards,
            CombatHandCardSelectionSurface value => value.Cards,
            CardRewardSelectionSurface value => value.Cards,
            CardBundleSelectionSurface value => value.Bundles.SelectMany(bundle => bundle.Cards),
            _ => Array.Empty<VisibleCard>()
        };

    private static PlayerEnvironmentCompleteness ToCompleteness(
        StateCompleteness completeness,
        IReadOnlyList<string> hiddenByPolicy,
        string? forcedStatus = null) => new(
            forcedStatus ?? (completeness.Missing.Count == 0 ? "complete" : "partial"),
            completeness.PlayerVisibleSemantics,
            completeness.InteractionDiscovery,
            completeness.Missing,
            hiddenByPolicy);

    internal static PlayerEnvironmentCompleteness ToCompleteness(
        PlayerReadCompleteness completeness,
        IReadOnlyList<string> hiddenByPolicy) => new(
            completeness.Missing.Count == 0 ? "comple