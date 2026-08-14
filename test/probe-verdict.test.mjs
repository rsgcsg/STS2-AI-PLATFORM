import assert from "node:assert/strict";
import test from "node:test";
import { evaluateMenuControlGate, evaluateShippedProbe } from "../src/probe-verdict.mjs";

test("requires a clear endpoint, loaded Connector, and a real snapshot", () => {
  const verdict = evaluateShippedProbe({
    endpointWasClear: true,
    processStarted: true,
    processExit: { code: 0, signal: null },
    capabilities: {
      protocol_version: "1.0.0",
      host: {
        runtime_instance_id: "runtime-1",
        host_kind: "headless",
        implementation: { artifact_sha256: "abc", module_version_id: "mvid" }
      },
      game: {
        version: "test",
        modset: { status: "exact_player_environment_only" }
      }
    },
    snapshots: [{
      ok: true,
      value: {
        snapshot_id: "state-1",
        status: "interactive",
        bound_actions: { status: "complete", actions: [{ bound_action_id: "action-1" }] }
      }
    }],
    stdout: "DisplayServer: headless"
  });
  assert.equal(verdict.verdict, "h0_pass");
  assert.equal(verdict.snapshot_id, "state-1");
  assert.equal(verdict.headless_driver_log_evidence, true);
});

test("does not mistake process startup for a qualified probe", () => {
  const verdict = evaluateShippedProbe({
    endpointWasClear: true,
    processStarted: true,
    processExit: { code: 1, signal: null },
    capabilities: null,
    snapshots: []
  });
  assert.equal(verdict.verdict, "h0_incomplete");
  assert.deepEqual(verdict.errors, [
    "connector_not_observed",
    "player_environment_snapshot_not_observed",
    "headless_display_driver_not_observed"
  ]);
});

test("rejects a Connector that reports a live UI host or extra Mods", () => {
  const verdict = evaluateShippedProbe({
    endpointWasClear: true,
    processStarted: true,
    processExit: { code: 0, signal: null },
    capabilities: {
      host: { runtime_instance_id: "runtime-1", host_kind: "live_ui" },
      game: { modset: { status: "additional_loaded_mods" } }
    },
    snapshots: [{
      ok: true,
      value: {
        status: "interactive",
        bound_actions: { status: "complete", actions: [{ bound_action_id: "action-1" }] }
      }
    }],
    stdout: "Rendering device name: N/A (headless)"
  });
  assert.deepEqual(verdict.errors, [
    "connector_host_kind_not_headless",
    "unsupported_modset"
  ]);
});

test("rejects evidence when the endpoint belonged to an older process", () => {
  const verdict = evaluateShippedProbe({
    endpointWasClear: false,
    processStarted: true,
    processExit: { code: 0, signal: null },
    capabilities: {
      host: { runtime_instance_id: "ambiguous", host_kind: "headless" },
      game: { modset: { status: "exact_player_environment_only" } }
    },
    snapshots: [{
      ok: true,
      value: {
        snapshot_id: "state-1",
        status: "interactive",
        bound_actions: { status: "complete", actions: [{ bound_action_id: "action-1" }] }
      }
    }],
    stdout: "headless"
  });
  assert.equal(verdict.verdict, "h0_incomplete");
  assert.deepEqual(verdict.errors, ["endpoint_was_already_owned"]);
});

test("does not promote a settling snapshot to H0 pass", () => {
  const verdict = evaluateShippedProbe({
    endpointWasClear: true,
    processStarted: true,
    processExit: { code: 0, signal: null },
    capabilities: {
      host: { runtime_instance_id: "runtime-1", host_kind: "headless" },
      game: { modset: { status: "exact_player_environment_only" } }
    },
    snapshots: [{
      ok: true,
      value: {
        snapshot_id: "state-1",
        status: "settling",
        bound_actions: { status: "complete", actions: [] }
      }
    }],
    stdout: "Rendering device name: N/A (headless)"
  });
  assert.equal(verdict.verdict, "h0_incomplete");
  assert.deepEqual(verdict.errors, ["interactive_decision_not_mounted"]);
});

test("accepts delivery, duplicate idempotency, stale refusal, and successor", () => {
  const receipt = {
    request_id: "request-1",
    delivery: "delivered",
    action: { bound_action_id: "action-1" }
  };
  const verdict = evaluateMenuControlGate({
    initialSnapshot: { snapshot_id: "state-1", interaction: { kind: "main_menu" } },
    receipt,
    duplicateReceipt: { ...receipt },
    staleReceipt: {
      request_id: "request-2",
      delivery: "not_delivered",
      reason_code: "stale_snapshot",
      retry: { allowed: true, reason: "fresh_snapshot_required" }
    },
    successorSnapshot: {
      snapshot_id: "state-2",
      status: "interactive",
      interaction: { kind: "singleplayer_menu" }
    }
  });
  assert.equal(verdict.verdict, "h1_pass");
  assert.deepEqual(verdict.errors, []);
});

test("rejects a duplicate that resolves to a different action", () => {
  const verdict = evaluateMenuControlGate({
    initialSnapshot: { interaction: { kind: "main_menu" } },
    receipt: {
      request_id: "request-1",
      delivery: "delivered",
      action: { bound_action_id: "action-1" }
    },
    duplicateReceipt: {
      request_id: "request-1",
      delivery: "delivered",
      action: { bound_action_id: "action-2" }
    },
    staleReceipt: {
      delivery: "not_delivered",
      reason_code: "stale_snapshot",
      retry: { allowed: true, reason: "fresh_snapshot_required" }
    },
    successorSnapshot: { status: "interactive", interaction: { kind: "singleplayer_menu" } }
  });
  assert.deepEqual(verdict.errors, ["duplicate_request_not_idempotent"]);
});
