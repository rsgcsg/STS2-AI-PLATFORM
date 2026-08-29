namespace STS2HumanAnnotator.Core;

public static class CanonicalTransitionEvidenceContract
{
    public const int SchemaVersion = 1;
    public const string Schema = "sts2.human-annotator/canonical-transition-evidence-1";
    public const string CollectionMode = "serialized_human_input";
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
    string AdmissionEpochId,
    string ActionWitnessId,
    string NativeMechanism,
    SemanticFrameReference PreStateRef,
    RecordedBoundAction Action,
    SemanticFrameReference SuccessorRef,
    string ProofStatus,
    IReadOnlyList<string> Invariants,
    IReadOnlyList<string> NonClaims);

public static class CanonicalTransitionEvidenceValidator
{
    private static readonly string[] RequiredInvariants =
    {
        "complete_pre_state_and_catalog",
        "chosen_action_exactly_once_in_pre_catalog",
        "one_mutation_in_flight",
        "native_terminal_or_direct_commit_observed",
        "no_intervening_human_mutation",
        "complete_authoritative_successor"
    };

    public static IReadOnlyList<string> Validate(CanonicalTransitionEvidence value)
    {
        var errors = new List<string>();
        if (value.SchemaVersion != CanonicalTransitionEvidenceContract.SchemaVersion
            || value.Schema != CanonicalTransitionEvidenceContract.Schema)
            errors.Add("schema_invalid");
        if (value.CollectionMode != CanonicalTransitionEvidenceContract.CollectionMode)
            errors.Add("collection_mode_invalid");
        if (value.ActionSequence <= 0)
            errors.Add("action_sequence_invalid");
        if (new[]
            {
                value.TransitionId,
                value.SessionId,
                value.TimelineId,
                value.RunId,
                value.AdmissionEpochId,
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
        if (value.ProofStatus != "canonical_s_a_s_prime")
            errors.Add("proof_status_invalid");
        foreach (string required in RequiredInvariants)
        {
            if (!value.Invariants.Contains(required, StringComparer.Ordinal))
                errors.Add($"invariant_missing:{required}");
        }
        if (value.PreStateRef.SnapshotId == value.SuccessorRef.SnapshotId)
            errors.Add("successor_snapshot_not_advanced");
        return errors;
    }
}
