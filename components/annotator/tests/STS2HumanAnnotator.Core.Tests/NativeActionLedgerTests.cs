using System.Text.Json;
using System.Text.Json.Nodes;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class NativeActionLedgerTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public void RapidAcceptedPairInvalidatesEveryStrictCandidate()
    {
        var ledger = new AcceptedHumanActionLedger();

        AcceptedActionAdmission first = ledger.Accept("targeted-a1");
        AcceptedActionAdmission second = ledger.Accept("targeted-a2");

        Assert.True(first.StrictTransitionEligible);
        Assert.False(second.StrictTransitionEligible);
        Assert.Equal(new[] { "targeted-a1" }, second.PriorOpenActionIds);
        Assert.Equal(new[] { "targeted-a1" }, second.InvalidatedStrictCandidateIds);
        Assert.False(ledger.CanAdmitStrictTransition("targeted-a1"));
        Assert.False(ledger.CanAdmitStrictTransition("targeted-a2"));
    }

    [Fact]
    public void ThreeActionBurstAccountsEveryAcceptedActionWithoutInventingSuccessors()
    {
        var ledger = new AcceptedHumanActionLedger();

        Assert.True(ledger.Accept("targeted-a1").Accounted);
        Assert.True(ledger.Accept("untargeted-a2").Accounted);
        AcceptedActionAdmission third = ledger.Accept("end-turn-a3");

        Assert.True(third.Accounted);
        Assert.Equal(new[] { "targeted-a1", "untargeted-a2" }, third.PriorOpenActionIds);
        Assert.Equal(3, ledger.Count);
        Assert.True(ledger.RecoveryBoundaryRequired);
        Assert.All(new[] { "targeted-a1", "untargeted-a2", "end-turn-a3" },
            id => Assert.False(ledger.CanAdmitStrictTransition(id)));
    }

    [Fact]
    public void FinishedSingleActionCanSettleButCancelledActionCannot()
    {
        var ledger = new AcceptedHumanActionLedger();
        ledger.Accept("single-a1");
        Assert.True(ledger.MarkTerminal("single-a1", NativeActionLifecycleKinds.Finished));
        Assert.True(ledger.CanAdmitStrictTransition("single-a1"));
        Assert.True(ledger.CompleteStrictTransition("single-a1"));

        ledger.Accept("cancelled-a2");
        Assert.True(ledger.MarkTerminal("cancelled-a2", NativeActionLifecycleKinds.Cancelled));
        Assert.False(ledger.CanAdmitStrictTransition("cancelled-a2"));
        Assert.True(ledger.ObserveRecoveryBoundary());
        Assert.False(ledger.HasOpenEvidence);
    }

    [Fact]
    public void RecoveryRequiresEveryRapidActionToReachATerminalLifecycle()
    {
        var ledger = new AcceptedHumanActionLedger();
        ledger.Accept("a1");
        ledger.Accept("a2");
        ledger.MarkTerminal("a1", NativeActionLifecycleKinds.Finished);

        Assert.False(ledger.ObserveRecoveryBoundary());
        Assert.True(ledger.HasUnresolvedLifecycle);

        ledger.MarkTerminal("a2", NativeActionLifecycleKinds.Cancelled);
        Assert.True(ledger.ObserveRecoveryBoundary());
        Assert.True(ledger.Accept("a3").StrictTransitionEligible);
    }

    [Fact]
    public void ResetDropsPriorSessionStateAndCapacityFailsClosed()
    {
        var ledger = new AcceptedHumanActionLedger(capacity: 2);
        ledger.Accept("a1");
        ledger.Accept("a2");
        AcceptedActionAdmission overflow = ledger.Accept("a3");

        Assert.False(overflow.Accounted);
        Assert.Equal("native_action_ledger_capacity_exceeded", overflow.FailureCode);

        ledger.Reset();
        Assert.False(ledger.HasOpenEvidence);
        Assert.True(ledger.Accept("new-session-a1").StrictTransitionEligible);
    }

    [Fact]
    public void PauseAndCloseTreatEveryUnresolvedNativeActionAsPendingWork()
    {
        var ledger = new AcceptedHumanActionLedger();
        ledger.Accept("a1");
        ledger.Accept("a2");
        var recording = new RecordingLifecycleSnapshot(
            RecordingLifecycleState.Recording,
            "session-test",
            T0,
            "recording");

        RecordingCommandResult paused = RecordingLifecycleStateMachine.Apply(
            recording,
            RecordingCommandKind.Pause,
            null,
            T0.AddSeconds(1),
            ledger.HasUnresolvedLifecycle);
        RecordingCommandResult closing = RecordingLifecycleStateMachine.Apply(
            paused.Lifecycle,
            RecordingCommandKind.Close,
            null,
            T0.AddSeconds(2),
            ledger.HasUnresolvedLifecycle);

        Assert.True(paused.Accepted);
        Assert.True(closing.Accepted);
        Assert.True(closing.Pending);
        ledger.MarkTerminal("a1", NativeActionLifecycleKinds.Finished);
        Assert.True(ledger.HasUnresolvedLifecycle);
        ledger.MarkTerminal("a2", NativeActionLifecycleKinds.Finished);
        Assert.False(ledger.HasUnresolvedLifecycle);
    }

    [Fact]
    public void PlayerChoicePauseResumeHistoryIsValidAndOrdered()
    {
        NativeActionLedgerEvent[] events =
        {
            Event(1, "accepted", "waiting_for_execution"),
            Event(2, "started", "executing"),
            Event(3, "paused_for_player_choice", "gathering_player_choice"),
            Event(4, "ready_to_resume", "ready_to_resume_executing", queueId: 2),
            Event(5, "resumed", "executing", queueId: 2),
            Event(6, "finished", "finished", queueId: 2),
            Event(7, "strict_transition_admitted", "finished", queueId: 2)
        };

        Assert.Empty(NativeActionLedgerValidator.Validate(events));
    }

    [Fact]
    public void LifecycleAfterTerminalAndIdentityDriftFailAudit()
    {
        NativeActionLedgerEvent accepted = Event(1, "accepted", "waiting_for_execution");
        NativeActionLedgerEvent finished = Event(2, "finished", "finished");
        NativeActionLedgerEvent lateResume = Event(3, "resumed", "executing");
        NativeActionLedgerEvent drift = Event(4, "strict_transition_invalidated", "finished") with
        {
            RecordId = "record-other"
        };

        IReadOnlyList<string> errors = NativeActionLedgerValidator.Validate(
            new[] { accepted, finished, lateResume, drift });

        Assert.Contains("native_action_lifecycle_after_terminal", errors);
        Assert.Contains("native_action_identity_drift", errors);
    }

    [Fact]
    public void LifecycleMustStartAtAcceptedAndFollowExactPauseResumeOrder()
    {
        NativeActionLedgerEvent[] events =
        {
            Event(2, "accepted", "waiting_for_execution"),
            Event(3, "resumed", "executing"),
            Event(4, "cancelled", "cancelled"),
            Event(5, "strict_transition_invalidated", "cancelled")
        };

        IReadOnlyList<string> errors = NativeActionLedgerValidator.Validate(events);

        Assert.Contains("native_action_sequence_does_not_start_at_one", errors);
        Assert.Contains("native_action_lifecycle_order_invalid", errors);
    }

    [Fact]
    public void CurrentAcceptedEventRequiresDecisionEvidenceAndDoesNotRepeatIt()
    {
        NativeActionLedgerEvent accepted = Event(1, "accepted", "waiting_for_execution") with
        {
            DecisionPre = null
        };
        NativeActionLedgerEvent started = Event(2, "started", "executing") with
        {
            DecisionPre = DecisionPre()
        };

        IReadOnlyList<string> errors = NativeActionLedgerValidator.Validate(
            new[] { accepted, started });

        Assert.Contains("native_action_decision_evidence_invalid", errors);
        Assert.Contains("native_action_decision_evidence_repeated", errors);
    }

    [Fact]
    public void LegacyV1LedgerRemainsReadableWithoutDecisionEvidence()
    {
        NativeActionLedgerEvent accepted = Event(
            1,
            "accepted",
            "waiting_for_execution",
            legacy: true);
        NativeActionLedgerEvent started = Event(
            2,
            "started",
            "executing",
            legacy: true);
        NativeActionLedgerEvent finished = Event(
            3,
            "finished",
            "finished",
            legacy: true);
        NativeActionLedgerEvent invalidated = Event(
            4,
            "strict_transition_invalidated",
            "finished",
            legacy: true);

        Assert.Empty(NativeActionLedgerValidator.Validate(
            new[] { accepted, started, finished, invalidated }));
    }

    [Fact]
    public void StorePersistsAdditiveLedgerWithoutChangingDecisionSchema()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sts2-native-ledger-{Guid.NewGuid():N}");
        try
        {
            HumanCaptureProfile profile = HumanCaptureProfiles.CombatReadRichV2;
            var manifest = new RecordingManifestV2(
                HumanRecorderV2Contract.SchemaVersion,
                HumanRecorderV2Contract.ManifestSchema,
                "session-test",
                "timeline-test",
                T0,
                HumanRecorderContract.ProductVersion,
                new string('a', 40),
                "osx-arm64",
                profile.ProfileId,
                EvidenceIdentity.Sha256Json(profile),
                profile.SupportedActionFamilies,
                profile.NonClaims);
            using (V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile))
                store.AppendNativeActionEvent(Event(1, "accepted", "waiting_for_execution"));

            string path = Path.Combine(root, "session-test", "native-action-ledger.jsonl");
            NativeActionLedgerEvent persisted = JsonSerializer.Deserialize<NativeActionLedgerEvent>(
                File.ReadAllText(path), EvidenceJson.Options)!;
            Assert.Equal(NativeActionLedgerContract.EventSchema, persisted.Schema);
            Assert.Equal("record-a1", persisted.RecordId);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static NativeActionLedgerEvent Event(
        long sequence,
        string kind,
        string nativeState,
        uint? queueId = 1,
        bool legacy = false) => new(
            legacy
                ? NativeActionLedgerContract.LegacySchemaVersion
                : NativeActionLedgerContract.SchemaVersion,
            legacy
                ? NativeActionLedgerContract.LegacyEventSchema
                : NativeActionLedgerContract.EventSchema,
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
            !legacy && kind == NativeActionLifecycleKinds.Accepted ? DecisionPre() : null,
            !legacy && kind == NativeActionLifecycleKinds.Accepted
                ? new NativeWitnessEvidence(
                    "native_human_action",
                    "PlayCardAction",
                    "card-a1",
                    new Dictionary<string, string>(),
                    T0)
                : null,
            !legacy && kind == NativeActionLifecycleKinds.Accepted
                ? new ExactMappingEvidence("exact_unique", 1, "native_witness", null)
                : null,
            !legacy && kind == NativeActionLifecycleKinds.Accepted
                ? new RecordedBoundAction(
                    "bound-action-a1",
                    "play",
                    "card-a1",
                    new Dictionary<string, string>(),
                    "Play card")
                : null);

    private static FrozenDecisionFrameV2 DecisionPre() => new(
        "snapshot-a1",
        "interaction-a1",
        "combat",
        "sts2.player-environment/snapshot-1",
        new string('a', 64),
        1,
        JsonNode.Parse("{\"surface\":{\"kind\":\"combat\"}}")!,
        Array.Empty<ReadEvidence>());
}
