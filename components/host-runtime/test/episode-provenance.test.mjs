import assert from "node:assert/strict";
import test from "node:test";
import {
  canonicalizeEpisodeSeed,
  evaluateEpisodeProvenance
} from "../src/episode-provenance.mjs";

test("episode seeds use the game's documented canonical form", () => {
  assert.equal(canonicalizeEpisodeSeed(" oiAbc123 "), "01ABC123");
  assert.equal(canonicalizeEpisodeSeed(null), null);
  assert.throws(() => canonicalizeEpisodeSeed("bad-seed"), /1-64 ASCII/u);
  assert.throws(() => canonicalizeEpisodeSeed(""), /1-64 ASCII/u);
});

test("episode provenance binds requested and actual seed to one runtime", () => {
  const result = evaluateEpisodeProvenance({
    requestedSeed: "oiAbc123",
    expectedRuntimeInstanceId: "runtime-1",
    response: {
      status: "observed",
      response: {
        status: "seed_observed",
        runtime_instance_id: "runtime-1",
        requested_seed: "01ABC123",
        actual_seed: "01ABC123",
        seed_matches: true
      }
    }
  });
  assert.equal(result.verdict, "provenance_pass");
  assert.deepEqual(result.errors, []);
});

test("episode provenance fails closed for a stale runtime or wrong actual seed", () => {
  const result = evaluateEpisodeProvenance({
    requestedSeed: "ABC123",
    expectedRuntimeInstanceId: "runtime-1",
    response: {
      status: "observed",
      response: {
        status: "seed_mismatch",
        runtime_instance_id: "runtime-2",
        requested_seed: "ABC123",
        actual_seed: "XYZ789",
        seed_matches: false
      }
    }
  });
  assert.equal(result.verdict, "provenance_incomplete");
  assert.ok(result.errors.includes("runtime_instance_changed"));
  assert.ok(result.errors.includes("actual_seed_mismatch"));
  assert.ok(result.errors.includes("seed_match_not_proven"));
});

test("unrequested provenance does not create a release claim", () => {
  assert.equal(evaluateEpisodeProvenance({
    requestedSeed: null,
    expectedRuntimeInstanceId: "runtime-1",
    response: null
  }).verdict, "not_requested");
});
