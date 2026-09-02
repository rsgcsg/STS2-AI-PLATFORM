using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

public static class ExecutionSemanticActionSpaceContract
{
    public const int SchemaVersion = 2;
    public const string Schema =
        "sts2.human-annotator/execution-semantic-action-space-2";
    public const int LegacySchemaVersion = 1;
    public const string LegacySchema =
        "sts2.human-annotator/execution-semantic-action-space-1";

    public static bool IsCurrent(int schemaVersion, string schema) =>
        schemaVersion == SchemaVersion && schema == Schema;

    public static bool IsSupported(int schemaVersion, string schema) =>
        IsCurrent(schemaVersion, schema)
        || (schemaVersion == LegacySchemaVersion && schema == LegacySchema);
}

/// <summary>
/// One action in an STS2-owned semantic decision captured at the exact native
/// binding boundary: before GameAction execution, or before a source-local
/// callback admits the corresponding native mutation. These are observation
/// facts, not public delivery authority or an Annotator legality engine.
/// </summary>
public sealed record ExecutionSemanticAction(
    string Key,
    string Verb,
    string? SubjectReferentId,
    IReadOnlyDictionary<string, string> Arguments,
    string NativeLegalityBasis);

/// <summary>
/// Durable projection of the read-only Native Foundation decision joined to an
/// exact Human-correlated mutation. Semantic state/action ownership remains
/// with STS2 and Native Foundation; this value only preserves that observation.
/// </summary>
public sealed record ExecutionSemanticActionSpaceEvidence(
    int SchemaVersion,
    string Schema,
    string ActionWitnessId,
    string Phase,
    string Status,
    string Scope,
    string SemanticStateDigest,
    JsonNode SemanticState,
    string SemanticCatalogDigest,
    IReadOnlyList<ExecutionSemanticAction> Actions,
    string ObservedActionKey,
    string ObservedMembership,
    int ObservedMatchCount,
    IReadOnlyList<string> NativeEvidence,
    IReadOnlyList<string> NonClaims,
    string? Detail)
{
    /// <summary>
    /// Exact Connector BoundAction selected by the Human root that was bound
    /// to <see cref="ObservedActionKey"/>. Public and native verbs may differ.
    /// </summary>
    public string? HumanBoundActionId { get; init; }
}

public sealed record ExecutionSemanticActionSpaceReference(
    string ActionWitnessId,
    string SemanticStateDigest,
    string SemanticCatalogDigest,
    string ContentSha256,
    string ObjectRef);

public static class ExecutionSemanticActionSpaceValidator
{
    public static IReadOnlyList<string> Validate(
        ExecutionSemanticActionSpaceEvidence? value,
        SemanticActionReference? action = null)
    {
        var errors = new List<string>();
        if (value == null)
        {
            errors.Add("execution_semantic_action_space_missing");
            return errors;
        }
        if (!ExecutionSemanticActionSpaceContract.IsSupported(
                value.SchemaVersion,
                value.Schema))
            errors.Add("execution_semantic_action_space_schema_invalid");
        if (new[]
            {
                value.ActionWitnessId,
                value.Phase,
                value.Status,
                value.Scope,
                value.SemanticStateDigest,
                value.SemanticCatalogDigest,
                value.ObservedActionKey,
                value.ObservedMembership
            }.Any(string.IsNullOrWhiteSpace))
            errors.Add("execution_semantic_action_space_identity_missing");
        if (value.Phase is not ("before_execution" or "before_native_action_admission"))
            errors.Add("execution_semantic_action_space_phase_invalid");
        if (value.SchemaVersion == ExecutionSemanticActionSpaceContract.SchemaVersion
            && string.IsNullOrWhiteSpace(value.HumanBoundActionId))
            errors.Add("execution_semantic_human_binding_missing");
        if (value.Status != "captured"
            || value.Scope == "unavailable"
            || value.Actions.Count == 0
            || value.NativeEvidence.Count == 0)
            errors.Add("execution_semantic_action_space_incomplete");
        if (value.ObservedMembership != "exact_once"
            || value.ObservedMatchCount != 1
            || value.Actions.Count(candidate =>
                candidate.Key == value.ObservedActionKey) != 1)
            errors.Add("execution_semantic_action_membership_invalid");
        if (value.Actions.Any(candidate =>
                string.IsNullOrWhiteSpace(candidate.Key)
                || string.IsNullOrWhiteSpace(candidate.Verb)
                || string.IsNullOrWhiteSpace(candidate.NativeLegalityBasis)
                || candidate.Arguments.Any(argument =>
                    string.IsNullOrWhiteSpace(argument.Key)
                    || string.IsNullOrWhiteSpace(argument.Value))))
            errors.Add("execution_semantic_action_invalid");

        if (action != null)
        {
            if (value.ActionWitnessId != action.ActionWitnessId)
                errors.Add("execution_semantic_action_witness_mismatch");
            if (action.BoundAction == null)
            {
                errors.Add("execution_semantic_human_action_missing");
            }
            else
            {
                ExecutionSemanticAction? selected = value.Actions.SingleOrDefault(candidate =>
                    candidate.Key == value.ObservedActionKey);
                bool exactBinding = value.SchemaVersion
                    == ExecutionSemanticActionSpaceContract.SchemaVersion
                    ? value.HumanBoundActionId == action.BoundAction.BoundActionId
                    : selected != null
                      && selected.Verb == action.BoundAction.Verb
                      && selected.SubjectReferentId == action.BoundAction.SubjectReferentId
                      && SameArguments(selected.Arguments, action.BoundAction.Arguments);
                if (!exactBinding)
                {
                    errors.Add("execution_semantic_human_action_mismatch");
                }
            }
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool SameArguments(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out string? value)
                            && value == pair.Value);

}
