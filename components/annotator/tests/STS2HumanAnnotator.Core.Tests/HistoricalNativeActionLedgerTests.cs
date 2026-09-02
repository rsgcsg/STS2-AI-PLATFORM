using System.Text.Json;
using System.Text.Json.Nodes;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class HistoricalNativeActionLedgerTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public void PlayerChoicePauseResumeHistoryIsValidAndOrdered()
    {
        HistoricalNativeActionLedgerEvent[] events =
        {
            Event(1, "accepted", "waiting_for_execution"),
            Event(2, "started", "executing"),
            Event(3, "paused_for_player_choice", "gathering_player_choice"),
            Event(4, "ready_to_resume", "ready_to_resume_executing", queueId: 2),
            Event(5, "resumed", "executing", queueId: 2),
            Event(6, "finished", "finished", queueId: 2),
            Event(7, "strict_transition_admitted", "finished", queueId: 2)
        };

        Assert.Empty(HistoricalNativeActionLedgerValidator.Validate(events));
    }

    [Fact]
    public void LifecycleAfterTerminalAndIdentityDriftFailAudit()
    {
        HistoricalNativeActionLedgerEvent accepted = Event(1, "accepted", "waiting_for_execution");
        HistoricalNativeActionLedgerEvent finished = Event(2, "finished", "finished");
        HistoricalNativeActionLedgerEvent lateResume = Event(3, "resumed", "executing");
        HistoricalNativeActionLedgerEvent drift = Event(4, "strict_transition_invalidated", "finished") with
        {
            RecordId = "record-other"
        };

        IReadOnlyList<string> errors = HistoricalNativeActionLedgerValidator.Validate(
            new[] { accepted, finished, lateResume, drift });

        Assert.Contains("native_action_lifecycle_after_terminal", errors);
        Assert.Contains("native_action_identity_drift", errors);
    }

    [Fact]
    public void LifecycleMustStartAtAcceptedAndFollowExactPauseResumeOrder()
    {
        HistoricalNativeActionLedgerEvent[] events =
        {
            Event(2, "accepted", "waiting_for_execution"),
            Event(3, "resumed", "executing"),
            Event(4, "cancelled", "cancelled"),
            Event(5, "strict_transition_invalidated", "cancelled")
        };

        IReadOnlyList<string> errors = HistoricalNativeActionLedgerValidator.Validate(events);

        Assert.Contains("native_action_sequence_does_not_start_at_one", errors);
        Assert.Contains("native_action_lifecycle_order_invalid", errors);
    }

    [Fact]
    public void CurrentAcceptedEventRequiresDecisionEvidenceAndDoesNotRepeatIt()
    {
        HistoricalNativeActionLedgerEvent accepted = Event(1, "accepted", "waiting_for_execution") with
        {
            DecisionPre = null
        };
        HistoricalNativeActionLedgerEvent started = Event(2, "started", "executing") with
        {
            DecisionPre = DecisionPre()
        };

        IReadOnlyList<string> errors = HistoricalNativeActionLedgerValidator.Validate(
            new[] { accepted, started });

        Assert.Contains("native_action_decision_evidence_invalid", errors);
        Assert.Contains("native_action_decision_evidence_repeated", errors);
    }

    [Fact]
    public void LegacyV1LedgerRemainsReadableWithoutDecisionEvidence()
    {
        HistoricalNativeActionLedgerEvent accepted = Event(
            1,
            "accepted",
            "waiting_for_execution",
            legacy: true);
        HistoricalNativeActionLedgerEvent started = Event(
            2,
            "started",
            "executing",
            legacy: true);
        HistoricalNativeActionLedgerEvent finished = Event(
            3,
            "finished",
            "finished",
            legacy: true);
        HistoricalNativeActionLedgerEvent invalidated = Event(
            4,
            "strict_transition_invalidated",
            "finished",
            legacy: true);

        Assert.Empty(HistoricalNativeActionLedgerValidator.Validate(
            new[] { accepted, started, finished, invalidated }));
    }

    [Fact]
    public void CurrentStoreDoesNotCreateHistoricalNativeLedger()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sts2-native-ledger-{Guid.NewGuid():N}");
        try
        {
            HumanCaptureProfile profile = HumanCaptureProfiles.CombatReadRich;
            var manifest = new CurrentRecordingManifest(
                CurrentRecordingContract.SchemaVersion,
                CurrentRecordingContract.ManifestSchema,
                "session-test",
                "timeline-test",
                T0,
                CurrentRecordingContract.ProductVersion,
                new string('a', 40),
                "osx-arm64",
                profile.ProfileId,
                EvidenceIdentity.Sha256Json(profile),
                profile.SupportedActionFamilies,
                profile.NonClaims);
            using (RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile))
            {
                Assert.False(File.Exists(
                    Path.Combine(store.DirectoryPath, "native-action-ledger.jsonl")));
            }

            // Historical ledger events remain directly readable for archival
            // verification, but the current store has no production writer.
            IReadOnlyList<string> errors = HistoricalNativeActionLedgerValidator.Validate(
                new[]
                {
                    Event(1, "accepted", "waiting_for_execution", legacy: true),
                    Event(2, "started", "executing", legacy: true),
                    Event(3, "finished", "finished", legacy: true),
                    Event(4, "strict_transition_invalidated", "finished", legacy: true)
                });
            Assert.Empty(errors);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static HistoricalNativeActionLedgerEvent Event(
        long sequence,
        string kind,
        string nativeState,
        uint? queueId = 1,
        bool legacy = false) => new(
            legacy
                ? HistoricalNativeActionLedgerContract.LegacySchemaVersion
                : HistoricalNativeActionLedgerContract.SchemaVersion,
            legacy
                ? HistoricalNativeActionLedgerContract.LegacyEventSchema
                : HistoricalNativeActionLedgerContract.EventSchema,
            $"event-{sequence}",
            "session-test",
            "timeline-test",
            "run-0001",
            sequence,
            "game-action-a1",
            1,
            "record-a1",
            T0.AddMilliseconds(sequence),
            kind,
            "PlayCardAction",
            queueId,
            nativeState,
            Array.Empty<string>(),
            "strict_candidate",
            null,
            !legacy && kind == HistoricalNativeActionLifecycleKinds.Accepted ? DecisionPre() : null,
            !legacy && kind == HistoricalNativeActionLifecycleKinds.Accepted
                ? new NativeWitnessEvidence(
                    "native_human_action",
                    "PlayCardAction",
                    "card-a1",
                    new Dictionary<string, string>(),
                    T0)
                : null,
            !legacy && kind == HistoricalNativeActionLifecycleKinds.Accepted
                ? new ExactMappingEvidence("exact_unique", 1, "native_witness", null)
                : null,
            !legacy && kind == HistoricalNativeActionLifecycleKinds.Accepted
                ? new RecordedBoundAction(
                    "bound-action-a1",
                    "play",
                    "card-a1",
                    new Dictionary<string, string>(),
                    "Play card")
                : null);

    private static HistoricalReadRichDecisionFrame DecisionPre() => new(
        "snapshot-a1",
        "interaction-a1",
        "combat",
        "sts2.player-environment/snapshot-1",
        new string('a', 64),
        1,
        JsonNode.Parse("{\"surface\":{\"kind\":\"combat\"}}")!,
        Array.Empty<ReadEvidence>());
}
