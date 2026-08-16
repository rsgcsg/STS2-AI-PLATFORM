using System;
using System.Security.Cryptography;
using System.Text;
using Godot;
using STS2Connector.Authority;

namespace STS2Connector.HostControl;

internal sealed record HostShutdownRequest(
    string? ExpectedRuntimeInstanceId,
    string? HostControlToken);

internal sealed record HostShutdownResponse(
    string Status,
    string RuntimeInstanceId);

internal sealed record HostShutdownAuthorization(
    bool Allowed,
    string Status,
    string RuntimeInstanceId);

internal static class HostLifecycleControl
{
    private static string? _configuredToken;

    internal static bool Enabled => _configuredToken != null;

    internal static void Configure(string? token) =>
        _configuredToken = ResolveConfiguredToken(token);

    internal static string? ResolveConfiguredToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        if (token.Length != 64 || !IsLowerHex(token))
        {
            throw new InvalidOperationException(
                "STS2_CONNECTOR_HOST_CONTROL_TOKEN must be exactly 64 lowercase hexadecimal characters.");
        }
        return token;
    }

    internal static HostShutdownAuthorization Authorize(
        string? configuredToken,
        string? presentedToken,
        string? expectedRuntimeInstanceId,
        string actualRuntimeInstanceId)
    {
        if (configuredToken == null)
            return new HostShutdownAuthorization(false, "host_control_disabled", actualRuntimeInstanceId);
        if (!FixedTimeEquals(configuredToken, presentedToken))
            return new HostShutdownAuthorization(false, "host_control_unauthorized", actualRuntimeInstanceId);
        if (!string.Equals(expectedRuntimeInstanceId, actualRuntimeInstanceId, StringComparison.Ordinal))
            return new HostShutdownAuthorization(false, "runtime_instance_changed", actualRuntimeInstanceId);
        return new HostShutdownAuthorization(true, "shutdown_authorized", actualRuntimeInstanceId);
    }

    internal static HostShutdownAuthorization Authorize(HostShutdownRequest request)
    {
        string runtimeInstanceId = EnvironmentIdentityRuntime.HostIdentity().RuntimeInstanceId;
        return Authorize(
            _configuredToken,
            request.HostControlToken,
            request.ExpectedRuntimeInstanceId,
            runtimeInstanceId);
    }

    internal static HostShutdownResponse ScheduleShutdown(HostShutdownAuthorization authorization)
    {
        if (!authorization.Allowed)
            throw new InvalidOperationException("Host shutdown was not authorized.");
        if (Engine.GetMainLoop() is not SceneTree tree)
            throw new InvalidOperationException("The current Host has no SceneTree main loop.");
        tree.CallDeferred("quit");
        return new HostShutdownResponse("shutdown_requested", authorization.RuntimeInstanceId);
    }

    private static bool FixedTimeEquals(string expected, string? presented)
    {
        if (presented == null || presented.Length != expected.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(presented));
    }

    private static bool IsLowerHex(string value)
    {
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        }
        return true;
    }
}
