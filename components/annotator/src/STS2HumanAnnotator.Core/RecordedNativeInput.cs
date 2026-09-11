namespace STS2HumanAnnotator.Core;

/// <summary>An exact accepted native input, not a public BoundAction or proof
/// of legality at H. Execution membership is validated separately at S.</summary>
public sealed record RecordedNativeInput(
    string ActionKey,
    string Verb,
    string? SubjectReferentId,
    IReadOnlyDictionary<string, string> Arguments,
    string? Label);

public static class RecordedNativeInputValidator
{
    public static IReadOnlyList<string> Validate(SemanticActionReference action)
    {
        if (action.NativeInput is not { } input) return Array.Empty<string>();
        bool valid = action.BoundAction == null && action.NativeMechanism == "game_action"
            && action.NativeActionType == "PlayCardAction" && input.Verb == "play"
            && !string.IsNullOrWhiteSpace(input.ActionKey)
            && !string.IsNullOrWhiteSpace(input.SubjectReferentId)
            && input.Arguments.All(pair => !string.IsNullOrWhiteSpace(pair.Key)
                && !string.IsNullOrWhiteSpace(pair.Value))
            && action.NativeWitness is { NativeActionType: "PlayCardAction" } witness
            && !string.IsNullOrWhiteSpace(witness.Origin)
            && !string.IsNullOrWhiteSpace(witness.SubjectWitnessId)
            && action.Mapping is { Status: "exact_native_input", MatchCount: 1,
                Basis: "scoped_native_input_reference_equality" };
        return valid ? Array.Empty<string>() : new[] { "native_input_correlation_invalid" };
    }
}
