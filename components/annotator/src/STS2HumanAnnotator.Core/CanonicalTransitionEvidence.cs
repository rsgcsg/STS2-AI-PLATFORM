namespace STS2HumanAnnotator.Core;

public static class CanonicalTransitionEvidenceContract
{
    public const int SchemaVersion = 2;
    public const string Schema = "sts2.human-annotator/canonical-transition-evidence-2";
    public const string CollectionMode = "causal_human_native_observation";
    public const int LegacySchemaVersion = 1;
    public const string LegacySchema = "sts2.human-annotator/canonical-transition-evidence-1";
    public const string LegacyCollectionMode = "serialized_human_input";

    public static bool IsSupported(int schemaVersion, string schema) =>
        (schemaVersion == SchemaVersion && schema == Schema)
        || (schemaVersion == LegacySchemaVersion && schema == LegacySchema);
}

/// <summary>
/// One mechanically qualified S + A(S) -> A -> S' row. This additive stream
/// does not reinterpret historical Decision V2 or semantic trace schemas.
/// </summary>
public sealed record CanonicalTransitionEvidence(
    int SchemaVersion,
    string Schema,
    string TransitionId,
    string SessionId,
    string TimelineId,
    string RunId,
    long ActionSequence,
    DateTimeOffset RecordedAt,
    string CollectionMode,
    string? AdmissionEpochId,
    string ActionWitnessId,
    string NativeMechanism,
    SemanticFrameReference PreStateRef,
    RecordedBoundAction Action,
    SemanticFrameReference SuccessorRef,
    string ProofStatus,
    IReadOnlyList<string> Invariants,
    IReadOnlyList<string> NonClaims)
{
    public string? ActionSpaceAuthority { get; init; }
    public ExecutionSemanticActionSpaceReference? ExecutionSemanticActionSpaceRef { get; init; }
}

public static class CanonicalTransitionEvidenceValidator
{
    private static readonly string[] LegacyRequiredInvariants =
    {
        "complete_pre_state_and_catalog",
        "chosen_action_exactly_once_in_pre_catalog",
        "one_mutation_in_flight",
        "native_terminal_or_direct_commit_observed",
        "no_intervening_human_mutation",
        "complete_authoritative_successor"
    };

    private static readonly string[] RequiredInvariants =
    {
        "complete_execution_state",
        "chosen_action_exactly_once_in_authoritative_action_space",
        "exact_human_native_action_correlation",
        "native_terminal_or_direct_commit_observed",
        "no_intervening_human_mutation",
        "complete_authoritative_successor"
    };

    public static IReadOnlyList<string> Validate(CanonicalTransitionEvidence value)
    {
        var errors = new List<string>();
        if (!CanonicalTransitionEvidenceContract.IsSupported(
                value.SchemaVersion,
                value.Schema))
            errors.Add("schema_invalid");
        bool legacy = value.SchemaVersion == CanonicalTransitionEvidenceContract.LegacySchemaVersion;
        string expectedCollectionMode = legacy
            ? CanonicalTransitionEvidenceContract.LegacyCollectionMode
            : CanonicalTransitionEvidenceContract.CollectionMode;
        if (value.CollectionMode != expectedCollectionMode)
            errors.Add("collection_mode_invalid");
        if (value.ActionSequence <= 0)
            errors.Add("action_sequence_invalid");
        if (new[]
            {
                value.TransitionId,
                value.SessionId,
                value.TimelineId,
                value.RunId,
                value.ActionWitnessId,
                value.NativeMechanism,
                value.PreStateRef.SnapshotId,
                value.PreStateRef.ContentSha256,
                value.PreStateRef.ObjectRef,
                value.Action.BoundActionId,
                value.SuccessorRef.SnapshotId,
                value.SuccessorRef.ContentSha256,
                value.SuccessorRef.ObjectRef
            }.Any(string.IsNullOrWhiteSpace))
            errors.Add("identity_missing");
        if (legacy && string.IsNullOrWhiteSpace(value.AdmissionEpochId))
            errors.Add("identity_missing");
        if (!legacy
            && value.ActionSpaceAuthority is not ("native_semantic_execution" or "public_bound_actions"))
            errors.Add("action_space_authority_invalid");
        if (!legacy
            && value.ActionSpaceAuthority == "public_bound_actions"
            && value.NativeMechanism != "direct_ui_commit")
            errors.Add("public_action_space_authority_invalid");
        if (!legacy
            && value.ActionSpaceAuthority == "native_semantic_execution"
            && value.ExecutionSemanticActionSpaceRef == null)
            errors.Add("execution_semantic_action_space_ref_missing");
        if (value.ProofStatus != "canonical_s_a_s_prime")
            errors.Add("proof_status_invalid");
        foreach (string required in legacy ? LegacyRequiredInvariants : RequiredInvariants)
        {
            if (!value.Invariants.Contains(required, StringComparer.Ordinal))
                errors.Add($"invariant_missing:{required}");
        }
        if (value.PreStateRef.SnapshotId == value.SuccessorRef.SnapshotId)
            errors.Add("successor_snapshot_not_advanced");
        return errors;
    }
}
