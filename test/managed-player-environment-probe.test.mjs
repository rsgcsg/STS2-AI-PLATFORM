import assert from "node:assert/strict";
import test from "node:test";
import {
  chooseManagedPlayerEnvironmentAction,
  managedCapacityReportStatus,
  managedTerminalOutcome,
  summarizeManagedPlayerEnvironmentCapacityGroup
} from "../src/managed-player-environment-probe.mjs";

function snapshot(kind, actions, status = "interactive") {
  return {
    status,
    interaction: { kind },
    bound_actions: { status: "complete", actions }
  };
}

test("managed Player Environment probe policy consumes only canonical actions", () => {
  const play = { bound_action_id: "play", verb: "play", label: "Play Strike" };
  const end = { bound_action_id: "end", verb: "end_turn", label: "End turn" };
  assert.equal(chooseManagedPlayerEnvironmentAction(snapshot("combat_turn", [end, play])), play);
  assert.equal(chooseManagedPlayerEnvironmentAction(snapshot("event_option", [
    { bound_action_id: "b", verb: "activate", label: "B" },
    { bound_action_id: "a", verb: "activate", label: "A" }
  ])).bound_action_id, "a");
  assert.equal(chooseManagedPlayerEnvironmentAction(snapshot("reward_claim", [
    { bound_action_id: "proceed", verb: "activate", label: "Skip remaining rewards and continue" },
    {
      bound_action_id: "take",
      verb: "activate",
      label: "Claim 17 gold",
      subject: { kind: "gold", label: "17 gold" }
    }
  ])).bound_action_id, "take");
  assert.equal(chooseManagedPlayerEnvironmentAction(snapshot("card_reward_selection", [
    { bound_action_id: "skip", verb: "skip", label: "Skip card reward" },
    { bound_action_id: "take-b", verb: "select", label: "Take Zap" },
    { bound_action_id: "take-a", verb: "select", label: "Take Defend" }
  ])).bound_action_id, "take-a");
  assert.equal(chooseManagedPlayerEnvironmentAction(snapshot("treasure_relic_selection", [
    { bound_action_id: "skip-relic", verb: "skip", label: "Skip treasure relic" },
    { bound_action_id: "take-relic", verb: "select", label: "Take Bag" }
  ])).bound_action_id, "take-relic");
  assert.equal(chooseManagedPlayerEnvironmentAction(snapshot("shop_inventory", [], "visible_unsupported")), null);
});

test("managed probe records native terminal outcome without treating generic terminal as victory", () => {
  assert.deepEqual(managedTerminalOutcome({
    interaction: {
      kind: "game_over",
      content: { surface: { victory: false } }
    },
    persistent: { content: { run: { act: 1, floor: 11 } } }
  }), { victory: false, act: 1, floor: 11 });
  assert.deepEqual(managedTerminalOutcome({
    interaction: { kind: "game_over", content: { surface: {} } }
  }), { victory: null, act: null, floor: null });
  assert.equal(managedTerminalOutcome(snapshot("combat_turn", [])), null);
});

function capacityWorker(runtimeId, decisions, start, end, status = "bounded_partial_player_environment_measured") {
  return {
    report: {
      status,
      candidate: {
        manifest: { candidate_id: "candidate" },
        build: {
          source_patch_sha256: "patch",
          artifact_sha256: "artifact",
          runtime_sts2_sha256: "game"
        },
        adapter_runtime_instance_id: runtimeId,
        environment_fingerprint: "environment"
      },
      game_identity: { version: "v0", commit: "exact", runtime_main_assembly_hash: 1 },
      episode: {
        failure: null,
        seed_provenance: "game_reported_match",
        episodes_requested: 1,
        episodes_completed: 1,
        canonical_actions_attempted: decisions,
        canonical_actions_delivered: decisions,
        canonical_reads_completed: decisions + 1,
        episodes: [{ terminal: "game_over" }]
      },
      performance: {
        decision_window_started_ms: start,
        decision_window_ended_ms: end,
        decision_window_started_epoch_ms: 10_000 + start,
        decision_window_ended_epoch_ms: 10_000 + end,
        process_startup_seconds: 1,
        reset_inclusive_decision_window_seconds: (end - start) / 1000,
        delivered_decisions_per_second: decisions / ((end - start) / 1000),
        peak_rss_bytes: 100,
        resource_sample_errors: [],
        stage_totals: {},
        child_process: { cpu_ms: decisions }
      },
      process: { exit: { code: 0 }, diagnostics: [] },
      events: [{
        type: "action",
        episode_index: 0,
        action_index: decisions - 1,
        canonical_action: { label: `last-${runtimeId}` },
        delivery: "delivered",
        reason_code: null,
        detail: "settled"
      }]
    }
  };
}

test("canonical capacity aggregation requires one artifact and distinct runtimes", () => {
  const summary = summarizeManagedPlayerEnvironmentCapacityGroup([
    capacityWorker("runtime-a", 100, 100, 2_100),
    capacityWorker("runtime-b", 200, 110, 2_110)
  ], 3);
  assert.equal(summary.status, "measured_canonical_partial_unqualified");
  assert.equal(summary.delivered_canonical_decisions, 300);
  assert.equal(summary.completed_canonical_reads, 302);
  assert.equal(summary.aggregate_reset_inclusive_canonical_decisions_per_second, 300 / 2.01);
  assert.ok(Math.abs(summary.child_cpu_seconds - 0.3) < 1e-12);
  assert.equal(summary.workers[0].last_action.canonical_action.label, "last-runtime-a");
  assert.equal(summary.workers[0].last_action.detail, "settled");
  const completeSummary = summarizeManagedPlayerEnvironmentCapacityGroup([
    capacityWorker("runtime-c", 100, 100, 2_100, "bounded_player_environment_measured"),
    capacityWorker("runtime-d", 200, 110, 2_110, "bounded_player_environment_measured")
  ], 3);
  assert.equal(completeSummary.status, "measured_canonical_unqualified");
  const trainingSummary = summarizeManagedPlayerEnvironmentCapacityGroup([
    capacityWorker("runtime-e", 100, 100, 2_100, "bounded_training_profile_measured"),
    capacityWorker("runtime-f", 200, 110, 2_110, "bounded_training_profile_measured")
  ], 3);
  assert.equal(trainingSummary.status, "measured_training_profile_unqualified");
  assert.throws(() => summarizeManagedPlayerEnvironmentCapacityGroup([
    capacityWorker("runtime-a", 1, 0, 1),
    capacityWorker("runtime-a", 1, 0, 1)
  ], 1), /distinct runtime instance IDs/u);
});

test("capacity report aggregation preserves complete, partial, and unrecorded evidence", () => {
  assert.equal(managedCapacityReportStatus([
    { status: "measured_canonical_unqualified" },
    { status: "measured_canonical_unqualified" }
  ]), "measured_canonical_unqualified");
  assert.equal(managedCapacityReportStatus([
    { status: "measured_canonical_unqualified" },
    { status: "measured_canonical_partial_unqualified" }
  ]), "measured_canonical_partial_unqualified");
  assert.equal(managedCapacityReportStatus([
    { status: "measured_training_profile_unqualified" },
    { status: "measured_training_profile_unqualified" }
  ]), "measured_training_profile_unqualified");
  assert.equal(managedCapacityReportStatus([
    { status: "measured_training_profile_unqualified" },
    { status: "measured_canonical_unqualified" }
  ]), "measurement_incomplete");
});
