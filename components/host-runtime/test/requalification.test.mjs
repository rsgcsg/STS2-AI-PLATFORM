import assert from "node:assert/strict";
import test from "node:test";
import { planRuntimeRequalification } from "../src/requalification.mjs";

const expected = Object.freeze({
  id: "fixture-runtime",
  platform: "win32",
  architecture: "x64",
  gameVersion: "v1",
  gameCommit: "commit-a",
  executableSha256: "exe-a",
  runtimeMainAssemblyHash: 1,
  sts2AssemblySha256: "sts2-a",
  godotSharpAssemblySha256: "godot-a"
});

function identity(overrides = {}) {
  const value = { ...expected, ...overrides };
  return {
    platform: value.platform,
    architecture: value.architecture,
    release: { version: value.gameVersion, commit: value.gameCommit },
    executable: { sha256: value.executableSha256 },
    runtime_main_assembly_hash: value.runtimeMainAssemblyHash,
    sts2_assembly: { sha256: value.sts2AssemblySha256 },
    godotsharp_assembly: { sha256: value.godotSharpAssemblySha256 }
  };
}

test("an exact supported runtime requires no new qualification gates", () => {
  const result = planRuntimeRequalification(identity(), { supported: [expected], experimental: [] });
  assert.equal(result.status, "supported_identity_unchanged");
  assert.equal(result.authority, "supported_exact");
  assert.deepEqual(result.required_gates, []);
});

test("an exact experimental runtime remains fail closed", () => {
  const result = planRuntimeRequalification(identity(), { supported: [], experimental: [expected] });
  assert.equal(result.status, "known_experimental_qualification_required");
  assert.equal(result.authority, "fail_closed");
  assert.ok(result.required_gates.includes("bounded_journey"));
  assert.equal(result.automatic_promotion, false);
});

test("assembly drift requires source audit and all runtime gates", () => {
  const result = planRuntimeRequalification(identity({ sts2AssemblySha256: "sts2-b" }), {
    supported: [expected],
    experimental: []
  });
  assert.equal(result.status, "identity_drift_requalification_required");
  assert.deepEqual(result.identity_mismatches, ["sts2AssemblySha256"]);
  assert.equal(result.source_audit_required, true);
  assert.ok(result.required_gates.includes("exact_assembly_inventory_and_decompilation"));
  assert.ok(result.required_gates.includes("targeted_semantic_differential"));
});

test("platform or executable drift requires Host qualification", () => {
  const result = planRuntimeRequalification(identity({ executableSha256: "exe-b" }), {
    supported: [expected],
    experimental: []
  });
  assert.equal(result.host_qualification_required, true);
  assert.ok(result.required_gates.includes("host_bootstrap_and_process_lifecycle_review"));
  assert.equal(result.authority, "fail_closed");
});
