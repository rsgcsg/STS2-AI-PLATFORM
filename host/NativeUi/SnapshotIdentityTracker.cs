using System;

namespace STS2Connector.NativeUi;

internal sealed class SnapshotIdentityTracker
{
    private readonly string _sessionId;
    private string? _lastSignature;

    public SnapshotIdentityTracker(string? sessionId = null)
    {
        _sessionId = sessionId ?? Guid.NewGuid().ToString("N")[..10];
        StateId = $"state_{_sessionId}_0";
    }

    public string StateId { get; private set; }
    public long Sequence { get; private set; }

    public (string StateId, long Sequence) Observe(string signature)
    {
        if (!string.Equals(_lastSignature, signature, StringComparison.Ordinal))
        {
            _lastSignature = signature;
            Sequence++;
            StateId = $"state_{_sessionId}_{Sequence:x}";
        }

        return (S