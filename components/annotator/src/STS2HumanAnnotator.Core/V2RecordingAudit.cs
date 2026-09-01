using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class V2RecordingAuditor
{
    public static RecordingAuditResult Audit(string recordingDirectory)
    {
        string directory = Path.GetFullPath(recordingDirectory);
        var errors = new Dictionary<string, long>(StringComparer.Ordinal);
        long valid = 0;
        long invalid = 0;
        var recordsById = new Dictionary<string, HumanDecisionRecordV2>(StringComparer.Ordinal);
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
                    if (!recordsById.TryAdd(record!.RecordId, record))
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
        IReadOnlyList<NativeActionLedgerEvent> nativeEvents = ValidateNativeActionLedger(
            directory,
            manifest,
            recordsById,
            errors);
        IReadOnlyList<SemanticBoundaryTraceEvent> semanticEvents = ValidateSemanticBoundaryTrace(
            directory,
            manifest,
            errors);
        IReadOnlyList<NativeSemanticDiscriminatorEvent> discriminatorEvents =
            ValidateNativeSemanticDiscriminator(directory, manifest, errors);
        ValidateSchemaTwoSemanticAccounting(
            nativeEvents,
            semanticEvents,
            discriminatorEvents,
            errors);
        ValidateCanonicalTransitions(
            directory,
            manifest,
            recordsById,
            semanticEvents,
            errors);
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
        RecordingManifestV2? manifest,
        IReadOnlyDictionary<string, HumanDecisionRecordV2> admittedRecords,
        IReadOnlyList<SemanticBoundaryTraceEvent> semanticEvents,
        IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "canonical-transitions.jsonl");
        // Pre-serialized recordings remain readable without this additive stream.
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
            bool legacy = value.SchemaVersion
                == CanonicalTransitionEvidenceContract.LegacySchemaVersion;
            if (legacy)
            {
                if (!admittedRecords.TryGetValue(recordId, out HumanDecisionRecordV2? record)
                    || record.Sequence != value.ActionSequence
                    || EvidenceIdentity.Sha256Json(record.Action)
                        != EvidenceIdentity.Sha256Json(value.Action))
                {
                    Add(errors, "canonical_transition_decision_mismatch");
                    continue;
                }
            }
            FrozenDecisionFrameV2? pre = ReadSemanticFrameReference(
                directory,
                value.PreStateRef,
                errors);
            FrozenDecisionFrameV2? successor = ReadSemanticFrameReference(
                directory,
                value.SuccessorRef,
                errors);
            if (legacy)
            {
                HumanDecisionRecordV2 record = admittedRecords[recordId];
                if (pre != null && SemanticFrameDigest(pre) != SemanticFrameDigest(record.Pre))
                    Add(errors, "canonical_transition_pre_mismatch");
                if (successor == null)
                    continue;
                if (successor.SnapshotId != record.Successor.SnapshotId
                    || successor.InteractionId != record.Successor.InteractionId
                    || successor.InteractionKind != record.Successor.InteractionKind)
                    Add(errors, "canonical_transition_successor_identity_mismatch");
                if (!JsonNode.DeepEquals(successor.Snapshot, record.Successor.Snapshot))
                    Add(errors, "canonical_transition_successor_snapshot_mismatch");
                JsonNode? successorReads = JsonSerializer.SerializeToNode(
                    successor.Reads,
                    EvidenceJson.Options);
                JsonNode? recordedReads = JsonSerializer.SerializeToNode(
                    record.Successor.Reads,
                    EvidenceJson.Options);
                if (!JsonNode.DeepEquals(successorReads, recordedReads))
                    Add(errors, "canonical_transition_successor_reads_mismatch");
                continue;
            }

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
            if (value == null
                || value.ActionWitnessId != reference.ActionWitnessId
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
        FrozenDecisionFrameV2 frame,
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

    private static FrozenDecisionFrameV2? ReadSemanticFrameReference(
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
            FrozenDecisionFrameV2? frame = JsonSerializer.Deserialize<FrozenDecisionFrameV2>(
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

    private static string SemanticFrameDigest(FrozenDecisionFrameV2 frame)
    {
        JsonNode node = JsonSerializer.SerializeToNode(frame, EvidenceJson.Options)
            ?? throw new InvalidDataException("Semantic frame serialization returned null.");
        return EvidenceIdentity.Sha256Text(EvidenceCanonicalJson.Serialize(node));
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

    private static IReadOnlyList<NativeActionLedgerEvent> ValidateNativeActionLedger(
        string directory,
        RecordingManifestV2? manifest,
        IReadOnlyDictionary<string, HumanDecisionRecordV2> admittedRecords,
        IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "native-action-ledger.jsonl");
        // V2 recordings sealed before the additive ledger remain readable.
        if (!File.Exists(path))
            return Array.Empty<NativeActionLedgerEvent>();

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
            bool admitted = admittedRecords.ContainsKey(disposition.RecordId);
            if (disposition.Kind == NativeActionLifecycleKinds.StrictTransitionAdmitted && !admitted)
                Add(errors, "native_action_admission_record_missing");
            if (disposition.Kind == NativeActionLifecycleKinds.StrictTransitionInvalidated && admitted)
                Add(errors, "native_action_invalidated_record_admitted");
        }
        foreach (NativeActionLedgerEvent accepted in events.Where(value =>
                     value.Kind == NativeActionLifecycleKinds.Accepted
                     && value.SchemaVersion == NativeActionLedgerContract.SchemaVersion
                     && admittedRecords.ContainsKey(value.RecordId)))
        {
            HumanDecisionRecordV2 record = admittedRecords[accepted.RecordId];
            if (accepted.DecisionPre == null
                || accepted.NativeWitness == null
                || accepted.Mapping == null
                || accepted.BoundAction == null
                || EvidenceIdentity.Sha256Json(accepted.DecisionPre) != EvidenceIdentity.Sha256Json(record.Pre)
                || EvidenceIdentity.Sha256Json(accepted.NativeWitness) != EvidenceIdentity.Sha256Json(record.NativeWitness)
                || EvidenceIdentity.Sha256Json(accepted.Mapping) != EvidenceIdentity.Sha256Json(record.Mapping)
                || EvidenceIdentity.Sha256Json(accepted.BoundAction) != EvidenceIdentity.Sha256Json(record.Action))
                Add(errors, "native_action_decision_record_mismatch");
        }
        return events;
    }

    private static IReadOnlyList<SemanticBoundaryTraceEvent> ValidateSemanticBoundaryTrace(
        string directory,
        RecordingManifestV2? manifest,
        IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "semantic-boundary-trace.jsonl");
        // The observation-only trace is additive; predecessor V2 sessions do
        // not gain or lose validity based on its absence.
        if (!File.Exists(path))
            return Array.Empty<SemanticBoundaryTraceEvent>();

        var events = new List<SemanticBoundaryTraceEvent>();
        var semanticFrames = new Dictionary<string, FrozenDecisionFrameV2>(StringComparer.Ordinal);
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
            if (schema is SemanticEvidenceContract.EventSchema
                or SemanticEvidenceContract.LegacyEventSchema)
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
        RecordingManifestV2? manifest,
        string line,
        IDictionary<string, FrozenDecisionFrameV2> semanticFrames,
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
            || !SemanticEvidenceContract.IsSupported(value.SchemaVersion, value.Schema)
            || manifest == null
            || value.SessionId != manifest.SessionId
            || value.TimelineId != manifest.TimelineId)
        {
            Add(errors, "semantic_evidence_event_session_mismatch");
            return null;
        }

        FrozenDecisionFrameV2? humanObservation = ResolveSemanticFrame(
            directory,
            value.HumanObservationRef,
            semanticFrames,
            errors);
        FrozenDecisionFrameV2? executionPre = ResolveSemanticFrame(
            directory,
            value.ExecutionPreRef,
            semanticFrames,
            errors);
        FrozenDecisionFrameV2? successor = ResolveSemanticFrame(
            directory,
            value.SuccessorRef,
            semanticFrames,
            errors);
        SemanticBoundaryObservation? boundary = null;
        if (value.Boundary != null)
        {
            FrozenDecisionFrameV2? state = ResolveSemanticFrame(
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

    private static FrozenDecisionFrameV2? ResolveSemanticFrame(
        string directory,
        SemanticFrameReference? reference,
        IDictionary<string, FrozenDecisionFrameV2> semanticFrames,
        IDictionary<string, long> errors)
    {
        if (reference == null)
            return null;
        string cacheKey = $"{reference.ContentSha256}\n{reference.ObjectRef}";
        if (semanticFrames.TryGetValue(cacheKey, out FrozenDecisionFrameV2? cached))
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
            FrozenDecisionFrameV2? frame = JsonSerializer.Deserialize<FrozenDecisionFrameV2>(
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

    private static void ValidateSchemaTwoSemanticAccounting(
        IReadOnlyList<NativeActionLedgerEvent> nativeEvents,
        IReadOnlyList<SemanticBoundaryTraceEvent> semanticEvents,
        IReadOnlyList<NativeSemanticDiscriminatorEvent> discriminatorEvents,
        IDictionary<string, long> errors)
    {
        // Current evidence can account a native root through either the
        // semantic timeline or the execution-bound discriminator. Older
        // sessions with neither stream retain their original meaning.
        bool hasSchemaTwoTrace = semanticEvents.Any(value =>
            value.SchemaVersion == SemanticBoundaryTraceContract.SchemaVersion
            && value.Schema == SemanticBoundaryTraceContract.EventSchema);
        if (!hasSchemaTwoTrace && discriminatorEvents.Count == 0)
            return;

        HashSet<string> semanticAcceptedRecordIds = semanticEvents
            .Where(value =>
                value.SchemaVersion == SemanticBoundaryTraceContract.SchemaVersion
                && value.Kind == SemanticBoundaryTraceKinds.ActionAccepted)
            .Select(value => value.Action.RecordId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> semanticAcceptedActionIds = semanticEvents
            .Where(value =>
                value.SchemaVersion == SemanticBoundaryTraceContract.SchemaVersion
                && value.Kind == SemanticBoundaryTraceKinds.ActionAccepted)
            .Select(value => value.Action.ActionWitnessId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> discriminatorAcceptedActionIds = discriminatorEvents
            .Where(value => value.Phase == "accepted")
            .Select(value => value.ActionWitnessId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> nativeAcceptedActionIds = nativeEvents
            .Where(value =>
                value.SchemaVersion == NativeActionLedgerContract.SchemaVersion
                && value.Kind == NativeActionLifecycleKinds.Accepted)
            .Select(value => value.ActionWitnessId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (NativeActionLedgerEvent accepted in nativeEvents.Where(value =>
                     value.SchemaVersion == NativeActionLedgerContract.SchemaVersion
                     && value.Kind == NativeActionLifecycleKinds.Accepted))
        {
            if (!semanticAcceptedRecordIds.Contains(accepted.RecordId)
                && !discriminatorAcceptedActionIds.Contains(accepted.ActionWitnessId))
                Add(errors, "semantic_trace_missing_accepted_native_action");
        }
        foreach (string discriminatorActionId in discriminatorAcceptedActionIds)
        {
            if (!nativeAcceptedActionIds.Contains(discriminatorActionId)
                && !semanticAcceptedActionIds.Contains(discriminatorActionId))
                Add(errors, "native_semantic_discriminator_accepted_without_canonical_accounting");
        }
    }

    private static IReadOnlyList<NativeSemanticDiscriminatorEvent>
        ValidateNativeSemanticDiscriminator(
            string directory,
            RecordingManifestV2? manifest,
            IDictionary<string, long> errors)
    {
        string path = Path.Combine(directory, "native-semantic-discriminator.jsonl");
        // The stream is additive. Historical recordings remain valid without it.
        if (!File.Exists(path))
            return Array.Empty<NativeSemanticDiscriminatorEvent>();

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
            return events;

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);
        foreach (string _ in report.Errors)
            Add(errors, "native_semantic_discriminator_analysis_failed");
        return events;
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
