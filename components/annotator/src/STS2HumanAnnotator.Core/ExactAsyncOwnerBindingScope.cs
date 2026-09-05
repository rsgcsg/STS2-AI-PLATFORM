using System.Runtime.CompilerServices;
using System.Threading;

namespace STS2HumanAnnotator.Core;

/// <summary>
/// Carries one immutable exact owner through .NET ExecutionContext and binds
/// the resulting native object by identity.  It deliberately has no global
/// "latest" value, ordering fallback, timeout, or enumerable lookup.
/// </summary>
public sealed class ExactAsyncOwnerBindingScope<TKey, TContext, TBinding>
    where TKey : class
    where TContext : class
    where TBinding : class
{
    private sealed record Frame(TContext Context, Frame? Previous);

    private sealed class Holder
    {
        internal Holder(TBinding binding) => Binding = binding;

        internal TBinding Binding { get; }
    }

    private sealed class Scope : IDisposable
    {
        private readonly ExactAsyncOwnerBindingScope<TKey, TContext, TBinding> _owner;
        private readonly Frame _frame;
        private bool _disposed;

        internal Scope(
            ExactAsyncOwnerBindingScope<TKey, TContext, TBinding> owner,
            Frame frame)
        {
            _owner = owner;
            _frame = frame;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            // A continuation that captured this frame owns an independent
            // ExecutionContext value. Restoring the caller cannot erase it.
            if (ReferenceEquals(_owner._current.Value, _frame))
                _owner._current.Value = _frame.Previous;
        }
    }

    private readonly AsyncLocal<Frame?> _current = new();
    private readonly ConditionalWeakTable<TKey, Holder> _bindings = new();
    private readonly object _gate = new();

    public IDisposable Enter(TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var frame = new Frame(context, _current.Value);
        _current.Value = frame;
        return new Scope(this, frame);
    }

    public bool TryBindCurrent(
        TKey key,
        Func<TContext, TBinding> createBinding)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(createBinding);
        Frame? frame = _current.Value;
        if (frame == null)
            return false;
        Set(key, createBinding(frame.Context));
        return true;
    }

    public void Set(TKey key, TBinding binding)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(binding);
        lock (_gate)
        {
            _bindings.Remove(key);
            _bindings.Add(key, new Holder(binding));
        }
    }

    public bool TryGet(TKey key, out TBinding? binding)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            if (_bindings.TryGetValue(key, out Holder? holder))
            {
                binding = holder.Binding;
                return true;
            }
        }
        binding = null;
        return false;
    }

    public bool TryTake(TKey key, out TBinding? binding)
    {
        lock (_gate)
        {
            if (!_bindings.TryGetValue(key, out Holder? holder))
            {
                binding = null;
                return false;
            }
            binding = holder.Binding;
            _bindings.Remove(key);
            return true;
        }
    }

    public void Forget(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
            _bindings.Remove(key);
    }
}
