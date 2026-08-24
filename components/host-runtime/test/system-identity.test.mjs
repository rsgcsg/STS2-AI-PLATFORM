import assert from "node:assert/strict";
import test from "node:test";
import { readSystemIdentity } from "../src/system-identity.mjs";

test("records portable machine context without inventing unavailable hardware facts", () => {
  const identity = readSystemIdentity();
  assert.equal(identity.platform, process.platform);
  assert.equal(identity.architecture, process.arch);
  assert.ok(identity.logical_cpu_count >= 1);
  assert.ok(identity.total_memory_bytes > 0);
  if (process.platform === "darwin") {
    assert.ok(identity.physical_core_count >= 1);
  } else if (identity.physical_core_count == null) {
    assert.ok(identity.unavailable_fields.includes("physical_core_count"));
  }
  assert.ok(identity.unavailable_fields.includes("storage_kind"));
});
