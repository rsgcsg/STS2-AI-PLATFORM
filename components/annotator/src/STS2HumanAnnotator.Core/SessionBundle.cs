using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public sealed record SessionBundleResult(
    string Status,
    string BundleDirectory,
    string BundleContentId,
    string SessionId,
    long RecordCount,
    string ExportSha256,
    string ChecksumsSha256);

public static class SessionBundlePacker
{
    private const string ProfileSchema = "stpd/human-collection-profile-v1";
    private const string AttestationMethod = "explicit_owner_pack";

    public static SessionBundleResult Pack(
        string recordingDirectory,
        string profilePath,
        string workerId,
        string campaignId,
        string outputDirectory,
        string packerSourceRevision,
        bool humanOriginAttested)
    {
        if (!humanOriginAttested)
            throw new InvalidDataException("Human origin must be explicitly attested.");
        ValidateIdentifier(workerId, nameof(workerId));
        ValidateIdentifier(campaignId, nameof(campaignId));
        if (!IsCommit(packerSourceRevision))
            throw new InvalidDataException("Packer source revision must be an exact Git SHA.");

        string source = Path.GetFullPath(recordingDirectory);
        string destination = Path.GetFullPath(outputDirectory);
        RecordingAuditResult audit = RecordingAuditor.Audit(source);
        if (!string.Equals(audit.Status, "pass", StringComparison.Ordinal))
            throw new InvalidDataException("Recording audit must pass before packing.");
        RecordingManifest recordingManifest = ReadJson<RecordingManifest>(
            Path.Combine(source, "recording-manifest.json"));
        IReadOnlyList<HumanDecisionRecord> records = RecordingAuditor.ReadAdmitted(source);
        if (records.Count == 0)
            throw new InvalidDataException("A session bundle requires admitted records.");

        JsonNode profileNode = JsonNode.Parse(File.ReadAllText(profilePath))
            ?? throw new InvalidDataException("Collection profile is empty.");
        JsonObject profile = profileNode as JsonObject
            ?? throw new InvalidDataException("Collection profile must be an object.");
        ValidateProfile(profile, recordingManifest, records);
        string canonicalProfile = CanonicalJson(profile);
        string profileSha = Sha256Text(canonicalProfile);
        string profileId = RequiredText(profile, "profile_id");

        string parent = Path.GetDirectoryName(destination)
            ?? throw new InvalidDataException("Bundle destination has no parent.");
        Directory.CreateDirectory(parent);
        string temporary = Path.Combine(parent, $".{Path.GetFileName(destination)}.tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string rawDirectory = Path.Combine(temporary, "raw");
            string auditDirectory = Path.Combine(temporary, "audit");
            string exportDirectory = Path.Combine(temporary, "export");
            string profileDirectory = Path.Combine(temporary, "profile");
            Directory.CreateDirectory(rawDirectory);
            Directory.CreateDirectory(auditDirectory);
            Directory.CreateDirectory(exportDirectory);
            Directory.CreateDirectory(profileDirectory);

            string[] rawFiles = RequiredRawFiles(source);
            foreach (string rawFile in rawFiles)
                File.Copy(rawFile, Path.Combine(rawDirectory, Path.GetFileName(rawFile)));
            WriteUtf8(
                Path.Combine(profileDirectory, "collection-profile.json"),
                canonicalProfile + "\n");

            var auditDocument = new JsonObject
            {
                ["schema"] = HumanRecorderContract.SessionBundleAuditSchema,
                ["status"] = audit.Status,
                ["valid_records"] = audit.ValidRecords,
                ["invalid_records"] = audit.InvalidRecords,
                ["invalidations"] = audit.Invalidations,
                ["errors"] = JsonSerializer.SerializeToNode(audit.Errors, EvidenceJson.Options),
                ["non_claims"] = JsonSerializer.SerializeToNode(audit.NonClaims, EvidenceJson.Options)
            };
            WriteUtf8(
                Path.Combine(auditDirectory, "audit-report.json"),
                CanonicalJson(auditDocument) + "\n");
            string exportPath = Path.Combine(exportDirectory, "decisions.jsonl");
            RecordingAuditor.ExportAdmitted(source, exportPath);
            string exportSha = EvidenceIdentity.Sha256File(exportPath);

            var rawChecksums = new JsonObject();
            foreach (string file in Directory.GetFiles(rawDirectory).Order(StringComparer.Ordinal))
                rawChecksums[Path.GetFileName(file)] = EvidenceIdentity.Sha256File(file);
            string[] runIds = records.Select(record => record.RunId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var attestation = new JsonObject
            {
                ["attested"] = true,
                ["method"] = AttestationMethod,
                ["worker_id"] = workerId,
                ["machine_verifiable"] = false
            };
            var identity = new JsonObject
            {
                ["schema"] = HumanRecorderContract.SessionBundleSchema,
                ["session_id"] = recordingManifest.SessionId,
                ["collection_profile_id"] = profileId,
                ["collection_profile_sha256"] = profileSha,
                ["campaign_id"] = campaignId,
                ["worker_id"] = workerId,
                ["human_origin_attestation"] = attestation.DeepClone(),
                ["record_count"] = records.Count,
                ["run_ids"] = new JsonArray(runIds
                    .Select(runId => (JsonNode?)JsonValue.Create(runId))
                    .ToArray()),
                ["export_sha256"] = exportSha,
                ["raw_file_sha256"] = rawChecksums,
                ["audit"] = new JsonObject
                {
                    ["status"] = audit.Status,
                    ["valid_records"] = audit.ValidRecords,
                    ["invalid_records"] = audit.InvalidRecords,
                    ["invalidations"] = audit.Invalidations
                }
            };
            string contentId = Sha256Text(CanonicalJson(identity));
            var bundleManifest = new JsonObject
            {
                ["schema_version"] = 1,
                ["schema"] = HumanRecorderContract.SessionBundleSchema,
                ["bundle_content_id"] = contentId,
                ["session_id"] = recordingManifest.SessionId,
                ["collection_profile_id"] = profileId,
                ["collection_profile_sha256"] = profileSha,
                ["campaign_id"] = campaignId,
                ["worker_id"] = workerId,
                ["human_origin_attestation"] = attestation,
                ["created_at"] = recordingManifest.CreatedAt.ToUniversalTime().ToString("O"),
                ["packer"] = new JsonObject
                {
                    ["product"] = "STS2 Native UI Human Annotator Tool",
                    ["version"] = HumanRecorderContract.ProductVersion,
                    ["source_revision"] = packerSourceRevision
                },
                ["record_count"] = records.Count,
                ["run_ids"] = new JsonArray(runIds
                    .Select(runId => (JsonNode?)JsonValue.Create(runId))
                    .ToArray()),
                ["export_sha256"] = exportSha,
                ["audit_status"] = audit.Status,
                ["content_identity"] = identity
            };
            WriteUtf8(
                Path.Combine(temporary, "session-bundle-manifest.json"),
                CanonicalJson(bundleManifest) + "\n");
            WriteChecksums(temporary);

            if (Directory.Exists(destination))
            {
                if (!DirectoriesEqual(temporary, destination))
                    throw new IOException("An immutable bundle already exists with different bytes.");
                Directory.Delete(temporary, recursive: true);
                return Result(destination, bundleManifest);
            }
            Directory.Move(temporary, destination);
            return Result(destination, bundleManifest);
        }
        catch
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
            throw;
        }
    }

