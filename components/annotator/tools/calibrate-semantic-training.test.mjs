import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";

import { calibrate } from "./calibrate-semantic-training.mjs";

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  if (value && typeof value === "object")
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
  return JSON.stringify(value);
}

function action(id = "action-1") {
  return { bound_action_id: id, verb: "play", subject_referent_id: "card-1", arguments: {} };
}

function frame(snapshotId, actions = [action()]) {
  return {
    snapshot_id: snapshotId,
    interaction_id: "combat-1",
    interaction_kind: "combat_turn",
    surface_schema: "surface-1",
    catalog_digest: "digest",
    catalog_count: actions.length,
    snapshot: {
      snapshot_id: snapshotId,
      completeness: { status: "complete" },
      bound_actions: { status: "complete", materialized_count: actions.length, actions }
    },
    reads: []
  };
}

async function fixture() {
  const root = await mkdtemp(path.join(os.tmpdir(), "semantic-calibration-"));
  await writeFile(path.join(root, "recording-manifest.json"), JSON.stringify({
    session_id: "session-1", timeline_id: "timeline-1", status: "closed"
  }));
  const refs = {};
  async function store(name, value) {
    const encoded = canonical(value);
    const digest = createHash("sha256").update(encoded).digest("hex");
    const relative = `semantic-frames/sha256/${digest.slice(0, 2)}/${digest}.json`;
    await mkdir(path.join(root, path.dirname(relative)), { recursive: true });
    await writeFile(path.join(root, relative), encoded);
    refs[name] = { snapshot_id: value.snapshot_id, content_sha256: digest, object_ref: relative };
  }
  await store("s0", frame("s0"));
  await store("s1", frame("s1", [action("action-2")]));
  await store("mismatch", frame("mismatch", [action("other")]));
  return { root, refs };
}

function event(sequence, kind, id, selected, extra = {}) {
  return {
    schema_version: 3,
    schema: "sts2.human-annotator/semantic-evidence-event-3",
    event_id: `event-${sequence}`,
    session_id: "session-1",
    timeline_id: "timeline-1",
    run_id: "run-1",
    sequence,
    observed_at: `2026-08-29T00:00:${String(sequence).padStart(2, "0")}Z`,
    kind,
    action: {
      action_witness_id: id,
      action_sequence: Number(id.slice(-1)),
      record_id: `record-${id}`,
      run_id: "run-1",
      native_action_type: "PlayCardAction",
      human_observation_snapshot_id: "s0",
      native_mechanism: "game_action",
      ...(selected ? { bound_action: selected } : {})
    },
    proof_status: extra.proof_status ?? "test",
    ...extra
  };
}

