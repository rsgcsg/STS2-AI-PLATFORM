import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, statSync } from "node:fs";
import path from "node:path";

const root = process.cwd();
const tracked = execFileSync(
  "git",
  ["ls-files", "--cached", "--others", "--exclude-standard", "-z"],
  { cwd: root, encoding: "utf8" },
)
  .split("\0")
  .filter(Boolean);

const files = tracked.filter((file) =>
  file.endsWith(".md") &&
  existsSync(path.resolve(root, file)) &&
  !file.startsWith("archive/"),
);

const markdownLink = /!?\[[^\]]*\]\(([^)]+)\)/g;
const failures = [];

for (const file of files) {
  const source = readFileSync(path.resolve(root, file), "utf8");
  for (const match of source.matchAll(markdownLink)) {
    let target = match[1].trim();
    if (target.startsWith("<") && target.endsWith(">")) {
      target = target.slice(1, -1);
    }
    target = target.split(/\s+['\"]/)[0].split("#", 1)[0].split("?", 1)[0];
    if (
      target.length === 0 ||
      target.startsWith("#") ||
      /^(?:https?:|mailto:|tel:|data:)/i.test(target)
    ) {
      continue;
    }

    const resolved = path.resolve(root, path.dirname(file), target);
    if (!resolved.startsWith(root + path.sep) && resolved !== root) {
      failures.push(`${file}: path escapes repository: ${target}`);
      continue;
    }
    if (!existsSync(resolved) || !statSync(resolved).isFile() && !statSync(resolved).isDirectory()) {
      failures.push(`${file}: missing local target: ${target}`);
    }
  }
}

if (failures.length > 0) {
  console.error(`Active Markdown link check failed (${failures.length}):`);
  for (const failure of failures) console.error(`- ${failure}`);
  process.exit(1);
}

console.log(`Active Markdown link check p