    private static SessionBundleResult Result(string directory, JsonObject manifest) => new(
        "pass",
        directory,
        RequiredText(manifest, "bundle_content_id"),
        RequiredText(manifest, "session_id"),
        manifest["record_count"]!.GetValue<int>(),
        RequiredText(manifest, "export_sha256"),
        EvidenceIdentity.Sha256File(Path.Combine(directory, "checksums.sha256")));

    private static void ValidateProfile(
        JsonObject profile,
        RecordingManifest manifest,
        IReadOnlyList<HumanDecisionRecord> records)
    {
        if (RequiredText(profile, "schema") != ProfileSchema)
            throw new InvalidDataException("Unsupported collection profile schema.");
        RequiredText(profile, "profile_id");
        if (RequiredText(profile, "platform") != manifest.Platform)
            throw new InvalidDataException("Collection profile platform drift.");
        if (RequiredText(profile, "record_schema") != HumanRecorderContract.RecordSchema)
            throw new InvalidDataException("Collection profile record schema drift.");
        string protocol = RequiredText(profile, "player_environment_protocol");
        JsonObject game = RequiredObject(profile, "game");
        JsonObject connector = RequiredObject(profile, "connector");
        JsonObject annotator = RequiredObject(profile, "annotator");
        JsonObject modset = RequiredObject(profile, "modset");
        HashSet<string> families = RequiredArray(profile, "allowed_action_families")
            .Select(node => node?.GetValue<string>()
                ?? throw new InvalidDataException("Collection profile family is not text."))
            .ToHashSet(StringComparer.Ordinal);
        if (families.Count == 0)
            throw new InvalidDataException("Collection profile has no allowed action families.");

        foreach (HumanDecisionRecord record in records)
        {
            if (record.SessionId != manifest.SessionId)
                throw new InvalidDataException("Decision session ID differs from recording manifest.");
            RequireEqual(game, "version", record.Environment.Game.Version);
            RequireEqual(game, "commit", record.Environment.Game.Commit);
            RequireEqual(game, "main_assembly_sha256", record.Environment.Game.MainAssemblySha256);
            RequireEqual(game, "main_assembly_mvid", record.Environment.Game.MainAssemblyModuleVersionId);
            RequireArtifact(connector, record.Environment.Connector, "Connector");
            RequireArtifact(annotator, record.Environment.Annotator, "Annotator");
            if (record.Environment.PlayerEnvironmentProtocol != protocol)
                throw new InvalidDataException("Player Environment protocol drift.");
            RequireEqual(modset, "status", record.Environment.ModsetStatus);
            RequireEqual(modset, "fingerprint", record.Environment.ModsetFingerprint);
            string family = record.DecisionFamily == "ordinary_combat" && record.Action.Verb == "play"
                ? "ordinary_combat.play_card"
                : $"{record.DecisionFamily}.{record.Action.Verb}";
            if (!families.Contains(family))
                throw new InvalidDataException($"Action family is outside the profile: {family}.");
        }
    }

