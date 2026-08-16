const SIGNATURES = Object.freeze([
  Object.freeze({
    id: "godot_invalid_task_id",
    pattern: /Invalid Task ID/iu,
    admitted_phases: Object.freeze([])
  }),
  Object.freeze({
    id: "godot_null_texture_parameter",
    pattern: /^ERROR: Parameter "t" is null\.$/iu,
    admitted_phases: Object.freeze(["before_native_shutdown"])
  }),
  Object.freeze({
    id: "godot_node_path_after_tree_exit",
    pattern: /^ERROR: Cannot get path of node as it is not in a scene tree\.$/iu,
    admitted_phases: Object.freeze(["after_native_shutdown"])
  }),
  Object.freeze({
    id: "godot_rid_leak_at_exit",
    pattern: /^ERROR: \d+ RID allocations? of type '.+' (?:was|were) leaked at exit\.$/iu,
    admitted_phases: Object.freeze(["after_native_shutdown"])
  }),
  Object.freeze({
    id: "godot_resources_in_use_at_exit",
    pattern: /^ERROR: \d+ resources? still in use at exit\.$/iu,
    admitted_phases: Object.freeze(["after_native_shutdown"])
  }),
  Object.freeze({
    id: "managed_unhandled_exception",
    pattern: /Unhandled exception/iu,
    admitted_phases: Object.freeze([])
  })
]);

function errorLines(text) {
  return text.split(/\r?\n/u)
    .map((line) => line.trim())
    .filter((line) => /^(?:ERROR|FATAL|Unhandled exception)/iu.test(line));
}

function analyzeSegment(text, phase) {
  const lines = errorLines(text);
  const counts = new Map();
  const unclassified = [];
  const wrongPhase = [];
  for (const line of lines) {
    const signature = SIGNATURES.find((entry) => entry.pattern.test(line));
    if (signature == null) {
      unclassified.push(line);
      continue;
    }
    counts.set(signature.id, (counts.get(signature.id) ?? 0) + 1);
    if (!signature.admitted_phases.includes(phase)) wrongPhase.push(line);
  }
  return {
    phase,
    status: lines.length === 0 ? "clean" : "runtime_errors_observed",
    error_line_count: lines.length,
    signatures: [...counts.entries()].map(([id, count]) => ({ id, count })),
    unclassified_error_line_count: unclassified.length,
    wrong_phase_error_line_count: wrongPhase.length,
    sample_error_lines: lines.slice(0, 20),
    sample_unclassified_error_lines: unclassified.slice(0, 10),
    sample_wrong_phase_error_lines: wrongPhase.slice(0, 10)
  };
}

export function analyzeRuntimeDiagnostics({
  stdout = "",
  stderr = "",
  beforeNativeShutdownStderr = null,
  afterNativeShutdownStderr = null
} = {}) {
  const all = analyzeSegment(`${stdout}\n${stderr}`, "unpartitioned");
  const partitioned = beforeNativeShutdownStderr != null && afterNativeShutdownStderr != null;
  const phases = partitioned
    ? {
        before_native_shutdown: analyzeSegment(
          beforeNativeShutdownStderr,
          "before_native_shutdown"
        ),
        after_native_shutdown: analyzeSegment(
          afterNativeShutdownStderr,
          "after_native_shutdown"
        )
      }
    : null;
  const phaseClassification = !partitioned
    ? "not_partitioned"
    : phases.before_native_shutdown.unclassified_error_line_count === 0
      && phases.before_native_shutdown.wrong_phase_error_line_count === 0
      && phases.after_native_shutdown.unclassified_error_line_count === 0
      && phases.after_native_shutdown.wrong_phase_error_line_count === 0
      ? "known_phase_scoped_diagnostics_only"
      : "unclassified_or_wrong_phase_diagnostics";
  return {
    status: all.error_line_count === 0 ? "clean" : "runtime_errors_observed",
    stderr_bytes: Buffer.byteLength(stderr),
    error_line_count: all.error_line_count,
    signatures: all.signatures,
    sample_error_lines: all.sample_error_lines,
    phase_classification: phaseClassification,
    phases
  };
}

export function evaluateNativeShutdownContainment({ diagnostics, processCleanup }) {
  const errors = [];
  if (processCleanup?.host_shutdown?.status !== "requested") {
    errors.push("native_shutdown_not_requested");
  }
  if (processCleanup?.code !== 0) errors.push("native_shutdown_nonzero_exit");
  if (processCleanup?.forced !== false) errors.push("forced_shutdown_used");
  if (diagnostics?.phase_classification !== "known_phase_scoped_diagnostics_only") {
    errors.push("diagnostics_not_exactly_phase_contained");
  }
  const clean = diagnostics?.status === "clean";
  return {
    verdict: errors.length > 0
      ? "shutdown_containment_rejected"
      : clean
        ? "clean_shutdown"
        : "bounded_containment_candidate",
    errors,
    qualification: "not_qualified",
    non_claim: "One bounded exact-artifact classification is not long-soak containment qualification."
  };
}
