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

    private static string Temp(string name) =>
        Path.Combine(Path.GetTempPath(), $"sts2-{name}-{Guid.NewGuid():N}");

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }
}
