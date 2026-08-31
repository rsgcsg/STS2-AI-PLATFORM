namespace STS2HumanAnnotator.Core;

public enum SerializedEvidenceAdmissionDecision
{
    Capture,
    ResolveBoundary,
    Invalidate
}

/// <summary>
/// Decides whether the recorder can open another strict causal evidence window.
/// This policy never authorizes or blocks STS2 input; an Invalidate result means
/// the Human action may proceed but cannot be claimed as a strict transition.
/// </summary>
public static class SerializedEvidenceAdmission
{
    public static SerializedEvidenceAdmissionDecision Evaluate(
        bool recording,
        bool nestedHumanScope,
        bool hasCausalDebt,
        bool nativeLifecycleOpen)
    {
        if (!recording || nestedHumanScope || !hasCausalDebt)
            return SerializedEvidenceAdmissionDecision.Capture;
        return nativeLifecycleOpen
            ? SerializedEvidenceAdmissionDecision.Invalidate
            : SerializedEvidenceAdmissionDecision.ResolveBoundary;
    }
}
