using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace STS2Connector.NativeUi;

internal sealed class NativeEntityRegistry
{
    private sealed record Identity(string Value);

    private readonly string _sessionPrefix = Guid.NewGuid().ToString("N")[..8];
    private readonly ConditionalWeakTable<object, Identity> _identities = new();
    private readonly ConcurrentDictionary<string, WeakReference<object>> _entities =
        new(StringComparer.Ordinal);
    private long _nextIdentity;

    public string GetId(object entity, string kind)
    {
        Identity identity = _identities.GetValue(entity, _ =>
        {
            long sequence = Interlocked.Increment(ref _nextIdentity);
            return new Identity($"{kind}_{_sessionPrefix}_{sequence:x}");
        });
        _entities[identity.Value] = new WeakReference<object>(entity);
        return identity.Value;
    }

    public bool TryResolve<T>(string entityId, out T? entity) where T : class
    {
        entity = null;
        if (!_entities.TryGetValue(entityId, out WeakReference<object>? reference)
            || !reference.TryGetTarget(out object? target))
        {
            _entities.TryRemove(entityId, out _);
            return false;
        }
        if (target is not T typed)
            return false;

        entity = typed;
        return true;
    }

    public IReadOnlyDictionary<string, object> CaptureExactReferences(
        IEnumerable<string> entityIds)
    {
        return entityIds
            .Distinct(StringComparer.Ordinal)
            .Select(entityId =>
            {
                object? target = null;
                bool found = _entities.TryGetValue(
                                 entityId,
                                 out WeakReference<object>? reference)
                             && reference.TryGetTarget(out target);
                return (entityId, found, target);
            })
            .Where(entry => entry.found && entry.target != null)
            .ToDictionary(
                entry => entry.entityId,
                entry => entry.target!,
                StringComparer.Ordinal);
    }
}
