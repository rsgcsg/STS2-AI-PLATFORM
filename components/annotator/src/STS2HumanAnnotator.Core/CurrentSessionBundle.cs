using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class SessionBundlePacker
{
    public static SessionBundleResult Pack(
        string recordingDirectory,
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
        if (packerSourceRevision.Length != 40 || !packerSourceRevision.All(Uri.IsHexDigit))
            throw new InvalidDataException("Packer source revision must be an exact Git SHA.");
        string source = Path.GetFullPath(recordingDirectory);
        string destination = Path.GetFullPath(outputDirectory);
        RecordingAuditResult audit = RecordingSessionAuditor.Audit(source);
        if (audit.Status != "pass")
            throw new InvalidDataException("Current recording audit must pass before packing.");
        CurrentRecordingManifest manifest = Read<CurrentRecordingManifest>(
            Path.Combine(source, "recording-manifest.json"));
        HumanCaptureProfile profile = Read<HumanCaptureProfile>(
            Path.Combine(source, "capture-profile.json"));
        IReadOnlyList<CurrentDecisionRecord> records = RecordingSessionAuditor.ReadAdmitted(source);
        if (records.Count == 0)
            throw new InvalidDataException("A current session bundle requires admitted records.");

        string parent = Path.GetDirectoryName(destination)
            ?? throw new InvalidDataException("Bundle destination has no parent.");
        Directory.CreateDirectory(parent);
        string temporary = Path.Combine(parent, $".{Path.GetFileName(destination)}.tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string raw = Path.Combine(temporary, "raw");
            string auditDirectory = Path.Combine(temporary, "audit");
            string exportDirectory = Path.Combine(temporary, "export");
            string profileDirectory = Path.Combine(temporary, "profile");
            CopyDirectory(source, raw);
            Directory.CreateDirectory(auditDirectory);
            Directory.CreateDirectory(exportDirectory);
            Directory.CreateDirectory(profileDirectory);
            File.Copy(
                Path.Combine(source, "capture-profile.json"),
                Path.Combine(profileDirectory, "capture-profile.json"));

            var auditDocument = new JsonObject
            {
                ["schema"] = CurrentRecordingContract.SessionBundleAuditSchema,
                ["status"] = audit.Status,
                ["valid_records"] = audit.ValidRecords,
                ["invalid_records"] = audit.InvalidRecords,
                ["invalidations"] = audit.Invalidations,
                ["errors"] = JsonSerializer.SerializeToNode(audit.Errors, EvidenceJson.Options),
                ["non_claims"] = JsonSerializer.SerializeToNode(audit.NonClaims, EvidenceJson.Options)
            };
            Write(Path.Combine(auditDirectory, "audit-report.json"),
                EvidenceCanonicalJson.Serialize(auditDocument) + "\n");
            string exportPath = Path.Combine(exportDirectory, "decisions.jsonl");
            RecordingSessionAuditor.ExportAdmitted(source, exportPath);
            string exportSha = EvidenceIdentity.Sha256File(exportPath);
            string[] runIds = records.Select(record => record.RunId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var attestation = new JsonObject
            {
                ["attested"] = true,
                ["method"] = "explicit_owner_pack",
                ["worker_id"] = workerId,
                ["machine_verifiable"] = false
            };
            JsonObject rawSha = RecursiveChecksums(raw);
            var identity = new JsonObject
            {
                ["schema"] = CurrentRecordingContract.SessionBundleSchema,
                ["session_id"] = manifest.SessionId,
                ["timeline_id"] = manifest.TimelineId,
                ["capture_profile_id"] = profile.ProfileId,
                ["capture_profile_sha256"] = EvidenceIdentity.Sha256Json(profile),
                ["campaign_id"] = campaignId,
                ["worker_id"] = workerId,
                ["human_origin_attestation"] = attestation.DeepClone(),
                ["record_count"] = records.Count,
                ["run_ids"] = new JsonArray(runIds
                    .Select(runId => (JsonNode?)JsonValue.Create(runId)).ToArray()),
                ["export_sha256"] = exportSha,
                ["raw_file_sha256"] = rawSha,
                ["audit"] = new JsonObject
                {
                    ["status"] = audit.Status,
                    ["valid_records"] = audit.ValidRecords,
                    ["invalid_records"] = audit.InvalidRecords,
                    ["invalidations"] = audit.Invalidations
                }
            };
            string contentId = EvidenceIdentity.Sha256Text(EvidenceCanonicalJson.Serialize(identity));
            var bundleManifest = new JsonObject
            {
                ["schema_version"] = CurrentRecordingContract.SchemaVersion,
                ["schema"] = CurrentRecordingContract.SessionBundleSchema,
                ["bundle_content_id"] = contentId,
                ["session_id"] = manifest.SessionId,
                ["timeline_id"] = manifest.TimelineId,
                ["capture_profile_id"] = profile.ProfileId,
                ["capture_profile_sha256"] = EvidenceIdentity.Sha256Json(profile),
                ["campaign_id"] = campaignId,
                ["worker_id"] = workerId,
                ["human_origin_attestation"] = attestation,
                ["created_at"] = manifest.CreatedAt.ToUniversalTime().ToString("O"),
                ["packer"] = new JsonObject
                {
                    ["product"] = "STS2 Native UI Human Annotator Tool",
                    ["version"] = CurrentRecordingContract.ProductVersion,
                    ["source_revision"] = packerSourceRevision
                },
                ["record_count"] = records.Count,
                ["run_ids"] = new JsonArray(runIds
                    .Select(runId => (JsonNode?)JsonValue.Create(runId)).ToArray()),
                ["export_sha256"] = exportSha,
                ["audit_status"] = audit.Status,
                ["content_identity"] = identity
            };
            Write(
                Path.Combine(temporary, "session-bundle-manifest.json"),
                EvidenceCanonicalJson.Serialize(bundleManifest) + "\n");
            WriteChecksums(temporary);
            if (Directory.Exists(destination))
            {
                if (!DirectoriesEqual(temporary, destination))
                    throw new IOException("An immutable current bundle already exists with different bytes.");
                Directory.Delete(temporary, true);
            }
            else
            {
                Directory.Move(temporary, destination);
            }
            return new SessionBundleResult(
                "pass",
                destination,
                contentId,
                manifest.SessionId,
                records.Count,
                exportSha,
                EvidenceIdentity.Sha256File(Path.Combine(destination, "checksums.sha256")));
        }
        catch
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, true);
            throw;
        }
    }

    private static JsonObject RecursiveChecksums(string directory)
    {
        var result = new JsonObject();
        foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            result[relative] = EvidenceIdentity.Sha256File(file);
        }
        return result;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)
                     .Prepend(source))
        {
            string relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(relative == "."
                ? destination
                : Path.Combine(destination, relative));
        }
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            // The retired native-action ledger is an archival reader input,
            // never part of a current bundle or its identity.
            if (string.Equals(
                    Path.GetFileName(file),
                    "native-action-ledger.jsonl",
                    StringComparison.Ordinal))
                continue;
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private static void WriteChecksums(string directory)
    {
        string content = string.Join("\n", Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "checksums.sha256")
            .Order(StringComparer.Ordinal)
            .Select(path => $"{EvidenceIdentity.Sha256File(path)}  {Path.GetRelativePath(directory, path).Replace('\\', '/') }")) + "\n";
        Write(Path.Combine(directory, "checksums.sha256"), content);
    }

    private static bool DirectoriesEqual(string first, string second)
    {
        string[] firstFiles = Directory.GetFiles(first, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(first, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal).ToArray();
        string[] secondFiles = Directory.GetFiles(second, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(second, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal).ToArray();
        return firstFiles.SequenceEqual(secondFiles, StringComparer.Ordinal)
               && firstFiles.All(relative => EvidenceIdentity.Sha256File(Path.Combine(first, relative))
                   == EvidenceIdentity.Sha256File(Path.Combine(second, relative)));
    }

    private static T Read<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), EvidenceJson.Options)
        ?? throw new InvalidDataException($"JSON file is empty: {path}");

    private static void ValidateIdentifier(string value, string name)
    {
        if (value.Length is < 3 or > 64
            || !value.All(character => char.IsLower(character) || char.IsDigit(character)
                || character is '-' or '_'))
            throw new InvalidDataException($"{name} must be a lowercase pseudonymous identifier.");
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }
}
