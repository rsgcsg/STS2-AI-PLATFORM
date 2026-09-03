import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { isolatedProfileLaunch } from "../src/profile-isolation.mjs";
import {
  captureProfileTemplate,
  instantiateProfileTemplate,
  profileTemplatePaths
} from "../src/profile-template.mjs";

function nativeSettings(profile) {
  return path.join(profile.expected_user_data_root, "default", "1", "settings.save");
}

function exactGameIdentity(overrides = {}) {
  return {
    platform: "win32",
    architecture: "x64",
    release: { version: "v0.test", commit: "deadbeef", main_assembly_hash: 123 },
    executable: { sha256: "exe" },
    runtime_main_assembly_hash: 123,
    sts2_assembly: { sha256: "sts2" },
    godotsharp_assembly: { sha256: "godot" },
    ...overrides
  };
}

test("captures one native profile and instantiates independent generations", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-profile-template-"));
  try {
    const source = isolatedProfileLaunch(root, "source", "win32");
    mkdirSync(path.dirname(nativeSettings(source)), { recursive: true });
    writeFileSync(nativeSettings(source), JSON.stringify({ schema_version: 8, mods: true }));
    mkdirSync(path.join(source.expected_user_data_root, "logs"), { recursive: true });
    writeFileSync(path.join(source.expected_user_data_root, "logs", "godot.log"), "runtime");
    mkdirSync(path.join(source.expected_user_data_root, "sentry"), { recursive: true });
    writeFileSync(path.join(source.expected_user_data_root, "sentry", "installation_id"), "runtime");
    writeFileSync(`${nativeSettings(source)}.before-headless-mod-consent`, "local backup");
    const captured = captureProfileTemplate({
      localRoot: root,
      profileId: "source",
      templateId: "clean",
      gameIdentity: exactGameIdentity()
    });
    const first = instantiateProfileTemplate({
      localRoot: root,
      templateId: "clean",
      profileId: "worker-a",
      expectedGameIdentity: exactGameIdentity()
    });
    const second = instantiateProfileTemplate({
      localRoot: root,
      templateId: "clean",
      profileId: "worker-b",
      expectedGameIdentity: exactGameIdentity()
    });
    assert.notEqual(first.generation_id, second.generation_id);
    assert.equal(JSON.parse(readFileSync(nativeSettings(isolatedProfileLaunch(
      root, "worker-a", "win32"
    )), "utf8")).schema_version, 8);
    assert.equal(captured.payload_sha256, first.template_payload_sha256);
    assert.deepEqual(captured.files.map((file) => file.path), ["default/1/settings.save"]);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("rejects a template changed after capture", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-profile-template-drift-"));
  try {
    const source = isolatedProfileLaunch(root, "source", "win32");
    mkdirSync(path.dirname(nativeSettings(source)), { recursive: true });
    writeFileSync(nativeSettings(source), JSON.stringify({ schema_version: 8 }));
    captureProfileTemplate({
      localRoot: root,
      profileId: "source",
      templateId: "clean",
      gameIdentity: exactGameIdentity()
    });
    const template = profileTemplatePaths(root, "clean");
    writeFileSync(path.join(template.user_data, "drift.txt"), "changed");
    assert.throws(() => instantiateProfileTemplate({
      localRoot: root,
      templateId: "clean",
      profileId: "worker",
      expectedGameIdentity: exactGameIdentity()
    }), /does not match/u);
    assert.equal(existsSync(path.join(root, "profiles", "worker")), false);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("rejects a template captured from a different exact runtime", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-profile-template-identity-"));
  try {
    const source = isolatedProfileLaunch(root, "source", "win32");
    mkdirSync(path.dirname(nativeSettings(source)), { recursive: true });
    writeFileSync(nativeSettings(source), JSON.stringify({ schema_version: 8 }));
    captureProfileTemplate({
      localRoot: root,
      profileId: "source",
      templateId: "clean",
      gameIdentity: exactGameIdentity()
    });
    assert.throws(() => instantiateProfileTemplate({
      localRoot: root,
      templateId: "clean",
      profileId: "worker",
      expectedGameIdentity: exactGameIdentity({
        sts2_assembly: { sha256: "changed" }
      })
    }), /does not match the current exact runtime/u);
    assert.equal(existsSync(path.join(root, "profiles", "worker")), false);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
