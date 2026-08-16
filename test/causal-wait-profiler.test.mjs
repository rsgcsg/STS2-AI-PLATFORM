import assert from "node:assert/strict";
import test from "node:test";
import {
  classifyObservedWaitBoundary,
  summarizeCausalWaits
} from "../src/causal-wait-profiler.mjs";

function action(kind, successor, receiptWait, semanticWait, observations = []) {
  return {
    type: "action",
    interaction_kind: kind,
    interaction_stage: "ready",
    action: { verb: "activate", label: `Advance ${kind}` },
    observed_successor_interaction_kind: successor,
    observed_successor_interaction_stage: "ready",
    timing: {
      policy_ms: 1,
      submit_to_receipt_ms: semanticWait - receiptWait - 1,
      receipt_to_successor_ms: receiptWait,
      semantic_decision_ms: semanticWait
    },
    successor_wait: {
      terminal: "stable_successor",
      poll_count: observations.length,
      observations
    }
  };
}

test("classifies measured boundaries without claiming an engine cause", () => {
  assert.equal(classifyObservedWaitBoundary(action("character_select", "event_option", 5, 10)), "run_mount_boundary");
  assert.equal(classifyObservedWaitBoundary(action("map_navigation", "combat_turn", 5, 10)), "room_mount_boundary");
  assert.equal(classifyObservedWaitBoundary(action("combat_turn", "combat_turn", 5, 10)), "combat_resolution_boundary");
  assert.equal(classifyObservedWaitBoundary(action("deck_upgrade_selection", "rest_site", 5, 10)), "selector_handoff_boundary");
  assert.equal(classifyObservedWaitBoundary(action("event_option", "reward_claim", 5, 10)), "surface_handoff_boundary");
});

test("profiles receipt waits and labels the zero-wait arithmetic as a non-claim", () => {
  const profile = summarizeCausalWaits([
    action("character_select", "event_option", 80, 100, [{
      status: "settling",
      interaction_kind: "character_select",
      interaction_stage: "committing",
      bound_action_status: "unavailable",
      bound_action_count: 0,
      sample_count: 2
    }]),
    action("map_navigation", "combat_turn", 40, 50, [{
      status: "interactive",
      interaction_kind: "combat_turn",
      interaction_stage: "ready",
      bound_action_status: "complete",
      bound_action_count: 4,
      sample_count: 1
    }])
  ]);

  assert.equal(profile.action_count, 2);
  assert.equal(profile.measured.semantic_decision_total_ms, 150);
  assert.equal(profile.measured.receipt_to_successor_total_ms, 120);
  assert.equal(profile.measured.receipt_to_successor_fraction, 0.8);
  assert.equal(profile.counterfactual_zero_observed_receipt_wait.normalized_decisions_per_second, 2 / 0.03);
  assert.equal(profile.by_observed_boundary.run_mount_boundary.count, 1);
  assert.equal(profile.by_observed_boundary.room_mount_boundary.count, 1);
  assert.deepEqual(profile.observed_wait_states, [
    { state: "settling/character_select/committing/unavailable/0", samples: 2 },
    { state: "interactive/combat_turn/ready/complete/4", samples: 1 }
  ]);
  assert.match(profile.counterfactual_zero_observed_receipt_wait.interpretation, /not a proven/u);
});
