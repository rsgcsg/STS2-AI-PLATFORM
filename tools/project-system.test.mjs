import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import {
  AGENT_CHAIN_BUDGET_BYTES,
  agentBudgetFindings,
  agentReferenceFindings,
  documentedCommandFindings,
  formatCloseout,
  markdownLinkFindings,
  projectIntegrityFindings,
  skillFindings
} from "./project-system.mjs";

function fixture() {
  return fs.mkdtempSync(path.join(os.tmpdir(), "sts2-project-system-"));
}

function write(root, relative, contents) {
  const file = path.join(root, relative);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, contents);
}

function git(root, ...args) {
  return execFileSync("git", args, { cwd: root, encoding: "utf8" }).trim();
}

test("broken internal Markdown links fail deterministically", () => {
  const root = fixture();
  try {
    write(root, "docs/guide.md", "Read [missing](missing.md).\n");
    assert.deepEqual(markdownLinkFindings(root), [{
      code: "markdown-target-missing",
      file: "docs/guide.md",
      message: "missing.md"
    }]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("missing DOCUMENT_MAP routes receive the routing-specific failure", () => {
  const root = fixture();
  try {
    write(root, "docs/DOCUMENT_MAP.md", "[Missing](missing.md)\n");
    assert.equal(markdownLinkFindings(root)[0]?.code, "document-map-route-missing");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("missing AGENTS path references fail", () => {
  const root = fixture();
  try {
    write(root, "AGENTS.md", "Read `docs/missing.md` before work.\n");
    assert.deepEqual(agentReferenceFindings(root), [{
      code: "agents-reference-missing",
      file: "AGENTS.md",
      message: "docs/missing.md"
    }]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("missing files referenced by a Skill fail", () => {
  const root = fixture();
  try {
    write(root, ".agents/skills/example/SKILL.md", [
      "---",
      "name: example",
      "description: Run a bounded example workflow.",
      "---",
      "",
      "Read [the contract](references/contract.md).",
      ""
    ].join("\n"));
    assert.equal(skillFindings(root).length, 0);
    assert.deepEqual(markdownLinkFindings(root), [{
      code: "markdown-target-missing",
      file: ".agents/skills/example/SKILL.md",
      message: "references/contract.md"
    }]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("a repository Skill directory requires a SKILL.md entrypoint", () => {
  const root = fixture();
  try {
    write(root, ".agents/skills/missing-entrypoint/agents/openai.yaml", "interface: {}\n");
    assert.deepEqual(skillFindings(root), [{
      code: "skill-entrypoint-missing",
      file: ".agents/skills/missing-entrypoint",
      message: "SKILL.md"
    }]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("documented npm run commands must exist in the owning package", () => {
  const root = fixture();
  try {
    write(root, "package.json", '{"scripts":{"check":"node ok.mjs"}}\n');
    write(root, "README.md", "```bash\nnpm run absent\n```\n");
    assert.deepEqual(documentedCommandFindings(root), [{
      code: "documented-command-missing",
      file: "README.md",
      message: "npm run absent"
    }]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("AGENTS chains above the project budget fail", () => {
  const root = fixture();
  try {
    write(root, "AGENTS.md", "x".repeat(AGENT_CHAIN_BUDGET_BYTES + 1));
    const findings = agentBudgetFindings(root);
    assert.equal(findings.length, 1);
    assert.equal(findings[0].code, "agents-instruction-budget-exceeded");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("invalid Skill metadata and explicit-only policy fail", () => {
  const root = fixture();
  try {
    write(root, ".agents/skills/invalid-name/SKILL.md", [
      "---",
      "name: wrong-name",
      "description: Maintain a repository Skill.",
      "---",
      "",
      "Instructions.",
      ""
    ].join("\n"));
    write(root, ".agents/skills/repo-skill-maintenance/SKILL.md", [
      "---",
      "name: repo-skill-maintenance",
      "description: Maintain a repository Skill when explicitly invoked.",
      "---",
      "",
      "Instructions.",
      ""
    ].join("\n"));
    write(root, ".agents/skills/repo-skill-maintenance/agents/openai.yaml", [
      "policy:",
      "  allow_implicit_invocation: true",
      ""
    ].join("\n"));
    const codes = skillFindings(root).map((item) => item.code);
    assert.ok(codes.includes("skill-name-invalid"));
    assert.ok(codes.includes("skill-invocation-policy-invalid"));
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("project-system command and portable-check corruption fail", () => {
  const root = fixture();
  try {
    write(root, "package.json", JSON.stringify({ scripts: { check: "node other.mjs" } }));
    const codes = projectIntegrityFindings(root).map((item) => item.code);
    assert.ok(codes.includes("project-system-file-missing"));
    assert.ok(codes.includes("project-system-command-invalid"));
    assert.ok(codes.includes("project-system-check-not-portable"));
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("closeout reports semantic review instead of rewriting truth", () => {
  const root = fixture();
  try {
    git(root, "init", "-b", "develop");
    git(root, "config", "user.email", "project-system@example.invalid");
    git(root, "config", "user.name", "Project System Test");
    write(root, "README.md", "baseline\n");
    git(root, "add", ".");
    git(root, "commit", "-m", "fixture");
    write(root, "README.md", "changed\n");
    const output = formatCloseout(root);
    assert.match(output, /Semantic freshness: human review required/u);
    assert.match(output, /README\.md/u);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
