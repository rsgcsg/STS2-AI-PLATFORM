import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { JsonlRecorder } from "../src/jsonl-recorder.mjs";

test("durably records and rotates JSONL without retaining an event array", () => {
  const directory = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-jsonl-"));
  try {
    const file = path.join(directory, "events.jsonl");
    const recorder = new JsonlRecorder(file, { flushEvery: 1, maxBytes: 20 });
    recorder.append({ sequence: 1 });
    recorder.append({ sequence: 2 });
    recorder.close();
    assert.equal(recorder.files.length, 2);
    assert.match(readFileSync(recorder.files[0], "utf8"), /"sequence":1/u);
    assert.match(readFileSync(recorder.files[1], "utf8"), /"sequence":2/u);
    assert.throws(() => recorder.append({ sequence: 3 }), /closed/u);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
