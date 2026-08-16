import assert from "node:assert/strict";
import test from "node:test";
import { normalizedDecisionTiming, summarizeDurations } from "../src/decision-timing.mjs";

test("measures one normalized semantic decision", () => {
  assert.deepEqual(normalizedDecisionTiming({
    snapshotReadyMs: 100,
    policySelectedMs: 103,
    submitStartedMs: 104,
    receiptMs: 110,
    successorReadyMs: 125
  }), {
    policy_ms: 3,
    submit_to_receipt_ms: 6,
    receipt_to_successor_ms: 15,
    semantic_decision_ms: 25
  });
});

test("summarizes latency without interpolating invented samples", () => {
  assert.deepEqual(summarizeDurations([1, 2, 3, 4, 100]), {
    count: 5,
    min_ms: 1,
    p50_ms: 3,
    p95_ms: 100,
    p99_ms: 100,
    max_ms: 100,
    mean_ms: 22
  });
});

test("rejects a non-monotonic timing chain", () => {
  assert.throws(() => normalizedDecisionTiming({
    snapshotReadyMs: 100,
    policySelectedMs: 99,
    submitStartedMs: 104,
    receiptMs: 110,
    successorReadyMs: 125
  }), /monotonic/u);
});
