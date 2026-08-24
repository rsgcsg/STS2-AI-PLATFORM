import assert from "node:assert/strict";
import test from "node:test";
import { createHostDriver, runHostScenario } from "../src/host-driver.mjs";

const semanticTarget = {
  schema: "sts2.headless/semantic-target-1",
  target_id: "sts2-v0.111.0-player-visible-v1",
  protocol_version: "1.0.0",
  game_build: { version: "v0.111.0", commit: "41cef1ea", main_assembly_hash: 1010476334 },
  content_policy_id: "vanilla_connector_only",
  information_policy_id: "player_visible_v1"
};

const scenario = {
  schema: "sts2.headless/scenario-1",
  scenario_id: "bounded-run-entry",
  seed: "H1DRIVER01",
  policy_id: "deterministic-probe-1",
  max_actions: 8
};

test("HostDriver keeps implementation identity separate from the semantic target", async () => {
  const driver = createHostDriver({
    driverId: "managed-exact-spike",
    hostKind: "managed_exact",
    semanticTarget,
    implementation: { source_revision: "candidate-source" },
    runScenario: async (received) => ({
      report: { received },
      events: [{ type: "stop", reason: "fixture" }]
    })
  });
  const result = await runHostScenario(driver, scenario);
  assert.equal(result.driver.host_kind, "managed_exact");
  assert.equal(result.driver.semantic_target.target_id, semanticTarget.target_id);
  assert.equal(result.report.received.seed, "H1DRIVER01");
});

test("HostDriver rejects an incomplete semantic target before a Host can run", () => {
  assert.throws(() => createHostDriver({
    driverId: "bad",
    hostKind: "candidate",
    semanticTarget: { schema: "sts2.headless/semantic-target-1" },
    implementation: {},
    runScenario: async () => ({ report: {}, events: [] })
  }), /semantic_target/u);
});
