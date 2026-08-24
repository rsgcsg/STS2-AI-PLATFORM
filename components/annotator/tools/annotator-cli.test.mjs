import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import path from "node:path";
import test from "node:test";

test("Annotator CLI exposes portable and exact-game entry points", () => {
  const result = spawnSync(process.execPath, [path.join(import.meta.dirname, "annotator.mjs"), "--help"], {
    encoding: "utf8"
  });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /Exact-game lifecycle:/u);
  assert.match(result.stdout, /pack-session/u);
});
