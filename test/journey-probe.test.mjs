import assert from "node:assert/strict";
import test from "node:test";
import {
  faultInjectionReady,
  chooseBoundAction,
  isRefreshableStaleReceipt,
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

test("seeded fault injection waits for comparable episode provenance", () => {
  assert.equal(faultInjectionReady({
    deliveredActions: 5,
    faultAfterDeliveredActions: 2,
    requestedSeed: "SEED",
    provenanceVerdict: "provenance_incomplete"
  }), false);
  assert.equal(faultInjectionReady({
    deliveredActions: 5,
    faultAfterDeliveredActions: 2,
    requestedSeed: "SEED",
    provenanceVerdict: "provenance_pass"
  }), true);
  assert.equal(faultInjectionReady({
    deliveredActions: 2,
    faultAfterDeliveredActions: 2,
    requestedSeed: null,
    provenanceVerdict: "provenance_incomplete"
  }), true);
});

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

test("deterministic test policy does not depend on runtime action publication order", () => {
  const bash = { bound_action_id: "runtime-bash", verb: "play", label: "Play Bash", arguments: [] };
  const strike = { bound_action_id: "runtime-strike", verb: "play", label: "Play Strike", arguments: [] };
  assert.equal(chooseBoundAction(snapshot("combat_turn", [strike, bash])).label, "Play Bash");
  assert.equal(chooseBoundAction(snapshot("combat_turn", [bash, strike])).label, "Play Bash");
});

test("combat policy binds the first visible playable card and target across runtimes", () => {
  const firstDefend = {
    bound_action_id: "runtime-defend-first",
    verb: "play",
    label: "Play Defend",
    subject_referent_id: "card-first",
    arguments: []
  };
  const secondDefend = {
    bound_action_id: "runtime-defend-second",
    verb: "play",
    label: "Play Defend",
    subject_referent_id: "card-second",
    arguments: []
  };
  const strikeSecondEnemy = {
    bound_action_id: "runtime-strike-enemy-2",
    verb: "play",
    label: "Play Strike -> second",
    subject_referent_id: "card-strike",
    arguments: [{ role: "target", referent_id: "enemy-second" }]
  };
  const strikeFirstEnemy = {
    bound_action_id: "runtime-strike-enemy-1",
    verb: "play",
    label: "Play Strike -> first",
    subject_referent_id: "card-strike",
    arguments: [{ role: "target", referent_id: "enemy-first" }]
  };
  const combat = snapshot("combat_turn", [
    secondDefend,
    strikeSecondEnemy,
    firstDefend,
    strikeFirstEnemy
  ]);
  combat.interaction.content = {
    context: {
      player: {
        hand: [
          { entity_id: "card-strike" },
          { entity_id: "card-first" },
          { entity_id: "card-second" }
        ]
      },
      enemies: [
        { entity_id: "enemy-first" },
        { entity_id: "enemy-second" }
      ]
    }
  };

  assert.equal(chooseBoundAction(combat), strikeFirstEnemy);
  combat.interaction.content.context.player.hand = [
    { entity_id: "card-first" },
    { entity_id: "card-second" }
  ];
  assert.equal(chooseBoundAction(combat), firstDefend);
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

test("typed tutorial policy advances exact combat pages and rejects unknown tutorial ids", () => {
  const previous = { bound_action_id: "previous", verb: "activate", label: "Previous page" };
  const next = { bound_action_id: "next", verb: "activate", label: "Next page" };
  const combatTutorial = snapshot("tutorial", [previous, next]);
  combatTutorial.interaction.content = {
    surface: { tutorial_id: "combat_rules_ftue", current_page: 2, total_pages: 3 }
  };
  assert.equal(chooseBoundAction(combatTutorial), next);

  const unknownTutorial = snapshot("tutorial", [next]);
  unknownTutorial.interaction.content = {
    surface: { tutorial_id: "unknown_mod_tutorial" }
  };
  assert.equal(chooseBoundAction(unknownTutorial), null);
});

test("bounded probes stop immediately after a non-delivery or unknown outcome", () => {
  assert.equal(
    terminalForReceipt({ delivery: "not_delivered", reason_code: "stale_snapshot" }),
    "not_delivered:stale_snapshot"
  );
  assert.equal(terminalForReceipt({ delivery: "unknown" }), "unknown_delivery");
  assert.equal(terminalForReceipt({ delivery: "delivered" }), null);
});

test("only proven not-delivered stale receipts can refresh the test consumer", () => {
  assert.equal(isRefreshableStaleReceipt({
    delivery: "not_delivered",
    reason_code: "stale_snapshot",
    successor: { snapshot_id: "state-new", status: "interactive" }
  }), true);
  assert.equal(isRefreshableStaleReceipt({
    delivery: "unknown",
    reason_code: "stale_snapshot",
    successor: { snapshot_id: "state-new" }
  }), false);
  assert.equal(isRefreshableStaleReceipt({
    delivery: "not_delivered",
    reason_code: "bound_action_not_current",
    successor: { snapshot_id: "state-new" }
  }), false);
  assert.equal(isRefreshableStaleReceipt({
    delivery: "not_delivered",
    reason_code: "stale_snapshot"
  }), false);
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

test("journey integrity fails when requested episode provenance is not verified", () => {
  const result = evaluateJourneyIntegrity({
    terminal: "action_limit",
    unknownCount: 0,
    readFailures: 0,
    successorFailures: 0,
    provenanceFailures: 1
  });
  assert.equal(result.verdict, "integrity_incomplete");
  assert.deepEqual(result.errors, ["episode_provenance_unverified"]);
});
