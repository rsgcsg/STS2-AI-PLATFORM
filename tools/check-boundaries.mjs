#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const checks = [
  ["components/annotator/tools/annotator.mjs", /\.\.[/\\]STS2-Connector|sibling STS2-/u],
  ["components/annotator/tools/workstation-platform.mjs", /\.\.[/\\]STS2-headless|sibling_sts2_headless/u],
  ["components/annotator/src/STS2HumanAnnotator.Mod/STS2HumanAnnotator.Mod.csproj", /STS2-Connector/u]
];
const errors = [];
for (const [relative, forbidden] of checks) {
  const contents = fs.readFileSync(path.join(root, relative), "utf8");
  if (forbidden.test(contents)) errors.push(`${relative}: legacy sibling coupling remains`);
}
const productionRoots = [
  "components/connector/host",
  "components/connector/sdk",
  "components/host-runtime/src",
  "components/annotator/src",
  "components/annotator/tools"
];
function visit(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (["bin", "obj", "out", "dist", "node_modules"].includes(entry.name)) continue;
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) visit(file);
    else if (/\.(?:cs|csproj|js|mjs|ts|json)$/u.test(entry.name)) {
      const contents = fs.readFileSync(file, "utf8");
      if (contents.includes("/Users/fire/Desktop/SpireAgentProject")) {
        errors.push(`${path.relative(root, file)}: local workspace path in production source`);
      }
    }
  }
}
for (const relative of productionRoots) visit(path.join(root, relative));
if (errors.length) throw new Error(errors.join("\n"));
console.log("platform dependency boundary checks passed");
