import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "../..");
const read = (relative) => fs.readFileSync(path.join(root, relative), "utf8");
const audit = JSON.parse(read("tools/ritsu-qualification/capability-audit.json"));
const measurements = JSON.parse(read("tools/ritsu-qualification/measurement-results.json"));
const strictConfig = JSON.parse(read("tools/ritsu-qualification/strict-config-fingerprint.json"));
const bridge = read("tools/ritsu-qualification/RitsuQualificationBridge.cs");

test("qualification is pinned to exact Direct and Ritsu refs", () => {
  assert.equal(audit.platform_oracle, "32c76156fed2c14f55427ee88590bf1979598d9d");
  assert.equal(audit.ritsu.stable_version, "0.5.18");
  assert.equal(audit.ritsu.stable_commit, "f224961a9392e010335da092240b90ee8235317f");
  assert.equal(audit.ritsu.development_commit, "c466809004f8ecd801956fea2bc3fef83a5d7ad5");
});

test("Ritsu-backed catalog returns the Platform-owned semantic contract", () => {
  assert.match(bridge, /NativeCombatDecision Capture\(INativeReferentIdentity identities\)/u);
  assert.match(bridge, /NativeCombatDecisionProvider\.Capture\(identities\)/u);
  assert.match(bridge, /NativePlayerChoiceLineage Capture\(\)/u);
  assert.match(bridge, /NativePlayerChoiceLineage\.Capture\(\)/u);
  assert.doesNotMatch(bridge, /record NativeCombatDecision/u);
  assert.doesNotMatch(bridge, /record NativeSemanticAction/u);
});

test("Ritsu effect events never masquerade as exact action lifecycle", () => {
  assert.match(bridge, /HasExactGameActionIdentity: false/u);
  assert.match(bridge, /HasCancelOrAbortDisposition: false/u);
  assert.match(bridge, /HasRootParentLineage: false/u);
  assert.match(bridge, /new\(action, observer\)/u);
  assert.equal(audit.sampled_contract.exact_game_action_identity, "platform_direct_required");
  assert.equal(audit.sampled_contract.player_choice_parent_lineage, "platform_direct_required");
});

test("Ritsu patching remains fail-closed for both exact Treasure targets", () => {
  assert.match(bridge, /ApplyRequiredPatcher/u);
  assert.match(bridge, /PatchTarget\.Method<NTreasureRoom>/u);
  assert.match(bridge, /nameof\(NTreasureRoom\.Create\)/u);
  assert.match(bridge, /"OnChestButtonReleased"/u);
  assert.match(bridge, /NativeTreasureDecisionProvider\.Register/u);
  assert.match(bridge, /NativeTreasureDecisionProvider\.ObserveChestOpening/u);
});

test("qualification inventory does not claim a dev-only semantic capability", () => {
  assert.equal(audit.stable_dev_split.sampled_capability_difference, "none");
  assert.equal(audit.stable_dev_split.development_only_requirement, false);
  assert.equal(audit.sampled_contract.combat_catalog, "platform_direct_required");
  assert.equal(audit.sampled_contract.treasure_catalog, "platform_direct_required");
});

test("integration ledger counts only whole categories and exact native touchpoints", () => {
  assert.equal(measurements.integration_deletion.whole_categories_removed, 0);
  assert.equal(measurements.integration_deletion.exact_native_touchpoint_reduction_percent, 0);
  assert.deepEqual(measurements.integration_deletion.direct_exact_treasure_touchpoints,
    measurements.integration_deletion.ritsu_exact_treasure_touchpoints);
  assert.equal(measurements.sampled_domains.combat.semantic_provider, "direct_retained");
  assert.equal(measurements.sampled_domains.player_choice.lineage_provider, "direct_retained");
  assert.equal(measurements.sampled_domains.treasure.catalog_provider, "direct_retained");
});

test("strict Ritsu fingerprint disables compatibility and network-facing diagnostics", () => {
  assert.equal(strictConfig.sha256,
    "98af0c838deb869eabe76e512ba6ac5a40574ebb2190f2eb6dffe07184c7a4b9");
  for (const value of Object.values(strictConfig.settings)) assert.equal(value, false);
  assert.equal(strictConfig.runtime_status, "specified_not_loaded");
});

test("runtime A/B is not claimed after semantic hard-gate failure", () => {
  assert.equal(measurements.runtime.ritsu_backed.loaded, false);
  assert.equal(measurements.runtime.ritsu_backed.visible_boot, "not_run_not_decision_relevant");
  assert.equal(measurements.runtime.ritsu_backed.headless_boot, "not_run_not_decision_relevant");
  assert.equal(measurements.runtime.direct.visible_boot, "pass_predecessor_exact_artifact");
  assert.equal(measurements.runtime.direct.headless_boot, "pass_predecessor_exact_artifact");
});
