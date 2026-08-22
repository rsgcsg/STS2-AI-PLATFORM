using System.Text.Json;

namespace STS2HumanAnnotator.Core;

public sealed record RecordingAuditResult(
    string Status,
    string RecordingDirectory,
    long ValidRecords,
    long InvalidRecords,
    long Invalidations,
    IReadOnlyDictionary<string, long> Errors,
    IReadOnlyList<string> NonClaims);

public static class RecordingAuditor
{
    public static RecordingAuditResult Audit(string recordingDirectory)
    {
        string directory = Path.GetFullPath(recordingDirectory);
        var errors = new Dictionary<string, long>(StringComparer.Ordinal);
        long valid = 0;
        long invalid = 0;
        long invalidations = 0;
        var recordIds = new HashSet<string>(StringComparer.Ordinal);
        long previousSequence = 0;
        string[] decisionPaths = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "run-*.jsonl").Order(StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        if (!File.Exists(Path.Combine(directory, "recording-manifest.json")))
            Add(errors, "manifest_missing");
        if (decisionPaths.Length == 0)
        {
            Add(errors, "decision_file_missing");
        }
        else
        {
            foreach (string decisionsPath in decisionPaths)
            {
                foreach ((string line, int lineNumber) in Lines(decisionsPath))
                {
                    HumanDecisionRecord? record;
                    try
                    {
                        record = JsonSerializer.Deserialize<HumanDecisionRecord>(line, EvidenceJson.Options);
                    }
                    catch (JsonException)
                    {
                        invalid++;
                        Add(errors, $"json_invalid_at_{Path.GetFileName(decisionsPath)}_line_{lineNumber}");
                        continue;
                    }
                    RecordValidationResult result = HumanDecisionRecordValidator.Validate(record);
                    if (!result.Valid)
                    {
                        invalid++;
                        foreach (string error in result.Errors)
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
                    valid++;
                }
            }
        }

        string invalidationsPath = Path.Combine(directory, "invalidations.jsonl");
        if (File.Exists(invalidationsPath))
            invalidations = Lines(invalidationsPath).LongCount();
        string status = errors.Count == 0 && invalid == 0 ? "pass" : "fail";
        return new RecordingAuditResult(
            status,
            directory,
            valid,
            invalid,
            invalidations,
            errors,
            new[]
            {
                "audit_does_not_prove_human_origin",
                "audit_does_not_prove_non_interference",
                "audit_does_not_qualify_unseen_families"
            });
    }

    public static long ExportAdmitted(string recordingDirectory, string outputPath)
    {
        RecordingAuditResult audit = Audit(recordingDirectory);
        if (!string.Equals(audit.Status, "pass", StringComparison.Ordinal))
            throw new InvalidDataException("Recording audit must pass before export.");
        string directory = Path.GetFullPath(recordingDirectory);
        string[] inputPaths = Directory.GetFiles(directory, "run-*.jsonl")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string output = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string temporary = output + $".tmp-{Guid.NewGuid():N}";
        using (var writer = new StreamWriter(temporary, append: false))
        {
            foreach (string inputPath in inputPaths)
            {
                foreach ((string line, _) in Lines(inputPath))
                    writer.WriteLine(line);
            }
        }
        File.Move(temporary, output, overwrite: true);
        return audit.ValidRecords;
    }

    public static IReadOnlyList<HumanDecisionRecord> ReadAdmitted(string recordingDirectory)
    {
        RecordingAuditResult audit = Audit(recordingDirectory);
        if (!string.Equals(audit.Status, "pass", StringComparison.Ordinal))
            throw new InvalidDataException("Recording audit must pass before records are read.");
        string directory = Path.GetFullPath(recordingDirectory);
        var records = new List<HumanDecisionRecord>();
        foreach (string path in Directory.GetFiles(directory, "run-*.jsonl")
                     .Order(StringComparer.Ordinal))
        {
            foreach ((string line, _) in Lines(path))
            {
                HumanDecisionRecord? record = JsonSerializer.Deserialize<HumanDecisionRecord>(
                    line,
                    EvidenceJson.Options);
                records.Add(record ?? throw new InvalidDataException("Decision record is null."));
            }
        }
        return records;
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
