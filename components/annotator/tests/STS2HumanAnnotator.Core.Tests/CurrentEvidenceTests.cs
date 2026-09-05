using System.Text.Json;
using System.Text.Json.Nodes;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class CurrentEvidenceTests
{
    [Fact]
    public void FullRunCoverageMapIsCompleteAndKeepsNestedLineageBlocked()
    {
        IReadOnlyList<FullRunCoverageEntry> entries = FullRunCoverageContract.Entries;

        Assert.Empty(FullRunCoverageContract.Validate());
        Assert.Equal(
            entries.Count,
            entries.Select(entry => entry.Family).Distinct(StringComparer.Ordinal).Count());

        HumanCaptureProfile profile = HumanCaptureProfiles.FullRunReadRich;
        foreach (string family in profile.SupportedActionFamilies)
        {
            FullRunCoverageEntry entry = Assert.Single(
                entries,
                value => string.Equals(value.Family, family, StringComparison.Ordinal));
            Assert.Equal(FullRunCoverageClassifications.InScopeImplemented, entry.Classification);
        }

        Assert.Equal(
            FullRunCoverageClassifications.InScopeImplemented,
            Assert.Single(entries, value => value.Family == "boss_relic.select").Classification);
        Assert.Equal(
            FullRunCoverageClassifications.InScopeImplemented,
            Assert.Single(entries, value => value.Family == "boss_relic.skip").Classification);
        Assert.Contains(entries, value =>
            value.Family == "act_change.ready"
            && value.AcceptedSeam.Contains("SetLocalPlayerReady", StringComparison.Ordinal)
            && value.LifecycleCommit.Contains("ExecuteAction", StringComparison.Ordinal)
            && value.NextAuthoritativeBoundary.Contains("ActEntered", StringComparison.Ordinal));

        foreach (FullRunCoverageEntry entry in entries.Where(value =>
                     value.Family.Contains("nested_selector", StringComparison.Ordinal)))
        {
            Assert.Equal(FullRunCoverageClassifications.Blocked, entry.Classification);
            Assert.Contains("BLOCKED", entry.AcceptedSeam + entry.NextAuthoritativeBoundary);
        }
    }

    [Fact]
    public void FullRunCoverageDeclaresEveryMandatoryFamilyAndBlocksQualification()
    {
        IReadOnlyList<FullRunCoverageEntry> entries = FullRunCoverageContract.Entries;
        HashSet<string> declared = entries
            .Select(entry => entry.Family)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            FullRunCoverageContract.MandatoryFamilies,
            family => Assert.Contains(family, declared));

        FullRunCoverageValidation validation =
            FullRunCoverageContract.ValidateForQualification();
        Assert.False(validation.QualificationReady);
        Assert.NotEmpty(validation.BlockedInScopeFamilies);
        Assert.Contains(
            "in_scope_blocked:generic_simple_card_selector",
            validation.Errors);
        Assert.Contains(
            "in_scope_blocked:shop_inventory.card_removal_nested_selector",
            validation.Errors);

        string[] unprovedFamilies =
        {
            "reward_nested.replacement_selection",
            "generic_simple_card_selector",
            "generic_deck_card_selector",
            "generic_combat_pile_selector",
            "generic_card_bundle_selector",
            "target_picker.cancel",
            "shop_inventory.card_removal_nested_selector",
            "event_option.nested_selector",
            "rest_site.nested_selector"
        };
        foreach (string family in unprovedFamilies)
        {
            FullRunCoverageEntry entry = Assert.Single(
                entries,
                value => value.Family == family);
            Assert.Equal(FullRunCoverageClassifications.Blocked, entry.Classification);
            Assert.Contains("BLOCKED", string.Join("|", new[]
            {
                entry.UiInput,
                entry.NativeOwner,
                entry.SemanticProvider,
                entry.AcceptedSeam,
                entry.LifecycleCommit,
                entry.NextAuthoritativeBoundary,
                entry.Justification
            }));
        }
    }

    [Fact]
    public void FullRunCoverageValidationRejectsOmittedMandatoryFamily()
    {
        IReadOnlyList<FullRunCoverageEntry> entries = FullRunCoverageContract.Entries
            .Where(entry => entry.Family != "target_picker.cancel")
            .ToArray();

        FullRunCoverageValidation validation =
            FullRunCoverageContract.ValidateForQualification(entries);
        Assert.False(validation.IsValid);
        Assert.False(validation.QualificationReady);
        Assert.Contains("target_picker.cancel", validation.MissingMandatoryFamilies);
        Assert.Contains("mandatory_family_missing:target_picker.cancel", validation.Errors);
    }

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
    public void FullRunProfileDeclaresRoomFamiliesWithoutChangingReadPolicy()
    {
        HumanCaptureProfile profile = HumanCaptureProfiles.FullRunReadRich;

        Assert.Equal("human-full-run-read-rich-v3", profile.ProfileId);
        Assert.Contains("combat_hand_selector.select", profile.SupportedActionFamilies);
        Assert.Contains("combat_hand_selector.deselect", profile.SupportedActionFamilies);
        Assert.Contains("combat_hand_selector.confirm", profile.SupportedActionFamilies);
        Assert.Contains("event_option.choose", profile.SupportedActionFamilies);
        Assert.Contains("event_option.proceed", profile.SupportedActionFamilies);
        Assert.Contains("shop_room.open", profile.SupportedActionFamilies);
        Assert.Contains("shop_room.proceed", profile.SupportedActionFamilies);
        Assert.Contains("shop_inventory.purchase", profile.SupportedActionFamilies);
        Assert.Contains("shop_inventory.card_removal", profile.SupportedActionFamilies);
        Assert.Contains("shop_inventory.close", profile.SupportedActionFamilies);
        Assert.Contains("rest_site.choose", profile.SupportedActionFamilies);
        Assert.Contains("rest_site.proceed", profile.SupportedActionFamilies);
        Assert.Contains(profile.Reads, read =>
            read.InteractionKind == "shop_inventory" && read.Kind == "shop_catalog");
        Assert.Contains(profile.NonClaims, claim =>
            claim.Contains("non_combat_successor", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadRichDecisionValidatesWithoutChangingV1()
    {
        HistoricalDecisionRecord v1 = RecordValidationTests.ValidRecord();
        CurrentDecisionRecord current = CurrentRecord(v1, Reads(v1));

        Assert.True(HistoricalDecisionRecordValidator.Validate(v1).Valid);
        RecordValidationResult result = CurrentDecisionRecordValidator.Validate(current);
        Assert.True(result.Valid, string.Join(',', result.Errors));

        CurrentDecisionRecord unified = current with
        {
            Environment = current.Environment with { ModsetStatus = "exact_platform_modset" }
        };
        RecordValidationResult unifiedResult = CurrentDecisionRecordValidator.Validate(unified);
        Assert.True(unifiedResult.Valid, string.Join(',', unifiedResult.Errors));
    }

    [Fact]
    public void ReadBindingAndFailureStatusFailClosed()
    {
        CurrentDecisionRecord record = CurrentRecord(
            RecordValidationTests.ValidRecord(),
            Reads(RecordValidationTests.ValidRecord()));
        ReadEvidence drifted = record.Pre.Reads[0] with { SnapshotId = "snapshot-elsewhere" };
        RecordValidationResult binding = CurrentDecisionRecordValidator.Validate(record with
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
        RecordValidationResult failure = CurrentDecisionRecordValidator.Validate(record with
        {
            Pre = record.Pre with { Reads = new[] { ambiguousFailure, record.Pre.Reads[1] } }
        });
        Assert.Contains("pre_read_failure_invalid", failure.Errors);
    }

    [Fact]
    public void CaptureProfileRequiresMaterializedReadsAndAdmittedFamily()
    {
        CurrentDecisionRecord record = CurrentRecord(
            RecordValidationTests.ValidRecord(),
            Reads(RecordValidationTests.ValidRecord()));
        CurrentDecisionRecord missing = record with
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
    public void CurrentStoreDeduplicatesBlobsAndAuditDetectsTampering()
    {
        string root = Temp("current-store");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord v1 = RecordValidationTests.ValidRecord();
                IReadOnlyList<ReadEvidence> pre = PersistReads(store, v1.Pre.SnapshotId);
                IReadOnlyList<ReadEvidence> successor = PersistReads(store, v1.Successor.SnapshotId);
                Assert.Equal(pre[0].PayloadSha256, pre[1].PayloadSha256);
                store.AppendDecision(CurrentRecord(v1, (pre, successor)));
            }
            RecordingAuditResult pass = RecordingSessionAuditor.Audit(session);
            Assert.Equal("pass", pass.Status);
            Assert.Equal(1, pass.ValidRecords);
            string blob = Directory.GetFiles(
                Path.Combine(session, "blobs"), "*.json", SearchOption.AllDirectories).Single();
            File.AppendAllText(blob, "tamper");
            RecordingAuditResult fail = RecordingSessionAuditor.Audit(session);
            Assert.Equal("fail", fail.Status);
            Assert.Contains("read_blob_missing_or_changed", fail.Errors);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void CurrentStoreRejectsHistoricalInvalidationSchema()
    {
        string root = Temp("current-invalidation-schema");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            using var store = RecordingSessionStore.Create(root, manifest, profile);

            Assert.Throws<InvalidDataException>(() => store.AppendInvalidation(
                new InvalidationRecord(
                    HistoricalRecordingContract.SchemaVersion,
                    HistoricalRecordingContract.InvalidationSchema,
                    "invalidation-legacy",
                    manifest.SessionId,
                    "run-0001",
                    DateTimeOffset.UnixEpoch,
                    "historical",
                    "archival-only",
                    null,
                    null,
                    "historical")));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void GeneratedChoiceFailedClosedOccurrenceRoundTripsAndIsAudited()
    {
        string root = Temp("generated-choice-occurrence");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord source = RecordValidationTests.ValidRecord();
                store.AppendDecision(CurrentRecord(source, (
                    PersistReads(store, source.Pre.SnapshotId),
                    PersistReads(store, source.Successor.SnapshotId))));
                store.AppendInvalidation(new InvalidationRecord(
                    CurrentRecordingContract.SchemaVersion,
                    CurrentRecordingContract.InvalidationSchema,
                    "invalidation-generated-choice",
                    manifest.SessionId,
                    source.RunId,
                    DateTimeOffset.UnixEpoch,
                    "semantic_causal_overlap",
                    "The parent continuation was not available for canonical child proof.",
                    source.Pre.SnapshotId,
                    "NChooseACardSelectionScreen.SelectHolder",
                    "decision_and_lifecycle_only")
                {
                    HumanOccurrence = new HumanActionOccurrenceEvidence(
                        "occurrence-generated-choice",
                        "NChooseACardSelectionScreen.SelectHolder",
                        "generated_card_choice",
                        "select",
                        "card:exact",
                        new Dictionary<string, string>
                        {
                            ["selected_card_holder"] = "card_holder:exact"
                        },
                        "choice_owner:exact",
                        "game_action:parent",
                        "GenericHookGameAction",
                        "gatheringplayerchoice",
                        "NChooseACardSelectionScreen.SelectHolder",
                        "failed_closed")
                });
            }

            InvalidationRecord persisted = JsonSerializer.Deserialize<InvalidationRecord>(
                File.ReadLines(Path.Combine(session, "invalidations.jsonl")).Single(),
                EvidenceJson.Options)!;
            Assert.Equal("card:exact", persisted.HumanOccurrence!.NativeSubjectWitnessId);
            Assert.Equal("game_action:parent", persisted.HumanOccurrence.PausedParentActionWitnessId);
            Assert.Equal("pass", RecordingSessionAuditor.Audit(session).Status);

            string original = File.ReadLines(Path.Combine(session, "invalidations.jsonl")).Single();
            JsonObject missing = JsonNode.Parse(original)!.AsObject();
            missing.Remove("human_occurrence");
            File.WriteAllText(
                Path.Combine(session, "invalidations.jsonl"),
                missing.ToJsonString(EvidenceJson.Options) + "\n");
            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);
            Assert.Equal("fail", audit.Status);
            Assert.True(audit.Errors.ContainsKey("generated_choice_human_occurrence_missing"));

            JsonObject incomplete = JsonNode.Parse(original)!.AsObject();
            incomplete["human_occurrence"]!["native_subject_witness_id"] = null;
            incomplete["human_occurrence"]!["native_operands"]!["selected_card_holder"] = null;
            File.WriteAllText(
                Path.Combine(session, "invalidations.jsonl"),
                incomplete.ToJsonString(EvidenceJson.Options) + "\n");
            RecordingAuditResult incompleteAudit = RecordingSessionAuditor.Audit(session);
            Assert.Equal("fail", incompleteAudit.Status);
            Assert.True(incompleteAudit.Errors.ContainsKey("generated_choice_subject_missing"));
            Assert.True(incompleteAudit.Errors.ContainsKey("generated_choice_selected_holder_missing"));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void HistoricalNativeLedgerSidecarIsIgnoredByCurrentAuditAndBundle()
    {
        string root = Temp("current-archival-ledger");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord source = RecordValidationTests.ValidRecord();
                store.AppendDecision(CurrentRecord(source, (
                    PersistReads(store, source.Pre.SnapshotId),
                    PersistReads(store, source.Successor.SnapshotId))));
            }

            // A historical sidecar is not a current authority and is ignored
            // by current audit and bundle materialization.
            File.WriteAllText(
                Path.Combine(session, "native-action-ledger.jsonl"),
                "not-current-ledger\n");
            RecordingAuditResult pass = RecordingSessionAuditor.Audit(session);
            Assert.Equal("pass", pass.Status);
            string output = Path.Combine(root, "bundle");
            SessionBundlePacker.Pack(
                session,
                "human-001",
                "human-read-rich-2026-08",
                output,
                new string('c', 40),
                true);
            Assert.False(File.Exists(Path.Combine(
                output,
                "raw",
                "native-action-ledger.jsonl")));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void CurrentAuditDoesNotRequireHistoricalLedgerAccounting()
    {
        string root = Temp("current-semantic-accounting");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord v1 = RecordValidationTests.ValidRecord();
                CurrentDecisionRecord current = CurrentRecord(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
                store.AppendDecision(current);

                var directAction = new SemanticActionReference(
                    "direct-action-accounted",
                    2,
                    "direct-record",
                    "run-0001",
                    "NPlayerHand.OnSelectModeConfirmButtonPressed",
                    null,
                    current.Pre.SnapshotId)
                {
                    NativeMechanism = "direct_ui_commit"
                };
                store.AppendSemanticBoundaryEvent(SemanticEvent(
                    manifest,
                    1,
                    SemanticBoundaryTraceKinds.ActionAccepted,
                    directAction,
                    current.Pre));
                store.AppendSemanticBoundaryEvent(SemanticEvent(
                    manifest,
                    2,
                    SemanticBoundaryTraceKinds.ActionCancelledBeforeStart,
                    directAction));
            }

            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);

            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
            Assert.DoesNotContain(
                audit.Errors.Keys,
                key => key.Contains("native_action", StringComparison.Ordinal));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void CurrentAuditAcceptsDiagnosticDiscriminatorWithoutUsingItAsAuthority()
    {
        string root = Temp("current-semantic-discriminator");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord v1 = RecordValidationTests.ValidRecord();
        CurrentDecisionRecord current = CurrentRecord(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
        store.AppendDecision(current);

                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    1,
                    "accepted",
                    "diagnostic-only-action"));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    2,
                    "before_execution",
                    "diagnostic-only-action") with
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
                    "diagnostic-only-action"));
                store.AppendNativeSemanticDiscriminatorEvent(DiscriminatorEvent(
                    manifest,
                    4,
                    "finished",
                    "diagnostic-only-action"));
            }

            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);

            Assert.True(
                audit.Status == "pass",
                JsonSerializer.Serialize(audit.Errors));
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
    public void ModernSemanticRootDoesNotRequireLegacyNativeLedgerProjection()
    {
        string root = Temp("modern-semantic-accounting");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord v1 = RecordValidationTests.ValidRecord();
        CurrentDecisionRecord current = CurrentRecord(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
        store.AppendDecision(current);
        CurrentDecisionFrame frame = current.Pre;
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

            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);

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
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            string discriminatorPath;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord v1 = RecordValidationTests.ValidRecord();
                CurrentDecisionRecord current = CurrentRecord(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId)));
                store.AppendDecision(current);
                CurrentDecisionFrame successor = current.Pre with
                {
                    SnapshotId = current.Successor.SnapshotId,
                    InteractionId = current.Successor.InteractionId,
                    InteractionKind = current.Successor.InteractionKind,
                    Snapshot = current.Successor.Snapshot,
                    Reads = current.Successor.Reads
                };

                var action = new SemanticActionReference(
                    "map-root",
                    1,
                    "map-record",
                    "run-0001",
                    "VoteForMapCoordAction",
                    1,
                    current.Pre.SnapshotId)
                {
                    RequiresNativePostCommit = true
                };
                var tracker = new SemanticBoundaryTracker();
                var drafts = new List<SemanticBoundaryTraceDraft>();
                drafts.AddRange(tracker.Accept(action, current.Pre));
                drafts.AddRange(tracker.ObserveBeforeActionExecution(
                    action.ActionWitnessId,
                    new SemanticBoundaryObservation(
                        SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
                        DateTimeOffset.UnixEpoch,
                        current.Pre.SnapshotId,
                        "interactive",
                        "complete",
                        current.Pre.InteractionId,
                        current.Pre.InteractionKind,
                        current.Pre,
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
                    SemanticMembership = "not_applicable",
                    SemanticMatchCount = 0
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
                discriminatorPath = Path.Combine(
                    session,
                    "native-semantic-discriminator.jsonl");
            }

            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);

            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
            NativeSemanticDiscriminatorReport diagnostic =
                NativeSemanticDiscriminatorAnalyzer.Analyze(
                    File.ReadLines(discriminatorPath)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => JsonSerializer.Deserialize<NativeSemanticDiscriminatorEvent>(
                            line,
                            EvidenceJson.Options)!)
                        .ToArray());
            Assert.Equal("fail", diagnostic.Status);
            Assert.Contains(diagnostic.Errors, value =>
                value.EndsWith(
                    "successful_action_not_exact_once_in_semantic_catalog",
                    StringComparison.Ordinal));

            File.WriteAllText(
                discriminatorPath,
                File.ReadAllText(discriminatorPath).Replace(
                    NativeSemanticDiscriminatorContract.EventSchema,
                    "tampered-native-semantic-schema",
                    StringComparison.Ordinal));
            RecordingAuditResult malformed = RecordingSessionAuditor.Audit(session);
            Assert.Equal("fail", malformed.Status);
            Assert.Contains(
                "native_semantic_discriminator_analysis_failed",
                malformed.Errors.Keys);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void SemanticBoundaryBatchIsVisibleInOrderAndReadableAfterClose()
    {
        string root = Temp("current-semantic-batch");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile))
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
        string root = Temp("current-semantic-batch-failure");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            using RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile);
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
        string root = Temp("current-semantic-evidence");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            string framePath;
            using (RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                CurrentDecisionFrame frame = CurrentRecord(
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

            RecordingAuditResult beforeTamper = RecordingSessionAuditor.Audit(session);
            Assert.DoesNotContain(
                beforeTamper.Errors.Keys,
                key => key.StartsWith("semantic_", StringComparison.Ordinal));
            File.AppendAllText(framePath, "tampered");
            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);
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
        string root = Temp("current-semantic-owner-ready-round-trip");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            string tracePath;
            using (RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord validRecord = RecordValidationTests.ValidRecord();
                CurrentDecisionRecord record = CurrentRecord(
                    validRecord,
                    (PersistReads(store, validRecord.Pre.SnapshotId),
                        PersistReads(store, validRecord.Successor.SnapshotId)));
                store.AppendDecision(record);
                CurrentDecisionFrame pre = record.Pre with
                {
                    SnapshotId = "snapshot-pre",
                    InteractionId = "map-owner",
                    InteractionKind = "map"
                };
                CurrentDecisionFrame successor = pre with
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

            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);
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
            RecordingAuditResult missingAudit = RecordingSessionAuditor.Audit(session);
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
            RecordingAuditResult mismatchedAudit = RecordingSessionAuditor.Audit(session);
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
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord source = RecordValidationTests.ValidRecord();
        CurrentDecisionRecord seed = CurrentRecord(
                    source,
                    (PersistReads(store, source.Pre.SnapshotId),
                        PersistReads(store, source.Successor.SnapshotId)));
                CurrentDecisionFrame humanObservation = seed.Pre;
                CurrentDecisionFrame successor = new(
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

                CurrentDecisionRecord decision = SemanticTransitionProjection.CreateDecision(
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

            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);
            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
            Assert.Single(RecordingSessionAuditor.ReadAdmitted(session));
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
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            string actionSpacePath;
            using (RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord source = RecordValidationTests.ValidRecord();
        CurrentDecisionRecord decision = CurrentRecord(
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
                var executionPre = new CurrentDecisionFrame(
                    "execution-pre",
                    decision.Pre.InteractionId,
                    decision.Pre.InteractionKind,
                    decision.Pre.SurfaceSchema,
                    EvidenceIdentity.Sha256Json(executionSnapshot["bound_actions"]!),
                    0,
                    executionSnapshot,
                    decision.Pre.Reads);
                var successor = new CurrentDecisionFrame(
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

            RecordingAuditResult audit = RecordingSessionAuditor.Audit(session);
            Assert.True(audit.Status == "pass", JsonSerializer.Serialize(audit.Errors));
            File.AppendAllText(actionSpacePath, "tampered");
            RecordingAuditResult tampered = RecordingSessionAuditor.Audit(session);
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
        string root = Temp("current-read-batch");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            using RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile);
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
    public void CurrentBundleIsPortableDeterministicAndImmutable()
    {
        string root = Temp("current-bundle");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord v1 = RecordValidationTests.ValidRecord();
                store.AppendDecision(CurrentRecord(v1, (
                    PersistReads(store, v1.Pre.SnapshotId),
                    PersistReads(store, v1.Successor.SnapshotId))));
            }
            string output = Path.Combine(root, "bundle");
            SessionBundleResult first = SessionBundlePacker.Pack(
                session,
                "human-001",
                "human-read-rich-2026-08",
                output,
                new string('c', 40),
                true);
            SessionBundleResult retry = SessionBundlePacker.Pack(
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
            Assert.Throws<IOException>(() => SessionBundlePacker.Pack(
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
        string root = Temp("current-multiple-sessions");
        try
        {
            HumanCaptureProfile profile = Profile();
            using RecordingSessionStore first = RecordingSessionStore.Create(
                root,
                Manifest(profile, "session-first", "timeline-first"),
                profile);
            using RecordingSessionStore second = RecordingSessionStore.Create(
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
        string root = Temp("current-write-failure");
        try
        {
            HumanCaptureProfile profile = Profile();
            using RecordingSessionStore store = RecordingSessionStore.Create(root, Manifest(profile), profile);
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
        string root = Temp("current-canonical-transition");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            SemanticFrameReference preRef;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord value = RecordValidationTests.ValidRecord();
                CurrentDecisionRecord record = CurrentRecord(
                    value,
                    (PersistReads(store, value.Pre.SnapshotId),
                        PersistReads(store, value.Successor.SnapshotId)));
                store.AppendDecision(record);
                preRef = store.PersistSemanticFrame(record.Pre);
                var successor = new CurrentDecisionFrame(
                    record.Successor.SnapshotId,
                    record.Successor.InteractionId,
                    record.Successor.InteractionKind,
                    record.Pre.SurfaceSchema,
                    record.Pre.CatalogDigest,
                    record.Pre.CatalogCount,
                    record.Successor.Snapshot.DeepClone(),
                    record.Successor.Reads);
                SemanticFrameReference successorRef = store.PersistSemanticFrame(successor);
                SemanticActionReference semanticAction = new(
                    "ui-action-test",
                    record.Sequence,
                    record.RecordId,
                    record.RunId,
                    record.NativeWitness.NativeActionType,
                    7,
                    record.Pre.SnapshotId)
                {
                    NativeMechanism = "direct_ui_commit",
                    BoundAction = record.Action,
                    NativeWitness = record.NativeWitness,
                    Mapping = record.Mapping
                };
                store.AppendSemanticBoundaryEvents(new[]
                {
                    SemanticEvent(
                        manifest,
                        1,
                        SemanticBoundaryTraceKinds.ActionAccepted,
                        semanticAction,
                        record.Pre),
                    SemanticEvent(
                        manifest,
                        2,
                        SemanticBoundaryTraceKinds.BoundaryObserved,
                        semanticAction) with
                    {
                        Boundary = new SemanticBoundaryObservation(
                            SemanticBoundaryWitnessKinds.BeforeHumanActionExecution,
                            DateTimeOffset.UnixEpoch,
                            record.Pre.SnapshotId,
                            "interactive",
                            "complete",
                            record.Pre.InteractionId,
                            record.Pre.InteractionKind,
                            record.Pre,
                            semanticAction.ActionWitnessId)
                    },
                    SemanticEvent(
                        manifest,
                        3,
                        SemanticBoundaryTraceKinds.ActionStarted,
                        semanticAction),
                    SemanticEvent(
                        manifest,
                        4,
                        SemanticBoundaryTraceKinds.ActionFinished,
                        semanticAction)
                });
                store.AppendSemanticBoundaryEvent(new SemanticBoundaryTraceEvent(
                    SemanticBoundaryTraceContract.SchemaVersion,
                    SemanticBoundaryTraceContract.EventSchema,
                    "semantic-proof-test",
                    manifest.SessionId,
                    manifest.TimelineId,
                    record.RunId,
                    5,
                    DateTimeOffset.UnixEpoch,
                    SemanticBoundaryTraceKinds.TransitionProved,
                    semanticAction,
                    "proved_native_commit_then_boundary",
                    null,
                    new SemanticBoundaryObservation(
                        SemanticBoundaryWitnessKinds.CompleteInteractiveObservation,
                        DateTimeOffset.UnixEpoch,
                        successor.SnapshotId,
                        "interactive",
                        "complete",
                        successor.InteractionId,
                        successor.InteractionKind,
                        successor,
                        null),
                    record.Pre,
                    successor,
                    null,
                    Array.Empty<string>())
                {
                    HumanObservation = record.Pre
                });
                store.AppendCanonicalTransition(Canonical(record, preRef, successorRef));
            }

            RecordingAuditResult pass = RecordingSessionAuditor.Audit(session);
            Assert.True(
                pass.Status == "pass",
                JsonSerializer.Serialize(pass.Errors, EvidenceJson.Options));
            File.AppendAllText(Path.Combine(session, preRef.ObjectRef), "tampered");
            RecordingAuditResult tampered = RecordingSessionAuditor.Audit(session);
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
        string root = Temp("current-pre-serialized-compatibility");
        try
        {
            HumanCaptureProfile profile = Profile();
            CurrentRecordingManifest manifest = Manifest(profile);
            string session;
            using (var store = RecordingSessionStore.Create(root, manifest, profile))
            {
                session = store.DirectoryPath;
                AppendJournal(store, manifest);
                HistoricalDecisionRecord value = RecordValidationTests.ValidRecord();
                store.AppendDecision(CurrentRecord(
                    value,
                    (PersistReads(store, value.Pre.SnapshotId),
                        PersistReads(store, value.Successor.SnapshotId))));
            }
            File.Delete(Path.Combine(session, "canonical-transitions.jsonl"));

            Assert.Equal("pass", RecordingSessionAuditor.Audit(session).Status);
        }
        finally
        {
            Delete(root);
        }
    }

    private static CanonicalTransitionEvidence Canonical(
        CurrentDecisionRecord record,
        SemanticFrameReference preRef,
        SemanticFrameReference successorRef) => new(
        CanonicalTransitionEvidenceContract.SchemaVersion,
        CanonicalTransitionEvidenceContract.Schema,
        $"canonical-{record.RecordId}",
        record.SessionId,
        record.TimelineId,
        record.RunId,
        record.Sequence,
        DateTimeOffset.UnixEpoch,
        CanonicalTransitionEvidenceContract.CollectionMode,
        null,
        "ui-action-test",
        "direct_ui_commit",
        preRef,
        record.Action,
        successorRef,
        "canonical_s_a_s_prime",
        new[]
        {
            "complete_execution_state",
            "chosen_action_exactly_once_in_authoritative_action_space",
            "exact_human_native_action_correlation",
            "native_terminal_or_direct_commit_observed",
            "no_intervening_human_mutation",
            "complete_authoritative_successor"
        },
        new[] { "not_business_completion" })
    {
        ActionSpaceAuthority = "public_bound_actions"
    };

    private static HumanCaptureProfile Profile() => new(
        2,
        CurrentRecordingContract.CaptureProfileSchema,
        "human-combat-read-rich-v2",
        CurrentRecordingContract.RecordSchema,
        new[] { "ordinary_combat.play_card", "ordinary_combat.end_turn" },
        new[]
        {
            new CaptureReadRequirement("pre", "run_deck", true),
            new CaptureReadRequirement("pre", "combat_piles", true),
            new CaptureReadRequirement("successor", "run_deck", true),
            new CaptureReadRequirement("successor", "combat_piles", true)
        },
        new[] { "ordinary_combat_only", "not_full_run" });

    private static CurrentRecordingManifest Manifest(
        HumanCaptureProfile profile,
        string sessionId = "session-test",
        string timelineId = "timeline-test") => new(
        2,
        CurrentRecordingContract.ManifestSchema,
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

    private static void AppendJournal(RecordingSessionStore store, CurrentRecordingManifest manifest)
    {
        store.AppendRunEvent(new RunJournalEvent(
            2,
            CurrentRecordingContract.RunJournalSchema,
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
            CurrentRecordingContract.RunJournalSchema,
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

    private static IReadOnlyList<ReadEvidence> PersistReads(RecordingSessionStore store, string snapshotId)
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
        HistoricalDecisionRecord record)
    {
        ReadEvidence Read(string kind, string snapshotId, string suffix) => new(
            2,
            CurrentRecordingContract.ReadEvidenceSchema,
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

    private static CurrentDecisionRecord CurrentRecord(
        HistoricalDecisionRecord value,
        (IReadOnlyList<ReadEvidence> Pre, IReadOnlyList<ReadEvidence> Successor) reads) => new(
        2,
        CurrentRecordingContract.RecordSchema,
        value.RecordId,
        value.SessionId,
        value.RunId,
        "timeline-test",
        value.Sequence,
        value.RecordedAt,
        value.Environment,
        "human-combat-read-rich-v2",
        new CurrentDecisionFrame(
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
        new CurrentSuccessor(
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
        CurrentRecordingManifest manifest,
        long sequence,
        string kind,
        SemanticActionReference action,
        CurrentDecisionFrame? humanObservation = null) => new(
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
        CurrentRecordingManifest manifest,
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
        CurrentRecordingManifest manifest,
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
        CurrentRecordingManifest manifest,
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
