using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class HumanCaptureProfileValidator
{
    private static readonly HashSet<string> Phases = new(StringComparer.Ordinal)
    {
        "pre",
        "successor"
    };

    public static RecordValidationResult Validate(HumanCaptureProfile? profile)
    {
        var errors = new List<string>();
        if (profile == null)
            return new RecordValidationResult(false, new[] { "capture_profile_missing" });
        if (profile.SchemaVersion != HumanRecorderV2Contract.SchemaVersion
            || profile.Schema != HumanRecorderV2Contract.CaptureProfileSchema)
            errors.Add("capture_profile_schema_mismatch");
        if (string.IsNullOrWhiteSpace(profile.ProfileId)
            || profile.RecordSchema != HumanRecorderV2Contract.RecordSchema)
            errors.Add("capture_profile_identity_incomplete");
        if (profile.SupportedActionFamilies.Count == 0
            || profile.SupportedActionFamilies.Any(string.IsNullOrWhiteSpace)
            || profile.SupportedActionFamilies.Count
            != profile.SupportedActionFamilies.Distinct(StringComparer.Ordinal).Count())
            errors.Add("capture_profile_action_families_invalid");
        if (profile.Reads.Count == 0
            || profile.Reads.Any(read => !Phases.Contains(read.Phase)
                || string.IsNullOrWhiteSpace(read.Kind))
            || profile.Reads.Count != profile.Reads
                .Select(read => $"{read.Phase}\0{read.Kind}")
                .Distinct(StringComparer.Ordinal)
                .Count())
            errors.Add("capture_profile_reads_invalid");
        return new RecordValidationResult(errors.Count == 0, errors);
    }

    public static RecordValidationResult ValidateRecord(
        HumanCaptureProfile profile,
        HumanDecisionRecordV2 record)
    {
        var errors = new List<string>();
        if (!string.Equals(record.CaptureProfileId, profile.ProfileId, StringComparison.Ordinal))
            errors.Add("record_capture_profile_mismatch");
        string family = ResolveActionFamily(record.DecisionFamily, record.Action.Verb);
        if (!profile.SupportedActionFamilies.Contains(family, StringComparer.Ordinal))
            errors.Add("record_action_family_outside_profile");
        ValidateRequiredReads(profile, record.Pre.Reads, "pre", errors);
        ValidateRequiredReads(profile, record.Successor.Reads, "successor", errors);
        return new RecordValidationResult(errors.Count == 0, errors);
    }

    public static string ResolveActionFamily(string decisionFamily, string verb) =>
        decisionFamily == "ordinary_combat" && verb == "play"
            ? "ordinary_combat.play_card"
            : decisionFamily == "ordinary_combat" && verb == "use"
                ? "ordinary_combat.use_potion"
            : $"{decisionFamily}.{verb}";

    private static void ValidateRequiredReads(
        HumanCaptureProfile profile,
        IReadOnlyList<ReadEvidence> reads,
        string phase,
        ICollection<string> errors)
    {
        HashSet<string> materialized = reads
            .Where(read => string.Equals(read.Status, "materialized", StringComparison.Ordinal))
            .Select(read => read.Kind)
            .ToHashSet(StringComparer.Ordinal);
        foreach (CaptureReadRequirement requirement in profile.Reads.Where(read =>
                     read.Required && string.Equals(read.Phase, phase, StringComparison.Ordinal)))
        {
            if (!materialized.Contains(requirement.Kind))
                errors.Add($"{phase}_required_read_missing_{requirement.Kind}");
        }
    }
}

