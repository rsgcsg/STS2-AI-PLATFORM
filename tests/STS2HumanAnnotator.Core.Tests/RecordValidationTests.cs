using System.Text.Json.Nodes;
using System.Text.Json;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class RecordValidationTests
{
    [Fact]
    public void NativeRootActionGateAcceptsOnlyOneExpectedRoot()
    {
        var gate = new AcceptedRootActionGate("PlayCardAction");

        Assert.False(gate.Accepts("ReadyToBeginEnemyTurnAction"));
        Assert.True(gate.Accepts("PlayCardAction"));
        Assert.False(gate.TryClaim("ReadyToBeginEnemyTurnAction"));
        Assert.True(gate.TryClaim("PlayCardAction"));
        Assert.False(gate.TryClaim("PlayCardAction"));
    }

    [Fact]
    public void ExactRecordPasses()
    {
        RecordValidationResult result = HumanDecisionRecordValidator.Validate(ValidRecord());

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("zero", 0)]
    [InlineData("ambiguous", 2)]
    public void NonUniqueMappingFailsClosed(string status, int count)
    {
        HumanDecisionRecord record = ValidRecord() with
        {
            Mapping = new ExactMappingEvidence(
                status,
                count,
                "reference_equality_to_frozen_host_binding",
                null)
        };

        RecordValidationResult result = HumanDecisionRecordValidator.Validate(record);

        Assert.False(result.Valid);
        Assert.Contains("mapping_not_exact_unique", result.Errors);
    }

    [Fact]
    public void SameSnapshotCannotMasqueradeAsSuccessor()
    {
        HumanDecisionRecord valid = ValidRecord();
        HumanDecisionRecord record = valid with
        {
            Successor = valid.Successor with { SnapshotId = valid.Pre.SnapshotId }
        };

        RecordValidationResult result = HumanDecisionRecordValidator.Validate(record);

        Assert.False(result.Valid);
        Assert.Contains("stable_successor_missing", result.Errors);
    }

    [Fact]
    public void CatalogTamperingFailsIndependentAuditValidation()
    {
        HumanDecisionRecord valid = ValidRecord();
        JsonObject snapshot = (JsonObject)valid.Pre.Snapshot.DeepClone();
        JsonArray actions = (JsonArray)snapshot["bound_actions"]!["actions"]!;
        ((JsonObject)actions[0]!)["verb"] = "invented";

        RecordValidationResult result = HumanDecisionRecordValidator.Validate(
            valid with { Pre = valid.Pre with { Snapshot = snapshot } });

        Assert.False(result.Valid);
        Assert.Contains("pre_frame_evidence_mismatch", result.Errors);
    }

    [Fact]
    public void NestedRuntimeDriftFailsIndependentAuditValidation()
    {
        HumanDecisionRecord valid = ValidRecord();
        JsonObject successor = (JsonObject)valid.Successor.Snapshot.DeepClone();
        successor["session"]!["runtime_instance_id"] = "runtime-elsewhere";

        RecordValidationResult result = HumanDecisionRecordValidator.Validate(
            valid with { Successor = valid.Successor with { Snapshot = successor } });

        Assert.False(result.Valid);
        Assert.Contains("successor_runtime_identity_mismatch", result.Errors);
    }

    [Fact]
    public void StoreIsAppendOnlyAndAuditable()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sts2-annotator-test-{Guid.NewGuid():N}");
        try
        {
            var manifest = new RecordingManifest(
                1,
                HumanRecorderContract.ManifestSchema,
                "session-test",
                DateTimeOffset.UnixEpoch,
                "0.1.0",
                new string('a', 40),
                "test",
                new[] { "ordinary_combat" },
                Array.Empty<string>());
            using (RecordingStore store = RecordingStore.Create(root, manifest))
            {
                store.AppendDecision(ValidRecord());
                store.AppendDecision(ValidRecord() with
                {
                    RecordId = "record-2",
                    RunId = "run-0002",
                    Sequence = 2
                });
                store.AppendInvalidation(new InvalidationRecord(
                    1,
                    HumanRecorderContract.InvalidationSchema,
                    "invalidation-1",
                    "session-test",
                    "run-0001",
                    DateTimeOffset.UnixEpoch,
                    "fixture_negative",
                    "test",
                    "snapshot-a",
                    "fixture",
                    "fixture"));
            }

            string session = Path.Combine(root, "session-test");
            Assert.False(File.ReadAllBytes(Path.Combine(session, "recording-manifest.json"))
                .Take(3)
                .SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
            RecordingAuditResult audit = RecordingAuditor.Audit(session);
            Assert.Equal("pass", audit.Status);
            Assert.Equal(2, audit.ValidRecords);
            Assert.Equal(1, audit.Invalidations);
            Assert.Single(File.ReadLines(Path.Combine(session, "run-0001.jsonl")));
            Assert.Single(File.ReadLines(Path.Combine(session, "run-0002.jsonl")));
            string export = Path.Combine(root, "export.jsonl");
            Assert.Equal(2, RecordingAuditor.ExportAdmitted(session, export));
            Assert.Equal(2, File.ReadLines(export).Count());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionBundleIsDeterministicAndRefusesChangedExistingOutput()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sts2-bundle-test-{Guid.NewGuid():N}");
        try
        {
            string session = CreateSession(root);
            string profile = WriteProfile(root);
            string output = Path.Combine(root, "bundle");
            SessionBundleResult first = SessionBundlePacker.Pack(
                session,
                profile,
                "human-001",
                "human-combat-smoke-2026-08",
                output,
                new string('c', 40),
                humanOriginAttested: true);
            string checksums = File.ReadAllText(Path.Combine(output, "checksums.sha256"));

            SessionBundleResult retry = SessionBundlePacker.Pack(
                session,
                profile,
                "human-001",
                "human-combat-smoke-2026-08",
                output,
                new string('c', 40),
                humanOriginAttested: true);

            Assert.Equal(first.BundleContentId, retry.BundleContentId);
            Assert.Equal(checksums, File.ReadAllText(Path.Combine(output, "checksums.sha256")));
            Assert.Equal(1, first.RecordCount);
            Assert.True(File.Exists(Path.Combine(output, "raw", "run-0001.jsonl")));
            Assert.True(File.Exists(Path.Combine(output, "export", "decisions.jsonl")));
            Assert.True(File.Exists(Path.Combine(output, "profile", "collection-profile.json")));

            File.AppendAllText(Path.Combine(output, "export", "decisions.jsonl"), "tampered\n");
            Assert.Throws<IOException>(() => SessionBundlePacker.Pack(
                session,
                profile,
                "human-001",
                "human-combat-smoke-2026-08",
                output,
                new string('c', 40),
                humanOriginAttested: true));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionBundleRejectsProfileDriftAndMissingAttestation()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sts2-bundle-drift-{Guid.NewGuid():N}");
        try
        {
            string session = CreateSession(root);
            string profile = WriteProfile(root);
            Assert.Throws<InvalidDataException>(() => SessionBundlePacker.Pack(
                session,
                profile,
                "human-001",
                "human-combat-smoke-2026-08",
                Path.Combine(root, "not-attested"),
                new string('c', 40),
                humanOriginAttested: false));

            JsonObject drifted = JsonNode.Parse(File.ReadAllText(profile))!.AsObject();
            drifted["connector"]!["artifact_sha256"] = new string('d', 64);
            File.WriteAllText(profile, drifted.ToJsonString());
            Assert.Throws<InvalidDataException>(() => SessionBundlePacker.Pack(
                session,
                profile,
                "human-001",
                "human-combat-smoke-2026-08",
                Path.Combine(root, "drifted"),
                new string('c', 40),
                humanOriginAttested: true));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateSession(string root)
    {
        var manifest = new RecordingManifest(
            1,
            HumanRecorderContract.ManifestSchema,
            "session-test",
            DateTimeOffset.UnixEpoch,
            "0.1.0",
            new string('b', 40),
            "osx-arm64",
            new[] { "ordinary_combat.play_card", "ordinary_combat.end_turn" },
            Array.Empty<string>());
        using (RecordingStore store = RecordingStore.Create(root, manifest))
            store.AppendDecision(ValidRecord());
        return Path.Combine(root, "session-test");
    }

    private static string WriteProfile(string root)
    {
        HumanDecisionRecord record = ValidRecord();
        var profile = new
        {
            schema = "stpd/human-collection-profile-v1",
            profile_id = "human-mac-combat-v1",
            platform = "osx-arm64",
            game = new
            {
                version = record.Environment.Game.Version,
                commit = record.Environment.Game.Commit,
                main_assembly_sha256 = record.Environment.Game.MainAssemblySha256,
                main_assembly_mvid = record.Environment.Game.MainAssemblyModuleVersionId
            },
            connector = ProfileArtifact(record.Environment.Connector),
            annotator = ProfileArtifact(record.Environment.Annotator),
            player_environment_protocol = record.Environment.PlayerEnvironmentProtocol,
            modset = new
            {
                status = record.Environment.ModsetStatus,
                fingerprint = record.Environment.ModsetFingerprint
            },
            record_schema = HumanRecorderContract.RecordSchema,
            allowed_action_families = new[]
            {
                "ordinary_combat.play_card",
                "ordinary_combat.end_turn"
            }
        };
        string path = Path.Combine(root, "profile.json");
        File.WriteAllText(path, JsonSerializer.Serialize(profile, EvidenceJson.IndentedOptions));
        return path;
    }

    private static object ProfileArtifact(ExactArtifactIdentity artifact) => new
    {
        source_revision = artifact.SourceRevision,
        source_digest_sha256 = artifact.SourceDigestSha256,
        artifact_sha256 = artifact.Sha256,
        mvid = artifact.ModuleVersionId
    };

    internal static HumanDecisionRecord ValidRecord()
    {
        string sha = new('a', 64);
        string revision = new('b', 40);
        string mvid = "00000000-0000-0000-0000-000000000001";
        var artifact = new ExactArtifactIdentity("artifact", "1", revision, sha, sha, mvid);
        var recordedAction = new RecordedBoundAction(
            "bound-1",
            "play",
            "card-1",
            new Dictionary<string, string>(),
            "Play Strike");
        JsonObject preSnapshot = Snapshot("snapshot-a", "interaction-a", recordedAction);
        JsonObject successorSnapshot = Snapshot("snapshot-b", "interaction-b", recordedAction);
        string catalogDigest = EvidenceIdentity.Sha256Json(preSnapshot["bound_actions"]!);
        return new HumanDecisionRecord(
            1,
            HumanRecorderContract.RecordSchema,
            "record-1",
            "session-test",
            "run-0001",
            1,
            DateTimeOffset.UnixEpoch,
            new RecorderEnvironmentIdentity(
                new ExactGameIdentity("v0.111.0", "41cef1ea", sha, mvid),
                artifact,
                artifact,
                "1.0.0",
                "runtime-1",
                "environment-1",
                "canary_exact_observer_modset",
                sha),
            new FrozenDecisionFrame(
                "snapshot-a",
                "interaction-a",
                "combat_turn",
                "surface-a",
                catalogDigest,
                1,
                preSnapshot),
            new NativeWitnessEvidence(
                "native_card_play_ui",
                "PlayCardAction",
                "card-1",
                new Dictionary<string, string>(),
                DateTimeOffset.UnixEpoch),
            new ExactMappingEvidence(
                "exact_unique",
                1,
                "reference_equality_to_frozen_host_binding",
                null),
            recordedAction,
            new StableSuccessor(
                "snapshot-b",
                "interactive",
                "interaction-b",
                "combat_turn",
                DateTimeOffset.UnixEpoch,
                successorSnapshot),
            "ordinary_combat",
            "surface-a",
            new RecordEligibility("admitted", new[] { "fixture" }, Array.Empty<string>()));
    }

    private static JsonObject Snapshot(
        string snapshotId,
        string interactionId,
        RecordedBoundAction action) => new()
    {
        ["snapshot_id"] = snapshotId,
        ["status"] = "interactive",
        ["interaction"] = new JsonObject
        {
            ["interaction_id"] = interactionId,
            ["kind"] = "combat_turn",
            ["content_schema"] = "surface-a"
        },
        ["bound_actions"] = new JsonObject
        {
            ["status"] = "complete",
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["bound_action_id"] = action.BoundActionId,
                    ["verb"] = action.Verb,
                    ["subject_referent_id"] = action.SubjectReferentId,
                    ["arguments"] = new JsonArray(),
                    ["label"] = action.Label
                }
            }
        },
        ["completeness"] = new JsonObject { ["status"] = "complete" },
        ["session"] = new JsonObject
        {
            ["runtime_instance_id"] = "runtime-1",
            ["environment_fingerprint"] = "environment-1"
        }
    };
}
