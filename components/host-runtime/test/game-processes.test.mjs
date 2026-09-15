import assert from "node:assert/strict";
import test from "node:test";
import { listGameProcesses, readGameProcessStartedAt } from "../src/game-processes.mjs";

test("Unix process discovery uses executable names rather than setup arguments", () => {
  const calls = [];
  const result = listGameProcesses("darwin", { failClosed: true, spawnProcess: (...args) => {
    calls.push(args);
    return { status: 0, stdout: "100 /Library/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2\n101 node\n102 SlayTheSpire2\n103 SlayTheSpire2Helper\n" };
  } });
  assert.deepEqual(calls[0].slice(0, 2), ["ps", ["-Ao", "pid=,comm="]]);
  assert.deepEqual(result, ["100 /Library/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2", "102 SlayTheSpire2"]);
});

test("Unix enumeration failure stays unknown under strict discovery", () => {
  assert.throws(() => listGameProcesses("linux", { failClosed: true, spawnProcess: () => ({ status: 1 }) }), /Could not enumerate/);
});

test("process generation is an OS observation and unavailable starts fail closed", () => {
  assert.equal(readGameProcessStartedAt(100, "win32", { spawnProcess: () => ({ status: 0, stdout: "2026-09-15T00:00:00Z" }) }), "2026-09-15T00:00:00.000Z");
  assert.throws(() => readGameProcessStartedAt(100, "linux", { spawnProcess: () => ({ status: 1, stdout: "" }) }), /generation_unavailable/);
  assert.throws(() => readGameProcessStartedAt("100;command", "win32"), /id_invalid/);
});
