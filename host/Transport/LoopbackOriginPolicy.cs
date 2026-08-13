using System;

namespace STS2Connector;

internal static class LoopbackOriginPolicy
{
    public static bool IsAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
            return false;
        if (uri.Scheme is not ("http" or "https"))
            return false;

        return uri.IsLoopback
               || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
               || uri.Host == "127.0.0.1"
               || uri.Host