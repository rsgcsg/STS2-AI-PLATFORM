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

test("captures one native profile and instantiates independent generations", () => {
  const root = mkdtempSync(path.join(os.tmpdir(), "sts2-profile-template-"));
  try {
    const source = isolatedProfileLaunch(root, "source", "win32");
    mkdirSync(path.dirname(nativeSettings(source)), { recursive: true });
    writeFileSync(nativeSettings(source), JSON.stringify({ schema_version: 8, mods: true }));
    const captured = captureProfileTemplate({
      localRoot: root,
      profileId: "source",
      templateId: "clean",
      gameIdentity: { assembly_sha256: "exact" }
    });
    const first = instantiateProfileTemplate({
      localRoot: root,
      templateId: "clean",
      profileId: "worker-a"
    });
    const second = instantiateProfileTemplate({
      localRoot: root,
      templateId: "clean",
      profileId: "worker-b"
    });
    assert.notEqual(first.generation_id, second.generation_id);
    assert.equal(JSON.parse(readFileSync(nativeSettings(isolatedProfileLaunch(
      root, "worker-a", "win32"
    )), "utf8")).schema_version, 8);
    assert.equal(captured.payload_sha256, first.template_payload_sha256);
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
      gameIdentity: {}
    });
    const template = profileTemplatePaths(root, "clean");
    writeFileSync(path.join(template.home, "drift.txt"), "changed");
    assert.throws(() => instantiateProfileTemplate({
      localRoot: root,
      templateId: "clean",
      profileId: "worker"
    }), /does not match/u);
    assert.equal(existsSync(path.join(root, "profiles", "worker")), false);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
