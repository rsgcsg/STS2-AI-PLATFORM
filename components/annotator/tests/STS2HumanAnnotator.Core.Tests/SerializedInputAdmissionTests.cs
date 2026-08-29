using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class SerializedInputAdmissionTests
{
    [Fact]
    public void FirstMutationIsAllowedWithoutCausalDebt() =>
        Assert.Equal(
            SerializedInputAdmissionDecision.Allow,
            SerializedInputAdmission.Evaluate(true, false, false, false));

    [Fact]
    public void AdditionalMutationIsBlockedWhileNativeLifecycleIsOpen() =>
        Assert.Equal(
            SerializedInputAdmissionDecision.Block,
            SerializedInputAdmission.Evaluate(true, false, true, true));

    [Fact]
    public void TerminalDebtRequiresAnAuthoritativeBoundaryBeforeAdmission() =>
        Assert.Equal(
            SerializedInputAdmissionDecision.ResolveBoundary,
            SerializedInputAdmission.Evaluate(true, false, true, false));

    [Fact]
    public void NestedNativeHelperDoesNotCreateASecondGate() =>
        Assert.Equal(
            SerializedInputAdmissionDecision.Allow,
            SerializedInputAdmission.Evaluate(true, true, true, true));

    [Fact]
    public void RecorderNeverChangesGameplayWhenNoSessionIsRecording() =>
        Assert.Equal(
            SerializedInputAdmissionDecision.Allow,
            SerializedInputAdmission.Evaluate(false, false, true, true));
}
