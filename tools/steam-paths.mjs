import { existsSync, readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import os from "node:os";
import path from "node:path";

export const STS2_APP_ID = "2868840";

export function resolveGameDir(env = process.env, platform = process.platform, home = os.homedir()) {
  if (env.STS2_GAME_DIR) return path.resolve(env.STS2_GAME_DIR);
  const discovered = discoverSteamGameDir(platform, home, env);
  if (discovered) return discovered;
  if (platform === "darwin") {
    return path.join(home, "Library/Application Support/Steam/steamapps/common/Slay the Spire 2");
  }
  if (platform === "linux") {
    return path.join(home, ".steam/steam/steamapps/common/Slay the Spire 2");
  }
  throw new Error("Could not locate the STS2 Steam installation; set STS2_GAME_DIR explicitly.");
}

export function discoverSteamGameDir(platform, home, env = process.env) {
  const roots = [];
  if (platform === "win32") {
    if (env.STEAM_PATH) roots.push(env.STEAM_PATH);
    const registry = spawnSync(
      "reg",
      ["query", "HKCU\\Software\\Valve\\Steam", "/v", "SteamPath"],
      { encoding: "utf8", stdio: "pipe" }
    );
    const registryPath = registry.status === 0
      ? registry.stdout.match(/SteamPath\s+REG_SZ\s+(.+)$/imu)?.[1]?.trim()
      : null;
    if (registryPath) roots.push(registryPath);
    roots.push("C:\\Program Files (x86)\\Steam", "C:\\Program Files\\Steam");
  } else if (platform === "darwin") {
    roots.push(path.join(home, "Library/Application Support/Steam"));
  } else if (platform === "linux") {
    roots.push(path.join(home, ".steam/steam"), path.join(home, ".local/share/Steam"));
  }

  for (const root of [...new Set(roots.map((value) => path.resolve(value)))]) {
    const libraries = [root];
    const libraryFile = path.join(root, "steamapps", "libraryfolders.vdf");
    if (existsSync(libraryFile)) {
      for (const match of readFileSync(libraryFile, "utf8").matchAll(/"path"\s+"([^"]+)"/gu)) {
        libraries.push(match[1].replace(/\\\\/gu, "\\"));
      }
    }
    for (const library of [...new Set(libraries)]) {
      const manifest = path.join(library, "steamapps", `appmanifest_${STS2_APP_ID}.acf`);
      const game = path.join(library, "steamapps", "common", "Slay the Spire 2");
      if (existsSync(manifest) && existsSync(game)) return path.resolve(game);
    }
  }
  return null;
}

export function resolveModsDir(gameDir, platform = process.platform) {
  if (platform === "win32") return path.win32.join(gameDir, "mods");
  return platform === "darwin"
    ? path.posix.join(gameDir, "SlayTheSpire2.app/Contents/MacOS/mods")
    : path.posix.join(gameDir, "mods");
}
