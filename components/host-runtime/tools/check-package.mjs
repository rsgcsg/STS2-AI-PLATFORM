#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const npmExecPath = process.env.npm_execpath;
const args = ["pack", "--dry-run", "--json"];
const result = typeof npmExecPath === "string" && npmExecPath.length > 0
  ? spawnSync(process.execPath, [npmExecPath, ...args], {
      cwd: root,
      encoding: "utf8",
      stdio: "pipe"
    })
  : spawnSync(process.platform === "win32" ? "npm.cmd" : "npm", args, {
      cwd: root,
      encoding: "utf8",
      stdio: "pipe",
      shell: process.platform === "win32"
    });
if (result.error) throw result.error;
if (result.status !== 0) throw new Error(result.stderr || result.stdout);

const report = JSON.parse(result.stdout)[0];
if (report?.name !== "@rsgcsg/sts2-host-runtime") {
  throw new Error(`Host Runtime pack resolved the wrong package: ${report?.name ?? "missing"}`);
}
const files = new Set(report.files.map((entry) => entry.path));
for (const required of [
  "tools/headless.mjs",
  "tools/managed-pe-driver.mjs",
  "src/managed-player-environment.mjs",
  "src/project-identity.mjs",
  "consumers/python/sts2_headless/__init__.py",
  "consumers/python/sts2_headless/client.py",
  "experiments/managed-exact/manifest.json",
  "package.json",
  "README.md",
  "LICENSE"
]) {
  if (!files.has(required)) throw new Error(`Host Runtime package is missing ${required}`);
}
const forbidden = [...files].filter((file) =>
  file.startsWith(".local/")
  || file.startsWith("test/")
  || file.startsWith("docs/evidence/")
  || /\.(?:dll|exe|pdb|pck|save)$/iu.test(file)
);
if (forbidden.length > 0) {
  throw new Error(`Host Runtime package contains forbidden files: ${forbidden.join(", ")}`);
}
console.log(JSON.stringify({
  status: "host_runtime_package_clean",
  name: report.name,
  version: report.version,
  filename: report.filename,
  file_count: files.size,
  packed_size: report.size,
  unpacked_size: report.unpackedSize,
  integrity: report.integrity
}, null, 2));
