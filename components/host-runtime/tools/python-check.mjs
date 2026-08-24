import { spawnSync } from "node:child_process";
import { delimiter, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(fileURLToPath(new URL("..", import.meta.url)));
const consumerRoot = resolve(root, "consumers", "python");

function pythonCandidates() {
  const configured = process.env.PYTHON?.trim();
  const candidates = configured ? [[configured]] : [];
  if (process.platform === "win32") {
    candidates.push(["python"], ["py", "-3"]);
  } else {
    candidates.push(["python3"], ["python"]);
  }
  return candidates;
}

function selectPython() {
  for (const candidate of pythonCandidates()) {
    const result = spawnSync(candidate[0], [...candidate.slice(1), "--version"], {
      cwd: root,
      encoding: "utf8",
      windowsHide: true,
    });
    if (result.status === 0) return candidate;
  }
  throw new Error("Python 3 is required; set PYTHON to an exact interpreter path");
}

function run(python, args) {
  const inherited = process.env.PYTHONPATH?.trim();
  const pythonPath = inherited ? `${consumerRoot}${delimiter}${inherited}` : consumerRoot;
  const result = spawnSync(python[0], [...python.slice(1), ...args], {
    cwd: root,
    env: { ...process.env, PYTHONPATH: pythonPath },
    stdio: "inherit",
    windowsHide: true,
  });
  if (result.error) throw result.error;
  return result.status ?? 1;
}

const mode = process.argv[2];
const python = selectPython();
let status;
if (mode === "test") {
  status = run(python, ["-m", "unittest", "discover", "-s", "consumers/python/tests", "-v"]);
} else if (mode === "check") {
  status = run(python, [
    "-m",
    "compileall",
    "-q",
    "consumers/python/sts2_headless",
    "consumers/python/tests",
  ]);
  if (status === 0) {
    status = run(python, ["-m", "unittest", "discover", "-s", "consumers/python/tests", "-v"]);
  }
} else if (mode === "smoke") {
  status = run(python, ["-m", "sts2_headless.smoke", ...process.argv.slice(3)]);
} else {
  throw new Error("usage: node tools/python-check.mjs <check|test|smoke> [smoke arguments]");
}
process.exitCode = status;
