using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class NativeSemanticDiscriminatorTests
{
    [Fact]
    public void RapidChainProvesExactMembershipAndBuildsCausalHandoff()
    {
        NativeSemanticDiscriminatorEvent[] events =
        {
            Event(1, "accepted", "a1"),
            Event(2, "accepted", "a2"),
            Event(3, "before_execution", "a1", membership: "exact_once", state: "s0"),
            Event(4, "started", "a1"),
            Event(5, "finished", "a1"),
            Event(6, "before_execution", "a2", membership: "exact_once", state: "s1"),
            Event(7, "started", "a2"),
            Event(8, "finished", "a2")
        };

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);

        Assert.Equal("pass", report.Status);
        Assert.Equal(2, report.Successful);
        Assert.Equal(2, report.ExactOnceMembership);
        NativeSemanticHandoffCandidate handoff = Assert.Single(report.HandoffCandidates);
        Assert.Equal("a1", handoff.PriorActionWitnessId);
        Assert.Equal("a2", handoff.NextActionWitnessId);
        Assert.Equal("s1", handoff.SharedStateDigest);
        Assert.False(handoff.CrossedPlayerChoiceCommit);
    }

    [Fact]
    public void EffectCancelledLaterCardIsAccountedWithoutPretendingSuccess()
    {
        NativeSemanticDiscriminatorEvent[] events =
        {
            Event(1, "accepted", "a1"),
            Event(2, "accepted", "a2"),
            Event(3, "before_execution", "a1", "exact_once", "s0"),
            Event(4, "started", "a1"),
            Event(5, "finished", "a1"),
            Event(6, "cancelled", "a2")
        };

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);

        Assert.Equal("pass", report.Status);
        Assert.Equal(1, report.Successful);
        Assert.Equal(1, report.Cancelled);
        Assert.Contains(report.Actions, value =>
            value.ActionWitnessId == "a2"
            && value.Disposition == "cancelled"
            && value.Membership == "not_executed");
    }

    [Fact]
    public void AbortAfterExecutionAdmissionDoesNotBecomeSuccessful()
    {
        NativeSemanticDiscriminatorEvent[] events =
        {
            Event(1, "accepted", "a1"),
            Event(2, "before_execution", "a1", membership: "exact_once", state: "s0"),
            Event(3, "started", "a1"),
            Event(4, "aborted_before_commit", "a1"),
            Event(5, "finished", "a1")
        };

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);

        Assert.Equal("pass", report.Status);
        Assert.Equal(0, report.Successful);
        Assert.Equal(1, report.Aborted);
    }

    [Fact]
    public void SuccessfulActionAbsentFromSemanticCatalogFailsClosed()
    {
        NativeSemanticDiscriminatorEvent[] events =
        {
            Event(1, "accepted", "a1"),
            Event(2, "before_execution", "a1", membership: "absent", state: "s0"),
            Event(3, "started", "a1"),
            Event(4, "finished", "a1")
        };

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);

        Assert.Equal("fail", report.Status);
        Assert.Equal(1, report.Unknown);
        Assert.Contains(report.Errors, value =>
            value.EndsWith("successful_action_not_exact_once_in_semantic_catalog", StringComparison.Ordinal));
        Assert.True(NativeSemanticDiscriminatorAnalyzer.IsDiagnosticOnlyError(
            report.Errors.Single(value =>
                value.EndsWith("successful_action_not_exact_once_in_semantic_catalog", StringComparison.Ordinal))));
    }

    [Fact]
    public void EnvelopeIntegrityErrorsRemainFatalWhileCoverageErrorsStayDiagnostic()
    {
        Assert.True(NativeSemanticDiscriminatorAnalyzer.IsDiagnosticOnlyError(
            "a1:successful_action_not_exact_once_in_semantic_catalog"));
        Assert.False(NativeSemanticDiscriminatorAnalyzer.IsDiagnosticOnlyError(
            "native_semantic_discriminator_sequence_gap"));
        Assert.False(NativeSemanticDiscriminatorAnalyzer.IsDiagnosticOnlyError(
            "a1:action_run_identity_changed"));
    }

    [Fact]
    public void CanonicalBoundaryCaptureCanDelegateDuplicateExecutionSample()
    {
        NativeSemanticDiscriminatorEvent[] events =
        {
            Event(1, "accepted", "a1"),
            Event(2, "before_execution", "a1") with
            {
                CaptureStatus = "not_sampled",
                Scope = "not_sampled",
                Detail = NativeSemanticDiscriminatorContract
                    .CanonicalBoundaryCaptureDelegatedDetail
            },
            Event(3, "started", "a1"),
            Event(4, "finished", "a1")
        };

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);

        Assert.Equal("pass", report.Status);
        Assert.Equal(1, report.Successful);
        Assert.Equal(0, report.Unknown);
        Assert.Equal("successful_capture_delegated", report.Actions[0].Disposition);
        Assert.Equal("delegated_to_canonical_boundary", report.Actions[0].Membership);
    }

    [Fact]
    public void DuplicateAcceptanceAndTruncatedSequenceFailClosed()
    {
        NativeSemanticDiscriminatorEvent[] events =
        {
            Event(2, "accepted", "a1"),
            Event(3, "accepted", "a1"),
            Event(4, "before_execution", "a1", membership: "exact_once", state: "s0"),
            Event(5, "finished", "a1")
        };

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);

        Assert.Equal("fail", report.Status);
        Assert.Contains("native_semantic_discriminator_sequence_gap", report.Errors);
        Assert.Contains(report.Errors, value =>
            value.EndsWith("accepted_event_duplicate", StringComparison.Ordinal));
    }

    [Fact]
    public void PlayerChoicePauseResumeStaysOnParentAndBreaksOrdinaryRootHandoff()
    {
        NativeSemanticDiscriminatorEvent[] events =
        {
            Event(1, "accepted", "a1"),
            Event(2, "before_execution", "a1", membership: "exact_once", state: "s0"),
            Event(3, "started", "a1"),
            Event(4, "paused_for_player_choice", "a1"),
            Event(5, "player_choice_commit", "choice1", related: "a1"),
            Event(6, "ready_to_resume", "a1"),
            Event(7, "before_execution_resume", "a1", state: "choice-state"),
            Event(8, "resumed", "a1"),
            Event(9, "finished", "a1"),
            Event(10, "accepted", "a2"),
            Event(11, "before_execution", "a2", membership: "exact_once", state: "s1"),
            Event(12, "started", "a2"),
            Event(13, "finished", "a2")
        };

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);

        Assert.Equal("pass", report.Status);
        Assert.Equal(1, report.PlayerChoicePauses);
        Assert.Equal(1, report.PlayerChoiceResumes);
        NativeSemanticHandoffCandidate handoff = Assert.Single(report.HandoffCandidates);
        Assert.False(handoff.CrossedPlayerChoiceCommit);
    }

    [Fact]
    public void LifecycleRowsMayBeUnsampledWithoutChangingActionDisposition()
    {
        NativeSemanticDiscriminatorEvent[] events =
        {
            Event(1, "accepted", "a1"),
            Event(2, "before_execution", "a1", membership: "exact_once", state: "s0"),
            Event(3, "started", "a1") with
            {
                CaptureStatus = "not_sampled",
                Scope = "not_sampled",
                Detail = NativeSemanticDiscriminatorContract.LifecycleOnlyDetail
            },
            Event(4, "paused_for_player_choice", "a1") with
            {
                CaptureStatus = "not_sampled",
                Scope = "not_sampled",
                Detail = NativeSemanticDiscriminatorContract.LifecycleOnlyDetail
            },
            Event(5, "ready_to_resume", "a1") with
            {
                CaptureStatus = "not_sampled",
                Scope = "not_sampled",
                Detail = NativeSemanticDiscriminatorContract.LifecycleOnlyDetail
            },
            Event(6, "resumed", "a1") with
            {
                CaptureStatus = "not_sampled",
                Scope = "not_sampled",
                Detail = NativeSemanticDiscriminatorContract.LifecycleOnlyDetail
            },
            Event(7, "finished", "a1") with
            {
                CaptureStatus = "not_sampled",
                Scope = "not_sampled",
                Detail = NativeSemanticDiscriminatorContract.LifecycleOnlyDetail
            }
        };

        NativeSemanticDiscriminatorReport report =
            NativeSemanticDiscriminatorAnalyzer.Analyze(events);

        Assert.Equal("pass", report.Status);
        Assert.Equal(1, report.Successful);
        Assert.Equal(0, report.Unknown);
        Assert.Equal("successful_membership_proved", report.Actions[0].Disposition);
    }

    [Fact]
    public void StorePersistsIndependentDiscriminatorStream()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sts2-native-semantic-{Guid.NewGuid():N}");
        try
        {
            HumanCaptureProfile profile = HumanCaptureProfiles.CombatReadRich;
            var manifest = new CurrentRecordingManifest(
                CurrentRecordingContract.SchemaVersion,
                CurrentRecordingContract.ManifestSchema,
                "session-test",
                "timeline-test",
                DateTimeOffset.UnixEpoch,
                CurrentRecordingContract.ProductVersion,
                new string('a', 40),
                "test-runtime",
                profile.ProfileId,
                EvidenceIdentity.Sha256Json(profile),
                profile.SupportedActionFamilies,
                profile.NonClaims);
            using (RecordingSessionStore store = RecordingSessionStore.Create(root, manifest, profile))
                store.AppendNativeSemanticDiscriminatorEvent(Event(1, "accepted", "a1"));

            string path = Path.Combine(
                root,
                "session-test",
                "native-semantic-discriminator.jsonl");
            Assert.True(File.Exists(path));
            Assert.Contains("native-semantic-discriminator-event-1", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static NativeSemanticDiscriminatorEvent Event(
        long sequence,
        string phase,
        string action,
        string? membership = null,
        string? state = null,
        string? related = null) =>
        new(
            NativeSemanticDiscriminatorContract.SchemaVersion,
            NativeSemanticDiscriminatorContract.EventSchema,
            $"event-{sequence}",
            "session-test",
            "timeline-test",
            "run-test",
            sequence,
            DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            phase,
            action,
            action.StartsWith("choice", StringComparison.Ordinal) ? "DirectChoice" : "PlayCardAction",
            (uint)sequence,
            phase,
            "captured",
            "combat_play_phase",
            state,
            null,
            "catalog",
            membership == "exact_once" ? new[] { "play|card|" } : Array.Empty<string>(),
            membership == null ? null : "play|card|",
            membership,
            membership == null ? null : membership == "exact_once" ? 1 : 0,
            "snapshot-test",
            "interactive",
            "combat_turn",
            "complete",
            1,
            "ui-catalog",
            "exact_unique",
            1,
            related,
            null,
            Array.Empty<string>());

}
