using System.Runtime.CompilerServices;
using System.Threading;

namespace STS2HumanAnnotator.Mod;

internal static class NativeWitnessIdentity
{
    private sealed record Identity(string Value);

    private static readonly string Prefix = Guid.NewGuid().ToString("N")[..8];
    private static readonly ConditionalWeakTable<object, Identity> Identities = new();
    private static long _next;

    internal static string Get(object value, string kind) =>
        Identities.GetValue(value, _ =>
        {
            long sequence = Interlocked.Increment(ref _next);
            return new Identity($"{kind}_{Prefix}_{sequence:x}");
        }).Value;
}
