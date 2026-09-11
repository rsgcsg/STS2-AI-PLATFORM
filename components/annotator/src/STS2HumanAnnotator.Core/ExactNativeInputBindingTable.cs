using System.Runtime.CompilerServices;

namespace STS2HumanAnnotator.Core;

/// <summary>Retains an input on its exact native action across deferred enqueue.
/// This is identity storage, not acceptance, execution, or a causal ledger.</summary>
public sealed class ExactNativeInputBindingTable<TAction, TInput>
    where TAction : class where TInput : class
{
    private readonly ConditionalWeakTable<TAction, TInput> _bindings = new();
    private readonly object _gate = new();

    public bool TryBind(TAction action, TInput input)
    {
        lock (_gate)
        {
            if (_bindings.TryGetValue(action, out var prior))
                return ReferenceEquals(prior, input);
            _bindings.Add(action, input);
            return true;
        }
    }

    // Do not consume at RequestEnqueue: STS2 can defer the same object and
    // request it again. The existing accepted-root gate handles duplicates.
    public bool TryGet(TAction action, out TInput? input) =>
        _bindings.TryGetValue(action, out input);
}
