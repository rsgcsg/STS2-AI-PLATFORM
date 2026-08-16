import assert from "node:assert/strict";
import test from "node:test";
import {
  canonicalDecisionDigest,
  canonicalizeSnapshot,
  compareCanonicalDecisions
} from "../src/semantic-decision.mjs";

function snapshot(prefix, damage = 6) {
  const card = `${prefix}-card`;
  const enemy = `${prefix}-enemy`;
  return {
    protocol_version: "1.0.0",
    snapshot_id: `${prefix}-snapshot`,
    sequence: 9,
    observed_at: "2026-08-16T00:00:00Z",
    status: "interactive",
    persistent: { content_schema: "run-1", content: { floor: 1 } },
    interaction: {
      interaction_id: `${prefix}-interaction`,
      kind: "combat_turn",
      stage: "ready",
      prompt: "Choose",
      content_schema: "combat-1",
      content: { surface: { kind: "combat", focused: card }, context: { kind: "run" } },
      capabilities: [{ verb: "play", arguments: [{ role: "target", required: true }] }]
    },
    referents: [
      { referent_id: enemy, role: "target", kind: "entity", label: "Slime", state: { visible: true, observation_basis: "native_visible_fact" }, properties_schema: "enemy-1", properties: { hp: 10 } },
      { referent_id: card, role: "card", kind: "entity", label: "Strike", state: { visible: true, enabled: true, observation_basis: "native_visible_fact" }, properties_schema: "card-1", properties: { damage } }
    ],
    bound_actions: {
      schema: "sts2.player-environment/bound-actions-1",
      status: "complete", materialized_count: 1, total_count: 1, limit: 10,
      ordering_semantics: "native",
      actions: [{ bound_action_id: `${prefix}-action`, verb: "play", interaction_id: `${prefix}-interaction`, subject_referent_id: card, arguments: [{ role: "target", referent_id: enemy }], label: "Play Strike" }]
    },
    reads: [{ read_id: `${prefix}-read`, kind: "combat_piles", target_referent_id: card, content_schema: "piles-1", visibility_basis: "player_visible", snapshot_bound: true, ordering_semantics: "native", hidden_by_policy: ["draw_order"] }],
    completeness: { status: "complete", missing: [], hidden_by_policy: ["rng"] },
    session: { runtime_instance_id: `${prefix}-runtime`, environment_fingerprint: `${prefix}-environment` },
    information_policy: { id: "fair-player", scope: "visible", includes_hidden_information: false, unknown_field_behavior: "fail_closed" }
  };
}

test("canonical decisions ignore runtime-local identities while preserving bindings", () => {
  const left = snapshot("left");
  const right = snapshot("right");
  assert.equal(compareCanonicalDecisions(left, right).equal, true);
  assert.equal(canonicalDecisionDigest(left), canonicalDecisionDigest(right));
  const canonical = canonicalizeSnapshot(left);
  const action = canonical.bound_actions.actions[0];
  const subject = canonical.referents.find(
    (referent) => referent.canonical_referent_id === action.subject_referent_id
  );
  const target = canonical.referents.find(
    (referent) => referent.canonical_referent_id === action.arguments[0].referent_id
  );
  assert.equal(subject.role, "card");
  assert.equal(target.role, "target");
});

test("canonical decisions detect a visible semantic change", () => {
  assert.equal(compareCanonicalDecisions(snapshot("left"), snapshot("right", 7)).equal, false);
});
