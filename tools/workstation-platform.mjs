import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";

export async function loadHeadlessWorkstationApi(annotatorRoot) {
  const headlessRoot = path.resolve(annotatorRoot, "..", "STS2-headless");
  const installationModule = path.join(headlessRoot, "src", "game-installation.mjs");
  const runtimeModule = path.join(headlessRoot, "src", "runtime-probe.mjs");
  const hostModule = path.join(headlessRoot, "src", "headless-host.mjs");
  if (![installationModule, runtimeModule, hostModule].every(fs.existsSync)) return null;

  const [installation, runtime, host] = await Promise.all([
    import(pathToFileURL(installationModule).href),
    import(pathToFileURL(runtimeModule).href),
    import(pathToFileURL(hostModule).href)
  ]);
  return {
    source: "sibling_sts2_headless",
    headless_root: headlessRoot,
    discoverGameDirectory: installation.discoverGameDirectory,
    resolveInstallation: installation.resolveInstallation,
    readDiskIdentity: installation.readDiskIdentity,
    listGameProcesses: runtime.listGameProcesses,
    processCommand: host.processCommand
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
  const runtime = compatibility?.runtimes?.find((candidate) =>
    candidate.platform === platform
      && candidate.architecture === architecture
      && candidate.game_version === gameRelease?.version
      && candidate.game_commit === gameRelease?.commit
      && candidate.runtime_main_assembly_hash === gameRelease?.main_assembly_hash
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
