import assert from "node:assert/strict";
import test from "node:test";
import { analyzeRuntimeDiagnostics } from "../src/runtime-diagnostics.mjs";

test("accepts an empty runtime stderr stream", () => {
  assert.equal(analyzeRuntimeDiagnostics({ stderr: "" }).status, "clean");
});

test("records known and generic runtime errors without hiding them", () => {
  const result = analyzeRuntimeDiagnostics({
    stderr: "ERROR: Invalid Task ID\nERROR: Invalid Task ID\n"
  });
  assert.equal(result.status, "runtime_errors_observed");
  assert.equal(result.error_line_count, 2);
  assert.deepEqual(result.signatures, [{ id: "godot_invalid_task_id", count: 2 }]);
});
