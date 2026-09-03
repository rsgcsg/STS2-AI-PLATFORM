using STS2Connector.LiveHost;
using STS2Platform.NativeFoundation;
using MegaCrit.Sts2.Core.Map;

namespace STS2Connector.Host.Tests;

public sealed class NativeFoundationContractTests
{
    [Fact]
    public void VisibleProjectionCannotCreateSemanticAuthority()
    {
        var visible = new object();
        var hidden = new object();
        var decision = new NativeCombatDecision(
            "captured",
            "combat_play_phase",
            true,
            new[]
            {
                Action("visible", visible),
                Action("hidden", hidden),
                Action("other-verb", visible, "use")
            },
            Array.Empty<string>(),
            null);

        IReadOnlyList<NativeSemanticAction> projection =
            NativeDecisionProjection.VisibleSubjects(decision, "play", new[] { visible });

        NativeSemanticAction action = Assert.Single(projection);
        Assert.Equal("visible", action.Key);
        Assert.DoesNotContain(projection, candidate => ReferenceEquals(candidate.NativeSubject, hidden));
        Assert.Equal(3, decision.Actions.Count);
    }

    [Fact]
    public void PresentationProfilesProjectTheSameCanonicalDecision()
    {
        var first = new object();
        var second = new object();
        var decision = new NativeCombatDecision(
            "captured",
            "combat_play_phase",
            true,
            new[] { Action("first", first), Action("second", second) },
            Array.Empty<string>(),
            null);

        IReadOnlyList<NativeSemanticAction> visibleHost =
            NativeDecisionProjection.VisibleSubjects(decision, "play", new[] { first });
        IReadOnlyList<NativeSemanticAction> headlessHost =
            NativeDecisionProjection.VisibleSubjects(decision, "play", new[] { first, second });

        Assert.Equal(new[] { "first", "second" }, decision.Actions.Select(action => action.Key));
        Assert.Equal("first", Assert.Single(visibleHost).Key);
        Assert.Equal(new[] { "first", "second" }, headlessHost.Select(action => action.Key));
        Assert.Equal(new[] { "first", "second" }, decision.Actions.Select(action => action.Key));
    }

    [Fact]
    public void MapDestinationMechanicsUseTheNativeMapTopology()
    {
        var start = new MapPoint(0, -1) { PointType = MapPointType.Ancient };
        var boss = new MapPoint(0, 2) { PointType = MapPointType.Boss };
        var map = new MockCraftedActMap(2, 2, start, boss);
        map.Put(0, 0);
        map.Put(1, 0);
        map.Put(0, 1);
        MapPoint left = map.GetPoint(0, 0)!;
        MapPoint right = map.GetPoint(1, 0)!;
        MapPoint last = map.GetPoint(0, 1)!;
        start.AddChildPoint(left);
        start.AddChildPoint(right);
        left.AddChildPoint(last);

        Assert.Same(start, Assert.Single(NativeMapDecisionProvider.GetDestinations(
            map,
            Array.Empty<MapCoord>(),
            null,
            point => point.Children)));
        Assert.Equal(
            new[] { left, right },
            NativeMapDecisionProvider.GetDestinations(
                map,
                new[] { start.coord },
                start,
                point => point.Children));
        Assert.Same(boss, Assert.Single(NativeMapDecisionProvider.GetDestinations(
            map,
            new[] { start.coord, left.coord, last.coord },
            last,
            _ => throw new InvalidOperationException("Last-row travel bypassed the boss."))));
    }

    [Fact]
    public void NonCombatPresentationCannotCreateOrDuplicateNativeActions()
    {
        var nativeReward = new object();
        var presentationOnly = new object();
        var actions = new[]
        {
            Action("claim-native", nativeReward, "claim")
        };

        Assert.Empty(NativeDecisionProjection.VisibleSubjects(
            actions,
            "claim",
            new[] { presentationOnly }));
        Assert.Equal(
            "claim-native",
            Assert.Single(NativeDecisionProjection.VisibleSubjects(
                actions,
                "claim",
                new[] { nativeReward, nativeReward })).Key);
    }

    [Fact]
    public void ExactPresentationBijectionRejectsMissingAndDuplicateBindings()
    {
        var first = new object();
        var second = new object();

        Assert.True(NativeDecisionProjection.HasExactReferenceBijection(
            new[] { first, second },
            new[] { second, first }));
        Assert.False(NativeDecisionProjection.HasExactReferenceBijection(
            new[] { first, second },
            new[] { first, first }));
        Assert.False(NativeDecisionProjection.HasExactReferenceBijection(
            new[] { first },
            new[] { first, second }));
    }

    [Fact]
    public void NativeMembershipRequiresOneExactSubjectReference()
    {
        var reward = new object();
        var duplicate = new NativeRewardDecision(
            "captured",
            "room_rewards",
            true,
            true,
            Array.Empty<MegaCrit.Sts2.Core.Rewards.Reward>(),
            new[]
            {
                Action("first", reward, "claim"),
                Action("second", reward, "claim")
            },
            Array.Empty<string>(),
            null);
        var exact = duplicate with { Actions = new[] { Action("only", reward, "claim") } };

        Assert.False(NativeSemanticActionCatalog.ContainsExactlyOnce(
            duplicate.Actions,
            "claim",
            reward));
        Assert.True(NativeSemanticActionCatalog.ContainsExactlyOnce(
            exact.Actions,
            "claim",
            reward));
        Assert.False(NativeSemanticActionCatalog.ContainsExactlyOnce(
            exact.Actions,
            "claim",
            new object()));
    }

