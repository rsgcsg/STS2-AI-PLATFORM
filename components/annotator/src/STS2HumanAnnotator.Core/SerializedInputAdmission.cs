namespace STS2HumanAnnotator.Core;

public enum SerializedInputAdmissionDecision
{
    Allow,
    ResolveBoundary,
    Block
}

/// <summary>
/// Stateless admission policy for the canonical one-mutation-at-a-time lane.
/// Existing native lifecycle and semantic ledgers remain the only state owners.
/// </summary>
public static class SerializedInputAdmission
{
    public static SerializedInputAdmissionDecision Evaluate(
        bool recording,
        bool nestedHumanScope,
        bool hasCausalDebt,
        bool nativeLifecycleOpen)
    {
        if (!recording || nestedHumanScope || !hasCausalDebt)
            return SerializedInputAdmissionDecision.Allow;
        return nativeLifecycleOpen
            ? SerializedInputAdmissionDecision.Block
            : SerializedInputAdmissionDecision.ResolveBoundary;
    }
}
