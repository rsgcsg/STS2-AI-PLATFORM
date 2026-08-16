using System;
using System.Runtime.InteropServices;

namespace STS2Connector.Authority;

internal sealed record ExactGamePermission(
    string? SupportId,
    string Status,
    bool ActionExecutionAllowed,
    string Detail);

/// <summary>
/// Exact game admission is distinct from native UI legality. This table admits
/// only audited binaries; current owners and operands are still rediscovered at
/// execution time.
/// </summary>
internal static class ExactGameCompatibility
{
    internal const string CanaryEnvironmentVariable =
        "STS2_CONNECTOR_EXPERIMENTAL_GAME_ID";
    internal const string SupportedMacId = "darwin-arm64-v0.111.0-41cef1ea";
    internal const string WindowsCandidateId =
        "win32-x64-v0.111.0-41cef1ea-candidate";

    private sealed record RuntimeTuple(
        string Id,
        bool Supported,
        string Platform,
        string Architecture,
        string Version,
        string Commit,
        int MainAssemblyHash,
        string MainAssemblySha256,
        string MainAssemblyMvid);

    private static readonly RuntimeTuple[] KnownRuntimes =
    [
        new(
            SupportedMacId,
            true,
            "darwin",
            "arm64",
            "v0.111.0",
            "41cef1ea",
            1010476334,
            "9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4",
            "57785517-0b16-42b9-8b36-bad6fb28384b"),
        new(
            WindowsCandidateId,
            false,
            "win32",
            "x64",
            "v0.111.0",
            "41cef1ea",
            222455745,
            "0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9",
            "73b63ee0-6c0a-47bb-b0d1-b21f6d94222e")
    ];

    internal static ExactGamePermission Evaluate(
        string? version,
        string? commit,
        int? mainAssemblyHash,
        string? mainAssemblySha256,
        string? mainAssemblyMvid,
        string platform,
        string architecture,
        string? requestedCanaryId)
    {
        if (string.IsNullOrWhiteSpace(version)
            || string.IsNullOrWhiteSpace(commit)
            || !mainAssemblyHash.HasValue
            || string.IsNullOrWhiteSpace(mainAssemblySha256)
            || string.IsNullOrWhiteSpace(mainAssemblyMvid))
        {
            return new ExactGamePermission(
                null,
                "identity_incomplete",
                false,
                "Game mutation is disabled because the exact game assembly identity is incomplete.");
        }

        RuntimeTuple? matched = Array.Find(KnownRuntimes, item =>
            string.Equals(item.Platform, platform, StringComparison.Ordinal)
            && string.Equals(item.Architecture, architecture, StringComparison.Ordinal)
            && string.Equals(item.Version, version, StringComparison.Ordinal)
            && string.Equals(item.Commit, commit, StringComparison.Ordinal)
            && item.MainAssemblyHash == mainAssemblyHash.Value
            && string.Equals(item.MainAssemblySha256, mainAssemblySha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.MainAssemblyMvid, mainAssemblyMvid, StringComparison.OrdinalIgnoreCase));

        if (matched is null)
        {
            return new ExactGamePermission(
                null,
                "unsupported_exact_game",
                false,
                "The exact platform, game build and assembly identity is not admitted for mutation.");
        }

        if (matched.Supported)
        {
            return new ExactGamePermission(
                matched.Id,
                "supported_exact",
                true,
                $"Exact supported game identity {matched.Id} matched.");
        }

        bool canaryEnabled = string.Equals(
            requestedCanaryId,
            matched.Id,
            StringComparison.Ordinal);
        return canaryEnabled
            ? new ExactGamePermission(
                matched.Id,
                "canary_exact",
                true,
                $"Exact candidate game identity {matched.Id} was explicitly enabled for this process.")
            : new ExactGamePermission(
                matched.Id,
                "known_unqualified",
                false,
                $"Exact candidate game identity {matched.Id} is known but not supported; mutation requires an explicit process-local canary opt-in.");
    }

    internal static string CurrentPlatform() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win32"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
        : "unknown";

    internal static string CurrentArchitecture() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => "unknown"
        };

    internal static bool IsExecutionStatus(string? status) =>
        string.Equals(status, "supported_exact", StringComparison.Ordinal)
        || string.Equals(status, "canary_exact", StringComparison.Ordinal);
}
