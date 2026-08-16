import assert from "node:assert/strict";
import test from "node:test";
import {
  requestHostProvenance,
  requestHostShutdown
} from "../src/runtime-probe.mjs";

test("Host control routes share authentication without sharing response semantics", async (context) => {
  const calls = [];
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (url, options) => {
    calls.push({ url, body: JSON.parse(options.body) });
    const provenance = url.endsWith("/provenance");
    return {
      ok: true,
      status: 200,
      json: async () => provenance
        ? { status: "seed_observed" }
        : { status: "shutdown_requested" }
    };
  };
  context.after(() => {
    globalThis.fetch = originalFetch;
  });

  const common = {
    endpoint: "http://127.0.0.1:15526",
    hostControlToken: "a".repeat(64),
    expectedRuntimeInstanceId: "runtime-1"
  };
  assert.equal((await requestHostProvenance(common)).status, "observed");
  assert.equal((await requestHostShutdown(common)).status, "requested");
  assert.deepEqual(calls.map((call) => call.url), [
    "http://127.0.0.1:15526/api/host-control/provenance",
    "http://127.0.0.1:15526/api/host-control/shutdown"
  ]);
  assert.deepEqual(calls[0].body, {
    expected_runtime_instance_id: "runtime-1",
    host_control_token: "a".repeat(64)
  });
});

test("Host provenance remains unavailable without exact process-local credentials", async () => {
  const result = await requestHostProvenance({
    endpoint: "http://127.0.0.1:15526",
    hostControlToken: null,
    expectedRuntimeInstanceId: "runtime-1"
  });
  assert.equal(result.status, "unavailable");
  assert.equal(result.error, "host_control_not_configured");
});
