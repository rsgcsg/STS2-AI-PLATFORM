import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import {
  compareFilesystemSnapshots,
  sharedGameUserDataRoot,
  snapshotFilesystemTree
} from "../src/filesystem-sentinel.mjs";

test("resolves the shared STS2 user-data root without using an isolated profile", () => {
  assert.equal(
    sharedGameUserDataRoot({ platform: "win32", environment: { APPDATA: "C:\\Users\\player\\AppData\\Roaming" } }),
    "C:\\Users\\player\\AppData\\Roaming\\SlayTheSpire2"
  );
  assert.equal(
    sharedGameUserDataRoot({ platform: "linux", environment: {}, home: "/home/player" }),
    "/home/player/.local/share/SlayTheSpire2"
  );
});

test("filesystem sentinel detects content and metadata mutations", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-sentinel-"));
  try {
    mkdirSync(path.join(root, "default"));
    const settings = path.join(root, "default", "settings.save");
    writeFileSync(settings, "before");
    const before = snapshotFilesystemTree(root);
    const same = snapshotFilesystemTree(root);
    assert.equal(compareFilesystemSnapshots(before, same).unchanged, true);
    writeFileSync(settings, "after");
    const after = snapshotFilesystemTree(root);
    assert.equal(compareFilesystemSnapshots(before, after).unchanged, false);
    assert.equal(after.file_count, 1);
    assert.equal(after.total_file_bytes, 5);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("filesystem sentinel represents an absent profile without creating it", () => {
  const root = path.join(os.tmpdir(), `sts2-headless-missing-${Date.now()}`);
  const snapshot = snapshotFilesystemTree(root);
  assert.equal(snapshot.present, false);
  assert.equal(snapshot.tree_sha256, null);
});
