import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const files = [];

function walk(directory) {
  for (const entry of readdirSync(directory)) {
    if ([".git", ".local", "node_modules"].includes(entry)) continue;
    const file = path.join(directory, entry);
    if (statSync(file).isDirectory()) walk(file);
    else if (file.endsWith(".md")) files.push(file);
  }
}

walk(root);
const failures = [];
for (const file of files) {
  const content = readFileSync(file, "utf8");
  for (const match of content.matchAll(/\[[^\]]+\]\(([^)]+)\)/gu)) {
    const target = match[1];
    if (/^(?:https?:|mailto:|#)/u.test(target)) continue;
    const local = decodeURIComponent(target.split("#", 1)[0]);
    if (local && !existsSync(path.resolve(path.dirname(file), local))) {
      failures.push(`${path.relative(root, file)} -> ${target}`);
    }
  }
}

if (failures.length > 0) {
  console.error(`Broken local Markdown links:\n${failures.join("\n")}`);
  process.exit(1);
}
console.log(`markdown links passed (${files.length} files)`);
