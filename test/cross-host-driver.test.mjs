import assert from "node:assert/strict";
import test from "node:test";
import { sliceScenarioEvents } from "../src/cross-host-driver.mjs";

const scenario = {
  schema: "sts2.headless/scenario-1",
  scenario_id: "first-map-prefix-v1",
  seed: "H1CROSSHOST01",
  policy_id: "deterministic-probe-1",
  max_actions: 2,
  start_interaction_kind: "map_navigation",
  read_policy: "none"
};

function action(kind, label) {
  return {
    type: "action",
    canonical_decision: {
      interaction: { kind },
      information_policy: { id: "player_visible_v1" }
    },
    canonical_selected_action: { verb: "activate", arguments: [], label }
  };
}

test("cross-Host scenario slicing removes only the declared preamble and bounds actions", () => {
  const result = sliceScenarioEvents([
    action("main_menu", "Open"),
    action("character_select", "Embark"),
    action("map_navigation", "Travel"),
    action("combat_turn", "Play"),
    action("combat_turn", "Extra")
  ], scenario);
  assert.equal(result.actionCount, 2);
  assert.deepEqual(result.events.map((event) => event.canonical_selected_action.label), ["Travel", "Play"]);
});

test("cross-Host scenario slicing fails closed when the shared boundary was not reached", () => {
  assert.throws(
    () => sliceScenarioEvents([action("main_menu", "Open")], scenario),
    /did not reach scenario boundary/u
  );
});
