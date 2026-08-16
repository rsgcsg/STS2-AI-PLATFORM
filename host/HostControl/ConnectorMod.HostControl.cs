using System;
using System.Net;
using STS2Connector.HostControl;

namespace STS2Connector;

public static partial class ConnectorMod
{
    private const int MaxHostControlBodyBytes = 4 * 1024;

    private static void HandlePostHostShutdown(
        HttpListenerRequest request,
        HttpListenerResponse response)
    {
        HostShutdownRequest? shutdown = ReadBoundedJsonBody<HostShutdownRequest>(
            request,
            response,
            MaxHostControlBodyBytes,
            "Host lifecycle control");
        if (shutdown == null)
            return;
        if (!IsSafeProtocolIdentifier(shutdown.ExpectedRuntimeInstanceId, 128)
            || shutdown.HostControlToken?.Length != 64)
        {
            SendApiError(
                response,
                400,
                "invalid_host_shutdown_contract",
                "An exact runtime instance and 256-bit process-local Host control token are required.");
            return;
        }

        try
        {
            HostShutdownAuthorization authorization = HostLifecycleControl.Authorize(shutdown);
            if (!authorization.Allowed)
            {
                SendApiError(
                    response,
                    authorization.Status == "runtime_instance_changed" ? 409 : 403,
                    authorization.Status,
                    "Host lifecycle shutdown failed closed.");
                return;
            }
            var task = RunOnMainThread(() => HostLifecycleControl.ScheduleShutdown(authorization));
            SendJson(response, task.GetAwaiter().GetResult());
        }
        catch (Exception exception)
        {
            SendApiInternalError(response, "host_shutdown_failed", exception);
        }
    }
}
