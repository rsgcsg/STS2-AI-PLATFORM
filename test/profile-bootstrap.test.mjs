import assert from "node:assert/strict";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { readNativeSettingsSchema } from "../src/profile-bootstrap.mjs";

test("admits only a valid positive native settings schema", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-native-settings-"));
  try {
    const file = path.join(root, "settings.save");
    assert.equal(readNativeSettingsSchema(file), null);
    writeFileSync(file, "not-json");
    assert.equal(readNativeSettingsSchema(file), null);
    writeFileSync(file, JSON.stringify({ schema_version: 0 }));
    assert.equal(readNativeSettingsSchema(file), null);
    writeFileSync(file, JSON.stringify({ schema_version: 8 }));
    assert.equal(readNativeSettingsSchema(file), 8);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