    private static void RequireArtifact(
        JsonObject profile,
        ExactArtifactIdentity artifact,
        string name)
    {
        try
        {
            RequireEqual(profile, "source_revision", artifact.SourceRevision);
            RequireEqual(profile, "source_digest_sha256", artifact.SourceDigestSha256);
            RequireEqual(profile, "artifact_sha256", artifact.Sha256);
            RequireEqual(profile, "mvid", artifact.ModuleVersionId);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException($"{name} identity drift: {exception.Message}", exception);
        }
    }

    private static string[] RequiredRawFiles(string directory)
    {
        string[] fixedNames = { "recording-manifest.json", "invalidations.jsonl", "coverage.json" };
        var paths = new List<string>();
        foreach (string name in fixedNames)
        {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path))
                throw new InvalidDataException($"Required raw file is missing: {name}.");
            paths.Add(path);
        }
        paths.AddRange(Directory.GetFiles(directory, "run-*.jsonl").Order(StringComparer.Ordinal));
        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    private static void WriteChecksums(string directory)
    {
        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "checksums.sha256")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var lines = files.Select(path =>
            $"{EvidenceIdentity.Sha256File(path)}  {Path.GetRelativePath(directory, path).Replace('\\', '/')}");
        WriteUtf8(Path.Combine(directory, "checksums.sha256"), string.Join("\n", lines) + "\n");
    }

    private static bool DirectoriesEqual(string first, string second)
    {
        string[] firstFiles = Directory.GetFiles(first, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(first, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] secondFiles = Directory.GetFiles(second, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(second, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        return firstFiles.SequenceEqual(secondFiles, StringComparer.Ordinal)
               && firstFiles.All(relative =>
                   EvidenceIdentity.Sha256File(Path.Combine(first, relative))
                   == EvidenceIdentity.Sha256File(Path.Combine(second, relative)));
    }

    private static string CanonicalJson(JsonNode node) => node switch
    {
        JsonObject value => "{" + string.Join(",", value
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => JsonSerializer.Serialize(pair.Key) + ":" + CanonicalJson(pair.Value!))) + "}",
        JsonArray value => "[" + string.Join(",", value.Select(item => CanonicalJson(item!))) + "]",
        _ => node.ToJsonString(EvidenceJson.Options)
    };

    private static T ReadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), EvidenceJson.Options)
        ?? throw new InvalidDataException($"JSON file is empty: {path}.");

    private static JsonObject RequiredObject(JsonObject value, string key) =>
        value[key] as JsonObject
        ?? throw new InvalidDataException($"Collection profile is missing object: {key}.");

    private static JsonArray RequiredArray(JsonObject value, string key) =>
        value[key] as JsonArray
        ?? throw new InvalidDataException($"Collection profile is missing array: {key}.");

    private static string RequiredText(JsonObject value, string key) =>
        value[key]?.GetValue<string>() is { Length: > 0 } text
            ? text
            : throw new InvalidDataException($"Required text is missing: {key}.");

    private static void RequireEqual(JsonObject value, string key, string? expected)
    {
        if (!string.Equals(RequiredText(value, key), expected, StringComparison.Ordinal))
            throw new InvalidDataException($"Identity field drift: {key}.");
    }

    private static void ValidateIdentifier(string value, string name)
    {
        if (value.Length is < 3 or > 64
            || !value.All(character => char.IsLower(character)
                || char.IsDigit(character)
                || character is '-' or '_'))
            throw new InvalidDataException($"{name} must be a lowercase pseudonymous identifier.");
    }

    private static bool IsCommit(string value) =>
        value.Length == 40 && value.All(character => Uri.IsHexDigit(character));

    private static string Sha256Text(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void WriteUtf8(string path, string value) =>
        File.WriteAllText(path, value, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}
