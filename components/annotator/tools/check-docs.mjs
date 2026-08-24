import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const required = [
  "README.md",
  "AGENTS.md",
  "CONTRIBUTING.md",
  "SECURITY.md",
  "docs/DOCUMENT_MAP.md",
  "docs/STATUS.md",
  "docs/ARCHITECTURE.md",
  "docs/DATA_CONTRACT.md",
  "docs/OPERATIONS.md",
  "docs/EVIDENCE.md",
  "docs/DEVELOPMENT.md",
  "docs/REVERSE_ENGINEERING_NOTES.md"
];
const errors = required
  .filter((file) => !fs.existsSync(path.join(root, file)))
  .map((file) => `missing required document: ${file}`);
const readme = fs.readFileSync(path.join(root, "README.md"), "utf8");
for (const term of ["exact process-local mapping", "Pending", "npm run verify:loaded"])
  if (!readme.includes(term)) errors.push(`README is missing current boundary: ${term}`);
const status = fs.readFileSync(path.join(root, "docs", "STATUS.md"), "utf8");
if (!status.includes("not human validated"))
  errors.push("status must preserve the human-validation non-claim");

console.log(JSON.stringify({ status: errors.length ? "fail" : "pass", errors }, null, 2));
if (errors.length) process.exit(1);
