namespace STS2Connector.NativeUi;

internal sealed record NativeInputResult(
    bool Accepted,
    string? ErrorCode,
    string? Detail,
    string? DeliveryEvidence)
{
    public static NativeInputResult Delivered(string? evidence) =>
        new(true, null, null, evidence);

    public static NativeInputResult Rejected(string code, string detail) =>
        new(false, code, detai