test("classifies exact handoff, polling successor, and mismatched execution catalog", async () => {
  const { root, refs } = await fixture();
  try {
    const events = [
      event(1, "action_accepted", "a1", action(), { human_observation_ref: refs.s0 }),
      event(2, "boundary_observed", "a1", action(), { execution_pre_ref: refs.s0,
        boundary: { immediately_consumed_by_action_witness_id: "a1" } }),
      event(3, "action_started", "a1", action(), { execution_pre_ref: refs.s0 }),
      event(4, "action_finished", "a1", action(), { execution_pre_ref: refs.s0 }),
      event(5, "transition_proved", "a1", action(), { execution_pre_ref: refs.s0,
        successor_ref: refs.s1, related_action_witness_id: "a2",
        proof_status: "proved_execution_handoff_boundary" }),
      event(6, "action_accepted", "a2", action("action-2"), { human_observation_ref: refs.s1 }),
      event(7, "boundary_observed", "a2", action("action-2"), { execution_pre_ref: refs.s1,
        boundary: { immediately_consumed_by_action_witness_id: "a2" } }),
      event(8, "action_started", "a2", action("action-2"), { execution_pre_ref: refs.s1 }),
      event(9, "action_finished", "a2", action("action-2"), { execution_pre_ref: refs.s1 }),
      event(10, "transition_proved", "a2", action("action-2"), { execution_pre_ref: refs.s1,
        successor_ref: refs.s0, proof_status: "proved_interactive_decision_boundary" }),
      event(11, "action_accepted", "a3", action(), { human_observation_ref: refs.s0 }),
      event(12, "boundary_observed", "a3", action(), { execution_pre_ref: refs.mismatch,
        boundary: { immediately_consumed_by_action_witness_id: "a3" } }),
      event(13, "action_started", "a3", action(), { execution_pre_ref: refs.mismatch }),
      event(14, "action_finished", "a3", action(), { execution_pre_ref: refs.mismatch }),
      event(15, "transition_unknown", "a3", action(), { execution_pre_ref: refs.mismatch,
        proof_status: "closed_without_boundary" })
    ];
    await writeFile(path.join(root, "semantic-boundary-trace.jsonl"),
      `${events.map(JSON.stringify).join("\n")}\n`);
    await writeFile(path.join(root, "native-action-ledger.jsonl"), "");
    await writeFile(path.join(root, "run-0001.jsonl"), "");

    const report = await calibrate(root);
    assert.deepEqual(report.summary.classifications, {
      canonical_s_a_s_prime: 1,
      state_action_space_unresolved: 1,
      successor_unresolved: 1
    });
    assert.equal(report.summary.rapid_rebind_valid, 1);
    assert.equal(report.summary.canonical_s_a, 2);
    assert.equal(report.summary.future_action_chain_candidate, 1);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("accepts only an exact native UI post-commit boundary for direct UI proof", async () => {
  const { root, refs } = await fixture();
  try {
    const direct = action();
    const events = [
      event(1, "action_accepted", "a1", direct, { human_observation_ref: refs.s0 }),
      event(2, "boundary_observed", "a1", direct, {
        execution_pre_ref: refs.s0,
        boundary: { witness_kind: "after_native_ui_commit" }
      }),
      event(3, "action_started", "a1", direct, { execution_pre_ref: refs.s0 }),
      event(4, "action_finished", "a1", direct, { execution_pre_ref: refs.s0 }),
      event(5, "transition_proved", "a1", direct, {
        execution_pre_ref: refs.s0,
        successor_ref: refs.s1,
        proof_status: "proved_native_post_commit_boundary",
        boundary: { witness_kind: "after_native_ui_commit" }
      })
    ];
    await writeFile(path.join(root, "semantic-boundary-trace.jsonl"),
      `${events.map(JSON.stringify).join("\n")}\n`);
    await writeFile(path.join(root, "native-action-ledger.jsonl"), "");
    await writeFile(path.join(root, "run-0001.jsonl"), "");

    const report = await calibrate(root);
    assert.equal(report.summary.canonical_s_a_s_prime, 1);
    assert.equal(report.summary.proof_reasons.native_post_commit_exact, 1);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("accepts a native Commit followed by an exact owner-ready boundary", async () => {
  const { root, refs } = await fixture();
  try {
    const selected = action();
    const events = [
      event(1, "action_accepted", "a1", selected, { human_observation_ref: refs.s0 }),
      event(2, "boundary_observed", "a1", selected, { execution_pre_ref: refs.s0 }),
      event(3, "action_started", "a1", selected, { execution_pre_ref: refs.s0 }),
      event(4, "native_commit_observed", "a1", selected, { execution_pre_ref: refs.s0 }),
      event(5, "action_finished", "a1", selected, { execution_pre_ref: refs.s0 }),
      event(6, "transition_proved", "a1", selected, {
        execution_pre_ref: refs.s0,
        successor_ref: refs.s1,
        proof_status: "proved_native_commit_then_owner_boundary",
        boundary: { witness_kind: "native_decision_owner_ready" }
      })
    ];
    await writeFile(path.join(root, "semantic-boundary-trace.jsonl"),
      `${events.map(JSON.stringify).join("\n")}\n`);
    await writeFile(path.join(root, "native-action-ledger.jsonl"), "");
    await writeFile(path.join(root, "run-0001.jsonl"), "");

    const report = await calibrate(root);
    assert.equal(report.summary.canonical_s_a_s_prime, 1);
    assert.equal(report.summary.proof_reasons.native_commit_then_owner_boundary_exact, 1);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("accepts a native Commit handoff only when it equals the next execution pre", async () => {
  const { root, refs } = await fixture();
  try {
    const first = action();
    const second = action("action-2");
    const events = [
      event(1, "action_accepted", "a1", first, { human_observation_ref: refs.s0 }),
      event(2, "boundary_observed", "a1", first, { execution_pre_ref: refs.s0 }),
      event(3, "action_started", "a1", first, { execution_pre_ref: refs.s0 }),
      event(4, "native_commit_observed", "a1", first, { execution_pre_ref: refs.s0 }),
      event(5, "action_finished", "a1", first, { execution_pre_ref: refs.s0 }),
      event(6, "transition_proved", "a1", first, {
        execution_pre_ref: refs.s0,
        successor_ref: refs.s1,
        related_action_witness_id: "a2",
        proof_status: "proved_native_commit_then_execution_handoff"
      }),
      event(7, "action_accepted", "a2", second, { human_observation_ref: refs.s1 }),
      event(8, "boundary_observed", "a2", second, {
        execution_pre_ref: refs.s1,
        boundary: { immediately_consumed_by_action_witness_id: "a2" }
      }),
      event(9, "action_started", "a2", second, { execution_pre_ref: refs.s1 }),
      event(10, "transition_unknown", "a2", second, {
        execution_pre_ref: refs.s1,
        proof_status: "closed_without_boundary"
      })
    ];
    await writeFile(path.join(root, "semantic-boundary-trace.jsonl"),
      `${events.map(JSON.stringify).join("\n")}\n`);
    await writeFile(path.join(root, "native-action-ledger.jsonl"), "");
    await writeFile(path.join(root, "run-0001.jsonl"), "");

    const report = await calibrate(root);
    assert.equal(report.summary.canonical_s_a_s_prime, 1);
    assert.equal(report.summary.proof_reasons.native_commit_then_execution_handoff_exact, 1);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("fails closed when a content-addressed semantic frame is tampered", async () => {
  const { root, refs } = await fixture();
  try {
    await writeFile(path.join(root, refs.s0.object_ref), "{}\n");
    await writeFile(path.join(root, "semantic-boundary-trace.jsonl"),
      `${JSON.stringify(event(1, "action_accepted", "a1", action(), {
        human_observation_ref: refs.s0
      }))}\n${JSON.stringify(event(2, "transition_unknown", "a1", action(), {
        execution_pre_ref: refs.s0
      }))}\n`);
    await writeFile(path.join(root, "native-action-ledger.jsonl"), "");
    await writeFile(path.join(root, "run-0001.jsonl"), "");
    await assert.rejects(() => calibrate(root), /digest mismatch/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
