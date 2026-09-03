import { pathToFileURL } from "node:url";

export function packageEntryHasLaunchAuthority({
  platform,
  npmMode,
  gitMode,
  declaredBin
}) {
  if (!declaredBin) return false;
  if (platform === "win32") return gitMode === "100755";
  return typeof npmMode === "number" && (npmMode & 0o111) !== 0;
}

export function moduleSpecifierForPath(file) {
  return pathToFileURL(file).href;
}
