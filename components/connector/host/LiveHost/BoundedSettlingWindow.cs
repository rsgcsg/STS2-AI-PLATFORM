using System;

namespace STS2Connector.LiveHost;

internal sealed class BoundedSettlingWindow
{
    private readonly TimeSpan _duration;
    private readonly object _gate = new();
    private DateTimeOffset? _startedAt;

    internal BoundedSettlingWindow(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));
        _duration = duration;
    }

    internal bool Observe(bool condition, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!condition)
            {
                _startedAt = null;
                return false;
            }

            _startedAt ??= now;
            return now - _startedAt <= _duration;
        }
    }
}
