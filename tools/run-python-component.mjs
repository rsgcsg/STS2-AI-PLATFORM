#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const [relativeCwd, ...args] = process.argv.slice(2);
if (!relativeCwd || args.length === 0) {
  throw new Error("usage: run-python-component.mjs <workspace-relative-cwd> <python args...>");
}
const candidates = process.env.PYTHON
  ? [process.env.PYTHON]
  : process.platform === "win32"
    ? ["python", "python3"]
    : ["python", "python3"];
let last;
for (const executable of candidates) {
  const probe = spawnSync(executable, ["-c", "import sys; assert sys.version_info >= (3, 11)"], {
    encoding: "utf8"
  });
  if (probe.status !== 0) {
    last = probe;
    continue;
  }
  const result = spawnSync(executable, args, {
    cwd: path.join(root, relativeCwd),
    encoding: "utf8",
    stdio: "inherit"
  });
  process.exit(result.status ?? 1);
}
throw new Error(
  `Python >=3.11 is required; set PYTHON to a supported interpreter.${last?.stderr ? ` ${last.stderr.trim()}` : ""}`
);