    [Fact]
    public void NativeSelectionJoinsPublicAliasByExactIdentityWithoutCreatingLegality()
    {
        var destination = new object();
        var other = new object();
        NativeSemanticAction[] catalog =
        {
            Action("travel-destination", destination, "travel"),
            Action("travel-other", other, "travel")
        };

        NativeObservedSemanticAction exact =
            NativeSemanticActionCatalog.DescribeByIdentity(
                catalog,
                "VoteForMapCoordAction",
                destination);
        NativeObservedSemanticAction absent =
            NativeSemanticActionCatalog.DescribeByIdentity(
                catalog,
                "VoteForMapCoordAction",
                new object());

        Assert.Equal("exact_once", exact.Membership);
        Assert.Equal("travel-destination", exact.Key);
        Assert.Equal("absent", absent.Membership);
        Assert.Null(absent.Key);
    }

    [Theory]
    [InlineData("CombatRoom", "NRewardsScreen", false, "room_rewards", "reward_claim")]
    [InlineData("CombatRoom", "NCardRewardSelectionScreen", false, "card_reward", "card_reward_selection")]
    [InlineData("EventRoom", null, true, "map_navigation", "map_navigation")]
    [InlineData("TreasureRoom", null, false, "treasure", "treasure_room")]
    public void CrossDomainProbeSeparatesSemanticAndInputOwners(
        string room,
        string? overlay,
        bool mapOpen,
        string semantic,
        string input)
    {
        NativeDomainOwnerObservation result =
            NativeDomainOwnerProbe.Classify(room, overlay, mapOpen);

        Assert.Equal("captured", result.Status);
        Assert.Equal(semantic, result.SemanticDomain);
        Assert.Equal(input, result.InputDomain);
        Assert.Contains("owner_discriminator_not_legality", result.NonClaims);
    }

    [Theory]
    [InlineData(true, "captured", true, true)]
    [InlineData(false, "captured", true, false)]
    [InlineData(true, "capture_failed", true, false)]
    [InlineData(true, "captured", false, false)]
    public void CombatProjectionRequiresPresentationAndSemanticReadiness(
        bool presentationReady,
        string semanticStatus,
        bool semanticDecisionOpen,
        bool expected)
    {
        Assert.Equal(
            expected,
            CombatTurnSurfaceReader.IsProjectionReady(
                presentationReady,
                semanticStatus,
                semanticDecisionOpen));
    }

    [Theory]
    [InlineData(false, false, false, false, false, "closed")]
    [InlineData(false, false, true, false, false, "opening")]
    [InlineData(false, false, false, true, false, "closed")]
    [InlineData(true, true, true, true, false, "relic_choice")]
    [InlineData(true, true, true, true, true, "resolving")]
    [InlineData(true, true, true, false, false, "resolving")]
    [InlineData(true, false, true, false, false, "completed")]
    public void TreasureStageComesFromNativeLifecycleRatherThanClickability(
        bool chestOpened,
        bool collectionOpen,
        bool chestOpeningObserved,
        bool hasRelicCollection,
        bool localVoteCommitted,
        string expected)
    {
        IReadOnlyList<MegaCrit.Sts2.Core.Models.RelicModel>? relics =
            hasRelicCollection
                ? new MegaCrit.Sts2.Core.Models.RelicModel[] { null! }
                : null;

        Assert.Equal(expected, NativeTreasureDecisionProvider.ClassifyStage(
            chestOpened,
            collectionOpen,
            chestOpeningObserved,
            relics,
            localVoteCommitted));
    }

    [Fact]
    public void TreasureMembershipRequiresOneExactNativeSubject()
    {
        var room = new object();
        var decision = new NativeTreasureDecision(
            "captured",
            "treasure",
            "completed",
            true,
            true,
            Array.Empty<MegaCrit.Sts2.Core.Models.RelicModel>(),
            new[] { Action("proceed", room, "proceed") },
            Array.Empty<string>(),
            null);

        Assert.True(NativeSemanticActionCatalog.ContainsExactlyOnce(
            decision.Actions,
            "proceed",
            room));
        Assert.False(NativeSemanticActionCatalog.ContainsExactlyOnce(
            decision.Actions,
            "proceed",
            new object()));
        Assert.False(NativeSemanticActionCatalog.ContainsExactlyOnce(
            new[] { Action("a", room, "proceed"), Action("b", room, "proceed") },
            "proceed",
            room));
    }

    private static NativeSemanticAction Action(
        string key,
        object subject,
        string verb = "play") =>
        new(
            key,
            verb,
            key,
            subject,
            Array.Empty<NativeSemanticOperand>(),
            "fixture_native_validator");
}
