using STS2Connector.LiveHost;
using STS2Platform.NativeFoundation;

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

    [Theory]
    [InlineData("CombatRoom", "NRewardsScreen", false, "room_rewards", "reward_claim")]
    [InlineData("CombatRoom", "NCardRewardSelectionScreen", false, "card_reward", "card_reward_selection")]
    [InlineData("EventRoom", null, true, "map_navigation", "map_navigation")]
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
