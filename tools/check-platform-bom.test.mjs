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

test("BOM check separates the loaded V2 artifact from later operations-only source", async () => {
  const bom = JSON.parse(fs.readFileSync(path.join(root, "platform-bom.json"), "utf8"));
  bom.current_v2_candidate.connector.current_component_source_revision = "0".repeat(40);
  bom.current_v2_candidate.annotator.current_component_source_revision = "0".repeat(40);
  bom.current_v2_candidate.annotator.loaded = "pending";
  bom.current_v2_candidate.native_human_gate.status = "pass";
  const errors = validatePlatformBom(bom, await readBomAuthorities(root));
  assert.ok(errors.some((error) => error.startsWith("V2 current Connector source:")));
  assert.ok(errors.some((error) => error.startsWith("V2 current Annotator source:")));
  assert.ok(errors.some((error) => error.startsWith("V2 annotator loaded:")));
  assert.ok(errors.some((error) => error.startsWith("V2 human gate:")));
});

test("BOM check rejects V2 evidence and selector-claim drift", async () => {
  const bom = JSON.parse(fs.readFileSync(path.join(root, "platform-bom.json"), "utf8"));
  bom.current_v2_candidate.native_human_gate.reads.run_deck = 59;
  bom.current_v2_candidate.native_human_gate.transfer.retry_status = "promoted";
  bom.current_v2_candidate.native_human_gate.generated_card_choice.runtime_status = "pass";
  bom.non_claims = bom.non_claims.filter(
    (claim) => claim !== "current_v2_candidate_generated_card_choice_not_exercised"
  );
  const errors = validatePlatformBom(bom, await readBomAuthorities(root));
  assert.ok(errors.some((error) => error.startsWith("V2 run-deck Reads:")));
  assert.ok(errors.some((error) => error.startsWith("V2 transfer retry:")));
  assert.ok(errors.some((error) => error.startsWith("V2 selector runtime:")));
  assert.ok(errors.includes("Current V2 candidate generated-card-choice non-claim is missing"));
});

test("BOM check rejects unified artifact, identity and evidence promotion", async () => {
  const bom = JSON.parse(fs.readFileSync(path.join(root, "platform-bom.json"), "utf8"));
  const candidate = bom.unified_platform_runtime_candidate;
  candidate.live_ui.current_component_source_revision = "0".repeat(40);
  candidate.live_ui.artifact_sha256 = "0".repeat(64);
  candidate.connector.current_component_source_revision = "0".repeat(40);
  candidate.game_mod.installed = "pending";
  candidate.runtime.execution_available = false;
  candidate.owner_ui_visibility = "pass";
  candidate.rapid_input_ledger_v1_owner_validation.valid_records = 13;
  candidate.rapid_input_ledger_v1_owner_validation.evidence_transfer_to_ledger_v2 = true;
  candidate.rapid_input_ledger_v2_loaded_candidate.owner_rapid_input = "pass";
  candidate.rapid_input_ledger_v2_loaded_candidate.evidence_transfer_from_ledger_v1 = true;
  candidate.recording_application_decision_gate.human_origin = "machine_proven";
  candidate.predecessor_human_session.evidence_transfer_to_unified_artifact = true;
  candidate.external_policy.checkpoint_status = "present";
  bom.non_claims = bom.non_claims.filter(
    (claim) => claim !== "s1_checkpoint_absent_shadow_one_step_auto_not_exercised"
  );
  bom.non_claims = bom.non_claims.filter(
    (claim) => claim !==
      "native_rejected_cancelled_attempt_absence_owner_attested_not_machine_attributable"
  );
  const errors = validatePlatformBom(bom, await readBomAuthorities(root));
  assert.ok(errors.some((error) => error.startsWith("candidate current Live UI source:")));
  assert.ok(errors.some((error) => error.startsWith("candidate common artifact SHA (live_ui):")));
  assert.ok(errors.some((error) => error.startsWith("candidate current Connector source:")));
  assert.ok(errors.some((error) => error.startsWith("candidate Game Mod installed:")));
  assert.ok(errors.some((error) => error.startsWith("candidate execution:")));
  assert.ok(errors.some((error) => error.startsWith("candidate owner UI visibility:")));
  assert.ok(errors.some((error) => error.startsWith("rapid ledger valid records:")));
  assert.ok(errors.some((error) => error.startsWith("rapid ledger evidence transfer:")));
  assert.ok(errors.some((error) => error.startsWith("rapid ledger v2 owner canary:")));
  assert.ok(errors.some((error) => error.startsWith("rapid ledger v2 evidence transfer:")));
  assert.ok(errors.some((error) => error.startsWith("accepted-only Human origin:")));
  assert.ok(errors.some((error) => error.startsWith("predecessor evidence transfer:")));
  assert.ok(errors.some((error) => error.startsWith("candidate policy checkpoint:")));
  assert.ok(errors.includes("S1 checkpoint/model-mode non-claim is missing"));
  assert.ok(errors.includes("Native-rejected attempt attribution non-claim is missing"));
});
