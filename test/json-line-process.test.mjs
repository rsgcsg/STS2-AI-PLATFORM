import assert from "node:assert/strict";
import test from "node:test";
import { startJsonLineProcess } from "../src/json-line-process.mjs";

const echoServer = String.raw`
const readline = require("node:readline");
console.log("boot diagnostic");
console.log(JSON.stringify({type:"ready", version:"test"}));
const lines = readline.createInterface({input: process.stdin});
lines.on("line", (line) => {
  const value = JSON.parse(line);
  console.log(JSON.stringify({type:"reply", value}));
  if (value.cmd === "quit") process.exit(0);
});
`;

test("JSON-line driver attributes one reply to one request and retains diagnostics", async () => {
  const { process: child, ready } = await startJsonLineProcess({
    command: process.execPath,
    args: ["-e", echoServer],
    readyTimeoutMs: 2_000
  });
  assert.deepEqual(ready, { type: "ready", version: "test" });
  assert.deepEqual(await child.request({ cmd: "step", n: 1 }, 2_000), {
    type: "reply",
    value: { cmd: "step", n: 1 }
  });
  assert.match(child.diagnostics[0].line, /boot diagnostic/u);
  const exit = await child.stop({ request: { cmd: "quit" }, timeoutMs: 2_000 });
  assert.equal(exit.code, 0);
});

test("JSON-line driver rejects concurrent requests", async () => {
  const delayedServer = String.raw`
  const readline = require("node:readline");
  console.log(JSON.stringify({type:"ready"}));
  readline.createInterface({input: process.stdin}).on("line", (line) => {
    setTimeout(() => console.log(JSON.stringify(JSON.parse(line))), 100);
  });
  `;
  const { process: child } = await startJsonLineProcess({
    command: process.execPath,
    args: ["-e", delayedServer],
    readyTimeoutMs: 2_000
  });
  const first = child.request({ n: 1 }, 2_000);
  await assert.rejects(child.request({ n: 2 }, 2_000), /one in-flight request/u);
  assert.deepEqual(await first, { n: 1 });
  await child.stop({ timeoutMs: 2_000 });
});
