import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import {
  enableConnectorModLoading,
  isolatedProfileLaunch,
  isolatedProfilePaths,
  resolveLaunchProfile,
  resetIsolatedProfile,
  validateProfileId
} from "../src/profile-isolation.mjs";

test("constructs a process-local Windows Godot user-data namespace", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-profile-win-"));
  try {
    const launch = isolatedProfileLaunch(root, "worker-01", "win32");
    assert.equal(launch.environment.APPDATA,
      path.join(root, "profiles", "worker-01", "home", "AppData", "Roaming"));
    assert.equal(launch.expected_user_data_root,
      path.join(root, "profiles", "worker-01", "home", "AppData", "Roaming", "SlayTheSpire2"));
    assert.deepEqual(launch.args, ["--force-steam=off", "--clientId=1"]);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("hard reset changes generation and removes only the selected namespace", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-profile-"));
  try {
    const first = isolatedProfileLaunch(root, "worker-a");
    const sibling = isolatedProfileLaunch(root, "worker-b");
    const sentinel = path.join(first.profile_root, "sentinel.txt");
    writeFileSync(sentinel, "old");
    const result = resetIsolatedProfile(root, "worker-a");
    assert.notEqual(result.generation_id, first.generation_id);
    assert.equal(existsSync(sentinel), false);
    assert.equal(existsSync(sibling.profile_root), true);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("profile IDs fail closed against traversal and ambiguous roots", () => {
  for (const value of ["", "../steam", "Worker", "a/b", "."]) {
    assert.throws(() => validateProfileId(value));
  }
  assert.throws(() => isolatedProfilePaths("C:\\tmp", "../steam", "win32"));
});

test("requires one unambiguous profile mode", () => {
  assert.throws(() => resolveLaunchProfile({ localRoot: "C:\\tmp" }), /Choose/u);
  assert.throws(() => resolveLaunchProfile({
    localRoot: "C:\\tmp",
    isolatedProfileId: "worker",
    sharedProfileAcknowledged: true
  }), /not both/u);
  assert.equal(resolveLaunchProfile({
    localRoot: "C:\\tmp",
    sharedProfileAcknowledged: true
  }).mode, "shared_steam_profile");
});

test("enables only the exact native mod-consent setting", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-consent-"));
  try {
    const launch = isolatedProfileLaunch(root, "worker", "win32");
    const settingsDirectory = path.dirname(path.join(
      launch.expected_user_data_root,
      "default", "1", "settings.save"
    ));
    const settingsFile = path.join(settingsDirectory, "settings.save");
    mkdirSync(settingsDirectory, { recursive: true });
    writeFileSync(settingsFile, JSON.stringify({
      schema_version: 8,
      language: "eng",
      mod_settings: null
    }));
    const result = enableConnectorModLoading(root, "worker", {
      expectedSettingsSchema: 8,
      acknowledgeEarlyAccessDisclaimer: true,
      platform: "win32"
    });
    const updated = JSON.parse(readFileSync(settingsFile, "utf8"));
    assert.equal(result.status, "enabled_cold_start_required");
    assert.deepEqual(updated, {
      schema_version: 8,
      language: "eng",
      mod_settings: { mods_enabled: true },
      seen_ea_disclaimer: true
    });
    assert.equal(result.acknowledgements.early_access_disclaimer, true);
    assert.equal(existsSync(result.backup_file), true);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("refuses to infer consent across a settings schema change", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-consent-schema-"));
  try {
    const launch = isolatedProfileLaunch(root, "worker", "win32");
    const settingsFile = path.join(
      launch.expected_user_data_root,
      "default", "1", "settings.save"
    );
    mkdirSync(path.dirname(settingsFile), { recursive: true });
    writeFileSync(settingsFile, JSON.stringify({ schema_version: 9, mod_settings: null }));
    assert.throws(() => enableConnectorModLoading(root, "worker", {
      expectedSettingsSchema: 8,
      platform: "win32"
    }), /schema mismatch/u);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
