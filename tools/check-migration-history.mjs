#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const manifest = JSON.parse(fs.readFileSync(path.join(root, "migration", "source-manifest.json")));
for (const source of manifest.sources) {
  for (const revision of [source.source_revision, source.import_commit]) {
    execFileSync("git", ["merge-base", "--is-ancestor", revision, "HEAD"], {
      cwd: root,
      stdio: "ignore"
    });
  }
  const sourceTree = execFileSync("git", ["rev-parse", `${source.source_revision}^{tree}`], {
    cwd: root,
    encoding: "utf8"
  }).trim();
  const importedTree = execFileSync(
    "git",
    ["rev-parse", `${source.import_commit}:${source.target_path}`],
    { cwd: root, encoding: "utf8" }
  ).trim();
  const secondParent = execFileSync("git", ["rev-parse", `${source.import_commit}^2`], {
    cwd: root,
    encoding: "utf8"
  }).trim();
  if (sourceTree !== source.source_tree || importedTree !== source.source_tree) {
    throw new Error(`source/import tree mismatch for ${source.component_id}`);
  }
  if (secondParent !== source.source_revision) {
    throw new Error(`import second parent mismatch for ${source.component_id}`);
  }
}
console.log(`migration history passed (${manifest.sources.length} original histories)`);
