import assert from "node:assert/strict";
import test from "node:test";

import { waitForLoadedReadiness } from "../loaded-readiness.mjs";

test("loaded readiness returns an immediately coherent runtime", async () => {
  const result = await waitForLoadedReadiness(async () => ({ ready: true, attempt: 1 }), {
    timeoutMs: 10,
    sleep: async () => {}
  });
  assert.deepEqual(result, { ready: true, attempt: 1 });
});

test("loaded readiness tolerates stale component identities during cold start", async () => {
  let attempt = 0;
  let clock = 0;
  const result = await waitForLoadedReadiness(async () => {
    attempt += 1;
    return { ready: attempt === 3, attempt };
  }, {
    timeoutMs: 10,
    intervalMs: 1,
    now: () => clock,
    sleep: async (milliseconds) => { clock += milliseconds; }
  });
  assert.deepEqual(result, { ready: true, attempt: 3 });
});

test("loaded readiness returns the latest incoherent state at the deadline", async () => {
  let attempt = 0;
  let clock = 0;
  const result = await waitForLoadedReadiness(async () => ({ ready: false, attempt: ++attempt }), {
    timeoutMs: 2,
    intervalMs: 1,
    now: () => clock,
    sleep: async (milliseconds) => { clock += milliseconds; }
  });
  assert.deepEqual(result, { ready: false, attempt: 3 });
});

test("loaded readiness surfaces the last probe error when no state was readable", async () => {
  let clock = 0;
  await assert.rejects(
    waitForLoadedReadiness(async () => { throw new Error("endpoint unavailable"); }, {
      timeoutMs: 1,
      intervalMs: 1,
      now: () => clock,
      sleep: async (milliseconds) => { clock += milliseconds; }
    }),
    /endpoint unavailable/u
  );
});
