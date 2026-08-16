import assert from "node:assert/strict";
import test from "node:test";
import { episodeSeed, summarizeSoakEpisodes } from "../src/soak-supervisor.mjs";

function episode(index, runtime, generation) {
  return {
    status: "measured",
    runtime_instance_ids: [runtime],
    generation_ids: [generation],
    remaining_processes: [],
    endpoint_release_pass: true,
    shutdown_containment_bounded: true,
    delivered_normalized_semantic_decisions: 8,
    common_decision_window_seconds: 4,
    failures: []
  };
}

test("derives bounded canonical episode seeds", () => {
  assert.equal(episodeSeed("hisoak", 12), "H1S0AK000012");
  assert.throws(() => episodeSeed("hisoak", 0), /positive integer/u);
});

test("soak summary requires unique runtimes, generations and released endpoints", () => {
  const result = summarizeSoakEpisodes(
    [episode(1, "runtime-a", "generation-a"), episode(2, "runtime-b", "generation-b")],
    2,
    1
  );
  assert.equal(result.verdict, "soak_smoke_pass");
  assert.equal(result.delivered_normalized_semantic_decisions, 16);
  assert.equal(result.aggregate_normalized_semantic_decisions_per_second, 2);
});

test("soak summary fails closed for reused runtime or infrastructure leakage", () => {
  const first = episode(1, "runtime", "generation-a");
  const second = episode(2, "runtime", "generation-b");
  second.remaining_processes = ["process"];
  second.endpoint_release_pass = false;
  const result = summarizeSoakEpisodes([first, second], 2, 1);
  assert.equal(result.verdict, "soak_incomplete");
  assert.ok(result.errors.includes("runtime_instance_reused"));
  assert.ok(result.errors.includes("process_leak_observed"));
  assert.ok(result.errors.includes("endpoint_leak_observed"));
});

test("soak summary fails closed when the normal player profile changes", () => {
  const result = summarizeSoakEpisodes(
    [episode(1, "runtime-a", "generation-a")],
    1,
    1,
    { unchanged: false }
  );
  assert.equal(result.verdict, "soak_incomplete");
  assert.ok(result.errors.includes("shared_profile_mutation_observed"));
});

test("soak summary rejects a missing or failed shutdown containment verdict", () => {
  const input = episode(1, "runtime-a", "generation-a");
  input.shutdown_containment_bounded = false;
  const result = summarizeSoakEpisodes([input], 1, 1, { unchanged: true });
  assert.equal(result.verdict, "soak_incomplete");
  assert.ok(result.errors.includes("shutdown_containment_rejected_or_missing"));
});
