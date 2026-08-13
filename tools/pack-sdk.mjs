#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const workspace = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const sdk = path.join(workspace, "sdk", "typescript");
const args = ["pack", "--json", ...process.argv.slice(2)];
const npmExecPath = process.env.npm_execpath;
const options = { cwd: sdk, encoding: "utf8", stdio: "pipe" };
const result = typeof npmExecPath === "string" && npmExecPath.length > 0
  ? spawnSync(process.execPath, [npmExecPath, ...args], options)
  : spawnSync(process.platform === "win32" ? "npm.cmd" : "npm", args, {
      ...options,
      shell: process.platform === "win32"
    });

if (result.error) throw result.error;
if (result.status !== 0) {
  process.stderr.write(result.stderr || result.stdout);
  process.exit(result.status ?? 1);
}

const report = JSON.parse(result.stdout)[0];
if (report?.name !== "@rsgcsg/sts2-connector-client") {
  throw new Error(`SDK pack resolved the wrong package: ${report?.name ?? "missing"}`);
}
const allowedRoot = new Set(["package.json", "README.md", "LICENSE"]);
const files = Array.isArray(report.files) ? report.files.map((entry) => entry.path) : [];
const forbidden = files.filter((file) =>
  (!file.startsWith("dist/") && !allowedRoot.has(file))
  || /\.(?:dll|exe|pdb|pck|zip)$/iu.test(file));
if (forbidden.length > 0) {
  throw new Error(`SDK package contains forbidden files: ${forbidden.join(", ")}`);
}
for (const required of allowedRoot) {
  if (!files.includes(required)) throw new Error(`SDK package is missing ${required}`);
}

console.log(JSON.stringify({
  status: "sdk_package_clean",
  name: report.name,
  version: report.version,
  filename: report.filename,
  file_count: files.length,
  packed_size: report.size,
  unpacked_size: report.unpackedSize,
  integrity: report.integrity
}, null, 2));
