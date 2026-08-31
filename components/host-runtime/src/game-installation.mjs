import { createHash } from "node:crypto";
import { existsSync, readFileSync, statSync } from "node:fs";
import os from "node:os";
import path from "node:path";

export const STS2_APP_ID = "2868840";

export function parseSteamLibraryFolders(content) {
  return [...content.matchAll(/"path"\s+"([^"]+)"/gu)]
    .map((match) => match[1].replace(/\\\\/gu, "\\"));
}

export function discoverGameDirectory({
  env = process.env,
  platform = process.platform,
  home = os.homedir()
} = {}) {
  if (env.STS2_GAME_DIR) return path.resolve(env.STS2_GAME_DIR);
  const steamRoots = platform === "darwin"
    ? [path.join(home, "Library/Application Support/Steam")]
    : platform === "linux"
      ? [path.join(home, ".steam/steam"), path.join(home, ".local/share/Steam")]
      : [env.STEAM_PATH, "C:\\Program Files (x86)\\Steam", "C:\\Program Files\\Steam"]
          .filter(Boolean);

  for (const root of [...new Set(steamRoots.map((entry) => path.resolve(entry)))]) {
    const libraries = [root];
    const libraryFile = path.join(root, "steamapps", "libraryfolders.vdf");
    if (existsSync(libraryFile)) {
      libraries.push(...parseSteamLibraryFolders(readFileSync(libraryFile, "utf8")));
    }
    for (const library of [...new Set(libraries.map((entry) => path.resolve(entry)))]) {
      const manifest = path.join(library, "steamapps", `appmanifest_${STS2_APP_ID}.acf`);
      const game = path.join(library, "steamapps", "common", "Slay the Spire 2");
      if (existsSync(manifest) && existsSync(game)) return game;
    }
  }
  return null;
}

export function resolveInstallation(gameDirectory, {
  platform = process.platform,
  arch = process.arch,
  home = os.homedir()
} = {}) {
  if (!gameDirectory) return null;
  const platformPath = platform === "win32" ? path.win32 : path.posix;
  const gameDir = platformPath.resolve(gameDirectory);
  if (platform === "darwin") {
    const app = platformPath.join(gameDir, "SlayTheSpire2.app", "Contents");
    const runtimeArch = arch === "x64" ? "x86_64" : "arm64";
    return {
      game_dir: gameDir,
      executable: platformPath.join(app, "MacOS", "Slay the Spire 2"),
      executable_cwd: platformPath.join(app, "MacOS"),
      mods_dir: platformPath.join(app, "MacOS", "mods"),
      data_dir: platformPath.join(app, "Resources", `data_sts2_macos_${runtimeArch}`),
      release_info: platformPath.join(app, "Resources", "release_info.json"),
      log_file: platformPath.join(home, "Library/Application Support/SlayTheSpire2/logs/godot.log")
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
      log_file: platformPath.join(home, "AppData", "Roaming", "SlayTheSpire2", "logs", "godot.log")
    };
  }
  const executable = ["SlayTheSpire2", "Slay the Spire 2"]
    .map((name) => platformPath.join(gameDir, name))
    .find(existsSync) ?? platformPath.join(gameDir, "SlayTheSpire2");
  return {
    game_dir: gameDir,
    executable,
    executable_cwd: gameDir,
    mods_dir: platformPath.join(gameDir, "mods"),
    data_dir: platformPath.join(gameDir, "data_sts2_linuxbsd_x86_64"),
    release_info: platformPath.join(gameDir, "release_info.json"),
    log_file: platformPath.join(home, ".local/share/SlayTheSpire2/logs/godot.log")
  };
}

export function sha256File(file) {
  if (!file || !existsSync(file)) return null;
  return createHash("sha256").update(readFileSync(file)).digest("hex");
}

export function readInstalledConnectorIdentity(installation) {
  const candidates = installation?.mods_dir
    ? [
        {
          identityFile: path.join(installation.mods_dir, "STS2_PLATFORM.identity"),
          dll: path.join(installation.mods_dir, "STS2_PLATFORM.dll")
        },
        {
          identityFile: path.join(installation.mods_dir, "STS2_MCP.identity"),
          dll: path.join(installation.mods_dir, "STS2_MCP.dll")
        }
      ]
    : [];
  const candidate = candidates.find(({ identityFile }) => existsSync(identityFile)) ?? candidates[0];
  const identityFile = candidate?.identityFile ?? null;
  const dll = candidate?.dll ?? null;
  if (!identityFile || !existsSync(identityFile)) {
    return { status: "missing", identity_file: identityFile, identity: null };
  }
  let identity;
  try {
    identity = JSON.parse(readFileSync(identityFile, "utf8"));
  } catch {
    return { status: "invalid_json", identity_file: identityFile, identity: null };
  }
  const installedSha = sha256File(dll);
  const sourceRevision = identity?.source_revision;
  const validRevision = typeof sourceRevision === "string"
    && /^[a-f0-9]{40}$/u.test(sourceRevision);
  if (!validRevision || installedSha == null || identity?.artifact_sha256 !== installedSha) {
    return {
      status: "identity_mismatch",
      identity_file: identityFile,
      identity,
      installed_sha256: installedSha
    };
  }
  return {
    status: "verified",
    identity_file: identityFile,
    identity,
    installed_sha256: installedSha
  };
}

export function sts2RuntimeAssemblyHash(file) {
  if (!file || !existsSync(file)) return null;
  const digest = createHash("sha1").update(readFileSync(file)).digest();
  return digest.readInt32LE(0);
}

function fileIdentity(file) {
  if (!file || !existsSync(file)) return { path: file, exists: false };
  const stat = statSync(file);
  return {
    path: file,
    exists: true,
    size: stat.size,
    modified_at: stat.mtime.toISOString(),
    sha256: sha256File(file)
  };
}

export function readDiskIdentity(installation) {
  if (!installation) return null;
  const release = existsSync(installation.release_info)
    ? JSON.parse(readFileSync(installation.release_info, "utf8"))
    : null;
  const sts2AssemblyPath = path.join(installation.data_dir, "sts2.dll");
  const runtimeMainAssemblyHash = sts2RuntimeAssemblyHash(sts2AssemblyPath);
  return {
    platform: process.platform,
    architecture: process.arch,
    game_dir: installation.game_dir,
    release_info_path: installation.release_info,
    release,
    executable: fileIdentity(installation.executable),
    release_declared_main_assembly_hash: release?.main_assembly_hash ?? null,
    runtime_main_assembly_hash: runtimeMainAssemblyHash,
    release_hash_matches_runtime_assembly:
      release?.main_assembly_hash == null || runtimeMainAssemblyHash == null
        ? null
        : release.main_assembly_hash === runtimeMainAssemblyHash,
    sts2_assembly: fileIdentity(sts2AssemblyPath),
    godotsharp_assembly: fileIdentity(path.join(installation.data_dir, "GodotSharp.dll"))
  };
}
