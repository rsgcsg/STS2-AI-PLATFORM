import assert from "node:assert/strict";
import test from "node:test";
import { packageEntrypointErrors, textBoundaryErrors } from "./check-boundaries.mjs";

test("active production code rejects predecessor release authority", () => {
  const errors = textBoundaryErrors(
    "components/host-runtime/src/release.mjs",
    'const url = "https://github.com/rsgcsg/STS2-Connector/releases/download/v1/file";'
  );
  assert.equal(errors.length, 1);
  assert.match(errors[0], /predecessor/u);
});

test("migration provenance may name a predecessor repository", () => {
  assert.deepEqual(textBoundaryErrors(
    "migration/source-manifest.json",
    '"url": "https://github.com/rsgcsg/STS2-Connector"'
  ), []);
});

test("production code rejects user-specific paths and reverse component imports", () => {
  assert.equal(textBoundaryErrors(
    "components/connector/tools/bad.mjs",
    'import value from "../../host-runtime/src/value.mjs"; const root = "/Users/dev/project";'
  ).length, 2);
});

test("declared package entrypoints must exist in the Git source authority", () => {
  const packageJson = { bin: { "fixture-tool": "bin/tool.mjs" } };
  assert.deepEqual(packageEntrypointErrors(
    "apps/fixture/package.json",
    packageJson,
    new Set(),
    () => true
  ), ["apps/fixture/package.json: bin target is not tracked: apps/fixture/bin/tool.mjs"]);
  assert.deepEqual(packageEntrypointErrors(
    "apps/fixture/package.json",
    packageJson,
    new Set(["apps/fixture/bin/tool.mjs"]),
    () => false
  ), ["apps/fixture/package.json: bin target is missing: apps/fixture/bin/tool.mjs"]);
  assert.deepEqual(packageEntrypointErrors(
    "apps/fixture/package.json",
    packageJson,
    new Set(["apps/fixture/bin/tool.mjs"]),
    () => true
  ), []);
});
