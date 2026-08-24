import assert from "node:assert/strict";
import test from "node:test";
import { resumeProcess, suspendProcess } from "../src/process-faults.mjs";

test("process fault controls reject an invalid process identifier", () => {
  assert.throws(() => suspendProcess(0), /positive process ID/u);
  assert.throws(() => resumeProcess(-1), /positive process ID/u);
});
