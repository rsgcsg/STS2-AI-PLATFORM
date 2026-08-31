import assert from "node:assert/strict";
import test from "node:test";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  parseSteamLibraryFolders,
  readInstalledConnectorIdentity,
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

test("resolves the Windows runtime log under the exact user profile", () => {
  const result = resolveInstallation("E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2", {
    platform: "win32",
    arch: "x64",
    home: "C:\\Users\\player"
  });
  assert.equal(
    result.log_file,
    "C:\\Users\\player\\AppData\\Roaming\\SlayTheSpire2\\logs\\godot.log"
  );
});

test("admits an installed Connector identity only when it matches the DLL", () => {
  const directory = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-connector-"));
  const modsDir = path.join(directory, "mods");
  mkdirSync(modsDir);
  const dll = path.join(modsDir, "STS2_MCP.dll");
  writeFileSync(dll, "candidate");
  const digest = "dda18a0e21ae47c53b4309434cbc02ae8bf764fa83a6defbb719431242722aa7";
  writeFileSync(path.join(modsDir, "STS2_MCP.identity"), JSON.stringify({
    source_revision: "a".repeat(40),
    artifact_sha256: digest,
    artifact_mvid: "11111111-2222-3333-4444-555555555555",
    source_protocol: "1.0.0"
  }));
  assert.equal(readInstalledConnectorIdentity({ mods_dir: modsDir }).status, "verified");
  writeFileSync(dll, "changed");
  assert.equal(readInstalledConnectorIdentity({ mods_dir: modsDir }).status, "identity_mismatch");
  rmSync(directory, { recursive: true, force: true });
});

test("prefers the unified Platform identity over a retired Connector sidecar", () => {
  const directory = mkdtempSync(path.join(os.tmpdir(), "sts2-platform-identity-"));
  const modsDir = path.join(directory, "mods");
  mkdirSync(modsDir);
  writeFileSync(path.join(modsDir, "STS2_PLATFORM.dll"), "unified-candidate");
  writeFileSync(path.join(modsDir, "STS2_PLATFORM.identity"), JSON.stringify({
    source_revision: "b".repeat(40),
    artifact_sha256: "77626f5e8bf379d0a876cb6e6926209c416bc05625d450b69f713610f0dd94f9",
    artifact_mvid: "11111111-2222-3333-4444-555555555555"
  }));
  writeFileSync(path.join(modsDir, "STS2_MCP.identity"), JSON.stringify({
    source_revision: "a".repeat(40),
    artifact_sha256: "0".repeat(64)
  }));
  const result = readInstalledConnectorIdentity({ mods_dir: modsDir });
  assert.equal(result.status, "verified");
  assert.equal(result.identity.source_revision, "b".repeat(40));
  assert.match(result.identity_file, /STS2_PLATFORM\.identity$/u);
  rmSync(directory, { recursive: true, force: true });
});
