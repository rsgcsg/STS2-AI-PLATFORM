using System.Text.Json.Nodes;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class SemanticTransitionProjectionTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-09-01T00:00:00Z");

    [Fact]
    public void ProvedSemanticTransitionProjectsWithoutBecomingAuthority()
    {
        RecorderEnvironmentIdentity environment = Environment();
        CurrentDecisionFrame pre = Frame("s0", "combat_turn", includeChosenAction: true);
        CurrentDecisionFrame successor = Frame("s1", "combat_turn", includeChosenAction: false);
        SemanticBoundaryTraceDraft draft = ProvedDraft(pre, successor);

        CurrentDecisionRecord record = SemanticTransitionProjection.CreateDecision(
            draft,
            environment,
            "session-test",
            "timeline-test",
            HumanCaptureProfiles.FullRunReadRich.ProfileId);

        Assert.Equal("record-a1", record.RecordId);
        Assert.Equal("ordinary_combat", record.DecisionFamily);
        Assert.Equal("h0", record.Pre.SnapshotId);
        Assert.Equal("s1", record.Successor.SnapshotId);
        Assert.Equal(T0.AddSeconds(1), record.RecordedAt);
        Assert.Same(environment, record.Environment);
        Assert.True(CurrentDecisionRecordValidator.Validate(record).Valid);
    }

    [Fact]
    public void UnknownTransitionCannotBeProjectedIntoDecisionData()
    {
        CurrentDecisionFrame pre = Frame("s0", "combat_turn", includeChosenAction: true);
        SemanticBoundaryTraceDraft draft = ProvedDraft(
            pre,
            Frame("s1", "combat_turn", includeChosenAction: false)) with
        {
            Kind = SemanticBoundaryTraceKinds.TransitionUnknown,
            SemanticSuccessor = null,
            ProofStatus = "terminal_close_unknown"
        };

        Assert.Throws<InvalidDataException>(() => SemanticTransitionProjection.CreateDecision(
            draft,
            Environment(),
            "session-test",
            "timeline-test",
            HumanCaptureProfiles.FullRunReadRich.ProfileId));
    }

    [Fact]
    public void CanonicalProjectionKeepsExactSemanticFrameIdentity()
    {
        CurrentDecisionFrame pre = Frame("s0", "combat_turn", includeChosenAction: true);
        CurrentDecisionFrame successor = Frame("s1", "combat_turn", includeChosenAction: false);
        SemanticBoundaryTraceDraft draft = ProvedDraft(pre, successor) with
        {
            Action = ProvedDraft(pre, successor).Action with
            {
                NativeMechanism = "direct_ui_commit"
            }
        };
        var preRef = new SemanticFrameReference("s0", new string('1', 64), "semantic-frames/pre.json");
        var successorRef = new SemanticFrameReference("s1", new string('2', 64), "semantic-frames/successor.json");

        CanonicalTransitionEvidence value = SemanticTransitionProjection.CreateCanonical(
            draft,
            preRef,
            successorRef,
            null,
            "session-test",
            "timeline-test");

        Assert.Equal("direct_ui_commit", value.NativeMechanism);
        Assert.Equal("game-action-a1", value.ActionWitnessId);
        Assert.Equal("public_bound_actions", value.ActionSpaceAuthority);
        Assert.Empty(CanonicalTransitionEvidenceValidator.Validate(value));
    }

    [Fact]
    public void GameActionWithoutTypedExecutionActionSpaceFailsClosed()
    {
        CurrentDecisionFrame pre = Frame("s0", "combat_turn", includeChosenAction: true);
        CurrentDecisionFrame successor = Frame("s1", "combat_turn", includeChosenAction: false);
        SemanticBoundaryTraceDraft draft = ProvedDraft(pre, successor);

        Assert.Throws<InvalidDataException>(() => SemanticTransitionProjection.CreateCanonical(
            draft,
            new SemanticFrameReference("s0", new string('1', 64), "semantic-frames/pre.json"),
            new SemanticFrameReference("s1", new string('2', 64), "semantic-frames/successor.json"),
            null,
            "session-test",
            "timeline-test"));
    }

    [Theory]
    [InlineData("play", "card-a1")]
    [InlineData("end_turn", null)]
    [InlineData("use", "potion-a1")]
    public void NativeExecutionCatalogQualifiesWhilePublicCatalogIsSettling(
        string verb,
        string? subject)
    {
        CurrentDecisionFrame pre = Frame(
            "s0",
            "combat_turn",
            includeChosenAction: false,
            status: "settling");
        CurrentDecisionFrame successor = Frame(
            "s1",
            "combat_turn",
            includeChosenAction: false);
        SemanticBoundaryTraceDraft original = ProvedDraft(pre, successor);
        RecordedBoundAction selected = original.Action.BoundAction! with
        {
            Verb = verb,
            SubjectReferentId = subject,
            Arguments = verb == "use"
                ? new Dictionary<string, string> { ["target"] = "enemy-a1" }
                : new Dictionary<string, string>()
        };
        SemanticActionReference action = original.Action with { BoundAction = selected };
        ExecutionSemanticActionSpaceEvidence evidence = SemanticActionSpace(action);
        SemanticBoundaryTraceDraft draft = original with { Action = action };
        draft = draft with { ExecutionSemanticActionSpace = evidence };
        var preRef = new SemanticFrameReference("s0", new string('1', 64), "semantic-frames/pre.json");
        var successorRef = new SemanticFrameReference("s1", new string('2', 64), "semantic-frames/successor.json");
        var actionSpaceRef = new ExecutionSemanticActionSpaceReference(
            action.ActionWitnessId,
            evidence.SemanticStateDigest,
            evidence.SemanticCatalogDigest,
            new string('3', 64),
            "semantic-action-spaces/action.json");

        CanonicalTransitionEvidence value = SemanticTransitionProjection.CreateCanonical(
            draft,
            preRef,
            successorRef,
            actionSpaceRef,
            "session-test",
            "timeline-test");

        Assert.Equal("native_semantic_execution", value.ActionSpaceAuthority);
        Assert.Same(actionSpaceRef, value.ExecutionSemanticActionSpaceRef);
        Assert.Empty(CanonicalTransitionEvidenceValidator.Validate(value));
    }

    [Fact]
    public void NativeExecutionCatalogMismatchFailsClosed()
    {
        CurrentDecisionFrame pre = Frame("s0", "combat_turn", includeChosenAction: false);
        SemanticBoundaryTraceDraft original = ProvedDraft(
            pre,
            Frame("s1", "combat_turn", includeChosenAction: false));
        ExecutionSemanticActionSpaceEvidence evidence = SemanticActionSpace(original.Action) with
        {
            ObservedActionKey = "play|different-card|"
        };
        SemanticBoundaryTraceDraft draft = original with
        {
            ExecutionSemanticActionSpace = evidence
        };
        var reference = new ExecutionSemanticActionSpaceReference(
            original.Action.ActionWitnessId,
            evidence.SemanticStateDigest,
            evidence.SemanticCatalogDigest,
            new string('3', 64),
            "semantic-action-spaces/action.json");

        Assert.Throws<InvalidDataException>(() =>
            SemanticTransitionProjection.CreateCanonical(
                draft,
                new SemanticFrameReference("s0", new string('1', 64), "pre.json"),
                new SemanticFrameReference("s1", new string('2', 64), "successor.json"),
                reference,
                "session-test",
                "timeline-test"));
    }

    [Fact]
    public void NativeSemanticDecisionWithDifferentHumanBindingFailsClosed()
    {
        SemanticBoundaryTraceDraft original = ProvedDraft(
            Frame("s0", "combat_turn", includeChosenAction: false),
            Frame("s1", "combat_turn", includeChosenAction: false));
        ExecutionSemanticActionSpaceEvidence evidence = SemanticActionSpace(original.Action) with
        {
            HumanBoundActionId = "different-human-bound-action"
        };

        Assert.Throws<InvalidDataException>(() =>
            SemanticTransitionProjection.CreateCanonical(
                original with { ExecutionSemanticActionSpace = evidence },
                new SemanticFrameReference("s0", new string('1', 64), "pre.json"),
                new SemanticFrameReference("s1", new string('2', 64), "successor.json"),
                new ExecutionSemanticActionSpaceReference(
                    original.Action.ActionWitnessId,
                    evidence.SemanticStateDigest,
                    evidence.SemanticCatalogDigest,
                    new string('3', 64),
                    "native-semantic-decisions/action.json"),
                "session-test",
                "timeline-test"));
    }

    [Fact]
    public void ExactHumanBindingMayJoinDifferentPublicAndNativeVerbs()
    {
        SemanticBoundaryTraceDraft original = ProvedDraft(
            Frame("map-pre", "map_navigation", includeChosenAction: false),
            Frame("combat-ready", "combat_turn", includeChosenAction: false));
        RecordedBoundAction publicAction = original.Action.BoundAction! with
        {
            Verb = "activate",
            SubjectReferentId = "map-point-a1"
        };
        SemanticActionReference action = original.Action with { BoundAction = publicAction };
        ExecutionSemanticActionSpaceEvidence evidence = SemanticActionSpace(action) with
        {
            Phase = "before_native_action_admission",
            Scope = "map_navigation",
            Actions = new[]
            {
                new ExecutionSemanticAction(
                    "travel|map-point-a1|",
                    "travel",
                    "map-point-a1",
                    new Dictionary<string, string>(),
                    "MapTravel.GetTravelablePointsFrom")
            },
            ObservedActionKey = "travel|map-point-a1|"
        };
        SemanticBoundaryTraceDraft draft = original with
        {
            Action = action,
            ExecutionSemanticActionSpace = evidence
        };

        CanonicalTransitionEvidence canonical = SemanticTransitionProjection.CreateCanonical(
            draft,
            new SemanticFrameReference("map-pre", new string('1', 64), "pre.json"),
            new SemanticFrameReference("combat-ready", new string('2', 64), "successor.json"),
            new ExecutionSemanticActionSpaceReference(
                action.ActionWitnessId,
                evidence.SemanticStateDigest,
                evidence.SemanticCatalogDigest,
                new string('3', 64),
                "native-semantic-decisions/action.json"),
            "session-test",
            "timeline-test");

        Assert.Equal("activate", canonical.Action.Verb);
        Assert.Equal("native_semantic_execution", canonical.ActionSpaceAuthority);
    }

    private static SemanticBoundaryTraceDraft ProvedDraft(
        CurrentDecisionFrame pre,
        CurrentDecisionFrame successor)
    {
        var action = new SemanticActionReference(
            "game-action-a1",
            1,
            "record-a1",
            "run-0001",
            "PlayCardAction",
            1,
            pre.SnapshotId)
        {
            NativeMechanism = "game_action",
            NativeWitness = new NativeWitnessEvidence(
                "native_card_play_ui",
                "PlayCardAction",
                "card-a1",
                new Dictionary<string, string>(),
                T0),
            Mapping = new ExactMappingEvidence(
                "exact_unique",
                1,
                "reference_equality_to_frozen_host_binding",
                null),
            BoundAction = new RecordedBoundAction(
                "bound-action-a1",
                "play",
                "card-a1",
                new Dictionary<string, string>(),
                "Play card")
        };
        var boundary = new SemanticBoundaryObservation(
            SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
            T0.AddSeconds(1),
            successor.SnapshotId,
            "interactive",
            "complete",
            successor.InteractionId,
            successor.InteractionKind,
            successor,
            "next-action")
        {
            StateCompleteness = "complete",
            RequiredReadsStatus = "complete"
        };
        return new SemanticBoundaryTraceDraft(
            SemanticBoundaryTraceKinds.TransitionProved,
            action,
            "proved_execution_handoff_boundary",
            "next-action",
            boundary,
            pre,
            successor,
            null,
            Array.Empty<string>())
        {
            HumanObservation = Frame("h0", pre.InteractionKind, includeChosenAction: true)
        };
    }

    private static CurrentDecisionFrame Frame(
        string snapshotId,
        string interactionKind,
        bool includeChosenAction,
        string status = "interactive")
    {
        JsonArray actions = includeChosenAction
            ? new JsonArray(new JsonObject
            {
                ["bound_action_id"] = "bound-action-a1",
                ["verb"] = "play",
                ["subject_referent_id"] = "card-a1",
                ["arguments"] = new JsonArray(),
                ["label"] = "Play card"
            })
            : new JsonArray();
        var snapshot = new JsonObject
        {
            ["snapshot_id"] = snapshotId,
            ["status"] = status,
            ["session"] = new JsonObject
            {
                ["runtime_instance_id"] = "runtime-test",
                ["environment_fingerprint"] = new string('e', 64)
            },
            ["interaction"] = new JsonObject
            {
                ["interaction_id"] = $"interaction-{snapshotId}",
                ["kind"] = interactionKind,
                ["content_schema"] = "sts2.player-environment/snapshot-1"
            },
            ["completeness"] = new JsonObject { ["status"] = "complete" },
            ["bound_actions"] = new JsonObject
            {
                ["status"] = "complete",
                ["actions"] = actions
            }
        };
        JsonNode catalog = snapshot["bound_actions"]!;
        return new CurrentDecisionFrame(
            snapshotId,
            $"interaction-{snapshotId}",
            interactionKind,
            "sts2.player-environment/snapshot-1",
            EvidenceIdentity.Sha256Json(catalog),
            actions.Count,
            snapshot,
            Array.Empty<ReadEvidence>());
    }

    private static ExecutionSemanticActionSpaceEvidence SemanticActionSpace(
        SemanticActionReference action)
    {
        RecordedBoundAction selected = action.BoundAction!;
        string key = $"{selected.Verb}|{selected.SubjectReferentId ?? "-"}|";
        return new ExecutionSemanticActionSpaceEvidence(
            ExecutionSemanticActionSpaceContract.SchemaVersion,
            ExecutionSemanticActionSpaceContract.Schema,
            action.ActionWitnessId,
            "before_execution",
            "captured",
            "combat_play_phase",
            new string('a', 64),
            JsonNode.Parse("{\"turn\":1}")!,
            new string('b', 64),
            new[]
            {
                new ExecutionSemanticAction(
                    key,
                    selected.Verb,
                    selected.SubjectReferentId,
                    selected.Arguments,
                    "native_test_validator")
            },
            key,
            "exact_once",
            1,
            new[] { "native_test_validator" },
            new[] { "not_public_delivery_authority" },
            null)
        {
            HumanBoundActionId = selected.BoundActionId
        };
    }

    private static RecorderEnvironmentIdentity Environment() => new(
        new ExactGameIdentity(
            "0.111.0",
            "test",
            new string('a', 64),
            "11111111-1111-1111-1111-111111111111"),
        new ExactArtifactIdentity(
            "connector",
            "test",
            new string('b', 40),
            new string('c', 64),
            new string('d', 64),
            "22222222-2222-2222-2222-222222222222"),
        new ExactArtifactIdentity(
            "annotator",
            "test",
            new string('f', 40),
            new string('1', 64),
            new string('2', 64),
            "33333333-3333-3333-3333-333333333333"),
        "sts2.player-environment/1",
        "runtime-test",
        new string('e', 64),
        "exact_platform_modset",
        new string('3', 64));
}
