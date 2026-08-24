import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { readBomAuthorities, validatePlatformBom } from "./check-platform-bom.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

test("current Platform BOM agrees with component and package authorities", async () => {
  const bom = JSON.parse(fs.readFileSync(path.join(root, "platform-bom.json"), "utf8"));
  const errors = validatePlatformBom(bom, await readBomAuthorities(root));
  assert.deepEqual(errors, []);
});

test("BOM check rejects component and public Connector pin drift", async () => {
  const bom = JSON.parse(fs.readFileSync(path.join(root, "platform-bom.json"), "utf8"));
  bom.components.annotator.version = "9.9.9";
  bom.public_packages.connector_host.sha256 = "0".repeat(64);
  const errors = validatePlatformBom(bom, await readBomAuthorities(root));
  assert.ok(errors.some((error) => error.startsWith("annotator.version:")));
  assert.ok(errors.some((error) => error.startsWith("public Connector archive SHA:")));
});

test("BOM check rejects human gate drift and machine-proven origin claims", async () => {
  const bom = JSON.parse(fs.readFileSync(path.join(root, "platform-bom.json"), "utf8"));
  bom.exact_runtime_candidate.gates.annotator_human.runtime_instance_id = "wrong-runtime";
  bom.exact_runtime_candidate.gates.annotator_human.human_origin = "machine_proven";
  const errors = validatePlatformBom(bom, await readBomAuthorities(root));
  assert.ok(errors.some((error) => error.startsWith("human gate runtime:")));
  assert.ok(errors.some((error) => error.startsWith("human origin boundary:")));
});

test("BOM check separates current V2 source and load from predecessor human evidence", async () => {
  const bom = JSON.parse(fs.readFileSync(path.join(root, "platform-bom.json"), "utf8"));
  bom.current_v2_candidate.connector.source_revision = "0".repeat(40);
  bom.current_v2_candidate.annotator.loaded = "pending";
  bom.current_v2_candidate.native_human_gate.status = "pass";
  const errors = validatePlatformBom(bom, await readBomAuthorities(root));
  assert.ok(errors.some((error) => error.startsWith("V2 Connector source:")));
  assert.ok(errors.some((error) => error.startsWith("V2 annotator loaded:")));
  assert.ok(errors.some((error) => error.startsWith("V2 human gate:")));
});
