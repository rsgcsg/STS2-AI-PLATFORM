using STS2Connector.NativeUi;
using STS2Connector.PlayerEnvironment.Protocol;
using STS2Connector.PlayerEnvironment.Witness;
using STS2Platform.NativeFoundation;
using System.Text.Json.Nodes;

namespace STS2Connector.Host.Tests;

public sealed class ProcessLocalNativeWitnessTests
{
    [Fact]
    public void SemanticActionKeyIsDeterministicAndRoleOrdered()
    {
        string first = NativeSemanticActionCatalog.BuildKey(
            "play",
            "card-1",
            new Dictionary<string, string>
            {
                ["z_target"] = "enemy-2",
                ["a_mode"] = "normal"
            });
        string second = NativeSemanticActionCatalog.BuildKey(
            "play",
            "card-1",
            new Dictionary<string, string>
            {
                ["a_mode"] = "normal",
                ["z_target"] = "enemy-2"
            });

        Assert.Equal(first, second);
        Assert.Equal("play|card-1|a_mode=normal,z_target=enemy-2", first);
    }

    [Fact]
    public void ExactNativeInstanceSelectsOneDuplicateLookingCard()
    {
        var entities = new NativeEntityRegistry();
        var first = new object();
        var second = new object();
        string firstId = entities.GetId(first, "card");
        string secondId = entities.GetId(second, "card");
        PlayerEnvironmentBoundAction firstAction = Action("action-first", "play", firstId);
        PlayerEnvironmentBoundAction secondAction = Action("action-second", "play", secondId);
        ProcessLocalNativeWitnessFrame frame = Frame(
            entities,
            firstAction,
            secondAction);

        ProcessLocalNativeMatch result = frame.Resolve(new ProcessLocalObservedAction(
            "play",
            second,
            new Dictionary<string, object>()));

        Assert.Equal("exact_unique", result.Status);
        Assert.Equal(1, result.MatchCount);
        Assert.Equal("action-second", result.BoundActionId);
    }

    [Fact]
    public void ExactTargetReferenceDisambiguatesTargetedAction()
    {
        var entities = new NativeEntityRegistry();
        var card = new object();
        var firstTarget = new object();
        var secondTarget = new object();
        string cardId = entities.GetId(card, "card");
        PlayerEnvironmentBoundAction first = Action(
            "action-first-target",
            "play",
            cardId,
            new PlayerEnvironmentBoundActionArgument(
                "target",
                entities.GetId(firstTarget, "enemy")));
        PlayerEnvironmentBoundAction second = Action(
            "action-second-target",
            "play",
            cardId,
            new PlayerEnvironmentBoundActionArgument(
                "target",
                entities.GetId(secondTarget, "enemy")));

        ProcessLocalNativeMatch result = Frame(entities, first, second).Resolve(
            new ProcessLocalObservedAction(
                "play",
                card,
                new Dictionary<string, object> { ["target"] = firstTarget }));

        Assert.Equal("exact_unique", result.Status);
        Assert.Equal("action-first-target", result.BoundActionId);
    }

    [Fact]
    public void MissingAndAmbiguousMappingsFailClosed()
    {
        var entities = new NativeEntityRegistry();
        var card = new object();
        string cardId = entities.GetId(card, "card");
        ProcessLocalNativeWitnessFrame missingFrame = Frame(
            entities,
            Action("action-card", "play", cardId));
        ProcessLocalNativeMatch missing = missingFrame.Resolve(
            new ProcessLocalObservedAction(
                "play",
                new object(),
                new Dictionary<string, object>()));

        PlayerEnvironmentBoundAction duplicateA = Action("action-a", "play", cardId);
        PlayerEnvironmentBoundAction duplicateB = Action("action-b", "play", cardId);
        ProcessLocalNativeMatch ambiguous = Frame(entities, duplicateA, duplicateB).Resolve(
            new ProcessLocalObservedAction(
                "play",
                card,
                new Dictionary<string, object>()));

        Assert.Equal("zero", missing.Status);
        Assert.Null(missing.BoundActionId);
        Assert.Equal("ambiguous", ambiguous.Status);
        Assert.Equal(2, ambiguous.MatchCount);
        Assert.Null(ambiguous.BoundActionId);
    }

    [Fact]
    public void IncompleteOrNonInteractiveFrameCannotMatch()
    {
        var entities = new NativeEntityRegistry();
        var card = new object();
        PlayerEnvironmentBoundAction action = Action(
            "action-card",
            "play",
            entities.GetId(card, "card"));
        ProcessLocalNativeWitnessFrame frame = Frame(
            entities,
            new[] { action },
            snapshotStatus: "settling",
            projectionStatus: "complete");

        ProcessLocalNativeMatch result = frame.Resolve(new ProcessLocalObservedAction(
            "play",
            card,
            new Dictionary<string, object>()));

        Assert.Equal("frame_not_authoritative", result.Status);
        Assert.Equal(0, result.MatchCount);
    }

    private static PlayerEnvironmentBoundAction Action(
        string id,
        string verb,
        string? subject,
        params PlayerEnvironmentBoundActionArgument[] arguments) =>
        new(id, verb, "interaction-test", subject, arguments, id);

    private static ProcessLocalNativeWitnessFrame Frame(
        NativeEntityRegistry entities,
        params PlayerEnvironmentBoundAction[] actions) =>
        Frame(entities, actions, "interactive", "complete");

    private static ProcessLocalNativeWitnessFrame Frame(
        NativeEntityRegistry entities,
        IReadOnlyList<PlayerEnvironmentBoundAction> actions,
        string snapshotStatus,
        string projectionStatus)
    {
        var snapshot = new PlayerEnvironmentSnapshot(
            PlayerEnvironmentContract.ProtocolVersion,
            PlayerEnvironmentContract.SnapshotSchema,
            "snapshot-test",
            1,
            DateTimeOffset.UnixEpoch,
            snapshotStatus,
            null,
            new PlayerEnvironmentInteraction(
                "interaction-test",
                "combat_turn",
                "choosing",
                null,
                "surface-test",
                new PlayerEnvironmentInteractionContent(new JsonObject(), new JsonObject()),
                Array.Empty<PlayerEnvironmentInteractionCapability>()),
            Array.Empty<PlayerEnvironmentReferent>(),
            new PlayerEnvironmentBoundActionProjection(
                "sts2.player-environment/bound-actions-1",
                projectionStatus,
                actions.Count,
                actions.Count,
                512,
                "test",
                actions),
            Array.Empty<PlayerEnvironmentReadOpportunity>(),
            new PlayerEnvironmentCompleteness(
                "complete",
                "test",
                "test",
                Array.Empty<string>(),
                Array.Empty<string>()),
            new PlayerEnvironmentSessionReference("runtime-test", "environment-test"),
            new PlayerEnvironmentInformationPolicy(
                "player_visible_v1",
                "test",
                false,
                "omit"));
        return new ProcessLocalNativeWitnessFrame(
            snapshot,
            null!,
            new string('a', 64),
            false,
            entities.CaptureExactReferences(
                actions.SelectMany(action => action.Arguments
                        .Select(argument => argument.ReferentId)
                    .Append(action.SubjectReferentId))
                    .Where(referentId => referentId != null)
                    .Cast<string>()),
            actions.Select(action => action.BoundActionId).ToHashSet(StringComparer.Ordinal));
    }
}
