using System.Text.Json;
using System.Text.Json.Nodes;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class V2EvidenceTests
{
    [Theory]
    [InlineData("ordinary_combat", "play", "ordinary_combat.play_card")]
    [InlineData("ordinary_combat", "end_turn", "ordinary_combat.end_turn")]
    [InlineData("ordinary_combat", "use", "ordinary_combat.use_potion")]
    [InlineData("native_generated_card_choice", "select", "native_generated_card_choice.select")]
    public void ActionFamilyNormalizationIsSharedByAdmissionAndStatus(
        string decisionFamily,
        string verb,
        string expected)
    {
        Assert.Equal(
            expected,
            HumanCaptureProfileValidator.ResolveActionFamily(decisionFamily, verb));
    }

    [Fact]
    public void ReadRichDecisionValidatesWithoutChangingV1()
    {
        HumanDecisionRecord v1 = RecordValidationTests.ValidRecord();
        HumanDecisionRecordV2 v2 = V2Record(v1, Reads(v1));

        Assert.True(HumanDecisionRecordValidator.Validate(v1).Valid);
        RecordValidationResult result = HumanDecisionRecordV2Validator.Validate(v2);
        Assert.True(result.Valid, string.Join(',', result.Errors));

        HumanDecisionRecordV2 unified = v2 with
        {
            Environment = v2.Environment with { ModsetStatus = "exact_platform_modset" }
        };
        RecordValidationResult unifiedResult = HumanDecisionRecordV2Validator.Validate(unified);
        Assert.True(unifiedResult.Valid, string.Join(',', unifiedResult.Errors));
    }

    [Fact]
    public void ReadBindingAndFailureStatusFailClosed()
    {
        HumanDecisionRecordV2 record = V2Record(
            RecordValidationTests.ValidRecord(),
            Reads(RecordValidationTests.ValidRecord()));
        ReadEvidence drifted = record.Pre.Reads[0] with { SnapshotId = "snapshot-elsewhere" };
        RecordValidationResult binding = HumanDecisionRecordV2Validator.Validate(record with
        {
            Pre = record.Pre with { Reads = new[] { drifted, record.Pre.Reads[1] } }
        });
        Assert.Contains("pre_read_binding_mismatch", binding.Errors);

        ReadEvidence ambiguousFailure = record.Pre.Reads[0] with
        {
            Status = "failed",
            ErrorCode = null,
            PayloadRef = null,
            PayloadSha256 = null
        };
        RecordValidationResult failure = HumanDecisionRecordV2Validator.Validate(record with
        {
            Pre = record.Pre with { Reads = new[] { ambiguousFailure, record.Pre.Reads[1] } }
        });
        Assert.Contains("pre_read_failure_invalid", failure.Errors);
    }

    [Fact]
    public void CaptureProfileRequiresMaterializedReadsAndAdmittedFamily()
    {
        HumanDecisionRecordV2 record = V2Record(
            RecordValidationTests.ValidRecord(),
            Reads(RecordValidationTests.ValidRecord()));
        HumanDecisionRecordV2 missing = record with
        {
            Pre = record.Pre with { Reads = record.Pre.Reads.Take(1).ToArray() }
        };
        RecordValidationResult reads = HumanCaptureProfileValidator.ValidateRecord(Profile(), missing);
        Assert.Contains("pre_required_read_missing_combat_piles", reads.Errors);

        RecordValidationResult family = HumanCaptureProfileValidator.ValidateRecord(
            Profile(),
            record with { DecisionFamily = "unknown_selector" });
        Assert.Contains("record_action_family_outside_profile", family.Errors);
    }

    [Fact]
    public void V2StoreDeduplicatesBlobsAndAuditDetectsTampering()
    {
        string root = Temp("v2-store");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord v1 = RecordValidationTests.ValidRecord();
                IReadOnlyList<ReadEvidence> pre = PersistReads(store, v1.Pre.SnapshotId);
                IReadOnlyList<ReadEvidence> successor = PersistReads(store, v1.Successor.SnapshotId);
                Assert.Equal(pre[0].PayloadSha256, pre[1].PayloadSha256);
                store.AppendDecision(V2Record(v1, (pre, successor)));
            }
            RecordingAuditResult pass = V2RecordingAuditor.Audit(session);
            Assert.Equal("pass", pass.Status);
            Assert.Equal(1, pass.ValidRecords);
            string blob = Directory.GetFiles(
                Path.Combine(session, "blobs"), "*.json", SearchOption.AllDirectories).Single();
            File.AppendAllText(blob, "tamper");
            RecordingAuditResult fail = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", fail.Status);
            Assert.Contains("read_blob_missing_or_changed", fail.Errors);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void AdditiveNativeLedgerIsAuditedAndCopiedWithoutRedefiningV2Records()
    {
        string root = Temp("v2-native-ledger");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord v1 = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 v2 = V2Record(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
                store.AppendDecision(v2);
                var accepted = new NativeActionLedgerEvent(
                    NativeActionLedgerContract.SchemaVersion,
                    NativeActionLedgerContract.EventSchema,
                    "native-event-1",
                    manifest.SessionId,
                    manifest.TimelineId,
                    "run-0001",
                    1,
                    "game-action-1",
                    1,
                    v1.RecordId,
                    DateTimeOffset.UnixEpoch,
                    NativeActionLifecycleKinds.Accepted,
                    "PlayCardAction",
                    7,
                    "waiting_for_execution",
                    Array.Empty<string>(),
                    "strict_candidate",
                    null,
                    v2.Pre,
                    v2.NativeWitness,
                    v2.Mapping,
                    v2.Action);
                store.AppendNativeActionEvent(accepted);
                store.AppendNativeActionEvent(accepted with
                {
                    EventId = "native-event-2",
                    Sequence = 2,
                    Kind = NativeActionLifecycleKinds.Started,
                    NativeState = "executing",
                    TransitionEvidence = "lifecycle_observed",
                    DecisionPre = null,
                    NativeWitness = null,
                    Mapping = null,
                    BoundAction = null
                });
                store.AppendNativeActionEvent(accepted with
                {
                    EventId = "native-event-3",
                    Sequence = 3,
                    Kind = NativeActionLifecycleKinds.Finished,
                    NativeState = "finished",
                    TransitionEvidence = "lifecycle_observed",
                    DecisionPre = null,
                    NativeWitness = null,
                    Mapping = null,
                    BoundAction = null
                });
                store.AppendNativeActionEvent(accepted with
                {
                    EventId = "native-event-4",
                    Sequence = 4,
                    Kind = NativeActionLifecycleKinds.StrictTransitionAdmitted,
                    NativeState = "finished",
                    TransitionEvidence = "strict_v2_admitted",
                    DecisionPre = null,
                    NativeWitness = null,
                    Mapping = null,
                    BoundAction = null
                });
            }

            RecordingAuditResult pass = V2RecordingAuditor.Audit(session);
            Assert.True(
                pass.Status == "pass",
                JsonSerializer.Serialize(pass.Errors, EvidenceJson.Options));
            string output = Path.Combine(root, "bundle");
            V2SessionBundlePacker.Pack(
                session,
                "human-001",
                "human-read-rich-2026-08",
                output,
                new string('c', 40),
                true);
            Assert.True(File.Exists(Path.Combine(
                output,
                "raw",
                "native-action-ledger.jsonl")));

            string ledgerPath = Path.Combine(session, "native-action-ledger.jsonl");
            string originalLedger = File.ReadAllText(ledgerPath);
            string[] ledgerLines = File.ReadAllLines(ledgerPath);
            NativeActionLedgerEvent first = JsonSerializer.Deserialize<NativeActionLedgerEvent>(
                ledgerLines[0],
                EvidenceJson.Options)!;
            ledgerLines[0] = JsonSerializer.Serialize(
                first with
                {
                    BoundAction = first.BoundAction! with { Label = "tampered label" }
                },
                EvidenceJson.Options);
            File.WriteAllLines(ledgerPath, ledgerLines);
            RecordingAuditResult mismatch = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", mismatch.Status);
            Assert.Contains("native_action_decision_record_mismatch", mismatch.Errors);

            File.WriteAllText(
                ledgerPath,
                originalLedger.Replace(
                    NativeActionLedgerContract.EventSchema,
                    "tampered",
                    StringComparison.Ordinal));
            RecordingAuditResult failed = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", failed.Status);
            Assert.Contains("native_action_schema_invalid", failed.Errors);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void SchemaTwoAuditRejectsNativeAcceptedActionMissingFromSemanticTrace()
    {
        string root = Temp("v2-semantic-accounting");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord v1 = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 v2 = V2Record(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
                store.AppendDecision(v2);

                var nativeAccepted = new NativeActionLedgerEvent(
                    NativeActionLedgerContract.SchemaVersion,
                    NativeActionLedgerContract.EventSchema,
                    "native-event-1",
                    manifest.SessionId,
                    manifest.TimelineId,
                    "run-0001",
                    1,
                    "game-action-missing",
                    1,
                    v1.RecordId,
                    DateTimeOffset.UnixEpoch,
                    NativeActionLifecycleKinds.Accepted,
                    "PlayCardAction",
                    7,
                    "waiting_for_execution",
                    Array.Empty<string>(),
                    "strict_candidate",
                    null,
                    v2.Pre,
                    v2.NativeWitness,
                    v2.Mapping,
                    v2.Action);
                store.AppendNativeActionEvent(nativeAccepted);
                store.AppendNativeActionEvent(nativeAccepted with
                {
                    EventId = "native-event-2",
                    Sequence = 2,
                    Kind = NativeActionLifecycleKinds.Cancelled,
                    NativeState = "cancelled",
                    TransitionEvidence = "native_cancelled",
                    DecisionPre = null,
                    NativeWitness = null,
                    Mapping = null,
                    BoundAction = null
                });

                var directAction = new SemanticActionReference(
                    "direct-action-accounted",
                    2,
                    "direct-record",
                    "run-0001",
                    "NPlayerHand.OnSelectModeConfirmButtonPressed",
                    null,
                    v2.Pre.SnapshotId)
                {
                    NativeMechanism = "direct_ui_commit"
                };
                store.AppendSemanticBoundaryEvent(SemanticEvent(
                    manifest,
                    1,
                    SemanticBoundaryTraceKinds.ActionAccepted,
                    directAction,
                    v2.Pre));
                store.AppendSemanticBoundaryEvent(SemanticEvent(
                    manifest,
                    2,
                    SemanticBoundaryTraceKinds.ActionCancelledBeforeStart,
                    directAction));
            }

            RecordingAuditResult audit = V2RecordingAuditor.Audit(session);

            Assert.Equal("fail", audit.Status);
            Assert.Equal(1, audit.Errors["semantic_trace_missing_accepted_native_action"]);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void SchemaTwoAuditAcceptsNativeActionAccountedByValidDiscriminatorStream()
    {
        string root = Temp("v2-semantic-discriminator-accounting");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord v1 = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 v2 = V2Record(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
                store.AppendDecision(v2);

                var accepted = new NativeActionLedgerEvent(
                    NativeActionLedgerContract.SchemaVersion,
                    NativeActionLedgerContract.EventSchema,
                    "native-event-1",
                    manifest.SessionId,
                    manifest.TimelineId,
                    "run-0001",
                    1,
                    "game-action-accounted",
                    1,
                    v1.RecordId,
                    DateTimeOffset.UnixEpoch,
                    NativeActionLifecycleKinds.Accepted,
                    "PlayCardAction",
                    7,
                    "waiting_for_execution",
                    Array.Empty<string>(),
                    "strict_candidate",
                    null,
                    v2.Pre,
                    v2.NativeWitness,
                    v2.Mapping,
                    v2.Action);
                store.AppendNativeActionEvent(accepted);
                store.AppendNativeActionEvent(accepted with
                {
                    EventId = "native-event-2",
                    Sequence = 2,
                    Kind = NativeActionLifecycleKinds.Started,
                    NativeState = "executing",
                    TransitionEvidence = "lifecycle_observed",
                    DecisionPre = null,
                    NativeWitness = null,
                    Mapping = null,
                    BoundAction = null
                });
                store.AppendNativeActionEvent(accepted with
                {
                    EventId = "native-event-3",
                    Sequence = 3,
                    Kind = NativeActionLifecycleKinds.Finished,
                    NativeState = "finished",
                    TransitionEvidence = "lifecycle_observed",
                    DecisionPre = null,
                    NativeWitness = null,
                    Mapping = null,
                    BoundAction = null
                });
                store.AppendNativeActionEvent(accepted with
                {
                    EventId = "native-event-4",
                    Sequence = 4,
                    Kind = NativeActionLifecycleKinds.StrictTransitionAdmitted,
                    NativeState = "finished",
                    TransitionEvidence = "strict_v2_admitted",
                    DecisionPre = null,
                    NativeWitness = null,
                    Mapping = null,
                    BoundAction = null
                });

                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    1,
                    "accepted",
                    "game-action-accounted"));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    2,
                    "before_execution",
                    "game-action-accounted") with
                {
                    SemanticStateDigest = "semantic-state",
                    SemanticState = JsonNode.Parse("{\"energy\":3}"),
                    SemanticActionKeys = new[] { "play|card" },
                    ObservedActionKey = "play|card",
                    SemanticMembership = "exact_once",
                    SemanticMatchCount = 1
                });
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    3,
                    "started",
                    "game-action-accounted"));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    4,
                    "finished",
                    "game-action-accounted"));
            }

            RecordingAuditResult audit = V2RecordingAuditor.Audit(session);

            Assert.True(
                audit.Status == "pass",
                JsonSerializer.Serialize(audit.Errors));
            Assert.Empty(audit.Errors);

            string discriminator = Path.Combine(
                session,
                "native-semantic-discriminator.jsonl");
            File.WriteAllText(
                discriminator,
                File.ReadAllText(discriminator).Replace(
                    "game-action-accounted",
                    "game-action-orphan",
                    StringComparison.Ordinal));
            RecordingAuditResult tampered = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", tampered.Status);
            Assert.Equal(
                1,
                tampered.Errors["semantic_trace_missing_accepted_native_action"]);
            Assert.Equal(
                1,
                tampered.Errors[
                    "native_semantic_discriminator_accepted_without_canonical_accounting"]);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void ModernSemanticRootDoesNotRequireLegacyNativeLedgerProjection()
    {
        string root = Temp("modern-semantic-accounting");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord v1 = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 v2 = V2Record(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
                store.AppendDecision(v2);
                FrozenDecisionFrameV2 frame = v2.Pre;
                var action = new SemanticActionReference(
                    "semantic-only-root",
                    1,
                    "semantic-only-record",
                    "run-0001",
                    "VoteForMapCoordAction",
                    1,
                    frame.SnapshotId);
                store.AppendSemanticBoundaryEvent(SemanticEvent(
                    manifest,
                    1,
                    SemanticBoundaryTraceKinds.ActionAccepted,
                    action,
                    frame));
                store.AppendSemanticBoundaryEvent(SemanticEvent(
                    manifest,
                    2,
                    SemanticBoundaryTraceKinds.ActionCancelledBeforeStart,
                    action));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    1,
                    "accepted",
                    action.ActionWitnessId));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    2,
                    "before_execution",
                    action.ActionWitnessId) with
                {
                    SemanticStateDigest = "semantic-state",
                    SemanticState = JsonNode.Parse("{\"map\":true}"),
                    SemanticActionKeys = new[] { "activate|map" },
                    ObservedActionKey = "activate|map",
                    SemanticMembership = "exact_once",
                    SemanticMatchCount = 1
                });
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    3,
                    "started",
                    action.ActionWitnessId));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    4,
                    "finished",
                    action.ActionWitnessId));
            }

            RecordingAuditResult audit = V2RecordingAuditor.Audit(session);

            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
            Assert.DoesNotContain(
                "native_semantic_discriminator_accepted_without_canonical_accounting",
                audit.Errors.Keys);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void CanonicalRootCommitSuccessorPersistsAndAuditsEndToEnd()
    {
        string root = Temp("canonical-causal-path");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord v1 = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 v2 = V2Record(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
                store.AppendDecision(v2);
                FrozenDecisionFrameV2 successor = v2.Pre with
                {
                    SnapshotId = v2.Successor.SnapshotId,
                    InteractionId = v2.Successor.InteractionId,
                    InteractionKind = v2.Successor.InteractionKind,
                    Snapshot = v2.Successor.Snapshot,
                    Reads = v2.Successor.Reads
                };

                var action = new SemanticActionReference(
                    "map-root",
                    1,
                    "map-record",
                    "run-0001",
                    "VoteForMapCoordAction",
                    1,
                    v2.Pre.SnapshotId)
                {
                    RequiresNativePostCommit = true
                };
                var tracker = new SemanticBoundaryTracker();
                var drafts = new List<SemanticBoundaryTraceDraft>();
                drafts.AddRange(tracker.Accept(action, v2.Pre));
                drafts.AddRange(tracker.ObserveBeforeActionExecution(
                    action.ActionWitnessId,
                    new SemanticBoundaryObservation(
                        SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
                        DateTimeOffset.UnixEpoch,
                        v2.Pre.SnapshotId,
                        "interactive",
                        "complete",
                        v2.Pre.InteractionId,
                        v2.Pre.InteractionKind,
                        v2.Pre,
                        action.ActionWitnessId)));
                drafts.AddRange(tracker.Started(action.ActionWitnessId));
                drafts.AddRange(tracker.Finished(action.ActionWitnessId));
                drafts.AddRange(tracker.ObserveNativeCommit(
                    action.ActionWitnessId,
                    new NativeCompletionEvidence(
                        "map-commit",
                        "map_navigation",
                        "GameAction.Finished",
                        action.ActionWitnessId,
                        null,
                        "map-action",
                        "map-coordinate",
                        null,
                        true)));
                drafts.AddRange(tracker.ObserveDecisionBoundary(
                    new SemanticBoundaryObservation(
                        SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady,
                        DateTimeOffset.UnixEpoch.AddSeconds(1),
                        successor.SnapshotId,
                        "interactive",
                        "complete",
                        successor.InteractionId,
                        successor.InteractionKind,
                        successor,
                        null)
                    {
                        NativeDecisionOwnerReady = new NativeDecisionOwnerReadyEvidence(
                            successor.InteractionKind,
                            "combat-state-owner",
                            "MegaCrit.Sts2.Core.Combat.CombatState",
                            "CombatManager.TurnStarted->NEndTurnButton.OnTurnStarted.postfix")
                    }));
                store.AppendSemanticBoundaryEvents(drafts.Select((draft, index) =>
                    SemanticEvent(manifest, index + 1, draft)).ToArray());
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    1,
                    "accepted",
                    action.ActionWitnessId));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    2,
                    "before_execution",
                    action.ActionWitnessId) with
                {
                    SemanticStateDigest = "semantic-state",
                    SemanticState = JsonNode.Parse("{\"map\":true}"),
                    SemanticActionKeys = new[] { "activate|map" },
                    ObservedActionKey = "activate|map",
                    SemanticMembership = "exact_once",
                    SemanticMatchCount = 1
                });
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    3,
                    "started",
                    action.ActionWitnessId));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    4,
                    "finished",
                    action.ActionWitnessId));
            }

            RecordingAuditResult audit = V2RecordingAuditor.Audit(session);

            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void SemanticBoundaryBatchIsVisibleInOrderAndReadableAfterClose()
    {
        string root = Temp("v2-semantic-batch");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                SemanticActionReference action = new(
                    "action-batch",
                    1,
                    "record-batch",
                    "run-0001",
                    "PlayCardAction",
                    1,
                    "snapshot-a");
                store.AppendSemanticBoundaryEvents(new[]
                {
                    SemanticEvent(manifest, 1, SemanticBoundaryTraceKinds.ActionAccepted, action),
                    SemanticEvent(manifest, 2, SemanticBoundaryTraceKinds.ActionStarted, action)
                });

                Assert.Equal(
                    new[] { 1L, 2L },
                    ReadLiveLines(Path.Combine(session, "semantic-boundary-trace.jsonl"))
                        .Select(line => JsonSerializer.Deserialize<SemanticBoundaryTraceEvent>(
                            line,
                            EvidenceJson.Options)!.Sequence));
            }

            Assert.Equal(
                new[] { 1L, 2L },
                File.ReadLines(Path.Combine(session, "semantic-boundary-trace.jsonl"))
                    .Select(line => JsonSerializer.Deserialize<SemanticBoundaryTraceEvent>(
                        line,
                        EvidenceJson.Options)!.Sequence));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void SemanticBoundaryBatchValidatesBeforeWritingAndRejectsClosedStore()
    {
        string root = Temp("v2-semantic-batch-failure");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            using V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile);
            SemanticActionReference action = new(
                "action-batch",
                1,
                "record-batch",
                "run-0001",
                "PlayCardAction",
                1,
                "snapshot-a");
            string tracePath = Path.Combine(store.DirectoryPath, "semantic-boundary-trace.jsonl");

            Assert.Throws<InvalidDataException>(() => store.AppendSemanticBoundaryEvents(new[]
            {
                SemanticEvent(manifest, 1, SemanticBoundaryTraceKinds.ActionAccepted, action),
                SemanticEvent(manifest with { TimelineId = "timeline-other" }, 2,
                    SemanticBoundaryTraceKinds.ActionStarted, action)
            }));
            Assert.Empty(ReadLiveLines(tracePath));

            store.Dispose();
            Assert.Throws<ObjectDisposedException>(() => store.AppendSemanticBoundaryEvents(
                new[] { SemanticEvent(manifest, 1, SemanticBoundaryTraceKinds.ActionAccepted, action) }));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void SemanticEvidenceStoresAnExactFrameOnceAndAuditsTampering()
    {
        string root = Temp("v2-semantic-evidence");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            string framePath;
            using (V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                FrozenDecisionFrameV2 frame = V2Record(
                    RecordValidationTests.ValidRecord(),
                    (PersistReads(store, "snapshot-a"), PersistReads(store, "snapshot-b"))).Pre;
                SemanticFrameReference first = store.PersistSemanticFrame(frame);
                SemanticFrameReference second = store.PersistSemanticFrame(frame);
                Assert.Equal(first, second);
                Assert.Single(Directory.GetFiles(
                    Path.Combine(session, "semantic-frames"),
                    "*.json",
                    SearchOption.AllDirectories));

                var action = new SemanticActionReference(
                    "action-ref",
                    1,
                    "record-ref",
                    "run-0001",
                    "PlayCardAction",
                    1,
                    frame.SnapshotId);
                store.AppendSemanticEvidenceEvents(new[]
                {
                    SemanticEvidenceEvent(manifest, 1, SemanticBoundaryTraceKinds.ActionAccepted, action)
                        with { HumanObservationRef = first },
                    SemanticEvidenceEvent(
                        manifest,
                        2,
                        SemanticBoundaryTraceKinds.ActionCancelledBeforeStart,
                        action)
                });
                SemanticEvidenceEvent persisted = JsonSerializer.Deserialize<SemanticEvidenceEvent>(
                    ReadLiveLines(Path.Combine(session, "semantic-boundary-trace.jsonl")).First(),
                    EvidenceJson.Options)!;
                Assert.Equal(SemanticEvidenceContract.EventSchema, persisted.Schema);
                Assert.Equal(first, persisted.HumanObservationRef);
                framePath = Path.Combine(session, first.ObjectRef);
            }

            RecordingAuditResult beforeTamper = V2RecordingAuditor.Audit(session);
            Assert.DoesNotContain(
                beforeTamper.Errors.Keys,
                key => key.StartsWith("semantic_", StringComparison.Ordinal));
            File.AppendAllText(framePath, "tampered");
            RecordingAuditResult audit = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", audit.Status);
            Assert.True(audit.Errors.ContainsKey("semantic_frame_missing_or_changed"));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void SemanticEvidenceOwnerReadyRoundTripsAndFailsClosedWhenIncomplete()
    {
        string root = Temp("v2-semantic-owner-ready-round-trip");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            string tracePath;
            using (V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord validRecord = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 record = V2Record(
                    validRecord,
                    (PersistReads(store, validRecord.Pre.SnapshotId),
                        PersistReads(store, validRecord.Successor.SnapshotId)));
                store.AppendDecision(record);
                FrozenDecisionFrameV2 pre = record.Pre with
                {
                    SnapshotId = "snapshot-pre",
                    InteractionId = "map-owner",
                    InteractionKind = "map"
                };
                FrozenDecisionFrameV2 successor = pre with
                {
                    SnapshotId = "snapshot-successor",
                    InteractionId = "combat-owner",
                    InteractionKind = "combat_turn",
                    Snapshot = JsonNode.Parse("{\"energy\":3,\"combat\":true}")!
                };
                SemanticFrameReference preRef = store.PersistSemanticFrame(pre);
                SemanticFrameReference successorRef = store.PersistSemanticFrame(successor);
                var action = new SemanticActionReference(
                    "owner-ready-action",
                    1,
                    "owner-ready-record",
                    "run-0001",
                    "VoteForMapCoordAction",
                    1,
                    pre.SnapshotId)
                {
                    RequiresNativePostCommit = true
                };
                var completion = new NativeCompletionEvidence(
                    "owner-ready-commit",
                    "map_navigation",
                    "GameAction.Finished",
                    action.ActionWitnessId,
                    null,
                    "map-owner",
                    "map-coordinate",
                    null,
                    true);
                var ownerReady = new NativeDecisionOwnerReadyEvidence(
                    "combat_turn",
                    "combat-owner",
                    "MegaCrit.Sts2.Core.Combat.CombatState",
                    "CombatManager.TurnStarted->NEndTurnButton.OnTurnStarted.postfix");
                var boundary = SemanticBoundaryObservationCodec.Encode(
                    new SemanticBoundaryObservation(
                        SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady,
                        DateTimeOffset.UnixEpoch.AddSeconds(1),
                        successor.SnapshotId,
                        "interactive",
                        "complete",
                        successor.InteractionId,
                        successor.InteractionKind,
                        successor,
                        null)
                    {
                        NativeDecisionOwnerReady = ownerReady
                    },
                    store.PersistSemanticFrame);
                var executionBoundary = new SemanticBoundaryObservationReference(
                    SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
                    DateTimeOffset.UnixEpoch,
                    pre.SnapshotId,
                    "interactive",
                    "complete",
                    pre.InteractionId,
                    pre.InteractionKind,
                    preRef,
                    action.ActionWitnessId);
                store.AppendSemanticEvidenceEvents(new[]
                {
                    SemanticEvidenceEvent(
                        manifest,
                        1,
                        SemanticBoundaryTraceKinds.ActionAccepted,
                        action) with { HumanObservationRef = preRef },
                    SemanticEvidenceEvent(
                        manifest,
                        2,
                        SemanticBoundaryTraceKinds.BoundaryObserved,
                        action) with { Boundary = executionBoundary },
                    SemanticEvidenceEvent(
                        manifest,
                        3,
                        SemanticBoundaryTraceKinds.ActionStarted,
                        action),
                    SemanticEvidenceEvent(
                        manifest,
                        4,
                        SemanticBoundaryTraceKinds.ActionFinished,
                        action),
                    SemanticEvidenceEvent(
                        manifest,
                        5,
                        SemanticBoundaryTraceKinds.NativeCommitObserved,
                        action) with { NativeCompletion = completion },
                    SemanticEvidenceEvent(
                        manifest,
                        6,
                        SemanticBoundaryTraceKinds.TransitionProved,
                        action) with
                    {
                        ProofStatus = "proved_native_commit_then_owner_boundary",
                        Boundary = boundary,
                        ExecutionPreRef = preRef,
                        SuccessorRef = successorRef,
                        NativeCompletion = completion
                    }
                });
                tracePath = Path.Combine(session, "semantic-boundary-trace.jsonl");
            }

            RecordingAuditResult audit = V2RecordingAuditor.Audit(session);
            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
            JsonObject persistedTransition = JsonNode.Parse(
                    File.ReadLines(tracePath).Single(line =>
                        line.Contains("transition_proved", StringComparison.Ordinal)))!
                .AsObject();
            JsonObject persistedBoundary = persistedTransition["boundary"]!.AsObject();
            Assert.Equal(
                "combat_turn",
                persistedBoundary["native_decision_owner_ready"]!["domain"]!.GetValue<string>());
            Assert.Equal(
                "combat-owner",
                persistedBoundary["native_decision_owner_ready"]!["native_owner_witness_id"]!.GetValue<string>());

            string[] original = File.ReadAllLines(tracePath);
            JsonObject missing = JsonNode.Parse(original.Single(line =>
                    line.Contains("transition_proved", StringComparison.Ordinal)))!.AsObject();
            missing["boundary"]!.AsObject().Remove("native_decision_owner_ready");
            File.WriteAllLines(
                tracePath,
                original.Select(line => line.Contains("transition_proved", StringComparison.Ordinal)
                    ? missing.ToJsonString(EvidenceJson.Options)
                    : line));
            RecordingAuditResult missingAudit = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", missingAudit.Status);
            Assert.True(missingAudit.Errors.ContainsKey("semantic_native_owner_ready_evidence_invalid"));
            Assert.True(missingAudit.Errors.ContainsKey("semantic_transition_proof_incomplete"));

            JsonObject mismatched = JsonNode.Parse(original.Single(line =>
                    line.Contains("transition_proved", StringComparison.Ordinal)))!.AsObject();
            mismatched["boundary"]!["native_decision_owner_ready"]!["domain"] = "map";
            File.WriteAllLines(
                tracePath,
                original.Select(line => line.Contains("transition_proved", StringComparison.Ordinal)
                    ? mismatched.ToJsonString(EvidenceJson.Options)
                    : line));
            RecordingAuditResult mismatchedAudit = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", mismatchedAudit.Status);
            Assert.True(mismatchedAudit.Errors.ContainsKey("semantic_native_owner_ready_evidence_invalid"));
            Assert.True(mismatchedAudit.Errors.ContainsKey("semantic_transition_proof_incomplete"));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void TrackerSettlementProjectsDurableDecisionAndCanonicalEvidence()
    {
        string root = Temp("tracker-settlement-projection");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord source = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 seed = V2Record(
                    source,
                    (PersistReads(store, source.Pre.SnapshotId),
                        PersistReads(store, source.Successor.SnapshotId)));
                FrozenDecisionFrameV2 humanObservation = seed.Pre;
                FrozenDecisionFrameV2 successor = new(
                    seed.Successor.SnapshotId,
                    seed.Successor.InteractionId,
                    seed.Successor.InteractionKind,
                    seed.Pre.SurfaceSchema,
                    seed.Pre.CatalogDigest,
                    seed.Pre.CatalogCount,
                    seed.Successor.Snapshot,
                    seed.Successor.Reads);
                SemanticActionReference action = new(
                    "tracker-settlement-action",
                    source.Sequence,
                    source.RecordId,
                    source.RunId,
                    source.NativeWitness.NativeActionType,
                    null,
                    humanObservation.SnapshotId)
                {
                    NativeMechanism = "game_action",
                    RequiresNativePostCommit = true,
                    NativeWitness = source.NativeWitness,
                    Mapping = source.Mapping,
                    BoundAction = source.Action
                };
                string semanticKey =
                    $"{source.Action.Verb}|{source.Action.SubjectReferentId ?? "-"}|";
                var actionSpace = new ExecutionSemanticActionSpaceEvidence(
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
                            semanticKey,
                            source.Action.Verb,
                            source.Action.SubjectReferentId,
                            source.Action.Arguments,
                            "native_test_validator")
                    },
                    semanticKey,
                    "exact_once",
                    1,
                    new[] { "native_test_validator" },
                    new[] { "not_public_delivery_authority" },
                    null)
                {
                    HumanBoundActionId = source.Action.BoundActionId
                };
                var tracker = new SemanticBoundaryTracker();
                var drafts = new List<SemanticBoundaryTraceDraft>();
                drafts.AddRange(tracker.Accept(action, humanObservation));
                drafts.AddRange(tracker.ObserveBeforeActionExecution(
                    action.ActionWitnessId,
                    new SemanticBoundaryObservation(
                        SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
                        DateTimeOffset.UnixEpoch,
                        humanObservation.SnapshotId,
                        "settling",
                        "complete",
                        humanObservation.InteractionId,
                        humanObservation.InteractionKind,
                        humanObservation,
                        action.ActionWitnessId)
                    {
                        ExecutionSemanticActionSpace = actionSpace
                    }));
                drafts.AddRange(tracker.Started(action.ActionWitnessId));
                drafts.AddRange(tracker.Finished(action.ActionWitnessId));
                NativeCompletionEvidence completion = new(
                    "tracker-settlement-completion",
                    "ordinary_combat",
                    "native.test.commit",
                    action.ActionWitnessId,
                    "task-tracker-settlement",
                    "owner-tracker-settlement",
                    "operand-tracker-settlement",
                    null,
                    true);
                drafts.AddRange(tracker.ObserveNativeCommit(action.ActionWitnessId, completion));
                IReadOnlyList<SemanticBoundaryTraceDraft> provedDrafts =
                    tracker.ObserveDecisionBoundary(
                        new SemanticBoundaryObservation(
                            SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady,
                            DateTimeOffset.UnixEpoch.AddSeconds(1),
                            successor.SnapshotId,
                            "interactive",
                            "complete",
                            successor.InteractionId,
                            successor.InteractionKind,
                            successor,
                            null)
                        {
                            NativeDecisionOwnerReady = new NativeDecisionOwnerReadyEvidence(
                                successor.InteractionKind,
                                "owner-tracker-settlement",
                                "CombatState",
                                "native.test.owner-ready")
                        });
                SemanticBoundaryTraceDraft proved = Assert.Single(provedDrafts);
                drafts.AddRange(provedDrafts);

                Assert.Same(humanObservation, proved.HumanObservation);
                Assert.Same(completion, proved.NativeCompletion);
                Assert.Same(actionSpace, proved.ExecutionSemanticActionSpace);

                var semanticEvents = new List<SemanticEvidenceEvent>(drafts.Count);
                long sequence = 0;
                foreach (SemanticBoundaryTraceDraft draft in drafts)
                {
                    SemanticFrameReference? humanRef = draft.HumanObservation == null
                        ? null
                        : store.PersistSemanticFrame(draft.HumanObservation);
                    SemanticFrameReference? preRef = draft.SemanticPre == null
                        ? null
                        : store.PersistSemanticFrame(draft.SemanticPre);
                    SemanticFrameReference? successorFrameRef = draft.SemanticSuccessor == null
                        ? null
                        : store.PersistSemanticFrame(draft.SemanticSuccessor);
                    ExecutionSemanticActionSpaceReference? actionSpaceRef =
                        draft.ExecutionSemanticActionSpace == null
                            ? null
                            : store.PersistExecutionSemanticActionSpace(
                                draft.ExecutionSemanticActionSpace);
                    semanticEvents.Add(new SemanticEvidenceEvent(
                        SemanticEvidenceContract.SchemaVersion,
                        SemanticEvidenceContract.EventSchema,
                        $"tracker-semantic-event-{++sequence}",
                        manifest.SessionId,
                        manifest.TimelineId,
                        draft.Action.RunId,
                        sequence,
                        DateTimeOffset.UnixEpoch.AddMilliseconds(sequence),
                        draft.Kind,
                        draft.Action,
                        draft.ProofStatus,
                        draft.RelatedActionWitnessId,
                        draft.Boundary == null
                            ? null
                            : SemanticBoundaryObservationCodec.Encode(
                                draft.Boundary,
                                store.PersistSemanticFrame),
                        preRef,
                        successorFrameRef,
                        draft.Detail,
                        draft.NonClaims ?? Array.Empty<string>())
                    {
                        HumanObservationRef = humanRef,
                        NativeCompletion = draft.NativeCompletion,
                        ExecutionSemanticActionSpaceRef = actionSpaceRef
                    });
                }
                store.AppendSemanticEvidenceEvents(semanticEvents);

                HumanDecisionRecordV2 decision = SemanticTransitionProjection.CreateDecision(
                    proved,
                    source.Environment,
                    manifest.SessionId,
                    manifest.TimelineId,
                    profile.ProfileId);
                store.AppendDecision(decision);
                SemanticFrameReference preStateRef = store.PersistSemanticFrame(proved.SemanticPre!);
                SemanticFrameReference successorRef = store.PersistSemanticFrame(
                    proved.SemanticSuccessor!);
                ExecutionSemanticActionSpaceReference canonicalActionSpaceRef =
                    store.PersistExecutionSemanticActionSpace(
                        proved.ExecutionSemanticActionSpace!);
                store.AppendCanonicalTransition(SemanticTransitionProjection.CreateCanonical(
                    proved,
                    preStateRef,
                    successorRef,
                    canonicalActionSpaceRef,
                    manifest.SessionId,
                    manifest.TimelineId));
            }

            RecordingAuditResult audit = V2RecordingAuditor.Audit(session);
            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
            Assert.Single(V2RecordingAuditor.ReadAdmitted(session));
            Assert.Single(File.ReadLines(Path.Combine(session, "canonical-transitions.jsonl")));
            JsonObject provedEvent = JsonNode.Parse(
                    File.ReadLines(Path.Combine(session, "semantic-boundary-trace.jsonl"))
                        .Single(line => line.Contains("transition_proved", StringComparison.Ordinal)))!
                .AsObject();
            Assert.NotNull(provedEvent["human_observation_ref"]);
            Assert.NotNull(provedEvent["native_completion"]);
            Assert.NotNull(provedEvent["execution_semantic_action_space_ref"]);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void ExecutionSemanticActionSpaceRoundTripsIntoCanonicalAudit()
    {
        string root = Temp("execution-semantic-action-space-round-trip");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            string actionSpacePath;
            using (V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord source = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 decision = V2Record(
                    source,
                    (PersistReads(store, source.Pre.SnapshotId),
                        PersistReads(store, source.Successor.SnapshotId)));
                store.AppendDecision(decision);

                JsonNode executionSnapshot = decision.Pre.Snapshot.DeepClone();
                executionSnapshot["snapshot_id"] = "execution-pre";
                executionSnapshot["status"] = "settling";
                executionSnapshot["bound_actions"]!["actions"] = new JsonArray();
                executionSnapshot["bound_actions"]!["materialized_count"] = 0;
                executionSnapshot["bound_actions"]!["total_count"] = 0;
                var executionPre = new FrozenDecisionFrameV2(
                    "execution-pre",
                    decision.Pre.InteractionId,
                    decision.Pre.InteractionKind,
                    decision.Pre.SurfaceSchema,
                    EvidenceIdentity.Sha256Json(executionSnapshot["bound_actions"]!),
                    0,
                    executionSnapshot,
                    decision.Pre.Reads);
                var successor = new FrozenDecisionFrameV2(
                    decision.Successor.SnapshotId,
                    decision.Successor.InteractionId,
                    decision.Successor.InteractionKind,
                    decision.Pre.SurfaceSchema,
                    decision.Pre.CatalogDigest,
                    decision.Pre.CatalogCount,
                    decision.Successor.Snapshot.DeepClone(),
                    decision.Successor.Reads);
                var action = new SemanticActionReference(
                    "execution-semantic-action",
                    decision.Sequence,
                    decision.RecordId,
                    decision.RunId,
                    decision.NativeWitness.NativeActionType,
                    7,
                    decision.Pre.SnapshotId)
                {
                    NativeMechanism = "game_action",
                    NativeWitness = decision.NativeWitness,
                    Mapping = decision.Mapping,
                    BoundAction = decision.Action
                };
                string semanticKey = $"{decision.Action.Verb}|{decision.Action.SubjectReferentId ?? "-"}|";
                var actionSpace = new ExecutionSemanticActionSpaceEvidence(
                    ExecutionSemanticActionSpaceContract.SchemaVersion,
                    ExecutionSemanticActionSpaceContract.Schema,
                    action.ActionWitnessId,
                    "before_execution",
                    "captured",
                    "combat_play_phase",
                    new string('a', 64),
                    JsonNode.Parse("{\"player_phase\":\"Play\"}")!,
                    new string('b', 64),
                    new[]
                    {
                        new ExecutionSemanticAction(
                            semanticKey,
                            decision.Action.Verb,
                            decision.Action.SubjectReferentId,
                            decision.Action.Arguments,
                            "CardModel.CanPlayTargeting")
                    },
                    semanticKey,
                    "exact_once",
                    1,
                    new[] { "CardModel.CanPlayTargeting" },
                    new[] { "not_public_bound_action_delivery_authority" },
                    null)
                {
                    HumanBoundActionId = decision.Action.BoundActionId
                };
                var tracker = new SemanticBoundaryTracker();
                var drafts = new List<SemanticBoundaryTraceDraft>();
                drafts.AddRange(tracker.Accept(action, decision.Pre));
                drafts.AddRange(tracker.ObserveBeforeActionExecution(
                    action.ActionWitnessId,
                    new SemanticBoundaryObservation(
                        SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
                        DateTimeOffset.UnixEpoch,
                        executionPre.SnapshotId,
                        "settling",
                        "complete",
                        executionPre.InteractionId,
                        executionPre.InteractionKind,
                        executionPre,
                        action.ActionWitnessId)
                    {
                        ExecutionSemanticActionSpace = actionSpace
                    }));
                drafts.AddRange(tracker.Started(action.ActionWitnessId));
                drafts.AddRange(tracker.Finished(action.ActionWitnessId));
                drafts.AddRange(tracker.ObserveDecisionBoundary(
                    new SemanticBoundaryObservation(
                        SemanticBoundaryWitnessKinds.NativeDecisionOwnerReady,
                        DateTimeOffset.UnixEpoch.AddSeconds(1),
                        successor.SnapshotId,
                        "interactive",
                        "complete",
                        successor.InteractionId,
                        successor.InteractionKind,
                        successor,
                        null)
                    {
                        NativeDecisionOwnerReady = new NativeDecisionOwnerReadyEvidence(
                            successor.InteractionKind,
                            "combat-owner",
                            "CombatState",
                            "native-test-owner-ready")
                    }));

                int sequence = 0;
                foreach (SemanticBoundaryTraceDraft draft in drafts)
                {
                    SemanticFrameReference? humanRef = draft.HumanObservation == null
                        ? null
                        : store.PersistSemanticFrame(draft.HumanObservation);
                    SemanticFrameReference? preRef = draft.SemanticPre == null
                        ? null
                        : store.PersistSemanticFrame(draft.SemanticPre);
                    SemanticFrameReference? successorRef = draft.SemanticSuccessor == null
                        ? null
                        : store.PersistSemanticFrame(draft.SemanticSuccessor);
                    ExecutionSemanticActionSpaceReference? actionSpaceRef =
                        draft.ExecutionSemanticActionSpace == null
                            ? null
                            : store.PersistExecutionSemanticActionSpace(
                                draft.ExecutionSemanticActionSpace);
                    store.AppendSemanticEvidenceEvents(new[]
                    {
                        SemanticEvidenceEvent(
                            manifest,
                            ++sequence,
                            draft.Kind,
                            draft.Action) with
                        {
                            ProofStatus = draft.ProofStatus,
                            RelatedActionWitnessId = draft.RelatedActionWitnessId,
                            Boundary = draft.Boundary == null
                                ? null
                                : SemanticBoundaryObservationCodec.Encode(
                                    draft.Boundary,
                                    store.PersistSemanticFrame),
                            HumanObservationRef = humanRef,
                            ExecutionPreRef = preRef,
                            SuccessorRef = successorRef,
                            ExecutionSemanticActionSpaceRef = actionSpaceRef,
                            NativeCompletion = draft.NativeCompletion
                        }
                    });
                }

                SemanticBoundaryTraceDraft proved = drafts.Single(draft =>
                    draft.Kind == SemanticBoundaryTraceKinds.TransitionProved);
                SemanticFrameReference canonicalPre = store.PersistSemanticFrame(
                    proved.SemanticPre!);
                SemanticFrameReference canonicalSuccessor = store.PersistSemanticFrame(
                    proved.SemanticSuccessor!);
                ExecutionSemanticActionSpaceReference canonicalActionSpace =
                    store.PersistExecutionSemanticActionSpace(
                        proved.ExecutionSemanticActionSpace!);
                store.AppendCanonicalTransition(SemanticTransitionProjection.CreateCanonical(
                    proved,
                    canonicalPre,
                    canonicalSuccessor,
                    canonicalActionSpace,
                    manifest.SessionId,
                    manifest.TimelineId));
                actionSpacePath = Path.Combine(session, canonicalActionSpace.ObjectRef);
            }

            RecordingAuditResult audit = V2RecordingAuditor.Audit(session);
            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
            File.AppendAllText(actionSpacePath, "tampered");
            RecordingAuditResult tampered = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", tampered.Status);
            Assert.True(tampered.Errors.ContainsKey(
                "execution_semantic_action_space_missing_or_changed"));
        }
        finally
        {
            Delete(root);
        }
    }

    private static IReadOnlyList<string> ReadLiveLines(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return lines;
    }

    [Fact]
    public void ReadBatchPreservesCountsAndPayloads()
    {
        string root = Temp("v2-read-batch");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            using V2RecordingStore store = V2RecordingStore.Create(root, manifest, profile);
            JsonNode content = JsonNode.Parse("{\"cards\":[\"Strike\"]}")!;
            JsonNode completeness = JsonNode.Parse("{\"status\":\"complete\",\"missing\":[]}")!;
            IReadOnlyList<ReadEvidence> reads = store.PersistReads(new[]
            {
                CapturedRead("run_deck", content, completeness),
                CapturedRead("combat_piles", content, completeness)
            });

            Assert.Equal(2, reads.Count);
            RecordingStoreSnapshot snapshot = store.GetSnapshot();
            Assert.Equal(2, snapshot.Counters.ReadsMaterialized);
            Assert.Equal(0, snapshot.Counters.ReadsFailed);
            Assert.All(reads, read => Assert.True(File.Exists(Path.Combine(store.DirectoryPath, read.PayloadRef!))));
        }
        finally
        {
            Delete(root);
        }

        static CapturedReadPayload CapturedRead(
            string kind,
            JsonNode content,
            JsonNode completeness) => new(
            $"read-{kind}",
            kind,
            "snapshot-a",
            "runtime-1",
            "environment-1",
            "materialized",
            $"sts2.player-environment/read/{kind}-1",
            content.DeepClone(),
            completeness.DeepClone(),
            DateTimeOffset.UnixEpoch,
            null,
            null);
    }

    [Fact]
    public void V2BundleIsPortableDeterministicAndImmutable()
    {
        string root = Temp("v2-bundle");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord v1 = RecordValidationTests.ValidRecord();
                store.AppendDecision(V2Record(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId))));
            }
            string output = Path.Combine(root, "bundle");
            SessionBundleResult first = V2SessionBundlePacker.Pack(
                session,
                "human-001",
                "human-read-rich-2026-08",
                output,
                new string('c', 40),
                true);
            SessionBundleResult retry = V2SessionBundlePacker.Pack(
                session,
                "human-001",
                "human-read-rich-2026-08",
                output,
                new string('c', 40),
                true);
            Assert.Equal(first.BundleContentId, retry.BundleContentId);
            Assert.Equal(first.ChecksumsSha256, retry.ChecksumsSha256);
            Assert.NotEmpty(Directory.GetFiles(
                Path.Combine(output, "raw", "blobs"), "*.json", SearchOption.AllDirectories));
            File.AppendAllText(Path.Combine(output, "export", "decisions.jsonl"), "tamper\n");
            Assert.Throws<IOException>(() => V2SessionBundlePacker.Pack(
                session,
                "human-001",
                "human-read-rich-2026-08",
                output,
                new string('c', 40),
                true));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void IndependentSessionsNeverShareTheirTimelineOrStore()
    {
        string root = Temp("v2-multiple-sessions");
        try
        {
            HumanCaptureProfile profile = Profile();
            using V2RecordingStore first = V2RecordingStore.Create(
                root,
                Manifest(profile, "session-first", "timeline-first"),
                profile);
            using V2RecordingStore second = V2RecordingStore.Create(
                root,
                Manifest(profile, "session-second", "timeline-second"),
                profile);

            Assert.NotEqual(first.DirectoryPath, second.DirectoryPath);
            Assert.Equal("session-first", first.Manifest.SessionId);
            Assert.Equal("session-second", second.Manifest.SessionId);
            Assert.Equal("timeline-first", first.Manifest.TimelineId);
            Assert.Equal("timeline-second", second.Manifest.TimelineId);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void ReadBlobWriteFailureIsVisibleInStoreHealth()
    {
        string root = Temp("v2-write-failure");
        try
        {
            HumanCaptureProfile profile = Profile();
            using V2RecordingStore store = V2RecordingStore.Create(root, Manifest(profile), profile);
            JsonNode payload = JsonNode.Parse("{\"cards\":[{\"name\":\"Strike\"}]}")!;
            byte[] canonical = System.Text.Encoding.UTF8.GetBytes(
                "{\"cards\":[{\"name\":\"Strike\"}]}\n");
            string digest = EvidenceIdentity.Sha256Bytes(canonical);
            string blob = Path.Combine(store.DirectoryPath, "blobs", "sha256", digest[..2], $"{digest}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(blob)!);
            File.WriteAllText(blob, "collision");

            Assert.Throws<IOException>(() => store.PersistRead(new CapturedReadPayload(
                "read-run-deck",
                "run_deck",
                "snapshot-a",
                "runtime-1",
                "environment-1",
                "materialized",
                "sts2.player-environment/read/run-deck-1",
                payload,
                JsonNode.Parse("{\"status\":\"complete\"}")!,
                DateTimeOffset.UnixEpoch,
                null,
                null)));
            RecordingStoreSnapshot status = store.GetSnapshot();
            Assert.Equal("failed", status.AppendHealth);
            Assert.Equal("failed", status.DiskHealth);
            Assert.NotNull(status.LastError);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void CanonicalTransitionBindsExactDecisionFramesAndDetectsTampering()
    {
        string root = Temp("v2-canonical-transition");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            SemanticFrameReference preRef;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord value = RecordValidationTests.ValidRecord();
                HumanDecisionRecordV2 record = V2Record(
                    value,
                    (PersistReads(store, value.Pre.SnapshotId),
                        PersistReads(store, value.Successor.SnapshotId)));
                store.AppendDecision(record);
                preRef = store.PersistSemanticFrame(record.Pre);
                var successor = new FrozenDecisionFrameV2(
                    record.Successor.SnapshotId,
                    record.Successor.InteractionId,
                    record.Successor.InteractionKind,
                    record.Pre.SurfaceSchema,
                    record.Pre.CatalogDigest,
                    record.Pre.CatalogCount,
                    record.Successor.Snapshot.DeepClone(),
                    record.Successor.Reads);
                SemanticFrameReference successorRef = store.PersistSemanticFrame(successor);
                store.AppendCanonicalTransition(Canonical(record, preRef, successorRef));
            }

            RecordingAuditResult pass = V2RecordingAuditor.Audit(session);
            Assert.True(
                pass.Status == "pass",
                JsonSerializer.Serialize(pass.Errors, EvidenceJson.Options));
            File.AppendAllText(Path.Combine(session, preRef.ObjectRef), "tampered");
            RecordingAuditResult tampered = V2RecordingAuditor.Audit(session);
            Assert.Equal("fail", tampered.Status);
            Assert.True(tampered.Errors.ContainsKey(
                "canonical_transition_frame_missing_or_changed"));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void PreSerializedRecordingRemainsValidWithoutCanonicalStream()
    {
        string root = Temp("v2-pre-serialized-compatibility");
        try
        {
            HumanCaptureProfile profile = Profile();
            RecordingManifestV2 manifest = Manifest(profile);
            string session;
            using (var store = V2RecordingStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HumanDecisionRecord value = RecordValidationTests.ValidRecord();
                store.AppendDecision(V2Record(
                    value,
                    (PersistReads(store, value.Pre.SnapshotId),
                        PersistReads(store, value.Successor.SnapshotId))));
            }
            File.Delete(Path.Combine(session, "canonical-transitions.jsonl"));

            Assert.Equal("pass", V2RecordingAuditor.Audit(session).Status);
        }
        finally
        {
            Delete(root);
        }
    }

    private static CanonicalTransitionEvidence Canonical(
        HumanDecisionRecordV2 record,
        SemanticFrameReference preRef,
        SemanticFrameReference successorRef) => new(
        CanonicalTransitionEvidenceContract.LegacySchemaVersion,
        CanonicalTransitionEvidenceContract.LegacySchema,
        $"canonical-{record.RecordId}",
        record.SessionId,
        record.TimelineId,
        record.RunId,
        record.Sequence,
        DateTimeOffset.UnixEpoch,
        CanonicalTransitionEvidenceContract.LegacyCollectionMode,
        $"epoch-{preRef.ContentSha256}",
        "ui-action-test",
        "direct_ui_commit",
        preRef,
        record.Action,
        successorRef,
        "canonical_s_a_s_prime",
        new[]
        {
            "complete_pre_state_and_catalog",
            "chosen_action_exactly_once_in_pre_catalog",
            "one_mutation_in_flight",
            "native_terminal_or_direct_commit_observed",
            "no_intervening_human_mutation",
            "complete_authoritative_successor"
        },
        new[] { "not_business_completion" });

    private static HumanCaptureProfile Profile() => new(
        2,
        HumanRecorderV2Contract.CaptureProfileSchema,
        "human-combat-read-rich-v2",
        HumanRecorderV2Contract.RecordSchema,
        new[] { "ordinary_combat.play_card", "ordinary_combat.end_turn" },
        new[]
        {
            new CaptureReadRequirement("pre", "run_deck", true),
            new CaptureReadRequirement("pre", "combat_piles", true),
            new CaptureReadRequirement("successor", "run_deck", true),
            new CaptureReadRequirement("successor", "combat_piles", true)
        },
        new[] { "ordinary_combat_only", "not_full_run" });

    private static RecordingManifestV2 Manifest(
        HumanCaptureProfile profile,
        string sessionId = "session-test",
        string timelineId = "timeline-test") => new(
        2,
        HumanRecorderV2Contract.ManifestSchema,
        sessionId,
        timelineId,
        DateTimeOffset.UnixEpoch,
        "0.3.0",
        new string('b', 40),
        "osx-arm64",
        profile.ProfileId,
        EvidenceIdentity.Sha256Json(profile),
        profile.SupportedActionFamilies,
        profile.NonClaims);

    private static void AppendJournal(V2RecordingStore store, RecordingManifestV2 manifest)
    {
        store.AppendRunEvent(new RunJournalEvent(
            2,
            HumanRecorderV2Contract.RunJournalSchema,
            "event-1",
            manifest.SessionId,
            "run-unassigned",
            manifest.TimelineId,
            1,
            DateTimeOffset.UnixEpoch,
            "session_started",
            null,
            null,
            null));
        store.AppendRunEvent(new RunJournalEvent(
            2,
            HumanRecorderV2Contract.RunJournalSchema,
            "event-2",
            manifest.SessionId,
            "run-0001",
            manifest.TimelineId,
            2,
            DateTimeOffset.UnixEpoch,
            "run_started",
            null,
            "snapshot-a",
            null));
    }

    private static IReadOnlyList<ReadEvidence> PersistReads(V2RecordingStore store, string snapshotId)
    {
        JsonNode payload = JsonNode.Parse("{\"cards\":[{\"name\":\"Strike\"}]}" )!;
        JsonNode completeness = JsonNode.Parse("{\"status\":\"complete\",\"missing\":[]}")!;
        return new[] { "run_deck", "combat_piles" }
            .Select(kind => store.PersistRead(new CapturedReadPayload(
                $"read-{kind}",
                kind,
                snapshotId,
                "runtime-1",
                "environment-1",
                "materialized",
                $"sts2.player-environment/read/{kind}-1",
                payload.DeepClone(),
                completeness.DeepClone(),
                DateTimeOffset.UnixEpoch,
                null,
                null)))
            .ToArray();
    }

    private static (IReadOnlyList<ReadEvidence> Pre, IReadOnlyList<ReadEvidence> Successor) Reads(
        HumanDecisionRecord record)
    {
        ReadEvidence Read(string kind, string snapshotId, string suffix) => new(
            2,
            HumanRecorderV2Contract.ReadEvidenceSchema,
            $"read-evidence-{kind}-{suffix}",
            $"read-{kind}",
            kind,
            snapshotId,
            record.Environment.RuntimeInstanceId,
            record.Environment.EnvironmentFingerprint,
            "materialized",
            $"sts2.player-environment/read/{kind}-1",
            JsonNode.Parse("{\"status\":\"complete\"}"),
            $"blobs/sha256/aa/{new string('a', 64)}.json",
            new string('a', 64),
            DateTimeOffset.UnixEpoch,
            null,
            null);
        return (
            new[]
            {
                Read("run_deck", record.Pre.SnapshotId, "pre"),
                Read("combat_piles", record.Pre.SnapshotId, "pre")
            },
            new[]
            {
                Read("run_deck", record.Successor.SnapshotId, "successor"),
                Read("combat_piles", record.Successor.SnapshotId, "successor")
            });
    }

    private static HumanDecisionRecordV2 V2Record(
        HumanDecisionRecord value,
        (IReadOnlyList<ReadEvidence> Pre, IReadOnlyList<ReadEvidence> Successor) reads) => new(
        2,
        HumanRecorderV2Contract.RecordSchema,
        value.RecordId,
        value.SessionId,
        value.RunId,
        "timeline-test",
        value.Sequence,
        value.RecordedAt,
        value.Environment,
        "human-combat-read-rich-v2",
        new FrozenDecisionFrameV2(
            value.Pre.SnapshotId,
            value.Pre.InteractionId,
            value.Pre.InteractionKind,
            value.Pre.SurfaceSchema,
            value.Pre.CatalogDigest,
            value.Pre.CatalogCount,
            value.Pre.Snapshot,
            reads.Pre),
        value.NativeWitness,
        value.Mapping,
        value.Action,
        new StableSuccessorV2(
            value.Successor.SnapshotId,
            value.Successor.Status,
            value.Successor.InteractionId,
            value.Successor.InteractionKind,
            value.Successor.ObservedAt,
            value.Successor.Snapshot,
            reads.Successor),
        value.DecisionFamily,
        value.Surface,
        value.Eligibility);

    private static SemanticBoundaryTraceEvent SemanticEvent(
        RecordingManifestV2 manifest,
        long sequence,
        string kind,
        SemanticActionReference action,
        FrozenDecisionFrameV2? humanObservation = null) => new(
            SemanticBoundaryTraceContract.SchemaVersion,
            SemanticBoundaryTraceContract.EventSchema,
            $"semantic-event-{sequence}",
            manifest.SessionId,
            manifest.TimelineId,
            action.RunId,
            sequence,
            DateTimeOffset.UnixEpoch.AddMilliseconds(sequence),
            kind,
            action,
            kind == SemanticBoundaryTraceKinds.ActionAccepted
                ? "human_observation_recorded"
                : "not_a_successful_action",
            null,
            null,
            null,
            null,
            null,
            Array.Empty<string>())
        {
            HumanObservation = humanObservation
        };

    private static SemanticBoundaryTraceEvent SemanticEvent(
        RecordingManifestV2 manifest,
        long sequence,
        SemanticBoundaryTraceDraft draft) => new(
            SemanticBoundaryTraceContract.SchemaVersion,
            SemanticBoundaryTraceContract.EventSchema,
            $"semantic-event-{sequence}",
            manifest.SessionId,
            manifest.TimelineId,
            draft.Action.RunId,
            sequence,
            DateTimeOffset.UnixEpoch.AddMilliseconds(sequence),
            draft.Kind,
            draft.Action,
            draft.ProofStatus,
            draft.RelatedActionWitnessId,
            draft.Boundary,
            draft.SemanticPre,
            draft.SemanticSuccessor,
            draft.Detail,
            draft.NonClaims ?? Array.Empty<string>())
        {
            HumanObservation = draft.HumanObservation,
            NativeCompletion = draft.NativeCompletion
        };

    private static NativeSemanticDiscriminatorEvent DiscriminatorEvent(
        RecordingManifestV2 manifest,
        long sequence,
        string phase,
        string actionWitnessId) => new(
            NativeSemanticDiscriminatorContract.SchemaVersion,
            NativeSemanticDiscriminatorContract.EventSchema,
            $"discriminator-event-{sequence}",
            manifest.SessionId,
            manifest.TimelineId,
            "run-0001",
            sequence,
            DateTimeOffset.UnixEpoch.AddMilliseconds(sequence),
            phase,
            actionWitnessId,
            "PlayCardAction",
            7,
            phase,
            "captured",
            "combat_play_phase",
            null,
            null,
            "semantic-catalog",
            Array.Empty<string>(),
            null,
            null,
            null,
            "snapshot-test",
            "interactive",
            "combat_turn",
            "complete",
            1,
            "ui-catalog",
            null,
            null,
            null,
            null,
            Array.Empty<string>());

    private static SemanticEvidenceEvent SemanticEvidenceEvent(
        RecordingManifestV2 manifest,
        long sequence,
        string kind,
        SemanticActionReference action) => new(
            SemanticEvidenceContract.SchemaVersion,
            SemanticEvidenceContract.EventSchema,
            $"semantic-evidence-event-{sequence}",
            manifest.SessionId,
            manifest.TimelineId,
            action.RunId,
            sequence,
            DateTimeOffset.UnixEpoch.AddMilliseconds(sequence),
            kind,
            action,
            kind == SemanticBoundaryTraceKinds.ActionAccepted
                ? "human_observation_recorded"
                : "not_a_successful_action",
            null,
            null,
            null,
            null,
            null,
            Array.Empty<string>());

    private static string Temp(string name) =>
        Path.Combine(Path.GetTempPath(), $"sts2-{name}-{Guid.NewGuid():N}");

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }
}
