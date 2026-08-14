import assert from "node:assert/strict";
import test from "node:test";
import {
  evaluateRuntimeCompatibility,
  SUPPORTED_RUNTIME
} from "../src/compatibility.mjs";

function exactIdentity() {
  return {
    platform: SUPPORTED_RUNTIME.platform,
    architecture: SUPPORTED_RUNTIME.architecture,
    release: {
      version: SUPPORTED_RUNTIME.gameVersion,
      commit: SUPPORTED_RUNTIME.gameCommit
    },
    executable: { sha256: SUPPORTED_RUNTIME.executableSha256 },
    runtime_main_assembly_hash: SUPPORTED_RUNTIME.runtimeMainAssemblyHash,
    sts2_assembly: { sha256: SUPPORTED_RUNTIME.sts2AssemblySha256 },
    godotsharp_assembly: { sha256: SUPPORTED_RUNTIME.godotSharpAssemblySha256 }
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
