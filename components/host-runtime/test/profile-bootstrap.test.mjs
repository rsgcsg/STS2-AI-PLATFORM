import assert from "node:assert/strict";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import {
  evaluateProfileBootstrap,
  readNativeSettingsSchema
} from "../src/profile-bootstrap.mjs";

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

test("bootstrap proof uses isolated writes, Steam disable, and an unchanged shared profile", () => {
  assert.deepEqual(evaluateProfileBootstrap({
    settingsSchema: 8,
    steamDisabledObserved: true,
    sharedProfileSentinel: { unchanged: true }
  }), {
    status: "native_profile_bootstrap_pass",
    errors: []
  });
  assert.deepEqual(evaluateProfileBootstrap({
    settingsSchema: 8,
    steamDisabledObserved: false,
    sharedProfileSentinel: { unchanged: false }
  }), {
    status: "native_profile_bootstrap_incomplete",
    errors: ["steam_disable_not_observed", "shared_profile_changed_or_unmeasured"]
  });
});
