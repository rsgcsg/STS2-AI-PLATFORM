import assert from "node:assert/strict";
import test from "node:test";
import { parseWorkerCounts, summarizeCapacityGroup } from "../src/capacity-benchmark.mjs";

function result(id, start, end) {
  return {
    reportFile: `${id}.json`,
    measurement: { decision_window_started_ms: start, decision_window_ended_ms: end },
    report: {
      worker: { worker_id: id },
      profile: { profile_id: id, generation_id: `gen-${id}` },
      command: { connector: { endpoint: `http://127.0.0.1:16${id === "a" ? "001" : "002"}` } },
      loaded_identity: {
        protocol: "1.0-rc.2",
        host: {
          host_kind: "headless",
          runtime_instance_id: `runtime-${id}`,
          implementation: {
            source_revision: "source",
            artifact_sha256: "sha",
            module_version_id: "mvid"
          }
        },
        game: {
          version: "v0.111.0",
          commit: "commit",
          main_assembly_hash: 1,
          modset: { fingerprint: "modset" }
        }
      },
      performance: {
        delivered_normalized_semantic_decisions: 10,
        decision_window_seconds: (end - start) / 1000,
        normalized_semantic_decisions_per_second: 1,
        cpu_seconds: 2,
        peak_rss_bytes: 1024 ** 3,
        sample_errors: []
      },
      episode_provenance: { verdict: "provenance_pass", actual_seed: "H1CAPACITY01" },
      verdict: { integrity: { verdict: "integrity_pass" } }
    }
  };
}

test("parses a bounded unique worker ladder", () => {
  assert.deepEqual(parseWorkerCounts("1,2,4"), [1, 2, 4]);
  for (const value of ["", "1,1", "0", "33", "one"]) {
    assert.throws(() => parseWorkerCounts(value));
  }
});

test("summarizes one concurrent semantic decision window", () => {
  const summary = summarizeCapacityGroup([result("a", 1000, 11000), result("b", 2000, 12000)]);
  assert.equal(summary.status, "measured");
  assert.equal(summary.worker_count, 2);
  assert.equal(summary.delivered_normalized_semantic_decisions, 20);
  assert.equal(summary.common_decision_window_seconds, 11);
  assert.equal(summary.aggregate_normalized_semantic_decisions_per_second, 20 / 11);
  assert.equal(summary.summed_worker_peak_rss_bytes, 2 * (1024 ** 3));
  assert.equal(summary.episode_provenance_pass, true);
  assert.equal(summary.episode_seed, "H1CAPACITY01");
});

test("rejects duplicate runtimes and incomparable artifacts", () => {
  const first = result("a", 0, 1000);
  const duplicate = result("b", 0, 1000);
  duplicate.report.loaded_identity.host.runtime_instance_id = "runtime-a";
  assert.throws(() => summarizeCapacityGroup([first, duplicate]), /distinct runtime/u);
  const drift = result("b", 0, 1000);
  drift.report.loaded_identity.host.implementation.artifact_sha256 = "different";
  assert.throws(() => summarizeCapacityGroup([first, drift]), /comparable/u);
});

test("capacity measurement fails closed without comparable episode provenance", () => {
  const first = result("a", 0, 1000);
  const second = result("b", 0, 1000);
  second.report.episode_provenance.actual_seed = "OTHERSEED";
  assert.equal(summarizeCapacityGroup([first, second]).status, "measurement_incomplete");
});
