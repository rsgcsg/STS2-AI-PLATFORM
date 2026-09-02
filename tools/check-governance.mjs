#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

const requiredPaths = [
  "docs/ENGINEERING_GOVERNANCE.md",
  "docs/adr/README.md",
  ".agents/skills/README.md",
  ".github/PULL_REQUEST_TEMPLATE.md",
  ".github/pull_request_template.md",
  "tools/check-governance.mjs",
  "tools/check-governance.test.mjs"
];

const durableIndexPaths = [
  "docs/ENGINEERING_GOVERNANCE.md",
  "docs/adr/README.md",
  ".agents/skills/README.md"
];

function finding(code, file, message) {
  return { code, file, message };
}

function read(workspaceRoot, relative) {
  return fs.readFileSync(path.join(workspaceRoot, relative), "utf8").replace(/\r\n?/gu, "\n");
}

function has(workspaceRoot, relative) {
  return fs.existsSync(path.join(workspaceRoot, relative));
}

function requireTokens(findings, workspaceRoot, relative, tokens) {
  if (!has(workspaceRoot, relative)) return;
  const source = read(workspaceRoot, relative);
  for (const token of tokens) {
    if (!source.includes(token)) {
      findings.push(finding("governance-route-missing", relative, token));
    }
  }
}

export function requiredSurfaceFindings(workspaceRoot = root) {
  const findings = [];
  for (const relative of requiredPaths) {
    if (!has(workspaceRoot, relative)) {
      findings.push(finding("governance-surface-missing", relative, "required governance surface"));
    }
  }
  return findings;
}

export function routeFindings(workspaceRoot = root) {
  const findings = [];
  requireTokens(findings, workspaceRoot, "AGENTS.md", ["docs/ENGINEERING_GOVERNANCE.md"]);
  requireTokens(findings, workspaceRoot, "README.md", [
    "docs/ENGINEERING_GOVERNANCE.md",
    ".agents/skills/README.md"
  ]);
  requireTokens(findings, workspaceRoot, "docs/DOCUMENT_MAP.md", [
    "ENGINEERING_GOVERNANCE.md",
    "adr/README.md",
    "../.agents/skills/README.md"
  ]);
  requireTokens(findings, workspaceRoot, "docs/NEW_ENGINEER_GUIDE.md", [
    "ENGINEERING_GOVERNANCE.md",
    "G0",
    "G6"
  ]);
  requireTokens(findings, workspaceRoot, "CONTRIBUTING.md", [
    "docs/ENGINEERING_GOVERNANCE.md"
  ]);
  return findings;
}

export function durableTruthFindings(workspaceRoot = root) {
  const findings = [];
  for (const relative of durableIndexPaths) {
    if (!has(workspaceRoot, relative)) continue;
    const source = read(workspaceRoot, relative);
    const sha = source.match(/\b[0-9a-f]{40}\b/iu)?.[0];
    if (sha) findings.push(finding("governance-mutable-identity", relative, sha));
    const developRef = source.match(/\bdevelop@[0-9a-f]+\b/iu)?.[0];
    if (developRef) findings.push(finding("governance-mutable-identity", relative, developRef));
    const pr = source.match(/\bPR\s+#\d+\b/iu)?.[0];
    if (pr) findings.push(finding("governance-mutable-work-item", relative, pr));
  }
  return findings;
}

export function pullRequestTemplateFindings(workspaceRoot = root) {
  const findings = [];
  const upper = ".github/PULL_REQUEST_TEMPLATE.md";
  const lower = ".github/pull_request_template.md";
  if (!has(workspaceRoot, upper) || !has(workspaceRoot, lower)) return findings;
  const upperSource = read(workspaceRoot, upper).trim();
  const lowerSource = read(workspaceRoot, lower).trim();
  if (upperSource !== lowerSource) {
    findings.push(finding("pull-request-template-drift", ".github", "case-variant templates must remain identical until one is explicitly removed"));
  }
  const requiredTokens = [
    "Change class (`G0`-`G6`)",
    "Owning fact / layer",
    "latest head SHA",
    "Test shape",
    "Evidence level actually proved",
    "Merge method",
    "Remaining non-claims"
  ];
  for (const token of requiredTokens) {
    if (!upperSource.includes(token)) {
      findings.push(finding("pull-request-template-field-missing", upper, token));
    }
  }
  return findings;
}

function indexedLinks(source, pattern) {
  return [...source.matchAll(pattern)].map((match) => match[1]);
}

export function adrIndexFindings(workspaceRoot = root) {
  const findings = [];
  const directory = path.join(workspaceRoot, "docs", "adr");
  const indexPath = "docs/adr/README.md";
  if (!fs.existsSync(directory) || !has(workspaceRoot, indexPath)) return findings;
  const actual = fs.readdirSync(directory)
    .filter((name) => /^\d{4}-.*\.md$/u.test(name))
    .sort();
  const source = read(workspaceRoot, indexPath);
  const indexed = indexedLinks(source, /\((\d{4}-[^)]+\.md)\)/gu);
  for (const name of actual) {
    const count = indexed.filter((entry) => entry === name).length;
    if (count !== 1) findings.push(finding("adr-index-invalid", indexPath, `${name} indexed ${count} time(s)`));
  }
  for (const name of indexed) {
    if (!actual.includes(name)) findings.push(finding("adr-index-orphan", indexPath, name));
  }
  return findings;
}

