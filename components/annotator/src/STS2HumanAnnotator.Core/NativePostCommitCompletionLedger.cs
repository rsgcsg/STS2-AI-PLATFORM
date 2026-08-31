namespace STS2HumanAnnotator.Core;

/// <summary>
/// Describes the native completion that a direct UI root is waiting for. The
/// nullable native identities are optional constraints, never substitutes for
/// the root action identity carried by a completion signal.
/// </summary>
public sealed record NativePostCommitCompletionExpectation(
    string Family,
    string Kind,
    string? NativeOwnerWitnessId = null,
    string? NativeOperandWitnessId = null,
    string? NativeLineageWitnessId = null);

public sealed record NativePostCommitCompletionRegistration(
    string SessionId,
    long Generation,
    string ActionWitnessId,
    NativePostCommitCompletionExpectation Expectation);

/// <summary>
/// A completion signal captured at the exact native callback. It is queued
/// only to cross back to the game thread; the signal itself carries the
/// identity needed to reject stale, unmatched, or ambiguous completions.
/// </summary>
public sealed record NativePostCommitCompletion(
    string SessionId,
    long Generation,
    string CompletionId,
    string Family,
    string Kind,
    string TaskWitnessId,
    bool Succeeded,
    string? ActionWitnessId = null,
    string? NativeOwnerWitnessId = null,
    string? NativeOperandWitnessId = null,
    string? NativeLineageWitnessId = null);

public sealed record NativePostCommitCompletionResolution(
    string Status,
    NativePostCommitCompletion? Completion,
    NativePostCommitCompletionRegistration? Registration,
    string? Detail)
{
    public bool IsMatched => string.Equals(Status, "matched", StringComparison.Ordinal);

    public bool IsFailure => IsMatched && Completion?.Succeeded == false;
}

/// <summary>
/// Matches native post-commit signals to the exact direct Human root that
/// staged them. It deliberately has no FIFO/current-waiting fallback.
/// </summary>
public sealed class NativePostCommitCompletionLedger
{
    private readonly int _capacity;
    private readonly Dictionary<string, NativePostCommitCompletionRegistration> _registrations =
        new(StringComparer.Ordinal);

    public NativePostCommitCompletionLedger(int capacity = 128)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count => _registrations.Count;

    public bool Register(NativePostCommitCompletionRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrWhiteSpace(registration.SessionId)
            || registration.Generation <= 0
            || string.IsNullOrWhiteSpace(registration.ActionWitnessId)
            || string.IsNullOrWhiteSpace(registration.Expectation.Family)
            || string.IsNullOrWhiteSpace(registration.Expectation.Kind))
        {
            return false;
        }
        if (_registrations.ContainsKey(registration.ActionWitnessId)
            || _registrations.Count >= _capacity)
        {
            return false;
        }
        _registrations.Add(registration.ActionWitnessId, registration);
        return true;
    }

    public NativePostCommitCompletionResolution Complete(
        NativePostCommitCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        if (string.IsNullOrWhiteSpace(completion.SessionId)
            || completion.Generation <= 0
            || string.IsNullOrWhiteSpace(completion.CompletionId)
            || string.IsNullOrWhiteSpace(completion.Family)
            || string.IsNullOrWhiteSpace(completion.Kind)
            || string.IsNullOrWhiteSpace(completion.TaskWitnessId)
            || string.IsNullOrWhiteSpace(completion.ActionWitnessId)
            || !_registrations.TryGetValue(completion.ActionWitnessId, out NativePostCommitCompletionRegistration? registration)
            || !Matches(registration, completion))
        {
            return new NativePostCommitCompletionResolution(
                "no_match",
                completion,
                null,
                "No staged Human root matches the exact native completion identity.");
        }
        _registrations.Remove(registration.ActionWitnessId);
        return new NativePostCommitCompletionResolution(
            "matched",
            completion,
            registration,
            null);
    }

    public bool Remove(string actionWitnessId) =>
        !string.IsNullOrWhiteSpace(actionWitnessId)
        && _registrations.Remove(actionWitnessId);

    public void Reset() => _registrations.Clear();

    private static bool Matches(
        NativePostCommitCompletionRegistration registration,
        NativePostCommitCompletion completion)
    {
        NativePostCommitCompletionExpectation expectation = registration.Expectation;
        return string.Equals(registration.SessionId, completion.SessionId, StringComparison.Ordinal)
               && registration.Generation == completion.Generation
               && string.Equals(expectation.Family, completion.Family, StringComparison.Ordinal)
               && string.Equals(expectation.Kind, completion.Kind, StringComparison.Ordinal)
               && (completion.ActionWitnessId == null
                   || string.Equals(
                       registration.ActionWitnessId,
                       completion.ActionWitnessId,
                       StringComparison.Ordinal))
               && MatchesOptional(expectation.NativeOwnerWitnessId, completion.NativeOwnerWitnessId)
               && MatchesOptional(expectation.NativeOperandWitnessId, completion.NativeOperandWitnessId)
               && MatchesOptional(expectation.NativeLineageWitnessId, completion.NativeLineageWitnessId);
    }

    private static bool MatchesOptional(string? expected, string? actual) =>
        expected == null
        || string.Equals(expected, actual, StringComparison.Ordinal);
}
