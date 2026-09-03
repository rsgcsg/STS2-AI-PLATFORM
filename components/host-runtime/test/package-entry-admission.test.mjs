import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import {
  moduleSpecifierForPath,
  packageEntryHasLaunchAuthority
} from "../tools/package-entry-admission.mjs";

test("Windows validates declared bins against Git executable authority", () => {
  assert.equal(packageEntryHasLaunchAuthority({
    platform: "win32",
    npmMode: 0o644,
    gitMode: "100755",
    declaredBin: true
  }), true);
  assert.equal(packageEntryHasLaunchAuthority({
    platform: "win32",
    npmMode: 0o644,
    gitMode: "100644",
    declaredBin: true
  }), false);
  assert.equal(packageEntryHasLaunchAuthority({
    platform: "win32",
    npmMode: 0o644,
    gitMode: "100755",
    declaredBin: false
  }), false);
});

test("POSIX validates the actual npm package entry mode", () => {
  assert.equal(packageEntryHasLaunchAuthority({
    platform: "linux",
    npmMode: 0o755,
    gitMode: "100755",
    declaredBin: true
  }), true);
  assert.equal(packageEntryHasLaunchAuthority({
    platform: "darwin",
    npmMode: 0o644,
    gitMode: "100755",
    declaredBin: true
  }), false);
});

test("installed ESM imports use a portable file URL", () => {
  const file = path.resolve("node_modules", "example", "entry.mjs");
  const specifier = moduleSpecifierForPath(file);
  assert.match(specifier, /^file:\/\//u);
  assert.equal(fileURLToPath(specifier), file);
});
