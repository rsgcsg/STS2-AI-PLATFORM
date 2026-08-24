import assert from "node:assert/strict";
import test from "node:test";
import {
  EXPERIMENTAL_RUNTIMES,
  evaluateRuntimeCompatibility,
  SUPPORTED_RUNTIME
} from "../src/compatibility.mjs";

function exactIdentity(runtime = SUPPORTED_RUNTIME) {
  return {
    platform: runtime.platform,
    architecture: runtime.architecture,
    release: {
      version: runtime.gameVersion,
      commit: runtime.gameCommit
    },
    executable: { sha256: runtime.executableSha256 },
    runtime_main_assembly_hash: runtime.runtimeMainAssemblyHash,
    sts2_assembly: { sha256: runtime.sts2AssemblySha256 },
    godotsharp_assembly: { sha256: runtime.godotSharpAssemblySha256 }
  };
}

test("admits only the exact runtime tuple", () => {
  assert.equal(evaluateRuntimeCompatibility(exactIdentity()).status, "supported_exact");
  const changed = exactIdentity();
  changed.release.commit = "different";
  changed.sts2_assembly.sha256 = "different";
  assert.deepEqual(evaluateRuntimeCompatibility(changed).mismatches, [
    "gameCommit",
    "sts2AssemblySha256"
  ]);
});

test("missing identity fails closed", () => {
  const result = evaluateRuntimeCompatibility(null);
  assert.equal(result.status, "unsupported");
  assert.equal(result.mismatches.length, 8);
});

test("recognizes an exact candidate without promoting it to support", () => {
  const result = evaluateRuntimeCompatibility(exactIdentity(EXPERIMENTAL_RUNTIMES[0]));
  assert.equal(result.status, "known_experimental");
  assert.equal(result.support_id, EXPERIMENTAL_RUNTIMES[0].id);
  assert.deepEqual(result.mismatches, []);
});