export function skillIndexFindings(workspaceRoot = root) {
  const findings = [];
  const directory = path.join(workspaceRoot, ".agents", "skills");
  const indexPath = ".agents/skills/README.md";
  if (!fs.existsSync(directory) || !has(workspaceRoot, indexPath)) return findings;
  const actual = fs.readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && fs.existsSync(path.join(directory, entry.name, "SKILL.md")))
    .map((entry) => entry.name)
    .sort();
  const source = read(workspaceRoot, indexPath);
  const indexed = indexedLinks(source, /\(([^/)]+)\/SKILL\.md\)/gu);
  for (const name of actual) {
    const count = indexed.filter((entry) => entry === name).length;
    if (count !== 1) findings.push(finding("skill-index-invalid", indexPath, `${name} indexed ${count} time(s)`));
  }
  for (const name of indexed) {
    if (!actual.includes(name)) findings.push(finding("skill-index-orphan", indexPath, name));
  }
  return findings;
}

export function currentContextFindings(workspaceRoot = root) {
  const findings = [];
  const relative = "docs/memory/CURRENT.md";
  if (!has(workspaceRoot, relative)) return findings;
  const source = read(workspaceRoot, relative);
  if (Buffer.byteLength(source, "utf8") > 8 * 1024) {
    findings.push(finding("current-context-oversized", relative, "bounded handoff must remain at or below 8 KiB"));
  }
  for (const token of ["Resolve live GitHub refs", "override this file", "Remaining Platform non-claims"]) {
    if (!source.includes(token)) {
      findings.push(finding("current-context-safety-missing", relative, token));
    }
  }
  return findings;
}

export function packageFindings(workspaceRoot = root) {
  const findings = [];
  const relative = "package.json";
  if (!has(workspaceRoot, relative)) return findings;
  let packageJson;
  try {
    packageJson = JSON.parse(read(workspaceRoot, relative));
  } catch (error) {
    return [finding("governance-package-invalid", relative, error.message)];
  }
  const expected = "node --test tools/check-governance.test.mjs && node tools/check-governance.mjs";
  if (packageJson.scripts?.["check:governance"] !== expected) {
    findings.push(finding("governance-command-invalid", relative, `check:governance must equal ${expected}`));
  }
  if (!packageJson.scripts?.check?.includes("npm run check:governance")) {
    findings.push(finding("governance-check-not-portable", relative, "check must compose check:governance"));
  }
  return findings;
}

export function governanceFindings(workspaceRoot = root) {
  return [
    ...requiredSurfaceFindings(workspaceRoot),
    ...routeFindings(workspaceRoot),
    ...durableTruthFindings(workspaceRoot),
    ...pullRequestTemplateFindings(workspaceRoot),
    ...adrIndexFindings(workspaceRoot),
    ...skillIndexFindings(workspaceRoot),
    ...currentContextFindings(workspaceRoot),
    ...packageFindings(workspaceRoot)
  ];
}

function main() {
  const findings = governanceFindings(root);
  if (findings.length > 0) {
    console.error("Governance contract failed:");
    for (const item of findings) {
      console.error(`- [${item.code}] ${item.file}: ${item.message}`);
    }
    process.exitCode = 1;
    return;
  }
  console.log("governance contract passed");
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
