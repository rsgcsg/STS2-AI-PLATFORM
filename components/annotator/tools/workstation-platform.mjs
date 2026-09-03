import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";

export async function loadHostRuntimeWorkstationApi(annotatorRoot) {
  const hostRuntimeRoot = path.resolve(annotatorRoot, "..", "host-runtime");
  const workstationModule = path.join(hostRuntimeRoot, "src", "workstation-api.mjs");
  if (!fs.existsSync(workstationModule)) return null;

  const workstation = await import(pathToFileURL(workstationModule).href);
  return {
    source: "platform_host_runtime",
    host_runtime_root: hostRuntimeRoot,
    discoverGameDirectory: workstation.discoverGameDirectory,
    resolveInstallation: workstation.resolveInstallation,
    readDiskIdentity: workstation.readDiskIdentity,
    listGameProcesses: workstation.listGameProcesses,
    processCommand: workstation.processCommand
  };
}

export function defaultGameDirectory({
  env = process.env,
  platform = process.platform,
  home = os.homedir()
} = {}) {
  const platformPath = platform === "win32" ? path.win32 : path.posix;
  if (env.STS2_GAME_DIR) return platformPath.resolve(env.STS2_GAME_DIR);
  if (platform === "darwin") {
    return platformPath.join(
      home,
      "Library",
      "Application Support",
      "Steam",
      "steamapps",
      "common",
      "Slay the Spire 2"
    );
  }
  if (platform === "win32") {
    const steamRoot = env.STEAM_PATH
      || (env["ProgramFiles(x86)"]
        ? platformPath.join(env["ProgramFiles(x86)"], "Steam")
        : null)
      || (env.ProgramFiles ? platformPath.join(env.ProgramFiles, "Steam") : null)
      || "C:\\Program Files (x86)\\Steam";
    return platformPath.join(
      steamRoot,
      "steamapps",
      "common",
      "Slay the Spire 2"
    );
  }
  return platformPath.join(
    home,
    ".local",
    "share",
    "Steam",
    "steamapps",
    "common",
    "Slay the Spire 2"
  );
}

export function fallbackInstallation(gameDirectory, {
  platform = process.platform,
  arch = process.arch,
  home = os.homedir()
} = {}) {
  const platformPath = platform === "win32" ? path.win32 : path.posix;
  const gameDir = platformPath.resolve(gameDirectory);
  if (platform === "darwin") {
    const contents = platformPath.join(gameDir, "SlayTheSpire2.app", "Contents");
    const runtimeArch = arch === "x64" ? "x86_64" : "arm64";
    return {
      game_dir: gameDir,
      executable: platformPath.join(contents, "MacOS", "Slay the Spire 2"),
      executable_cwd: platformPath.join(contents, "MacOS"),
      mods_dir: platformPath.join(contents, "MacOS", "mods"),
      data_dir: platformPath.join(contents, "Resources", `data_sts2_macos_${runtimeArch}`),
      release_info: platformPath.join(contents, "Resources", "release_info.json"),
      log_file: platformPath.join(
        home,
        "Library",
        "Application Support",
        "SlayTheSpire2",
        "logs",
        "godot.log"
      )
    };
  }
  if (platform === "win32") {
    return {
      game_dir: gameDir,
      executable: platformPath.join(gameDir, "SlayTheSpire2.exe"),
      executable_cwd: gameDir,
      mods_dir: platformPath.join(gameDir, "mods"),
      data_dir: platformPath.join(gameDir, "data_sts2_windows_x86_64"),
      release_info: platformPath.join(gameDir, "release_info.json"),
      log_file: null
    };
  }
  return {
    game_dir: gameDir,
    executable: platformPath.join(gameDir, "SlayTheSpire2"),
    executable_cwd: gameDir,
    mods_dir: platformPath.join(gameDir, "mods"),
    data_dir: platformPath.join(gameDir, "data_sts2_linuxbsd_x86_64"),
    release_info: platformPath.join(gameDir, "release_info.json"),
    log_file: platformPath.join(home, ".local", "share", "SlayTheSpire2", "logs", "godot.log")
  };
}

export function resolveWorkstationInstallation({
  headlessApi,
  env = process.env,
  platform = process.platform,
  arch = process.arch,
  home = os.homedir()
}) {
  const override = env.STS2_GAME_DIR?.trim();
  if (headlessApi) {
    const gameDirectory = override
      ? (platform === "win32" ? path.win32 : path.posix).resolve(override)
      : headlessApi.discoverGameDirectory({ env, platform, home });
    if (gameDirectory) {
      return {
        ...headlessApi.resolveInstallation(gameDirectory, { platform, arch }),
        discovery_method: override ? "sts2_game_dir" : headlessApi.source
      };
    }
  }
  return {
    ...fallbackInstallation(defaultGameDirectory({ env, platform, home }), {
      platform,
      arch,
      home
    }),
    discovery_method: override ? "sts2_game_dir" : "platform_default"
  };
}