public static class HumanDecisionRecordV2Validator
{
    public static RecordValidationResult Validate(HumanDecisionRecordV2? record)
    {
        var errors = new List<string>();
        if (record == null)
            return new RecordValidationResult(false, new[] { "record_missing" });
        if (record.SchemaVersion != HumanRecorderV2Contract.SchemaVersion
            || record.Schema != HumanRecorderV2Contract.RecordSchema)
            errors.Add("schema_mismatch");
        if (string.IsNullOrWhiteSpace(record.RecordId)
            || string.IsNullOrWhiteSpace(record.SessionId)
            || string.IsNullOrWhiteSpace(record.RunId)
            || string.IsNullOrWhiteSpace(record.TimelineId)
            || string.IsNullOrWhiteSpace(record.CaptureProfileId)
            || record.Sequence <= 0)
            errors.Add("record_identity_incomplete");

        RecordValidationResult shell = HumanDecisionRecordValidator.Validate(ProjectV1(record));
        errors.AddRange(shell.Errors.Select(error => $"shell_{error}"));
        ValidateReads(record.Pre.Reads, "pre", record.Pre.SnapshotId, record.Environment, errors);
        ValidateReads(
            record.Successor.Reads,
            "successor",
            record.Successor.SnapshotId,
            record.Environment,
            errors);
        return new RecordValidationResult(errors.Count == 0, errors);
    }

    private static HumanDecisionRecord ProjectV1(HumanDecisionRecordV2 record) => new(
        HumanRecorderContract.SchemaVersion,
        HumanRecorderContract.RecordSchema,
        record.RecordId,
        record.SessionId,
        record.RunId,
        record.Sequence,
        record.RecordedAt,
        record.Environment,
        new FrozenDecisionFrame(
            record.Pre.SnapshotId,
            record.Pre.InteractionId,
            record.Pre.InteractionKind,
            record.Pre.SurfaceSchema,
            record.Pre.CatalogDigest,
            record.Pre.CatalogCount,
            record.Pre.Snapshot),
        record.NativeWitness,
        record.Mapping,
        record.Action,
        new StableSuccessor(
            record.Successor.SnapshotId,
            record.Successor.Status,
            record.Successor.InteractionId,
            record.Successor.InteractionKind,
            record.Successor.ObservedAt,
            record.Successor.Snapshot),
        record.DecisionFamily,
        record.Surface,
        record.Eligibility);

    private static void ValidateReads(
        IReadOnlyList<ReadEvidence> reads,
        string phase,
        string snapshotId,
        RecorderEnvironmentIdentity environment,
        ICollection<string> errors)
    {
        if (reads.Count != reads.Select(read => read.Kind).Distinct(StringComparer.Ordinal).Count())
            errors.Add($"{phase}_read_kind_duplicate");
        if (reads.Count
            != reads.Select(read => read.ReadEvidenceId).Distinct(StringComparer.Ordinal).Count())
            errors.Add($"{phase}_read_evidence_id_duplicate");
        foreach (ReadEvidence read in reads)
        {
            if (read.SchemaVersion != HumanRecorderV2Contract.SchemaVersion
                || read.Schema != HumanRecorderV2Contract.ReadEvidenceSchema
                || string.IsNullOrWhiteSpace(read.ReadEvidenceId)
                || string.IsNullOrWhiteSpace(read.ReadId)
                || string.IsNullOrWhiteSpace(read.Kind))
                errors.Add($"{phase}_read_identity_invalid");
            if (read.SnapshotId != snapshotId
                || read.RuntimeInstanceId != environment.RuntimeInstanceId
                || read.EnvironmentFingerprint != environment.EnvironmentFingerprint)
                errors.Add($"{phase}_read_binding_mismatch");
            if (read.Status == "materialized")
            {
                if (string.IsNullOrWhiteSpace(read.ContentSchema)
                    || read.Completeness is not JsonObject
                    || !IsSha256(read.PayloadSha256)
                    || !IsSafePayloadRef(read.PayloadRef)
                    || read.ErrorCode != null)
                    errors.Add($"{phase}_read_materialization_invalid");
            }
            else if (read.Status is "not_available" or "failed" or "stale")
            {
                if (read.PayloadRef != null || read.PayloadSha256 != null
                    || string.IsNullOrWhiteSpace(read.ErrorCode))
                    errors.Add($"{phase}_read_failure_invalid");
            }
            else
            {
                errors.Add($"{phase}_read_status_invalid");
            }
        }
    }

    private static bool IsSafePayloadRef(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        string normalized = value.Replace('\\', '/');
        return normalized.StartsWith("blobs/sha256/", StringComparison.Ordinal)
               && !normalized.StartsWith("/", StringComparison.Ordinal)
               && !normalized.Split('/').Contains("..", StringComparer.Ordinal);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
