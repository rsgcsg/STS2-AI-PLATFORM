import assert from "node:assert/strict";
import test from "node:test";
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