export function resolveConnectorCanaryEnvironment({
  compatibility,
  connectorBuild,
  gameRelease,
  gameIdentity,
  platform = process.platform,
  architecture = process.arch
}) {
  const sourceRevision = connectorBuild?.source_revision;
  if (!/^[0-9a-f]{40}$/u.test(sourceRevision ?? "")) {
    throw new Error("Connector build identity lacks an exact source revision.");
  }
  // release_info.main_assembly_hash is release metadata and is not the runtime
  // assembly hash used by Connector compatibility. Exact selected assembly bytes
  // are already pinned here by SHA-256 + MVID; do not compare the two hash domains.
  const runtime = compatibility?.runtimes?.find((candidate) =>
    candidate.platform === platform
      && candidate.architecture === architecture
      && candidate.game_version === gameRelease?.version
      && candidate.game_commit === gameRelease?.commit
      && candidate.main_assembly_sha256 === gameIdentity?.sha256
      && candidate.main_assembly_mvid === gameIdentity?.module_version_id
  );
  if (!runtime) {
    throw new Error("The installed STS2 identity is absent from Connector compatibility.");
  }
  if (!["supported_exact", "candidate_exact"].includes(runtime.status)) {
    throw new Error(`Connector runtime status is not launch-admissible: ${runtime.status}`);
  }

  const environment = {
    [compatibility.artifact_canary_environment_variable]: sourceRevision
  };
  if (runtime.status === "candidate_exact") {
    environment[compatibility.canary_environment_variable] = runtime.id;
  }
  return { runtime, environment };
}

export function commandMatchesExecutable(command, executable) {
  if (typeof command !== "string" || typeof executable !== "string") return false;
  const normalize = (value) => value.replaceAll("/", "\\").toLowerCase();
  return normalize(command).includes(normalize(executable));
}

export function prepareExactWindowsModSettings({
  settings,
  enabledModIds,
  allowedPreviouslyEnabledModIds
}) {
  if (settings == null || typeof settings !== "object" || Array.isArray(settings)
      || settings.mod_settings == null
      || typeof settings.mod_settings !== "object"
      || Array.isArray(settings.mod_settings)
      || !Array.isArray(settings.mod_settings.mod_list)) {
    throw new Error("Windows settings have an unexpected mod_settings shape.");
  }
  if (!Array.isArray(enabledModIds) || enabledModIds.length === 0
      || new Set(enabledModIds).size !== enabledModIds.length) {
    throw new Error("Exact enabled Mod IDs must be a non-empty unique list.");
  }
  const allowed = new Set(allowedPreviouslyEnabledModIds);
  const unexpected = settings.mod_settings.mod_list
    .filter((entry) => entry?.is_enabled === true && !allowed.has(entry.id))
    .map((entry) => entry.id ?? "unidentified");
  if (unexpected.length) {
    throw new Error(`Refusing to preserve an enabled non-admitted Modset: ${unexpected.join(", ")}`);
  }
  const managed = new Set(allowedPreviouslyEnabledModIds);
  const retained = settings.mod_settings.mod_list.filter((entry) => !managed.has(entry?.id));
  return {
    ...settings,
    mod_settings: {
      ...settings.mod_settings,
      mod_list: [
        ...allowedPreviouslyEnabledModIds.map((id) => ({
          id,
          is_enabled: enabledModIds.includes(id),
          source: "mods_directory"
        })),
        ...retained
      ],
      mods_enabled: true
    }
  };
}

export function prepareSoleWindowsModSettings({ settings, enabledModId }) {
  if (settings == null || typeof settings !== "object" || Array.isArray(settings)
      || settings.mod_settings == null
      || typeof settings.mod_settings !== "object"
      || Array.isArray(settings.mod_settings)
      || !Array.isArray(settings.mod_settings.mod_list)) {
    throw new Error("Windows settings have an unexpected mod_settings shape.");
  }
  if (typeof enabledModId !== "string" || enabledModId.length === 0) {
    throw new Error("The sole enabled Mod ID must be explicit.");
  }
  const existing = settings.mod_settings.mod_list.find((entry) => entry?.id === enabledModId);
  const retained = settings.mod_settings.mod_list
    .filter((entry) => entry?.id !== enabledModId)
    .map((entry) => ({ ...entry, is_enabled: false }));
  return {
    ...settings,
    mod_settings: {
      ...settings.mod_settings,
      mod_list: [
        { ...existing, id: enabledModId, is_enabled: true, source: "mods_directory" },
        ...retained
      ],
      mods_enabled: true
    }
  };
}

