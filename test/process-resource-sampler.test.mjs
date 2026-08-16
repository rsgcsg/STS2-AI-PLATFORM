import assert from "node:assert/strict";
import test from "node:test";
import { summarizeHostPerformance } from "../src/process-resource-sampler.mjs";

test("summarizes normalized decisions against measured CPU and RSS", () => {
  const result = summarizeHostPerformance({
    samples: [
      { monotonic_ms: 900, cpu_seconds_total: 10, rss_bytes: 1024 ** 3, private_bytes: 900 },
      { monotonic_ms: 1_500, cpu_seconds_total: 11, rss_bytes: 2 * 1024 ** 3, private_bytes: 1_800 },
      { monotonic_ms: 2_100, cpu_seconds_total: 12, rss_bytes: 1.5 * 1024 ** 3, private_bytes: 1_500 }
    ],
    decisionWindowStartedMs: 1_000,
    decisionWindowEndedMs: 2_000,
    deliveredDecisions: 10
  });
  assert.equal(result.status, "measured");
  assert.equal(result.normalized_semantic_decisions_per_second, 10);
  assert.equal(result.cpu_seconds, 2);
  assert.equal(result.peak_rss_bytes, 2 * 1024 ** 3);
  assert.equal(result.normalized_semantic_decisions_per_second_per_gib, 5);
});

test("does not invent resource measurements from one sample", () => {
  const result = summarizeHostPerformance({
    samples: [{ monotonic_ms: 1_500, cpu_seconds_total: 1, rss_bytes: 100, private_bytes: null }],
    decisionWindowStartedMs: 1_000,
    decisionWindowEndedMs: 2_000,
    deliveredDecisions: 1
  });
  assert.equal(result.status, "insufficient_samples");
  assert.equal(result.cpu_seconds, null);
  assert.equal(result.average_cpu_cores, null);
});

test("retains sampler errors but only fails errors inside the semantic decision window", () => {
  const input = {
    samples: [
      { monotonic_ms: 900, cpu_seconds_total: 1, rss_bytes: 100, private_bytes: null },
      { monotonic_ms: 1_500, cpu_seconds_total: 2, rss_bytes: 200, private_bytes: null },
      { monotonic_ms: 2_100, cpu_seconds_total: 3, rss_bytes: 300, private_bytes: null }
    ],
    decisionWindowStartedMs: 1_000,
    decisionWindowEndedMs: 2_000,
    deliveredDecisions: 1
  };
  const outside = summarizeHostPerformance({
    ...input,
    sampleErrors: [{ monotonic_ms: 800, error: "startup sampler race" }]
  });
  assert.equal(outside.status, "measured");
  assert.equal(outside.sample_errors.length, 1);
  assert.equal(outside.decision_window_sample_errors.length, 0);

  const inside = summarizeHostPerformance({
    ...input,
    sampleErrors: [{ monotonic_ms: 1_600, error: "measurement failure" }]
  });
  assert.equal(inside.status, "measurement_error");
  assert.equal(inside.decision_window_sample_errors.length, 1);
});
