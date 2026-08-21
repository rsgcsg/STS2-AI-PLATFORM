import assert from "node:assert/strict";
import test from "node:test";
import {
  settleReferenceReceipt,
  ShippedPlayerEnvironmentSession
} from "../src/shipped-player-environment.mjs";
import { handleReferenceDriverRequest } from "../tools/reference-pe-driver.mjs";

function fakeEpisode(seed, events) {
  const snapshot = { snapshot_id: `snapshot-${seed}`, interaction: { kind: "main_menu" } };
  return {
    snapshot,
    identity: { seed },
    observe: async () => snapshot,
    read: async (input) => ({ ...input, status: "observed" }),
    submit: async (input) => ({ ...input, delivery: "delivered", successor: snapshot }),
    close: async () => {
      events.push(`close:${seed}`);
      return { code: 0, signal: null };
    }
  };
}

test("Reference Player Environment resets by replacing the exact runtime", async () => {
  const events = [];
  const session = new ShippedPlayerEnvironmentSession(
    { marker: "options" },
    { startEpisode: async ({ seed, marker }) => {
      assert.equal(marker, "options");
      events.push(`start:${seed}`);
      return fakeEpisode(seed, events);
    } }
  );
  assert.equal((await session.reset("FIRST")).snapshot_id, "snapshot-FIRST");
  assert.equal((await session.reset("SECOND")).snapshot_id, "snapshot-SECOND");
  await session.close();
  assert.deepEqual(events, ["start:FIRST", "close:FIRST", "start:SECOND", "close:SECOND"]);
});

test("Reference JSONL requests preserve exact action and snapshot bindings", async () => {
  const calls = [];
  const session = {
    lastIdentity: { runtime_instance_id: "runtime-1" },
    reset: async (seed) => ({ snapshot_id: `snapshot-${seed}` }),
    observe: async () => ({ snapshot_id: "snapshot-current" }),
    read: async (input) => ({ status: "observed", ...input }),
    submit: async (input) => {
      calls.push(input);
      return { delivery: "delivered" };
    },
    provenance: async () => ({
      runtime_instance_id: "runtime-1",
      episode_provenance: { verdict: "provenance_pass", actual_seed: "EXACTSEED" }
    }),
    close: async () => ({ code: 0, signal: null })
  };
  const reset = await handleReferenceDriverRequest(session, {
    command: "reset", request_id: "transport-1", seed: "EXACTSEED"
  });
  assert.equal(reset.snapshot.snapshot_id, "snapshot-EXACTSEED");
  const step = await handleReferenceDriverRequest(session, {
    command: "step",
    request_id: "transport-2",
    mutation_request_id: "mutation-1",
    expected_snapshot_id: "snapshot-current",
    bound_action_id: "bound-action-1"
  });
  assert.equal(step.receipt.delivery, "delivered");
  assert.deepEqual(calls, [{
    requestId: "mutation-1",
    expectedSnapshotId: "snapshot-current",
    boundActionId: "bound-action-1"
  }]);
  const identity = await handleReferenceDriverRequest(session, {
    command: "episode_identity", request_id: "transport-3"
  });
  assert.equal(identity.identity.episode_provenance.verdict, "provenance_pass");
});

test("Reference delivery observes settling without retrying the mutation", async () => {
  const observed = [
    { snapshot_id: "before", status: "settling" },
    { snapshot_id: "after", status: "interactive" }
  ];
  let polls = 0;
  const receipt = await settleReferenceReceipt({
    receipt: {
      request_id: "mutation-1",
      delivery: "delivered",
      successor: { snapshot_id: "before", status: "settling" }
    },
    expectedSnapshotId: "before",
    observe: async () => observed[polls++],
    child: { exitCode: null, signalCode: null },
    timeoutMs: 50,
    pollIntervalMs: 0
  });
  assert.equal(receipt.delivery, "delivered");
  assert.equal(receipt.request_id, "mutation-1");
  assert.equal(receipt.successor.snapshot_id, "after");
  assert.equal(receipt.successor_observation, "driver_observed_after_delivery");
  assert.equal(polls, 2);
});
