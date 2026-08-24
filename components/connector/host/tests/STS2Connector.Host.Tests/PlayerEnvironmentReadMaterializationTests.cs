using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.PlayerEnvironment;
using STS2Connector.PlayerEnvironment.Protocol;
using System.Text.Json.Nodes;

namespace STS2Connector.Tests;

public sealed class PlayerEnvironmentReadMaterializationTests
{
    [Fact]
    public void RequiredReadsAreMaterializedOnceAndOnlyForRequestedKinds()
    {
        var calls = new List<string>();
        PlayerReadBuildResult Build(string kind)
        {
            calls.Add(kind);
            return PlayerReadBuildResult.Failure("fixture", kind);
        }

        IReadOnlyDictionary<string, PlayerReadBuildResult> materialized =
            PlayerEnvironmentService.MaterializeRequiredReads(
                new[] { "run_deck", "run_deck", "combat_piles" },
                Build);

        Assert.Equal(new[] { "run_deck", "combat_piles" }, calls);
        Assert.Equal(new[] { "run_deck", "combat_piles" }, materialized.Keys);
    }

    [Fact]
    public void MaterializedReadSuccessReusesTheAuthoritativeBuildResult()
    {
        PlayerReadBuildResult build = PlayerReadBuildResult.Success(
            new PlayerReadDraft(
                PlayerVisibleReadBuilder.RunDeckKind,
                "normal_player_read",
                "unordered_multiset",
                new RunDeckReadContent(
                    PlayerVisibleReadBuilder.RunDeckKind,
                    0,
                    Array.Empty<VisibleCard>()),
                new PlayerReadCompleteness(
                    "complete_for_player_run_deck_contents_without_semantic_order",
                    Array.Empty<string>(),
                    Array.Empty<string>())));

        PlayerEnvironmentReadResolution result = PlayerEnvironmentService.ResolveReadMaterialization(
            Snapshot(ReadOpportunity("read:run_deck", PlayerVisibleReadBuilder.RunDeckKind)),
            "read:run_deck",
            "snapshot-a",
            new Dictionary<string, PlayerReadBuildResult>
            {
                [PlayerVisibleReadBuilder.RunDeckKind] = build
            });

        Assert.Null(result.ErrorCode);
        Assert.Same(build, result.Build);
    }

    [Fact]
    public void StaleSnapshotWinsBeforeReadMaterializationLookup()
    {
        PlayerEnvironmentReadResolution result = PlayerEnvironmentService.ResolveReadMaterialization(
            Snapshot(ReadOpportunity("read:run_deck", PlayerVisibleReadBuilder.RunDeckKind)),
            "read:run_deck",
            "snapshot-stale",
            new Dictionary<string, PlayerReadBuildResult>());

        Assert.Equal("stale_state", result.ErrorCode);
    }

    [Fact]
    public void MissingReadCatalogEntryRemainsReadNotAvailable()
    {
        PlayerEnvironmentReadResolution result = PlayerEnvironmentService.ResolveReadMaterialization(
            Snapshot(),
            "read:run_deck",
            "snapshot-a",
            new Dictionary<string, PlayerReadBuildResult>());

        Assert.Equal("read_not_available", result.ErrorCode);
    }

    [Fact]
    public void AdvertisedReadWithoutMaterializationFailsClosed()
    {
        PlayerEnvironmentReadResolution result = PlayerEnvironmentService.ResolveReadMaterialization(
            Snapshot(ReadOpportunity("read:run_deck", PlayerVisibleReadBuilder.RunDeckKind)),
            "read:run_deck",
            "snapshot-a",
            new Dictionary<string, PlayerReadBuildResult>());

        Assert.Equal("read_materialization_missing", result.ErrorCode);
    }

    [Fact]
    public void UnsupportedMaterializationPreservesBuilderError()
    {
        PlayerEnvironmentReadResolution result = PlayerEnvironmentService.ResolveReadMaterialization(
            Snapshot(ReadOpportunity("read:future", "future")),
            "read:future",
            "snapshot-a",
            new Dictionary<string, PlayerReadBuildResult>
            {
                ["future"] = PlayerReadBuildResult.Failure(
                    "read_kind_not_implemented",
                    "fixture unsupported read")
            });

        Assert.Equal("read_kind_not_implemented", result.ErrorCode);
    }

    private static PlayerEnvironmentReadOpportunity ReadOpportunity(
        string readId,
        string kind) => new(
            readId,
            kind,
            null,
            PlayerEnvironmentContract.ReadContentSchema(kind),
            "fixture_player_visible_read",
            SnapshotBound: true,
            "unordered_multiset",
            Array.Empty<string>());

    private static PlayerEnvironmentSnapshot Snapshot(
        params PlayerEnvironmentReadOpportunity[] reads) => new(
        PlayerEnvironmentContract.ProtocolVersion,
        PlayerEnvironmentContract.SnapshotSchema,
        "snapshot-a",
        1,
        DateTimeOffset.UnixEpoch,
        "observed",
        null,
        new PlayerEnvironmentInteraction(
            "interaction-a",
            "map_navigation",
            "observed",
            null,
            "surface-test",
            new PlayerEnvironmentInteractionContent(new JsonObject(), new JsonObject()),
            Array.Empty<PlayerEnvironmentInteractionCapability>()),
        Array.Empty<PlayerEnvironmentReferent>(),
        new PlayerEnvironmentBoundActionProjection(
            "sts2.player-environment/bound-actions-1",
            "complete",
            0,
            0,
            512,
            "test",
            Array.Empty<PlayerEnvironmentBoundAction>()),
        reads,
        new PlayerEnvironmentCompleteness(
            "complete",
            "fixture",
            "fixture",
            Array.Empty<string>(),
            Array.Empty<string>()),
        new PlayerEnvironmentSessionReference("runtime-a", "environment-a"),
        new PlayerEnvironmentInformationPolicy("player_visible_v1", "fixture", false, "omit"));
}
