import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { readAnnotatorConfiguration, effectiveAnnotatorConfiguration } from "../annotator-configuration.mjs";

test("redeployment preserves a dedicated campaign and operator status location", (t) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "annotator-config-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const file = path.join(root, "STS2_HUMAN_ANNOTATOR.conf");
  const defaults = { recording_root: path.join(root, "checkout-recordings"), runtime_status_path: path.join(root, "checkout-status") };
  assert.deepEqual(readAnnotatorConfiguration(file, defaults), defaults);
  const configured = { recording_root: path.join(root, "campaign"), runtime_status_path: path.join(root, "status"), retained_option: "operator-value" };
  fs.writeFileSync(file, JSON.stringify(configured));
  const prepared = readAnnotatorConfiguration(file, defaults);
  fs.writeFileSync(file, JSON.stringify(prepared));
  assert.deepEqual(readAnnotatorConfiguration(file, defaults), configured);
  assert.equal(effectiveAnnotatorConfiguration(prepared, {}).runtime_status_path, configured.runtime_status_path);
  const environment = { STS2_HUMAN_ANNOTATOR_STATUS_PATH: path.join(root, "env-status") };
  assert.equal(effectiveAnnotatorConfiguration(prepared, environment).runtime_status_path, environment.STS2_HUMAN_ANNOTATOR_STATUS_PATH);
  assert.deepEqual(readAnnotatorConfiguration(file, defaults), configured, "an environment override must not rewrite operator config");
});

test("malformed existing locations fail before replacement instead of resetting", (t) => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "annotator-config-"));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const file = path.join(root, "STS2_HUMAN_ANNOTATOR.conf");
  const defaults = { recording_root: root, runtime_status_path: path.join(root, "status") };
  for (const content of ["{", "[]", "null", '{"recording_root":null}', '{"runtime_status_path":"relative"}']) {
    fs.writeFileSync(file, content);
    assert.throws(() => readAnnotatorConfiguration(file, defaults));
    assert.equal(fs.readFileSync(file, "utf8"), content);
  }
  assert.throws(() => effectiveAnnotatorConfiguration(defaults, { STS2_HUMAN_ANNOTATOR_RECORDING_ROOT: "relative" }));
});
