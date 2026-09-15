namespace STS2HumanAnnotator.Core;

/// <summary>
/// Exact invocation/owner correlation shared by native Proceed observers.
/// A match permits observing Commit and owner readiness at that return; it
/// proves neither a complete state nor a semantic successor.
/// </summary>
public static class NativeSynchronousOwnerHandoff
{
    public static bool Matches(
        Task completion,
        object? ownerBefore,
        bool ownerWasOpen,
        object? ownerAfter,
        bool ownerIsOpen,
        string? boundActionWitnessId,
        string? scopedActionWitnessId) =>
        completion.IsCompletedSuccessfully
        && ownerBefore != null && ReferenceEquals(ownerBefore, ownerAfter)
        && !ownerWasOpen && ownerIsOpen
        && !string.IsNullOrWhiteSpace(boundActionWitnessId)
        && string.Equals(boundActionWitnessId, scopedActionWitnessId, StringComparison.Ordinal);
}
