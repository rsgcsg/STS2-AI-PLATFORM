namespace STS2HumanAnnotator.Core;

/// <summary>
/// Correlates two exact native observations that may arrive in either order.
/// It owns no domain truth: callers supply both observations and reserve the
/// single durable commit attempt explicitly.
/// </summary>
public sealed class TwoSignalCommitGate
{
    private readonly object _gate = new();
    private bool _first;
    private bool _second;
    private bool _reserved;

    public bool ObserveFirst()
    {
        lock (_gate)
        {
            _first = true;
            return _second;
        }
    }

    public bool ObserveSecond()
    {
        lock (_gate)
        {
            _second = true;
            return _first;
        }
    }

    public bool TryReserveCommit()
    {
        lock (_gate)
        {
            if (!_first || !_second || _reserved)
                return false;
            _reserved = true;
            return true;
        }
    }

    public bool TryReleaseCommit()
    {
        lock (_gate)
        {
            if (!_reserved)
                return false;
            _reserved = false;
            return true;
        }
    }
}
