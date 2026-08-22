import assert from "node:assert/strict";
import test from "node:test";
import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { calculateSourceDigest } from "../src/project-identity.mjs";

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