export function resolveWindowsSteamSettings({
  env = process.env,
  platform = process.platform,
  expectedSchema
} = {}) {
  if (platform !== "win32") return null;
  const roaming = env.APPDATA;
  if (!roaming) throw new Error("APPDATA is unavailable; cannot resolve Windows STS2 settings.");
  const steamRoot = path.win32.join(roaming, "SlayTheSpire2", "steam");
  if (!fs.existsSync(steamRoot)) {
    throw new Error(`Windows STS2 Steam settings root is absent: ${steamRoot}`);
  }
  const candidates = fs.readdirSync(steamRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => path.win32.join(steamRoot, entry.name, "settings.save"))
    .filter(fs.existsSync);
  if (candidates.length !== 1) {
    throw new Error(`Expected exactly one Windows Steam settings.save, observed ${candidates.length}.`);
  }
  const file = candidates[0];
  const value = JSON.parse(fs.readFileSync(file, "utf8").replace(/^\uFEFF/u, ""));
  if (expectedSchema != null && value.schema_version !== expectedSchema) {
    throw new Error(
      `Windows settings schema drift: expected ${expectedSchema}, observed ${value.schema_version}.`
    );
  }
  if (value.mod_settings == null
      || typeof value.mod_settings !== "object"
      || Array.isArray(value.mod_settings)
      || !Array.isArray(value.mod_settings.mod_list)) {
    throw new Error("Windows settings have an unexpected mod_settings shape.");
  }
  return { file, value };
}

function sameArtifactIdentity(left, right) {
  return left?.sha256 === right?.sha256
    && left?.module_version_id === right?.module_version_id;
}

export function normalizeInstalledProvenance({
  provenance,
  currentGame,
  connectorBuild,
  platform = process.platform,
  architecture = process.arch
}) {
  if (provenance?.platform === platform && provenance?.architecture === architecture) {
    return { provenance, compatibility: "native" };
  }
  const legacyMac = provenance?.schema_version === 1
    && provenance?.platform == null
    && provenance?.architecture == null
    && platform === "darwin"
    && architecture === "arm64";
  if (!legacyMac) {
    throw new Error("Installed provenance belongs to a different platform or architecture.");
  }
  if (!sameArtifactIdentity(provenance.game?.sts2_identity, currentGame?.sts2_identity)) {
    throw new Error("Legacy macOS provenance no longer matches the exact STS2 assembly.");
  }
  if (provenance.connector_artifact?.sha256 !== connectorBuild?.artifact_sha256
      || provenance.connector_artifact?.module_version_id !== connectorBuild?.artifact_mvid) {
    throw new Error("Legacy macOS provenance no longer matches the exact Connector build.");
  }
  if (!/^[0-9a-f]{40}$/u.test(connectorBuild?.source_revision ?? "")
      || !/^[0-9a-f]{64}$/u.test(connectorBuild?.player_environment_source_digest ?? "")) {
    throw new Error("Connector build identity cannot upgrade legacy macOS provenance.");
  }
  return {
    compatibility: "legacy_macos_v1_derived_exact",
    provenance: {
      ...provenance,
      platform,
      architecture,
      game: {
        ...provenance.game,
        release: currentGame.release,
        executable: currentGame.executable
      },
      connector_build: connectorBuild
    }
  };
}

export function normalizeExactModsetCanary({
  canary,
  installed,
  connectorRuntime,
  platform = process.platform
}) {
  if (canary?.schema_version === 2) return canary;
  const legacyMac = installed.provenanceCompatibility === "legacy_macos_v1_derived_exact"
    && platform === "darwin"
    && canary?.schema_version == null
    && /^[0-9a-f]{64}$/u.test(canary?.modset_fingerprint ?? "")
    && canary?.connector_source_revision
      === installed.provenance.connector_build.source_revision;
  if (!legacyMac) {
    throw new Error("Exact Modset canary schema is unsupported for this installed process envelope.");
  }
  return {
    ...canary,
    schema_version: 2,
    connector_game_id: connectorRuntime.status === "candidate_exact"
      ? connectorRuntime.id
      : null,
    connector_artifact: installed.installedConnector,
    annotator_source_revision: installed.provenance.source_revision,
    annotator_source_digest_sha256: installed.provenance.source_digest_sha256,
    annotator_artifact: installed.installedAnnotator,
    game_release: installed.currentGame.release,
    game_executable_sha256: installed.currentGame.executable.sha256,
    game_sts2_identity: installed.currentGame.sts2_identity
  };
}
