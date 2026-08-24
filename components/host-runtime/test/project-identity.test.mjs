import assert from "node:assert/strict";
import test from "node:test";
import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { calculateSourceDigest, readProjectIdentity } from "../src/project-identity.mjs";

test("source digest is deterministic and ignores local evidence", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-source-"));
  writeFileSync(path.join(root, "a.txt"), "one");
  const first = calculateSourceDigest(root);
  mkdirSync(path.join(root, ".local"));
  writeFileSync(path.join(root, ".local", "evidence.json"), "private");
  assert.deepEqual(calculateSourceDigest(root), first);
  writeFileSync(path.join(root, "a.txt"), "two");
  assert.notEqual(calculateSourceDigest(root).sha256, first.sha256);
});

test("git source digest ignores generated files while retaining untracked source", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-git-source-"));
  execFileSync("git", ["init", "-q"], { cwd: root });
  writeFileSync(path.join(root, ".gitignore"), "__pycache__/\nbin/\n");
  writeFileSync(path.join(root, "tracked.txt"), "tracked");
  execFileSync("git", ["add", ".gitignore", "tracked.txt"], { cwd: root });
  const first = calculateSourceDigest(root);

  mkdirSync(path.join(root, "__pycache__"));
  writeFileSync(path.join(root, "__pycache__", "module.pyc"), "generated");
  mkdirSync(path.join(root, "bin"));
  writeFileSync(path.join(root, "bin", "artifact.dll"), "generated");
  assert.deepEqual(calculateSourceDigest(root), first);

  writeFileSync(path.join(root, "new-source.txt"), "untracked but relevant");
  assert.notDeepEqual(calculateSourceDigest(root), first);
});

test("installed package identity is exact without requiring a parent Git checkout", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-host-runtime-package-"));
  writeFileSync(path.join(root, "package.json"), JSON.stringify({
    name: "@rsgcsg/sts2-host-runtime",
    version: "1.1.0-rc.2"
  }));
  writeFileSync(path.join(root, "runtime.mjs"), "export const ready = true;\n");

  const identity = readProjectIdentity(root);
  assert.equal(identity.distribution_kind, "installed_package");
  assert.equal(identity.package_name, "@rsgcsg/sts2-host-runtime");
  assert.equal(identity.version, "1.1.0-rc.2");
  assert.equal(identity.source_revision, null);
  assert.equal(identity.component_tree_revision, null);
  assert.match(identity.source_digest_sha256, /^[a-f0-9]{64}$/u);
});
