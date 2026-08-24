import assert from "node:assert/strict";
import test from "node:test";
import {
  evaluateAutoSlayerUpperBound,
  parseAutoSlayerLog,
  requireConnectorOnlyModset
} from "../src/autoslayer-experiment.mjs";

test("parses official AutoSlayer progress without calling it semantic decisions", () => {
  const parsed = parseAutoSlayerLog([
    "12:00:00.000 [INFO] [AutoSlay] Starting run with seed=ABC",
    "12:00:01.000 [INFO] [AutoSlay] Entering Monster room (Act 1, Floor 1)",
    "12:00:02.000 [INFO] [AutoSlay] Action: Playing card",
    "12:00:03.000 [INFO] [AutoSlay] Entering RestSite room (Act 1, Floor 5)",
    "12:00:04.000 [INFO] [AutoSlay] Run completed successfully with seed=ABC"
  ].join("\n"));
  assert.equal(parsed.started, true);
  assert.equal(parsed.completed, true);
  assert.equal(parsed.room_entries, 2);
  assert.equal(parsed.max_act_floor_observed, 5);
  assert.equal(parsed.native_action_log_entries, 1);
});

test("admits only the exact Connector-only disk Modset before replacement", () => {
  const exact = ["STS2_MCP.dll", "STS2_MCP.json", "STS2_MCP.conf"]
    .map((name) => ({ name, size: 1, sha256: "x" }));
  assert.equal(requireConnectorOnlyModset(exact), exact);
  assert.throws(
    () => requireConnectorOnlyModset([...exact, { name: "other.dll", size: 1, sha256: "y" }]),
    /exact Connector-only disk Modset/u
  );
});

test("upper-bound verdict requires completion, rollback and profile isolation", () => {
  const pass = evaluateAutoSlayerUpperBound({
    processExit: { code: 0 },
    parsedLog: { started: true, completed: true, failed: false, room_entries: 49 },
    timedOut: false,
    rollbackVerified: true,
    sharedProfileUnchanged: true
  });
  assert.equal(pass.verdict, "autoslayer_upper_bound_pass");
  assert.equal(pass.normalized_semantic_decisions, null);
  assert.equal(pass.qualification, "not_qualified");
  const rejected = evaluateAutoSlayerUpperBound({
    processExit: { code: 0 },
    parsedLog: { started: true, completed: true, failed: false, room_entries: 49 },
    timedOut: false,
    rollbackVerified: false,
    sharedProfileUnchanged: true
  });
  assert.equal(rejected.verdict, "autoslayer_upper_bound_failed");
});
