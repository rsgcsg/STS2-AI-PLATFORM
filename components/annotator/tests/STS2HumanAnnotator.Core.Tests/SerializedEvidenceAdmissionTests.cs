using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class SerializedEvidenceAdmissionTests
{
    [Fact]
    public void FirstMutationCanOpenStrictEvidenceWindow() =>
        Assert.Equal(
            SerializedEvidenceAdmissionDecision.Capture,
            SerializedEvidenceAdmission.Evaluate(true, false, false, false));

    [Fact]
    public void ConcurrentNativeLifecycleInvalidatesEvidenceInsteadOfControllingGameplay() =>
        Assert.Equal(
            SerializedEvidenceAdmissionDecision.Invalidate,
            SerializedEvidenceAdmission.Evaluate(true, false, true, true));

    [Fact]
    public void TerminalDebtRequiresAnAuthoritativeBoundaryBeforeEvidenceAdmission() =>
        Assert.Equal(
            SerializedEvidenceAdmissionDecision.ResolveBoundary,
            SerializedEvidenceAdmission.Evaluate(true, false, true, false));

    [Fact]
    public void NestedNativeHelperDoesNotCreateASecondEvidenceGate() =>
        Assert.Equal(
            SerializedEvidenceAdmissionDecision.Capture,
            SerializedEvidenceAdmission.Evaluate(true, true, true, true));

    [Fact]
    public void RecorderDoesNotOpenEvidenceWindowWithoutActiveSession() =>
        Assert.Equal(
            SerializedEvidenceAdmissionDecision.Capture,
            SerializedEvidenceAdmission.Evaluate(false, false, true, true));
}
