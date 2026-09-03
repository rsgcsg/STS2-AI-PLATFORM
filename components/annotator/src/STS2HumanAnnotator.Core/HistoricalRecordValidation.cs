using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public sealed record RecordValidationResult(bool Valid, IReadOnlyList<string> Errors);

public static class HistoricalDecisionRecordValidator
{
    public static RecordValidationResult Validate(HistoricalDecisionRecord? record)
    {
        var errors = new List<string>();
        if (record == null)
            return new RecordValidationResult(false, new[] { "record_missing" });
        if (record.SchemaVersion != HistoricalRecordingContract.SchemaVersion
            || !string.Equals(record.Schema, HistoricalRecordingContract.RecordSchema, StringComparison.Ordinal))
            errors.Add("schema_mismatch");
        if (string.IsNullOrWhiteSpace(record.RecordId)
            || string.IsNullOrWhiteSpace(record.SessionId)
            || string.IsNullOrWhiteSpace(record.RunId)
            || record.Sequence <= 0)
            errors.Add("record_identity_incomplete");
        if (!IsSha256(record.Environment.Game.MainAssemblySha256)
            || !Guid.TryParse(record.Environment.Game.MainAssemblyModuleVersionId, out _))
            errors.Add("game_identity_incomplete");
        if (!IsExactArtifact(record.Environment.Connector)
            || !IsExactArtifact(record.Environment.Annotator))
            errors.Add("artifact_identity_incomplete");
        if (string.IsNullOrWhiteSpace(record.Environment.RuntimeInstanceId)
            || string.IsNullOrWhiteSpace(record.Environment.EnvironmentFingerprint)
            || string.IsNullOrWhiteSpace(record.Environment.ModsetFingerprint))
            errors.Add("runtime_identity_incomplete");
        if (!RecordingEnvironmentAdmission.IsExactModset(record.Environment.ModsetStatus))
            errors.Add("modset_not_exact_recording_envelope");
        if (record.Pre.Snapshot == null
            || string.IsNullOrWhiteSpace(record.Pre.SnapshotId)
            || record.Pre.CatalogCount <= 0
            || !IsSha256(record.Pre.CatalogDigest))
            errors.Add("pre_frame_not_complete");
        else
            ValidatePreFrame(record, errors);
        if (!string.Equals(record.Mapping.Status, "exact_unique", StringComparison.Ordinal)
            || record.Mapping.MatchCount != 1
            || !string.Equals(record.Mapping.Basis, "reference_equality_to_frozen_host_binding", StringComparison.Ordinal))
            errors.Add("mapping_not_exact_unique");
        if (string.IsNullOrWhiteSpace(record.Action.BoundActionId)
            || string.IsNullOrWhiteSpace(record.Action.Verb))
            errors.Add("action_incomplete");
        if (record.Successor.Snapshot == null
            || string.IsNullOrWhiteSpace(record.Successor.SnapshotId)
            || string.Equals(record.Pre.SnapshotId, record.Successor.SnapshotId, StringComparison.Ordinal)
            || !string.Equals(record.Successor.Status, "interactive", StringComparison.Ordinal))
            errors.Add("stable_successor_missing");
        else
            ValidateSuccessor(record, errors);
        if (string.IsNullOrWhiteSpace(record.NativeWitness.Origin)
            || string.IsNullOrWhiteSpace(record.NativeWitness.NativeActionType))
            errors.Add("native_witness_incomplete");
        if (!string.Equals(record.Eligibility.Status, "admitted", StringComparison.Ordinal))
            errors.Add("record_not_admitted");
        return new RecordValidationResult(errors.Count == 0, errors);
    }

