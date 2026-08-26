using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public sealed class V2RecordingStore : IDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private readonly object _gate = new();
    private readonly Dictionary<string, FileStream> _decisionFiles = new(StringComparer.Ordinal);
    private readonly FileStream _invalidations;
    private readonly FileStream _journal;
    private readonly FileStream _nativeActionLedger;
    private readonly FileStream _semanticBoundaryTrace;
    private readonly Dictionary<string, long> _families = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _readsByKind = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _invalidationsByReason = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _recordedActionFamilies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _invalidatedNativeActions = new(StringComparer.Ordinal);
    private long _admittedCount;
    private long _invalidationCount;
    private long _readMaterialized;
    private long _readFailed;
    private RecordingItemStatus? _lastRecord;
    private RecordingItemStatus? _lastInvalidation;
    private string _appendHealth = "healthy";
    private string _diskHealth = "healthy";
    private string? _lastError;
    private bool _closed;

    private V2RecordingStore(
        string directory,
        RecordingManifestV2 manifest,
        HumanCaptureProfile captureProfile)
    {
        DirectoryPath = directory;
        Manifest = manifest;
        CaptureProfile = captureProfile;
        Directory.CreateDirectory(directory);
        WriteCreateNew(
            Path.Combine(directory, "recording-manifest.json"),
            JsonSerializer.Serialize(manifest, EvidenceJson.IndentedOptions));
        WriteCreateNew(
            Path.Combine(directory, "capture-profile.json"),
            JsonSerializer.Serialize(captureProfile, EvidenceJson.IndentedOptions));
        _invalidations = OpenAppend(Path.Combine(directory, "invalidations.jsonl"));
        _journal = OpenAppend(Path.Combine(directory, "run-journal.jsonl"));
        _nativeActionLedger = OpenAppend(Path.Combine(directory, "native-action-ledger.jsonl"));
        _semanticBoundaryTrace = OpenAppend(Path.Combine(directory, "semantic-boundary-trace.jsonl"));
        WriteCoverage();
    }

    public string DirectoryPath { get; }
    public RecordingManifestV2 Manifest { get; }
    public HumanCaptureProfile CaptureProfile { get; }

    public RecordingStoreSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new RecordingStoreSnapshot(
                new RecordingCounters(
                    _admittedCount,
                    _invalidationCount,
                    _readMaterialized,
                    _readFailed),
                _lastRecord,
                _lastInvalidation,
                new Dictionary<string, long>(_recordedActionFamilies, StringComparer.Ordinal),
                new Dictionary<string, long>(_invalidatedNativeActions, StringComparer.Ordinal),
                new Dictionary<string, long>(_invalidationsByReason, StringComparer.Ordinal),
                _appendHealth,
                _diskHealth,
                _lastError,
                _closed);
        }
    }

    public static V2RecordingStore Create(
        string root,
        RecordingManifestV2 manifest,
        HumanCaptureProfile captureProfile)
    {
        RecordValidationResult profileValidation = HumanCaptureProfileValidator.Validate(captureProfile);
        if (!profileValidation.Valid)
            throw new InvalidDataException(
                $"Capture profile failed validation: {string.Join(',', profileValidation.Errors)}");
        if (manifest.SchemaVersion != HumanRecorderV2Contract.SchemaVersion
            || manifest.Schema != HumanRecorderV2Contract.ManifestSchema
            || manifest.CaptureProfileId != captureProfile.ProfileId
            || manifest.CaptureProfileSha256 != EvidenceIdentity.Sha256Json(captureProfile))
            throw new InvalidDataException("V2 recording manifest does not bind the capture profile.");
        return new V2RecordingStore(
            Path.Combine(Path.GetFullPath(root), SafeId(manifest.SessionId, nameof(manifest.SessionId))),
            manifest,
            captureProfile);
    }

    public ReadEvidence PersistRead(CapturedReadPayload capture)
    {
        EnsureOpen();
        try
        {
            return PersistReadCore(capture);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MarkWriteFailure(exception);
            throw;
        }
    }

    private ReadEvidence PersistReadCore(CapturedReadPayload capture)
    {
        string evidenceId = $"read-{Guid.NewGuid():N}";
        if (capture.Status != "materialized")
        {
            RecordRead(capture.Kind, materialized: false);
            return new ReadEvidence(
                HumanRecorderV2Contract.SchemaVersion,
                HumanRecorderV2Contract.ReadEvidenceSchema,
                evidenceId,
                capture.ReadId,
                capture.Kind,
                capture.SnapshotId,
                capture.RuntimeInstanceId,
                capture.EnvironmentFingerprint,
                capture.Status,
                capture.ContentSchema,
                capture.Completeness?.DeepClone(),
                null,
                null,
                capture.CapturedAt,
                capture.ErrorCode ?? "read_not_materialized",
                capture.Detail);
        }
        if (capture.Content == null || capture.Completeness == null
            || string.IsNullOrWhiteSpace(capture.ContentSchema))
            throw new InvalidDataException("A materialized Read requires content, schema and completeness.");
        byte[] payload = Encoding.UTF8.GetBytes(EvidenceCanonicalJson.Serialize(capture.Content) + "\n");
        string digest = EvidenceIdentity.Sha256Bytes(payload);
        string relative = $"blobs/sha256/{digest[..2]}/{digest}.json";
        string destination = ResolveRelative(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            if (EvidenceIdentity.Sha256File(destination) != digest)
                throw new IOException("Content-addressed Read blob collision.");
        }
        else
        {
            string temporary = destination + $".tmp-{Guid.NewGuid():N}";
            File.WriteAllBytes(temporary, payload);
            File.Move(temporary, destination);
        }
        RecordRead(capture.Kind, materialized: true);
        return new ReadEvidence(
            HumanRecorderV2Contract.SchemaVersion,
            HumanRecorderV2Contract.ReadEvidenceSchema,
            evidenceId,
            capture.ReadId,
            capture.Kind,
            capture.SnapshotId,
            capture.RuntimeInstanceId,
            capture.EnvironmentFingerprint,
            "materialized",
            capture.ContentSchema,
            capture.Completeness.DeepClone(),
            relative,
            digest,
            capture.CapturedAt,
            null,
            capture.Detail);
    }

    public void AppendDecision(HumanDecisionRecordV2 record)
    {
        EnsureOpen();
        RecordValidationResult validation = HumanDecisionRecordV2Validator.Validate(record);
        RecordValidationResult profileValidation =
            HumanCaptureProfileValidator.ValidateRecord(CaptureProfile, record);
        if (!validation.Valid || !profileValidation.Valid)
            throw new InvalidDataException(
                $"V2 decision record failed validation: {string.Join(',', validation.Errors.Concat(profileValidation.Errors))}");
        VerifyReadBlobs(record.Pre.Reads.Concat(record.Successor.Reads));
        ExecuteWrite(() =>
        {
            AppendLine(DecisionFile(record.RunId), record);
            _admittedCount++;
            _families[record.DecisionFamily] = _families.GetValueOrDefault(record.DecisionFamily) + 1;
            string actionFamily = HumanCaptureProfileValidator.ResolveActionFamily(
                record.DecisionFamily,
                record.Action.Verb);
            _recordedActionFamilies[actionFamily] =
                _recordedActionFamilies.GetValueOrDefault(actionFamily) + 1;
            _lastRecord = new RecordingItemStatus(
                record.RecordId,
                record.Action.Verb,
                record.RecordedAt,
                record.DecisionFamily);
            WriteCoverage();
        });
    }

    public void AppendRunEvent(RunJournalEvent value)
    {
        if (value.SchemaVersion != HumanRecorderV2Contract.SchemaVersion
            || value.Schema != HumanRecorderV2Contract.RunJournalSchema
            || value.SessionId != Manifest.SessionId
            || value.TimelineId != Manifest.TimelineId
            || value.Sequence <= 0
            || string.IsNullOrWhiteSpace(value.Kind))
            throw new InvalidDataException("Run journal event is invalid for this recording.");
        EnsureOpen();
        ExecuteWrite(() => AppendLine(_journal, value));
    }

    public void AppendNativeActionEvent(NativeActionLedgerEvent value)
    {
        if (!NativeActionLedgerContract.IsSupported(value.SchemaVersion, value.Schema)
            || value.SessionId != Manifest.SessionId
            || value.TimelineId != Manifest.TimelineId
            || value.Sequence <= 0
            || value.ActionSequence <= 0
            || string.IsNullOrWhiteSpace(value.EventId)
            || string.IsNullOrWhiteSpace(value.ActionWitnessId)
            || string.IsNullOrWhiteSpace(value.RecordId)
            || string.IsNullOrWhiteSpace(value.Kind)
            || string.IsNullOrWhiteSpace(value.NativeActionType))
            throw new InvalidDataException("Native action ledger event is invalid for this recording.");
        EnsureOpen();
        ExecuteWrite(() => AppendLine(_nativeActionLedger, value));
    }

    public void AppendSemanticBoundaryEvent(SemanticBoundaryTraceEvent value)
    {
        if (value.SchemaVersion != SemanticBoundaryTraceContract.SchemaVersion
            || value.Schema != SemanticBoundaryTraceContract.EventSchema
            || value.SessionId != Manifest.SessionId
            || value.TimelineId != Manifest.TimelineId
            || value.Sequence <= 0
            || string.IsNullOrWhiteSpace(value.EventId)
            || string.IsNullOrWhiteSpace(value.Kind)
            || string.IsNullOrWhiteSpace(value.Action.ActionWitnessId))
            throw new InvalidDataException("Semantic boundary trace event is invalid for this recording.");
        EnsureOpen();
        ExecuteWrite(() => AppendLine(_semanticBoundaryTrace, value));
    }

    public void AppendInvalidation(InvalidationRecord invalidation)
    {
        EnsureOpen();
        ExecuteWrite(() =>
        {
            AppendLine(_invalidations, invalidation);
            _invalidationCount++;
            _invalidationsByReason[invalidation.ReasonCode] =
                _invalidationsByReason.GetValueOrDefault(invalidation.ReasonCode) + 1;
            if (!string.IsNullOrWhiteSpace(invalidation.NativeActionType))
            {
                _invalidatedNativeActions[invalidation.NativeActionType] =
                    _invalidatedNativeActions.GetValueOrDefault(invalidation.NativeActionType) + 1;
            }
            _lastInvalidation = new RecordingItemStatus(
                invalidation.InvalidationId,
                invalidation.ReasonCode,
                invalidation.RecordedAt,
                invalidation.Detail);
            WriteCoverage();
        });
    }

    public static void WriteRuntimeStatus(string path, RecorderRuntimeStatus status) =>
        WriteAtomic(path, JsonSerializer.Serialize(status, EvidenceJson.IndentedOptions));

    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed)
                return;
            foreach (FileStream stream in _decisionFiles.Values)
                stream.Dispose();
            _invalidations.Dispose();
            _journal.Dispose();
            _nativeActionLedger.Dispose();
            _semanticBoundaryTrace.Dispose();
            _closed = true;
            _appendHealth = "closed";
        }
    }

    private void RecordRead(string kind, bool materialized)
    {
        ExecuteWrite(() =>
        {
            if (materialized)
                _readMaterialized++;
            else
                _readFailed++;
            _readsByKind[kind] = _readsByKind.GetValueOrDefault(kind) + 1;
            WriteCoverage();
        });
    }

    private void EnsureOpen()
    {
        lock (_gate)
        {
            if (_closed)
                throw new ObjectDisposedException(nameof(V2RecordingStore));
        }
    }

    private void ExecuteWrite(Action operation)
    {
        lock (_gate)
        {
            if (_closed)
                throw new ObjectDisposedException(nameof(V2RecordingStore));
            try
            {
                operation();
                _appendHealth = "healthy";
                _diskHealth = "healthy";
                _lastError = null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                MarkWriteFailureUnsafe(exception);
                throw;
            }
        }
    }

    private void MarkWriteFailure(Exception exception)
    {
        lock (_gate)
            MarkWriteFailureUnsafe(exception);
    }

    private void MarkWriteFailureUnsafe(Exception exception)
    {
        _appendHealth = "failed";
        _diskHealth = "failed";
        _lastError = exception.Message;
    }

    private void VerifyReadBlobs(IEnumerable<ReadEvidence> reads)
    {
        foreach (ReadEvidence read in reads.Where(read => read.Status == "materialized"))
        {
            string path = ResolveRelative(read.PayloadRef!);
            if (!File.Exists(path) || EvidenceIdentity.Sha256File(path) != read.PayloadSha256)
                throw new InvalidDataException($"Read blob is absent or changed: {read.ReadEvidenceId}");
        }
    }

    private string ResolveRelative(string relative)
    {
        string root = Path.GetFullPath(DirectoryPath) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(DirectoryPath, relative));
        if (!path.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidDataException("Evidence path escaped the recording directory.");
        return path;
    }

    private void WriteCoverage()
    {
        var coverage = new CoverageSummaryV2(
            HumanRecorderV2Contract.SchemaVersion,
            HumanRecorderV2Contract.CoverageSchema,
            Manifest.SessionId,
            _admittedCount,
            _invalidationCount,
            _readMaterialized,
            _readFailed,
            new Dictionary<string, long>(_families, StringComparer.Ordinal),
            new Dictionary<string, long>(_readsByKind, StringComparer.Ordinal),
            new Dictionary<string, long>(_invalidationsByReason, StringComparer.Ordinal),
            DateTimeOffset.UtcNow);
        WriteAtomic(
            Path.Combine(DirectoryPath, "coverage.json"),
            JsonSerializer.Serialize(coverage, EvidenceJson.IndentedOptions));
    }

    private FileStream DecisionFile(string runId)
    {
        string safe = SafeId(runId, nameof(runId));
        if (!_decisionFiles.TryGetValue(safe, out FileStream? stream))
        {
            stream = OpenAppend(Path.Combine(DirectoryPath, $"{safe}.jsonl"));
            _decisionFiles.Add(safe, stream);
        }
        return stream;
    }

    private static string SafeId(string value, string name) =>
        value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? value
            : throw new InvalidDataException($"{name} contains unsafe path characters.");

    private static FileStream OpenAppend(string path) => new(
        path,
        FileMode.Append,
        FileAccess.Write,
        FileShare.Read,
        4096,
        FileOptions.WriteThrough);

    private static void AppendLine<T>(FileStream stream, T value)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, EvidenceJson.Options);
        stream.Write(json);
        stream.WriteByte((byte)'\n');
        stream.Flush(true);
    }

    private static void WriteCreateNew(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        byte[] bytes = Utf8NoBom.GetBytes(content + "\n");
        stream.Write(bytes);
        stream.Flush(true);
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + $".tmp-{Guid.NewGuid():N}";
        File.WriteAllText(temporary, content + "\n", Utf8NoBom);
        File.Move(temporary, path, true);
    }
}

internal static class EvidenceCanonicalJson
{
    internal static string Serialize(JsonNode node) => node switch
    {
        JsonObject value => "{" + string.Join(",", value
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => JsonSerializer.Serialize(pair.Key) + ":" + Serialize(pair.Value!))) + "}",
        JsonArray value => "[" + string.Join(",", value.Select(item => Serialize(item!))) + "]",
        _ => node.ToJsonString(EvidenceJson.Options)
    };
}
