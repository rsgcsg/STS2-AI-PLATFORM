import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const ignored = new Set([".git", ".local", "node_modules"]);
const forbiddenExtensions = new Set([".dll", ".pck", ".dylib", ".so", ".exe"]);
const forbiddenNames = new Set(["current_run.save", "progress.save", "godot.log"]);
const homePrefix = process.env.HOME ? `${process.env.HOME}/` : null;
const failures = [];

function walk(directory) {
  for (const entry of readdirSync(directory)) {
    if (ignored.has(entry)) continue;
    const file = path.join(directory, entry);
    if (statSync(file).isDirectory()) {
      walk(file);
      continue;
    }
    if (forbiddenExtensions.has(path.extname(entry).toLowerCase()) || forbiddenNames.has(entry)) {
      failures.push(`proprietary_or_runtime_artifact:${path.relative(ROOT, file)}`);
    }
    if ([".md", ".mjs", ".json", ".yml", ".yaml"].includes(path.extname(entry))) {
      const content = readFileSync(file, "utf8");
      if (homePrefix && content.includes(homePrefix)) {
        failures.push(`user_specific_path:${path.relative(ROOT, file)}`);
      }
    }
  }
}

walk(ROOT);
if (failures.length > 0) {
  console.error(failures.join("\n"));
  process.exit(1);
}
console.log("repository boundary check passed");
