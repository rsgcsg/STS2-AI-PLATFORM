import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { calculateComponentIdentity } from "./component-identity.mjs";

function git(root, ...args) {
  return execFileSync("git", args, { cwd: root, encoding: "utf8" }).trim();
}

test("unrelated component commits do not change component identity", () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "sts2-platform-identity-"));
  try {
    git(root, "init", "-b", "main");
    git(root, "config", "user.email", "identity-test@example.invalid");
    git(root, "config", "user.name", "Component Identity Test");
    for (const name of ["a", "b"]) {
      fs.mkdirSync(path.join(root, "components", name, "contracts"), { recursive: true });
      fs.writeFileSync(path.join(root, "components", name, "package.json"), '{"version":"1.0.0"}\n');
      fs.writeFileSync(path.join(root, "components", name, "contracts", "contract.json"), "{}\n");
    }
    git(root, "add", ".");
    git(root, "commit", "-m", "fixture");
    const component = {
      path: "components/a",
      version_file: "package.json",
      contract_paths: ["contracts"]
    };
    const before = calculateComponentIdentity({ platformRoot: root, componentId: "a", component });
    fs.writeFileSync(path.join(root, "components", "b", "README.md"), "unrelated\n");
    git(root, "add", ".");
    git(root, "commit", "-m", "change b");
    const after = calculateComponentIdentity({ platformRoot: root, componentId: "a", component });
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
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "sts2-platform-identity-"));
  try {
    git(root, "init", "-b", "main");
    git(root, "config", "user.email", "identity-test@example.invalid");
    git(root, "config", "user.name", "Component Identity Test");
    fs.mkdirSync(path.join(root, "components", "a", "contracts"), { recursive: true });
    fs.writeFileSync(path.join(root, "components", "a", "package.json"), '{"version":"1.0.0"}\n');
    fs.writeFileSync(path.join(root, "components", "a", "contracts", "contract.json"), "{}\n");
    git(root, "add", ".");
    git(root, "commit", "-m", "fixture");
    const component = {
      path: "components/a",
      version_file: "package.json",
      contract_paths: ["contracts"]
    };
    const before = calculateComponentIdentity({ platformRoot: root, componentId: "a", component });
    fs.writeFileSync(path.join(root, "components", "a", "contracts", "contract.json"), '{"revision":2}\n');
    git(root, "add", ".");
    git(root, "commit", "-m", "change a");
    const after = calculateComponentIdentity({ platformRoot: root, componentId: "a", component });
    assert.notEqual(after.source_revision, before.source_revision);
    assert.notEqual(after.component_tree_revision, before.component_tree_revision);
    assert.notEqual(after.component_source_digest_sha256, before.component_source_digest_sha256);
    assert.notEqual(after.public_contract_digest_sha256, before.public_contract_digest_sha256);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
