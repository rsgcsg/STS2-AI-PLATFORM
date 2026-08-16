import assert from "node:assert/strict";
import test from "node:test";
import {
  chooseBoundAction,
  evaluateBoundedJourney,
  evaluateJourneyIntegrity,
  evaluateSurfaceCoverage,
  terminalForReceipt
} from "../src/journey-probe.mjs";

function snapshot(kind, actions, stage = "ready") {
  return {
    status: "interactive",
    interaction: { kind, stage },
    bound_actions: { status: "complete", actions }
  };
}

test("chooses only a published action and avoids back navigation", () => {
  const back = { bound_action_id: "back", verb: "cancel", label: "Back to main menu" };
  const standard = { bound_action_id: "standard", verb: "open", label: "Open Standard run setup" };
  assert.equal(chooseBoundAction(snapshot("singleplayer_menu", [back, standard])), standard);
  assert.equal(chooseBoundAction(snapshot("shop_inventory", [
    { bound_action_id: "buy", verb: "activate", label: "Buy card" },
    { bound_action_id: "close", verb: "cancel", label: "Close shop inventory" }
  ])).bound_action_id, "close");
});

test("combat policy uses a native play binding then end turn", () => {
  const end = { bound_action_id: "end", verb: "end_turn", label: "End turn" };
  const play = { bound_action_id: "play", verb: "play", label: "Play Strike -> enemy" };
  assert.equal(chooseBoundAction(snapshot("combat_turn", [end, play])), play);
  assert.equal(chooseBoundAction(snapshot("combat_turn", [end])), end);
});

test("first-run tutorial preference stays inside the advertised action set", () => {
  const disable = { bound_action_id: "disable", verb: "select", label: "Cancel" };
  const enable = { bound_action_id: "enable", verb: "select", label: "Confirm" };
  assert.equal(
    chooseBoundAction(snapshot("tutorial_preference", [disable, enable])),
    disable
  );
  assert.equal(
    chooseBoundAction(
      snapshot("tutorial_preference", [disable, enable]),
      { tutorialPreference: "enable" }
    ),
    enable
  );
  assert.throws(
    () => chooseBoundAction(
      snapshot("tutorial_preference", [disable, enable]),
      { tutorialPreference: "guess" }
    ),
    /Unsupported tutorial preference/u
  );
});

test("bounded probes stop immediately after a non-delivery or unknown outcome", () => {
  assert.equal(
    terminalForReceipt({ delivery: "not_delivered", reason_code: "stale_snapshot" }),
    "not_delivered:stale_snapshot"
  );
  assert.equal(terminalForReceipt({ delivery: "unknown" }), "unknown_delivery");
  assert.equal(terminalForReceipt({ delivery: "delivered" }), null);
});

test("bounded journey gate accepts equivalent run-entry surfaces", () => {
  const kinds = ["main_menu", "character_select", "reward_claim", "map_navigation"];
  const steps = [
    ...kinds.map((interaction_kind) => ({ interaction_kind, delivery: "delivered" })),
    ...Array.from({ length: 3 }, () => ({ interaction_kind: "combat_turn", delivery: "delivered" }))
  ];
  assert.equal(evaluateBoundedJourney({
    steps,
    terminal: "coverage_reached",
    unknownCount: 0,
    readFailures: 0
  }).verdict, "h2_pass");
  assert.equal(evaluateBoundedJourney({
    steps: steps.slice(0, -1),
    terminal: "action_limit",
    unknownCount: 0,
    readFailures: 0
  }).verdict, "h2_integrity_pass_coverage_incomplete");
});

test("bounded journey gate still requires one non-combat decision", () => {
  const result = evaluateSurfaceCoverage({
    surfaces: ["main_menu", "character_select", "map_navigation", "combat_turn"],
    combatDeliveries: 3
  });
  assert.equal(result.verdict, "coverage_incomplete");
  assert.deepEqual(
    result.missing_surface_groups.map((group) => group.id),
    ["non_combat_decision"]
  );
});

test("journey integrity does not fail merely because a coverage target was not visited", () => {
  assert.equal(evaluateJourneyIntegrity({
    terminal: "action_limit",
    unknownCount: 0,
    readFailures: 0,
    successorFailures: 0
  }).verdict, "integrity_pass");
  assert.equal(evaluateSurfaceCoverage({
    surfaces: ["main_menu"],
    combatDeliveries: 0
  }).verdict, "coverage_incomplete");
});
