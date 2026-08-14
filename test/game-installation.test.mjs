import assert from "node:assert/strict";
import test from "node:test";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  parseSteamLibraryFolders,
  resolveInstallation,
  sts2RuntimeAssemblyHash
} from "../src/game-installation.mjs";

test("parses Steam library paths", () => {
  const content = `"libraryfolders" { "0" { "path" "/games/Steam" } "1" { "path" "D:\\\\Steam" } }`;
  assert.deepEqual(parseSteamLibraryFolders(content), ["/games/Steam", "D:\\Steam"]);
});

test("computes the same signed little-endian SHA-1 prefix used by STS2", () => {
  const directory = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-hash-"));
  const file = path.join(directory, "sts2.dll");
  writeFileSync(file, "fixture");
  assert.equal(sts2RuntimeAssemblyHash(file), -1040986287);
  rmSync(directory, { recursive: true, force: true });
});

test("resolves the macOS shipped executable and architecture data directory", () => {
  const result = resolveInstallation("/games/Slay the Spire 2", {
    platform: "darwin",
    arch: "arm64"
  });
  assert.equal(result.executable, "/games/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2");
  assert.match(result.data_dir, /data_sts2_macos_arm64$/u);
});
