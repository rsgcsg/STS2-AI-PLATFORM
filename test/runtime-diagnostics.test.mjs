import assert from "node:assert/strict";
import test from "node:test";
import {
  analyzeRuntimeDiagnostics,
  evaluateNativeShutdownContainment
} from "../src/runtime-diagnostics.mjs";

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
  assert.equal(result.phase_classification, "not_partitioned");
});

test("admits only exact diagnostics in their observed lifecycle phase", () => {
  const result = analyzeRuntimeDiagnostics({
    stderr: [
      "ERROR: Parameter \"t\" is null.",
      "ERROR: Cannot get path of node as it is not in a scene tree.",
      "ERROR: 2 resources still in use at exit."
    ].join("\n"),
    beforeNativeShutdownStderr: "ERROR: Parameter \"t\" is null.\n",
    afterNativeShutdownStderr: [
      "ERROR: Cannot get path of node as it is not in a scene tree.",
      "ERROR: 2 resources still in use at exit."
    ].join("\n")
  });
  assert.equal(result.phase_classification, "known_phase_scoped_diagnostics_only");
  assert.equal(result.phases.before_native_shutdown.error_line_count, 1);
  assert.equal(result.phases.after_native_shutdown.error_line_count, 2);
  const containment = evaluateNativeShutdownContainment({
    diagnostics: result,
    processCleanup: { code: 0, forced: false, host_shutdown: { status: "requested" } }
  });
  assert.equal(containment.verdict, "bounded_containment_candidate");
  assert.equal(containment.qualification, "not_qualified");
});

test("rejects unknown diagnostics and forced process termination", () => {
  const result = analyzeRuntimeDiagnostics({
    stderr: "ERROR: New failure\n",
    beforeNativeShutdownStderr: "ERROR: New failure\n",
    afterNativeShutdownStderr: ""
  });
  const containment = evaluateNativeShutdownContainment({
    diagnostics: result,
    processCleanup: { code: 0, forced: true, host_shutdown: { status: "rejected" } }
  });
  assert.equal(result.phase_classification, "unclassified_or_wrong_phase_diagnostics");
  assert.equal(containment.verdict, "shutdown_containment_rejected");
  assert.ok(containment.errors.includes("forced_shutdown_used"));
});
