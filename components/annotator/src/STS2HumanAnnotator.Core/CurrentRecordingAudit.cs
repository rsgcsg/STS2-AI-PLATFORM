using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class RecordingSessionAuditor
{
    public static RecordingAuditResult Audit(string recordingDirectory)
    {
        string directory = Path.GetFullPath(recordingDirectory);
        var errors = new Dictionary<string, long>(StringComparer.Ordinal);
        long valid = 0;
        long invalid = 0;
        var recordIds = new HashSet<string>(StringComparer.Ordinal);
        long previousSequence = 0;
        CurrentRecordingManifest? manifest = ReadOrError<CurrentRecordingManifest>(
            Path.Combine(directory, "recording-manifest.json"), errors, "manifest_invalid");
        HumanCaptureProfile? profile = ReadOrError<HumanCaptureProfile>(
            Path.Combine(directory, "capture-profile.json"), errors, "capture_profile_invalid");
        if (manifest != null && profile != null
            && (manifest.Schema != CurrentRecordingContract.ManifestSchema
                || manifest.SchemaVersion != CurrentRecordingContract.SchemaVersion
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
                    CurrentDecisionRecord? record;
                    try
                    {
                        record = JsonSerializer.Deserialize<CurrentDecisionRecord>(line, EvidenceJson.Options);
                    }
                    catch (JsonException)
                    {
                        invalid++;
                        Add(errors, $"json_invalid_at_{Path.GetFileName(path)}_line_{lineNumber}");
                        continue;
                    }
                    RecordValidationResult result = CurrentDecisionRecordValidator.Validate(record);
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
        IReadOnlyList<SemanticBoundaryTraceEvent> semanticEvents = ValidateSemanticBoundaryTrace(
            directory,
            manifest,
            errors);
        ValidateNativeSemanticDiscriminator(directory, manifest, errors);
        ValidateCanonicalTransitions(directory, manifest, semanticEvents, errors);
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

    private static void ValidateCanonicalTransitions(
        string directory,
        CurrentRecordingManifest? manifest,
        IReadOnlyList<SemanticBoundaryTraceEvent> semanticEvents,
        IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "canonical-transitions.jsonl");
        // A current session may omit canonical rows until semantic proof is
        // persisted; archival streams are not promoted by this audit.
        if (!File.Exists(path))
            return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string line, _) in Lines(path))
        {
            CanonicalTransitionEvidence? value;
            try
            {
                value = JsonSerializer.Deserialize<CanonicalTransitionEvidence>(
                    line,
                    EvidenceJson.Options);
            }
            catch (JsonException)
            {
                Add(errors, "canonical_transition_json_invalid");
                continue;
            }
            if (value == null)
            {
                Add(errors, "canonical_transition_null");
                continue;
            }
            if (!CanonicalTransitionEvidenceContract.IsCurrent(
                    value.SchemaVersion,
                    value.Schema))
            {
                Add(errors, "canonical_transition_current_schema_required");
                continue;
            }
            foreach (string error in CanonicalTransitionEvidenceValidator.Validate(value))
                Add(errors, error);
            if (manifest == null
                || value.SessionId != manifest.SessionId
                || value.TimelineId != manifest.TimelineId)
                Add(errors, "canonical_transition_manifest_mismatch");
            if (!seen.Add(value.TransitionId))
                Add(errors, "canonical_transition_duplicate");
            string recordId = value.TransitionId.StartsWith("canonical-", StringComparison.Ordinal)
                ? value.TransitionId["canonical-".Length..]
                : string.Empty;
            CurrentDecisionFrame? pre = ReadSemanticFrameReference(
                directory,
                value.PreStateRef,
                errors);
            CurrentDecisionFrame? successor = ReadSemanticFrameReference(
                directory,
                value.SuccessorRef,
                errors);
            SemanticBoundaryTraceEvent[] proofs = semanticEvents.Where(candidate =>
                    candidate.Kind == SemanticBoundaryTraceKinds.TransitionProved
                    && candidate.Action.ActionWitnessId == value.ActionWitnessId)
                .ToArray();
            if (proofs.Length != 1)
            {
                Add(errors, "canonical_transition_semantic_proof_missing_or_duplicate");
                continue;
            }
            SemanticBoundaryTraceEvent proof = proofs[0];
            if (recordId != proof.Action.RecordId
                || value.RunId != proof.Action.RunId
                || value.ActionSequence != proof.Action.ActionSequence
                || proof.Action.BoundAction == null
                || EvidenceIdentity.Sha256Json(value.Action)
                    != EvidenceIdentity.Sha256Json(proof.Action.BoundAction))
                Add(errors, "canonical_transition_semantic_action_mismatch");
            if (pre == null || proof.SemanticPre == null
                || SemanticFrameDigest(pre) != SemanticFrameDigest(proof.SemanticPre))
                Add(errors, "canonical_transition_semantic_pre_mismatch");
            if (successor == null || proof.SemanticSuccessor == null
                || SemanticFrameDigest(successor)
                    != SemanticFrameDigest(proof.SemanticSuccessor))
                Add(errors, "canonical_transition_semantic_successor_mismatch");

            if (value.ActionSpaceAuthority == "native_semantic_execution")
            {
                ExecutionSemanticActionSpaceEvidence? actionSpace =
                    ReadExecutionSemanticActionSpaceReference(
                        directory,
                        value.ExecutionSemanticActionSpaceRef,
                        errors);
                if (actionSpace == null
                    || proof.ExecutionSemanticActionSpace == null
                    || EvidenceIdentity.Sha256Json(actionSpace)
                        != EvidenceIdentity.Sha256Json(proof.ExecutionSemanticActionSpace))
                    Add(errors, "canonical_transition_execution_semantic_evidence_mismatch");
                else
                {
                    foreach (string error in ExecutionSemanticActionSpaceValidator.Validate(
                                 actionSpace,
                                 proof.Action))
                        Add(errors, error);
                }
            }
            else if (value.ActionSpaceAuthority == "public_bound_actions"
                     && (pre == null || !PublicCatalogContainsExactlyOnce(pre, value.Action)))
            {
                Add(errors, "canonical_transition_public_action_space_invalid");
            }
        }
    }

    private static ExecutionSemanticActionSpaceEvidence?
        ReadExecutionSemanticActionSpaceReference(
            string directory,
            ExecutionSemanticActionSpaceReference? reference,
            IDictionary<string, long> errors,
            bool required = true)
    {
        if (reference == null)
        {
            if (required)
                Add(errors, "execution_semantic_action_space_ref_missing");
            return null;
        }
        string path;
        try
        {
            path = ResolveBelow(directory, reference.ObjectRef);
        }
        catch (InvalidDataException)
        {
            Add(errors, "execution_semantic_action_space_ref_invalid");
            return null;
        }
        if (!File.Exists(path) || EvidenceIdentity.Sha256File(path) != reference.ContentSha256)
        {
            Add(errors, "execution_semantic_action_space_missing_or_changed");
            return null;
        }
        try
        {
            ExecutionSemanticActionSpaceEvidence? value =
                JsonSerializer.Deserialize<ExecutionSemanticActionSpaceEvidence>(
                    File.ReadAllText(path),
                    EvidenceJson.Options);
            if (value == null)
            {
                Add(errors, "execution_semantic_action_space_identity_mismatch");
                return null;
            }
            if (!ExecutionSemanticActionSpaceContract.IsCurrent(
                    value.SchemaVersion,
                    value.Schema))
            {
                Add(errors, "execution_semantic_action_space_current_schema_required");
                return null;
            }
            if (value.ActionWitnessId != reference.ActionWitnessId
                || value.SemanticStateDigest != reference.SemanticStateDigest
                || value.SemanticCatalogDigest != reference.SemanticCatalogDigest)
            {
                Add(errors, "execution_semantic_action_space_identity_mismatch");
                return null;
            }
            return value;
        }
        catch (JsonException)
        {
            Add(errors, "execution_semantic_action_space_json_invalid");
            return null;
        }
    }

    private static bool PublicCatalogContainsExactlyOnce(
        CurrentDecisionFrame frame,
        RecordedBoundAction selected)
    {
        if (frame.Snapshot["completeness"]?["status"]?.GetValue<string>() != "complete"
            || frame.Snapshot["bound_actions"]?["status"]?.GetValue<string>() != "complete"
            || frame.Snapshot["bound_actions"]?["actions"] is not JsonArray actions
            || frame.CatalogCount != actions.Count)
            return false;
        return actions.Count(candidate =>
            candidate?["bound_action_id"]?.GetValue<string>() == selected.BoundActionId
            && candidate?["verb"]?.GetValue<string>() == selected.Verb
            && candidate?["subject_referent_id"]?.GetValue<string>()
                == selected.SubjectReferentId
            && PublicArgumentsMatch(candidate?["arguments"], selected.Arguments)) == 1;
    }

    private static bool PublicArgumentsMatch(
        JsonNode? node,
        IReadOnlyDictionary<string, string> expected)
    {
        if (node is not JsonArray values)
            return expected.Count == 0;
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonNode? value in values)
        {
            string? role = value?["role"]?.GetValue<string>();
            string? referent = value?["referent_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(role)
                || string.IsNullOrWhiteSpace(referent)
                || !actual.TryAdd(role, referent))
                return false;
        }
        return actual.Count == expected.Count
            && actual.All(pair => expected.TryGetValue(pair.Key, out string? referent)
                                  && referent == pair.Value);
    }

    private static CurrentDecisionFrame? ReadSemanticFrameReference(
        string directory,
        SemanticFrameReference reference,
        IDictionary<string, long> errors)
    {
        string path;
        try
        {
            path = ResolveBelow(directory, reference.ObjectRef);
        }
        catch (InvalidDataException)
        {
            Add(errors, "canonical_transition_frame_ref_invalid");
            return null;
        }
        if (!File.Exists(path) || EvidenceIdentity.Sha256File(path) != reference.ContentSha256)
        {
            Add(errors, "canonical_transition_frame_missing_or_changed");
            return null;
        }
        try
        {
            CurrentDecisionFrame? frame = JsonSerializer.Deserialize<CurrentDecisionFrame>(
                File.ReadAllText(path),
                EvidenceJson.Options);
            if (frame == null || frame.SnapshotId != reference.SnapshotId)
            {
                Add(errors, "canonical_transition_frame_identity_mismatch");
                return null;
            }
            return frame;
        }
        catch (JsonException)
        {
            Add(errors, "canonical_transition_frame_json_invalid");
            return null;
        }
    }

    private static string SemanticFrameDigest(CurrentDecisionFrame frame)
    {
        JsonNode node = JsonSerializer.SerializeToNode(frame, EvidenceJson.Options)
            ?? throw new InvalidDataException("Semantic frame serialization returned null.");
        return EvidenceIdentity.Sha256Text(EvidenceCanonicalJson.Serialize(node));
    }

    public static IReadOnlyList<CurrentDecisionRecord> ReadAdmitted(string recordingDirectory)
    {
        RecordingAuditResult audit = Audit(recordingDirectory);
        if (audit.Status != "pass")
            throw new InvalidDataException("Current recording audit must pass before records are read.");
        return DecisionPaths(Path.GetFullPath(recordingDirectory))
            .SelectMany(path => Lines(path).Select(item =>
                JsonSerializer.Deserialize<CurrentDecisionRecord>(item.Line, EvidenceJson.Options)
                ?? throw new InvalidDataException("Current decision record is null.")))
            .ToArray();
    }

    public static long ExportAdmitted(string recordingDirectory, string outputPath)
    {
        IReadOnlyList<CurrentDecisionRecord> records = ReadAdmitted(recordingDirectory);
        string destination = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + $".tmp-{Guid.NewGuid():N}";
        using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
        {
            writer.NewLine = "\n";
            foreach (CurrentDecisionRecord record in records)
                writer.WriteLine(JsonSerializer.Serialize(record, EvidenceJson.Options));
        }
        File.Move(temporary, destination, true);
        return records.Count;
    }

    private static void ValidateJournal(
        string directory,
        CurrentRecordingManifest? manifest,
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
                || value.Schema != CurrentRecordingContract.RunJournalSchema
                || value.SchemaVersion != CurrentRecordingContract.SchemaVersion
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

    private static IReadOnlyList<SemanticBoundaryTraceEvent> ValidateSemanticBoundaryTrace(
        string directory,
        CurrentRecordingManifest? manifest,
        IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "semantic-boundary-trace.jsonl");
        // The semantic trace is current evidence. A pre-trace recording is
        // archival input and is not promoted by this current audit.
        if (!File.Exists(path))
            return Array.Empty<SemanticBoundaryTraceEvent>();

        var events = new List<SemanticBoundaryTraceEvent>();
        var semanticFrames = new Dictionary<string, CurrentDecisionFrame>(StringComparer.Ordinal);
        foreach ((string line, _) in Lines(path))
        {
            string? schema;
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                schema = document.RootElement.TryGetProperty("schema", out JsonElement property)
                    ? property.GetString()
                    : null;
            }
            catch (JsonException)
            {
                Add(errors, "semantic_boundary_trace_json_invalid");
                continue;
            }
            if (schema == SemanticEvidenceContract.EventSchema)
            {
                SemanticBoundaryTraceEvent? materialized = MaterializeSemanticEvidenceEvent(
                    directory,
                    manifest,
                    line,
                    semanticFrames,
                    errors);
                if (materialized != null)
                    events.Add(materialized);
                continue;
            }
            if (schema != SemanticBoundaryTraceContract.EventSchema)
            {
                Add(errors, "semantic_boundary_trace_current_schema_required");
                continue;
            }
            SemanticBoundaryTraceEvent? value;
            try
            {
                value = JsonSerializer.Deserialize<SemanticBoundaryTraceEvent>(
                    line,
                    EvidenceJson.Options);
            }
            catch (JsonException)
            {
                Add(errors, "semantic_boundary_trace_json_invalid");
                continue;
            }
            if (value == null
                || manifest == null
                || value.SessionId != manifest.SessionId
                || value.TimelineId != manifest.TimelineId)
            {
                Add(errors, "semantic_boundary_trace_session_mismatch");
                continue;
            }
            events.Add(value);
        }
        foreach (string error in SemanticBoundaryTraceValidator.Validate(events))
            Add(errors, error);
        return events;
    }

    private static SemanticBoundaryTraceEvent? MaterializeSemanticEvidenceEvent(
        string directory,
        CurrentRecordingManifest? manifest,
        string line,
        IDictionary<string, CurrentDecisionFrame> semanticFrames,
        IDictionary<string, long> errors)
    {
        SemanticEvidenceEvent? value;
        try
        {
            value = JsonSerializer.Deserialize<SemanticEvidenceEvent>(line, EvidenceJson.Options);
        }
        catch (JsonException)
        {
            Add(errors, "semantic_evidence_event_json_invalid");
            return null;
        }
        if (value == null
            || !SemanticEvidenceContract.IsCurrent(value.SchemaVersion, value.Schema)
            || manifest == null
            || value.SessionId != manifest.SessionId
            || value.TimelineId != manifest.TimelineId)
        {
            Add(errors, "semantic_evidence_event_session_mismatch");
            return null;
        }

        CurrentDecisionFrame? humanObservation = ResolveSemanticFrame(
            directory,
            value.HumanObservationRef,
            semanticFrames,
            errors);
        CurrentDecisionFrame? executionPre = ResolveSemanticFrame(
            directory,
            value.ExecutionPreRef,
            semanticFrames,
            errors);
        CurrentDecisionFrame? successor = ResolveSemanticFrame(
            directory,
            value.SuccessorRef,
            semanticFrames,
            errors);
        SemanticBoundaryObservation? boundary = null;
        if (value.Boundary != null)
        {
            CurrentDecisionFrame? state = ResolveSemanticFrame(
                directory,
                value.Boundary.StateRef,
                semanticFrames,
                errors);
            boundary = SemanticBoundaryObservationCodec.Materialize(value.Boundary, state);
        }
        ExecutionSemanticActionSpaceEvidence? executionSemanticActionSpace =
            ReadExecutionSemanticActionSpaceReference(
                directory,
                value.ExecutionSemanticActionSpaceRef,
                errors,
                required: false);

        return new SemanticBoundaryTraceEvent(
            SemanticBoundaryTraceContract.SchemaVersion,
            SemanticBoundaryTraceContract.EventSchema,
            value.EventId,
            value.SessionId,
            value.TimelineId,
            value.RunId,
            value.Sequence,
            value.ObservedAt,
            value.Kind,
            value.Action,
            value.ProofStatus,
            value.RelatedActionWitnessId,
            boundary,
            executionPre,
            successor,
            value.Detail,
            value.NonClaims)
        {
            HumanObservation = humanObservation,
            NativeCompletion = value.NativeCompletion,
            ExecutionSemanticActionSpace = executionSemanticActionSpace
        };
    }

    private static CurrentDecisionFrame? ResolveSemanticFrame(
        string directory,
        SemanticFrameReference? reference,
        IDictionary<string, CurrentDecisionFrame> semanticFrames,
        IDictionary<string, long> errors)
    {
        if (reference == null)
            return null;
        string cacheKey = $"{reference.ContentSha256}\n{reference.ObjectRef}";
        if (semanticFrames.TryGetValue(cacheKey, out CurrentDecisionFrame? cached))
        {
            if (cached.SnapshotId != reference.SnapshotId)
            {
                Add(errors, "semantic_frame_identity_mismatch");
                return null;
            }
            return cached;
        }
        try
        {
            string path = ResolveBelow(directory, reference.ObjectRef);
            if (!File.Exists(path)
                || EvidenceIdentity.Sha256File(path) != reference.ContentSha256)
            {
                Add(errors, "semantic_frame_missing_or_changed");
                return null;
            }
            CurrentDecisionFrame? frame = JsonSerializer.Deserialize<CurrentDecisionFrame>(
                File.ReadAllText(path),
                EvidenceJson.Options);
            if (frame == null || frame.SnapshotId != reference.SnapshotId)
            {
                Add(errors, "semantic_frame_identity_mismatch");
                return null;
            }
            semanticFrames.Add(cacheKey, frame);
            return frame;
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or InvalidDataException)
        {
            Add(errors, "semantic_frame_invalid");
            return null;
        }
    }

    private static void
        ValidateNativeSemanticDiscriminator(
            string directory,
            CurrentRecordingManifest? manifest,
            IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "native-semantic-discriminator.jsonl");
        // The stream is diagnostic-only. It can report integrity failures, but
        // per-action coverage/membership never becomes current causal authority.
        if (!File.Exists(path))
            return;

        var events = new List<NativeSemanticDiscriminatorEvent>();
        foreach ((string line, _) in Lines(path))
        {
            NativeSemanticDiscriminatorEvent? value;
            try
            {
                value = JsonSerializer.Deserialize<NativeSemanticDiscriminatorEvent>(
                    line,
                    EvidenceJson.Options);
            }
            catch (JsonException)
            {
                Add(errors, "native_semantic_discriminator_json_invalid");
                continue;
            }
            if (value == null)
            {
                Add(errors, "native_semantic_discriminator_null");
                continue;
            }
            if (manifest == null
                || value.SessionId != manifest.SessionId
                || value.TimelineId != manifest.TimelineId)
                Add(errors, "native_semantic_discriminator_manifest_mismatch");
            events.Add(value);
        }
        if (events.Count == 0)
            return;

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);
        foreach (string _ in report.Errors.Where(error =>
                     !NativeSemanticDiscriminatorAnalyzer.IsDiagnosticOnlyError(error)))
            Add(errors, "native_semantic_discriminator_analysis_failed");
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