    private static bool IsExactArtifact(ExactArtifactIdentity artifact) =>
        !string.IsNullOrWhiteSpace(artifact.Product)
        && !string.IsNullOrWhiteSpace(artifact.Version)
        && IsSha256(artifact.SourceDigestSha256)
        && IsSha256(artifact.Sha256)
        && Guid.TryParse(artifact.ModuleVersionId, out _)
        && artifact.SourceRevision.Length == 40
        && artifact.SourceRevision.All(Uri.IsHexDigit);

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static void ValidatePreFrame(
        HistoricalDecisionRecord record,
        ICollection<string> errors)
    {
        if (record.Pre.Snapshot is not JsonObject snapshot
            || ReadString(snapshot, "snapshot_id") != record.Pre.SnapshotId
            || ReadString(snapshot, "status") != "interactive"
            || snapshot["interaction"] is not JsonObject interaction
            || ReadString(interaction, "interaction_id") != record.Pre.InteractionId
            || ReadString(interaction, "kind") != record.Pre.InteractionKind
            || ReadString(interaction, "content_schema") != record.Pre.SurfaceSchema
            || snapshot["completeness"] is not JsonObject completeness
            || ReadString(completeness, "status") != "complete"
            || snapshot["bound_actions"] is not JsonObject catalog
            || ReadString(catalog, "status") != "complete"
            || catalog["actions"] is not JsonArray actions
            || actions.Count != record.Pre.CatalogCount
            || !string.Equals(
                EvidenceIdentity.Sha256Json(catalog),
                record.Pre.CatalogDigest,
                StringComparison.Ordinal))
        {
            errors.Add("pre_frame_evidence_mismatch");
            return;
        }

        JsonObject[] matches = actions
            .OfType<JsonObject>()
            .Where(action => ReadString(action, "bound_action_id") == record.Action.BoundActionId)
            .ToArray();
        if (matches.Length != 1 || !SameAction(matches[0], record.Action))
            errors.Add("chosen_action_not_exactly_once_in_catalog");
        if (snapshot["session"] is not JsonObject session
            || ReadString(session, "runtime_instance_id") != record.Environment.RuntimeInstanceId
            || ReadString(session, "environment_fingerprint") != record.Environment.EnvironmentFingerprint)
            errors.Add("pre_runtime_identity_mismatch");
    }

    private static void ValidateSuccessor(
        HistoricalDecisionRecord record,
        ICollection<string> errors)
    {
        if (record.Successor.Snapshot is not JsonObject snapshot
            || ReadString(snapshot, "snapshot_id") != record.Successor.SnapshotId
            || ReadString(snapshot, "status") != record.Successor.Status
            || snapshot["interaction"] is not JsonObject interaction
            || ReadString(interaction, "interaction_id") != record.Successor.InteractionId
            || ReadString(interaction, "kind") != record.Successor.InteractionKind
            || snapshot["bound_actions"] is not JsonObject catalog
            || ReadString(catalog, "status") != "complete")
        {
            errors.Add("successor_evidence_mismatch");
            return;
        }

        if (snapshot["session"] is not JsonObject session
            || ReadString(session, "runtime_instance_id") != record.Environment.RuntimeInstanceId
            || ReadString(session, "environment_fingerprint") != record.Environment.EnvironmentFingerprint)
            errors.Add("successor_runtime_identity_mismatch");
    }

    private static bool SameAction(JsonObject value, RecordedBoundAction action)
    {
        if (ReadString(value, "verb") != action.Verb
            || ReadNullableString(value, "subject_referent_id") != action.SubjectReferentId
            || value["arguments"] is not JsonArray arguments)
            return false;
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonObject argument in arguments.OfType<JsonObject>())
        {
            string? role = ReadString(argument, "role");
            string? referentId = ReadString(argument, "referent_id");
            if (role == null || referentId == null || !actual.TryAdd(role, referentId))
                return false;
        }
        return actual.Count == arguments.Count
               && actual.Count == action.Arguments.Count
               && actual.All(pair => action.Arguments.TryGetValue(pair.Key, out string? value)
                                     && value == pair.Value);
    }

    private static string? ReadString(JsonObject value, string key)
    {
        try
        {
            return value[key]?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? ReadNullableString(JsonObject value, string key) =>
        value[key] == null ? null : ReadString(value, key);
}
