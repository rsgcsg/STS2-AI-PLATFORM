import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { calculateComponentIdentity } from "./component-identity.mjs";

const tool = path.join(import.meta.dirname, "component-identity.mjs");

function git(root, ...args) {
  return execFileSync("git", args, { cwd: root, encoding: "utf8" }).trim();
}

function fixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "sts2-platform-identity-"));
  git(root, "init", "-b", "main");
  git(root, "config", "user.email", "identity-test@example.invalid");
  git(root, "config", "user.name", "Component Identity Test");
  // Simulate a checkout that would normally materialize CRLF. Repository
  // attributes must keep source bytes stable for content identity.
  git(root, "config", "core.autocrlf", "true");
  fs.writeFileSync(path.join(root, ".gitattributes"), "* text=auto eol=lf\n");
  for (const name of ["a", "b"]) {
    fs.mkdirSync(path.join(root, "components", name, "contracts"), { recursive: true });
    fs.writeFileSync(path.join(root, "components", name, "package.json"), '{"version":"1.0.0"}\n');
    fs.writeFileSync(path.join(root, "components", name, "contracts", "contract.json"), "{}\n");
  }
  git(root, "add", ".");
  git(root, "commit", "-m", "fixture");
  return {
    root,
    component: {
      path: "components/a",
      version_file: "package.json",
      contract_paths: ["contracts"]
    }
  };
}

function identity(root, component) {
  return calculateComponentIdentity({ platformRoot: root, componentId: "a", component });
}

function changeComponentA(root, revision) {
  fs.writeFileSync(
    path.join(root, "components", "a", "contracts", "contract.json"),
    `${JSON.stringify({ revision })}\n`
  );
  git(root, "add", ".");
  git(root, "commit", "-m", `change a ${revision}`);
}

test("repository EOL policy keeps identity source bytes LF under autocrlf", () => {
  const { root } = fixture();
  try {
    git(root, "checkout", "--force", "HEAD");
    const contents = fs.readFileSync(
      path.join(root, "components", "a", "contracts", "contract.json"),
      "utf8"
    );
    assert.equal(contents, "{}\n");
    assert.equal(contents.includes("\r"), false);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("unrelated component commits do not change component identity", () => {
  const { root, component } = fixture();
  try {
    const before = identity(root, component);
    fs.writeFileSync(path.join(root, "components", "b", "README.md"), "unrelated\n");
    git(root, "add", ".");
    git(root, "commit", "-m", "change b");
    const after = identity(root, component);
    assert.notEqual(after.workspace_revision, before.workspace_revision);
    assert.equal(after.source_revision, before.source_revision);
    assert.equal(after.component_tree_revision, before.component_tree_revision);
    assert.equal(after.component_source_digest_sha256, before.component_source_digest_sha256);
    assert.equal(after.public_contract_digest_sha256, before.public_contract_digest_sha256);
    assert.equal(after.source_worktree_status, "clean");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("component commits change source, tree, and digest identities together", () => {
  const { root, component } = fixture();
  try {
    const before = identity(root, component);
    changeComponentA(root, 2);
    const after = identity(root, component);
    assert.notEqual(after.source_revision, before.source_revision);
    assert.notEqual(after.component_tree_revision, before.component_tree_revision);
    assert.notEqual(after.component_source_digest_sha256, before.component_source_digest_sha256);
    assert.notEqual(after.public_contract_digest_sha256, before.public_contract_digest_sha256);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("merge commits preserve path-scoped component source provenance", () => {
  const { root, component } = fixture();
  try {
    git(root, "checkout", "-b", "topic");
    changeComponentA(root, 2);
    const topicRevision = git(root, "rev-parse", "HEAD");
    const topicIdentity = identity(root, component);

    git(root, "checkout", "main");
    git(root, "merge", "--no-ff", "topic", "-m", "merge topic");
    const mergedIdentity = identity(root, component);

    assert.equal(mergedIdentity.source_revision, topicRevision);
    assert.equal(mergedIdentity.component_tree_revision, topicIdentity.component_tree_revision);
    assert.equal(mergedIdentity.component_source_digest_sha256,
      topicIdentity.component_source_digest_sha256);
    assert.equal(mergedIdentity.public_contract_digest_sha256,
      topicIdentity.public_contract_digest_sha256);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("squash integration rewrites commit provenance while preserving component content identity", () => {
  const { root, component } = fixture();
  try {
    git(root, "checkout", "-b", "topic");
    changeComponentA(root, 2);
    const topicRevision = git(root, "rev-parse", "HEAD");
    const topicIdentity = identity(root, component);

    git(root, "checkout", "main");
    git(root, "merge", "--squash", "topic");
    git(root, "commit", "-m", "squash topic");
    const squashRevision = git(root, "rev-parse", "HEAD");
    const squashedIdentity = identity(root, component);

    assert.notEqual(squashRevision, topicRevision);
    assert.equal(squashedIdentity.source_revision, squashRevision);
    assert.notEqual(squashedIdentity.source_revision, topicRevision);
    assert.equal(squashedIdentity.component_tree_revision, topicIdentity.component_tree_revision);
    assert.equal(squashedIdentity.component_source_digest_sha256,
      topicIdentity.component_source_digest_sha256);
    assert.equal(squashedIdentity.public_contract_digest_sha256,
      topicIdentity.public_contract_digest_sha256);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("quiet validation is shell-independent and emits no report", () => {
  const output = execFileSync(process.execPath, [tool, "--quiet"], {
    cwd: path.resolve(import.meta.dirname, ".."),
    encoding: "utf8"
  });
  assert.equal(output, "");
});
