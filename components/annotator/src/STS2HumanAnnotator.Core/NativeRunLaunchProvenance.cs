using System.Runtime.CompilerServices;

namespace STS2HumanAnnotator.Core;

/// <summary>Exact native initialization ownership; Launch alone does not prove a new run.</summary>
public sealed class NativeRunLaunchProvenance<T> where T : class
{
    private sealed record Origin(bool? IsNew);
    private readonly ConditionalWeakTable<T, Origin> _origins = new();

    private readonly object _gate = new();

    public void ObserveSetup(T state, bool isNew)
    {
        lock (_gate)
        {
            if (_origins.TryGetValue(state, out var existing))
            {
                if (existing.IsNew == isNew)
                    return;
                _origins.Remove(state);
                _origins.Add(state, new Origin(null)); // conflicting setup never upgrades provenance
                return;
            }
            _origins.Add(state, new Origin(isNew));
        }
    }

    public string JournalKind(T state)
    {
        lock (_gate)
            return !_origins.TryGetValue(state, out var origin) || origin.IsNew == null
                ? "run_launched_native_origin_unknown"
                : origin.IsNew.Value ? "run_started_native" : "run_resumed_native";
    }
}
