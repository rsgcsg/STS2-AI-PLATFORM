using System.Text;
using System.Text.Json;

namespace STS2HumanAnnotator.Core;

public static class V2RecordingAuditor
{
    public static RecordingAuditResult Audit(string recordingDirectory)
    {
        string directory = Path.GetFullPath(recordingDirectory);
        var errors = new Dictionary<string, long>(StringComparer.Ordinal);
        long valid = 0;
        long invalid = 0;
        var recordIds = new HashSet<string>(StringComparer.Ordinal);
        long previousSequence = 0;
        RecordingManifestV2? manifest = ReadOrError<RecordingManifestV2>(
            Path.Combine(directory, "recording-manifest.json"), errors, "manifest_invalid");
        HumanCaptureProfile? profile = ReadOrError<HumanCaptureProfile>(
            Path.Combine(directory, "capture-profile.json"), errors, "capture_profile_invalid");
        if (manifest != null && profile != null
            && (manifest.Schema != HumanRecorderV2Contract.ManifestSchema
                || manifest.SchemaVersion != HumanRecorderV2Contract.SchemaVersion
                || manifest.CaptureProfileId != profile.ProfileId
                || manifest.CaptureProfileSha256 != EvidenceIdentity.Sha256Json(profile)))
            Add(errors, "manifest_capture_profile_mismatch");
        if (profile != null)
        {
            RecordValidationResult result = HumanCaptureProfileValidator.Validate(profile);
            foreach (string error in result.Errors)
                Add(errors, error);
        }

        string[] decisionPaths = Directory.Exists(directory)
            ? DecisionPaths(directory)
            : Array.Empty<string>();
        if (decisionPaths.Length == 0)
        {
            Add(errors, "decision_file_missing");
        }
        else
        {
            foreach (string path in decisionPaths)
            {
                foreach ((string line, int lineNumber) in Lines(path))
                {
                    HumanDecisionRecordV2? record;
                    try
                    {
                        record = JsonSerializer.Deserialize<HumanDecisionRecordV2>(line, EvidenceJson.Options);
                    }
                    catch (JsonException)
                    {
                        invalid++;
                        Add(errors, $"json_invalid_at_{Path.GetFileName(path)}_line_{lineNumber}");
                        continue;
                    }
                    RecordValidationResult result = HumanDecisionRecordV2Validator.Validate(record);
                    RecordValidationResult profileResult = profile == null || record == null
                        ? new RecordValidationResult(false, new[] { "capture_profile_missing" })
                        : HumanCaptureProfileValidator.ValidateRecord(profile, record);
                    if (!result.Valid || !profileResult.Valid)
                    {
                        invalid++;
                        foreach (string error in result.Errors.Concat(profileResult.Errors))
                            Add(errors, error);
                        continue;
                    }
                    if (!recordIds.Add(record!.RecordId))
                    {
                        invalid++;
                        Add(errors, "duplicate_record_id");
                        continue;
                    }
                    if (record.Sequence <= previousSequence)
                    {
                        invalid++;
                        Add(errors, "sequence_not_strictly_increasing");
                        continue;
                    }
                    previousSequence = record.Sequence;
                    foreach (ReadEvidence read in record.Pre.Reads.Concat(record.Successor.Reads))
                    {
                        if (read.Status != "materialized")
                            continue;
                        string blob = ResolveBelow(directory, read.PayloadRef!);
                        if (!File.Exists(blob) || EvidenceIdentity.Sha256File(blob) != read.PayloadSha256)
                            Add(errors, "read_blob_missing_or_changed");
                    }
                    valid++;
                }
            }
        }

        ValidateJournal(directory, manifest, errors);
        ValidateNativeActionLedger(directory, manifest, recordIds, errors);
        long invalidations = File.Exists(Path.Combine(directory, "invalidations.jsonl"))
            ? Lines(Path.Combine(directory, "invalidations.jsonl")).LongCount()
            : 0;
        return new RecordingAuditResult(
            errors.Count == 0 && invalid == 0 ? "pass" : "fail",
            directory,
            valid,
            invalid,
            invalidations,
            errors,
            new[]
            {
                "audit_does_not_prove_human_origin",
                "audit_does_not_prove_non_interference",
                "audit_does_not_qualify_unseen_families",
                "read_capture_is_player_visible_evidence_not_hidden_state"
            });
    }

    public static IReadOnlyList<HumanDecisionRecordV2> ReadAdmitted(string recordingDirectory)
    {
        RecordingAuditResult audit = Audit(recordingDirectory);
        if (audit.Status != "pass")
            throw new InvalidDataException("V2 recording audit must pass before records are read.");
        return DecisionPaths(Path.GetFullPath(recordingDirectory))
            .SelectMany(path => Lines(path).Select(item =>
                JsonSerializer.Deserialize<HumanDecisionRecordV2>(item.Line, EvidenceJson.Options)
                ?? throw new InvalidDataException("V2 decision record is null.")))
            .ToArray();
    }

