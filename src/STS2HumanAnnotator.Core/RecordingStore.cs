using System.Text;
using System.Text.Json;

namespace STS2HumanAnnotator.Core;

public sealed class RecordingStore : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FileStream> _decisionFiles = new(StringComparer.Ordinal);
    private readonly FileStream _invalidations;
    private readonly Dictionary<string, long> _families = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _invalidationsByReason = new(StringComparer.Ordinal);
    private long _admittedCount;
    private long _invalidationCount;

    private RecordingStore(string directory, RecordingManifest manifest)
    {
        DirectoryPath = directory;
        Manifest = manifest;
        Directory.CreateDirectory(directory);
        WriteCreateNew(
            Path.Combine(directory, "recording-manifest.json"),
            JsonSerializer.Serialize(manifest, EvidenceJson.IndentedOptions));
        _invalidations = OpenAppend(Path.Combine(directory, "invalidations.jsonl"));
        WriteCoverage();
    }

    public string DirectoryPath { get; }

    public RecordingManifest Manifest { get; }

    public static RecordingStore Create(string root, RecordingManifest manifest)
    {
        string safeSession = manifest.SessionId.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            ? manifest.SessionId
            : throw new ArgumentException("Session ID contains unsafe path characters.", nameof(manifest));
        return new RecordingStore(Path.Combine(Path.GetFullPath(root), safeSession), manifest);
    }

    public void AppendDecision(HumanDecisionRecord record)
    {
        RecordValidationResult validation = HumanDecisionRecordValidator.Validate(record);
        if (!validation.Valid)
            throw new InvalidDataException($"Decision record failed validation: {string.Join(",", validation.Errors)}");
        lock (_gate)
        {
            AppendLine(DecisionFile(record.RunId), record);
            _admittedCount++;
            _families[record.DecisionFamily] = _families.GetValueOrDefault(record.DecisionFamily) + 1;
            WriteCoverage();
        }
    }

    public void AppendInvalidation(InvalidationRecord invalidation)
    {
        lock (_gate)
        {
            AppendLine(_invalidations, invalidation);
            _invalidationCount++;
            _invalidationsByReason[invalidation.ReasonCode] =
                _invalidationsByReason.GetValueOrDefault(invalidation.ReasonCode) + 1;
            WriteCoverage();
        }
    }

    public void WriteRuntimeStatus(string path, RecorderRuntimeStatus status)
    {
        WriteAtomic(path, JsonSerializer.Serialize(status, EvidenceJson.IndentedOptions));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (FileStream stream in _decisionFiles.Values)
                stream.Dispose();
            _invalidations.Dispose();
        }
    }

    private void WriteCoverage()
    {
        var coverage = new CoverageSummary(
            HumanRecorderContract.SchemaVersion,
            Manifest.SessionId,
            _admittedCount,
            _invalidationCount,
            new Dictionary<string, long>(_families, StringComparer.Ordinal),
            new Dictionary<string, long>(_invalidationsByReason, StringComparer.Ordinal),
            DateTimeOffset.UtcNow);
        WriteAtomic(
            Path.Combine(DirectoryPath, "coverage.json"),
            JsonSerializer.Serialize(coverage, EvidenceJson.IndentedOptions));
    }

    private static FileStream OpenAppend(string path) => new(
        path,
        FileMode.Append,
        FileAccess.Write,
        FileShare.Read,
        4096,
        FileOptions.WriteThrough);

    private FileStream DecisionFile(string runId)
    {
        if (!runId.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'))
            throw new InvalidDataException("Run ID contains unsafe path characters.");
        if (!_decisionFiles.TryGetValue(runId, out FileStream? stream))
        {
            stream = OpenAppend(Path.Combine(DirectoryPath, $"{runId}.jsonl"));
            _decisionFiles.Add(runId, stream);
        }
        return stream;
    }

    private static void AppendLine<T>(FileStream stream, T value)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, EvidenceJson.Options);
        stream.Write(json);
        stream.WriteByte((byte)'\n');
        stream.Flush(flushToDisk: true);
    }

    private static void WriteCreateNew(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        byte[] bytes = Encoding.UTF8.GetBytes(content + "\n");
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + $".tmp-{Guid.NewGuid():N}";
        File.WriteAllText(temporary, content + "\n", Encoding.UTF8);
        File.Move(temporary, path, overwrite: true);
    }
}
