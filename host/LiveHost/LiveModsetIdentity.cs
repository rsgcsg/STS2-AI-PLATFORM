using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.NativeUi;

namespace STS2Connector.LiveHost;

internal static class LiveModsetIdentity
{
    internal const string ConnectorModId = "STS2_MCP";
    private const string FingerprintScope =
        "manager_state+ordered_manifest_identity+load_state+source+workshop_id+loaded_assembly_name_version_mvid";

    public static ModsetIdentity Read()
    {
        try
        {
            string managerState = ModManager.State.ToString();
            LoadedModIdentity[] mods = ModManager.Mods
                .Select(BuildMod)
                .ToArray();
            return Evaluate(
                managerState,
                mods,
                typeof(ConnectorMod).Assembly.ManifestModule.ModuleVersionId.ToString("D"),
                ConnectorMod.Version);
        }
        catch (Exception ex)
        {
            return new ModsetIdentity(
                "unavailable",
                StableIdentityHash.Object(new { state = "unavailable", error = ex.GetType().Name }),
                FingerprintScope,
                Array.Empty<LoadedModIdentity>(),
                $"Loaded Modset identity is unavailable because {ex.GetType().Name} was raised.");
        }
    }

    internal static ModsetIdentity Evaluate(
        string managerState,
        IReadOnlyList<LoadedModIdentity> mods,
        string connectorModuleVersionId,
        string connectorVersion)
    {
        string fingerprint = StableIdentityHash.Object(new
        {
            managerState,
            mods
        });
        LoadedModIdentity[] loaded = mods
            .Where(mod => string.Equals(mod.LoadState, "Loaded", StringComparison.Ordinal))
            .ToArray();
        bool hazardousDetectedState = mods.Any(mod =>
            string.Equals(mod.LoadState, "Failed", StringComparison.Ordinal)
            || string.Equals(mod.LoadState, "AddedAtRuntime", StringComparison.Ordinal));
        LoadedModIdentity? connector = loaded.FirstOrDefault(mod =>
            string.Equals(mod.Id, ConnectorModId, StringComparison.Ordinal));
        bool exactConnectorAssembly = connector?.Assemblies.Any(assembly =>
            string.Equals(assembly.ModuleVersionId, connectorModuleVersionId, StringComparison.OrdinalIgnoreCase)) == true;
        bool exactConnectorVersion = string.Equals(connector?.Version, connectorVersion, StringComparison.Ordinal);
        bool exact = string.Equals(managerState, "Initialized", StringComparison.Ordinal)
                     && loaded.Length == 1
                     && connector != null
                     && exactConnectorAssembly
                     && exactConnectorVersion
                     && !hazardousDetectedState;
        if (exact)
        {
            return new ModsetIdentity(
                "exact_player_environment_only",
                fingerprint,
                FingerprintScope,
                mods,
                "ModManager is initialized and the only loaded Mod is this exact STS2Connector assembly.");
        }
        string status = !string.Equals(managerState, "Initialized", StringComparison.Ordinal)
            ? "manager_not_initialized"
            : hazardousDetectedState
                ? "hazardous_mod_state_detected"
                : loaded.Length == 0
                        ? "connector_not_loaded"
                    : loaded.Length > 1
                        ? "additional_loaded_mods"
                        : connector == null
                            ? "connector_identity_missing"
                            : "connector_identity_mismatch";
        return new ModsetIdentity(
            status,
            fingerprint,
            FingerprintScope,
            mods,
            "The complete loaded Modset identity is recorded; current native UI mechanics remain the only actionability authority.");
    }

    private static LoadedModIdentity BuildMod(Mod mod)
    {
        LoadedModAssemblyIdentity[] assemblies = mod.assemblies
            .Select(BuildAssembly)
            .OrderBy(assembly => assembly.Name, StringComparer.Ordinal)
            .ThenBy(assembly => assembly.ModuleVersionId, StringComparer.Ordinal)
            .ToArray();
        return new LoadedModIdentity(
            mod.manifest?.id ?? "unknown",
            mod.manifest?.version,
            mod.modSource.ToString(),
            mod.state.ToString(),
            mod.manifest?.affectsGameplay ?? true,
            mod.workshopId?.ToString(CultureInfo.InvariantCulture),
            assemblies);
    }

    private static LoadedModAssemblyIdentity BuildAssembly(Assembly assembly)
    {
        AssemblyName name = assembly.GetName();
        return new LoadedModAssemblyIdentity(
            name.Name ?? "unknown",
            name.Version?.ToString(),
            assembly.ManifestModule.ModuleVersionId.ToString("D"));
    }
}