    public static long ExportAdmitted(string recordingDirectory, string outputPath)
    {
        IReadOnlyList<HumanDecisionRecordV2> records = ReadAdmitted(recordingDirectory);
        string destination = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + $".tmp-{Guid.NewGuid():N}";
        using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
        {
            writer.NewLine = "\n";
            foreach (HumanDecisionRecordV2 record in records)
                writer.WriteLine(JsonSerializer.Serialize(record, EvidenceJson.Options));
        }
        File.Move(temporary, destination, true);
        return records.Count;
    }

    private static void ValidateJournal(
        string directory,
        RecordingManifestV2? manifest,
        IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "run-journal.jsonl");
        if (!File.Exists(path))
        {
            Add(errors, "run_journal_missing");
            return;
        }
        long previous = 0;
        foreach ((string line, _) in Lines(path))
        {
            RunJournalEvent? value;
            try
            {
                value = JsonSerializer.Deserialize<RunJournalEvent>(line, EvidenceJson.Options);
            }
            catch (JsonException)
            {
                Add(errors, "run_journal_json_invalid");
                continue;
            }
            if (value == null
                || value.Schema != HumanRecorderV2Contract.RunJournalSchema
                || value.SchemaVersion != HumanRecorderV2Contract.SchemaVersion
                || value.Sequence <= previous
                || manifest == null
                || value.SessionId != manifest.SessionId
                || value.TimelineId != manifest.TimelineId)
            {
                Add(errors, "run_journal_invalid");
                continue;
            }
            previous = value.Sequence;
        }
        if (previous == 0)
            Add(errors, "run_journal_empty");
    }

    private static void ValidateNativeActionLedger(
        string directory,
        RecordingManifestV2? manifest,
        IReadOnlySet<string> admittedRecordIds,
        IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "native-action-ledger.jsonl");
        // V2 recordings sealed before the additive ledger remain readable.
        if (!File.Exists(path))
            return;

        var events = new List<NativeActionLedgerEvent>();
        foreach ((string line, _) in Lines(path))
        {
            NativeActionLedgerEvent? value;
            try
            {
                value = JsonSerializer.Deserialize<NativeActionLedgerEvent>(line, EvidenceJson.Options);
            }
            catch (JsonException)
            {
                Add(errors, "native_action_ledger_json_invalid");
                continue;
            }
            if (value == null
                || manifest == null
                || value.SessionId != manifest.SessionId
                || value.TimelineId != manifest.TimelineId)
            {
                Add(errors, "native_action_ledger_session_mismatch");
                continue;
            }
            events.Add(value);
        }
        foreach (string error in NativeActionLedgerValidator.Validate(events))
            Add(errors, error);
        foreach (NativeActionLedgerEvent disposition in events.Where(value =>
                     value.Kind is NativeActionLifecycleKinds.StrictTransitionAdmitted
                         or NativeActionLifecycleKinds.StrictTransitionInvalidated))
        {
            bool admitted = admittedRecordIds.Contains(disposition.RecordId);
            if (disposition.Kind == NativeActionLifecycleKinds.StrictTransitionAdmitted && !admitted)
                Add(errors, "native_action_admission_record_missing");
            if (disposition.Kind == NativeActionLifecycleKinds.StrictTransitionInvalidated && admitted)
                Add(errors, "native_action_invalidated_record_admitted");
        }
    }

    private static T? ReadOrError<T>(string path, IDictionary<string, long> errors, string error)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), EvidenceJson.Options);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            Add(errors, error);
            return default;
        }
    }

    private static string[] DecisionPaths(string directory) =>
        Directory.GetFiles(directory, "run-*.jsonl")
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                "run-journal.jsonl",
                StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string ResolveBelow(string rootDirectory, string relative)
    {
        string root = Path.GetFullPath(rootDirectory) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(rootDirectory, relative));
        if (!path.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidDataException("Read payload path escaped the recording directory.");
        return path;
    }

    private static IEnumerable<(string Line, int Number)> Lines(string path)
    {
        int number = 0;
        foreach (string line in File.ReadLines(path))
        {
            number++;
            if (!string.IsNullOrWhiteSpace(line))
                yield return (line, number);
        }
    }

    private static void Add(IDictionary<string, long> errors, string key)
    {
        errors.TryGetValue(key, out long count);
        errors[key] = count + 1;
    }
}
