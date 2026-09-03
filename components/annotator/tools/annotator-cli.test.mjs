import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { resolveCliPath } from "./cli-paths.mjs";

const root = path.resolve(import.meta.dirname, "..");

test("evidence CLI paths are resolved from the caller working directory", () => {
  const caller = path.join(path.parse(root).root, "workspace");

  assert.equal(
    resolveCliPath("components/annotator/.local/recordings/session-1", caller),
    path.join(caller, "components/annotator/.local/recordings/session-1")
  );
  assert.equal(resolveCliPath(path.join(caller, "absolute"), root), path.join(caller, "absolute"));
});

test("Annotator CLI exposes portable and exact-game entry points", () => {
  const result = spawnSync(process.execPath, [path.join(import.meta.dirname, "annotator.mjs"), "--help"], {
    encoding: "utf8"
  });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /Exact-game lifecycle:/u);
  assert.match(result.stdout, /pack-session/u);
});

test("Annotator package, native Mod, and Connector dependency versions agree", () => {
  const packageMetadata = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
  const manifest = JSON.parse(fs.readFileSync(
    path.join(root, "src", "STS2HumanAnnotator.Mod", "mod_manifest.json"),
    "utf8"
  ));
  const contracts = fs.readFileSync(
    path.join(root, "src", "STS2HumanAnnotator.Core", "Contracts.cs"),
    "utf8"
  );
  const connectorManifest = JSON.parse(fs.readFileSync(
    path.join(root, "..", "connector", "host", "mod_manifest.json"),
    "utf8"
  ));
  const nativeVersion = contracts.match(/public const string ProductVersion = "([^"]+)";/u)?.[1];
  const connectorDependency = manifest.dependencies.find(({ id }) => id === "STS2_MCP");

  assert.equal(manifest.version, packageMetadata.version);
  assert.equal(nativeVersion, packageMetadata.version);
  assert.equal(connectorDependency?.min_version, connectorManifest.version);
});
