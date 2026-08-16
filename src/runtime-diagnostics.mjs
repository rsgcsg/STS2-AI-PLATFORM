const SIGNATURES = Object.freeze([
  Object.freeze({ id: "godot_invalid_task_id", pattern: /Invalid Task ID/giu }),
  Object.freeze({ id: "godot_null_texture_parameter", pattern: /Parameter "t" is null/giu }),
  Object.freeze({ id: "managed_unhandled_exception", pattern: /Unhandled exception/giu })
]);

function countMatches(text, pattern) {
  return [...text.matchAll(pattern)].length;
}

export function analyzeRuntimeDiagnostics({ stdout = "", stderr = "" } = {}) {
  const signatures = SIGNATURES.map(({ id, pattern }) => ({
    id,
    count: countMatches(`${stdout}\n${stderr}`, pattern)
  })).filter((entry) => entry.count > 0);
  const errorLines = stderr.split(/\r?\n/u)
    .map((line) => line.trim())
    .filter((line) => /^(?:ERROR|FATAL|Unhandled exception)/iu.test(line));
  return {
    status: errorLines.length === 0 && signatures.length === 0 ? "clean" : "runtime_errors_observed",
    stderr_bytes: Buffer.byteLength(stderr),
    error_line_count: errorLines.length,
    signatures,
    sample_error_lines: errorLines.slice(0, 20)
  };
}
