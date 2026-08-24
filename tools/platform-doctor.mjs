#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readIdentityReport } from "./component-identity.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const commands = [
  ["connector", ["--prefix", "components/connector", "run", "doctor", "--silent"]],
  ["host-runtime", ["--prefix", "components/host-runtime", "run", "doctor", "--silent"]],
  ["annotator", ["--prefix", "components/annotator", "run", "doctor", "--silent"]]
];
const components = {};
for (const [component, args] of commands) {
  const result = spawnSync(process.platform === "win32" ? "npm.cmd" : "npm", args, {
    cwd: root,
    encoding: "utf8",
    shell: process.platform === "win32"
  });
  components[component] = {
    exit_code: result.status,
    status: result.status === 0 ? "pass" : "action_required",
    output: `${result.stdout ?? ""}${result.stderr ?? ""}`.trim()
  };
}
console.log(JSON.stringify({
  schema: "sts2.ai-platform/doctor-report-1",
  source: readIdentityReport(root),
  components,
  non_claims: ["doctor_is_not_loaded_evidence", "doctor_is_not_live_exercise"]
}, null, 2));
