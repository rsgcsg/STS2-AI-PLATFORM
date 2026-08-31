using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class CanonicalTransitionEvidenceTests
{
    private static CanonicalTransitionEvidence Valid() => new(
        CanonicalTransitionEvidenceContract.SchemaVersion,
        CanonicalTransitionEvidenceContract.Schema,
        "transition-1",
        "session-1",
        "timeline-1",
        "run-1",
        1,
        DateTimeOffset.UnixEpoch,
        CanonicalTransitionEvidenceContract.CollectionMode,
        "epoch-s0",
        "action-1",
        "game_action",
        new SemanticFrameReference("snapshot-1", new string('a', 64), "frames/a.json"),
        new RecordedBoundAction(
            "bound-1",
            "play",
            "card-1",
            new Dictionary<string, string>(),
            "Play"),
        new SemanticFrameReference("snapshot-2", new string('b', 64), "frames/b.json"),
        "canonical_s_a_s_prime",
        new[]
        {
            "complete_pre_state_and_catalog",
            "chosen_action_exactly_once_in_pre_catalog",
            "one_mutation_in_flight",
            "native_terminal_or_direct_commit_observed",
            "no_intervening_human_mutation",
            "complete_authoritative_successor"
        },
        new[] { "not_business_completion" });

    [Fact]
    public void CompleteSerializedTransitionPasses() =>
        Assert.Empty(CanonicalTransitionEvidenceValidator.Validate(Valid()));

    [Fact]
    public void SameSnapshotCannotMasqueradeAsSuccessor()
    {
        CanonicalTransitionEvidence value = Valid() with
        {
            SuccessorRef = Valid().PreStateRef
        };
        Assert.Contains(
            "successor_snapshot_not_advanced",
            CanonicalTransitionEvidenceValidator.Validate(value));
    }

    [Fact]
    public void MissingSerializationInvariantFailsClosed()
    {
        CanonicalTransitionEvidence value = Valid() with
        {
            Invariants = Valid().Invariants
                .Where(value => value != "one_mutation_in_flight")
                .ToArray()
        };
        Assert.Contains(
            "invariant_missing:one_mutation_in_flight",
            CanonicalTransitionEvidenceValidator.Validate(value));
    }
}
