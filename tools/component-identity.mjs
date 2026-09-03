#!/usr/bin/env node
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";
import { componentGitFiles, componentGitState } from "./component-git.mjs";

const PLATFORM_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function digestFiles(root, files) {
  const digest = crypto.createHash("sha256");
  for (const relative of files) {
    digest.update(relative).update("\0");
    digest.update(fs.readFileSync(path.join(root, relative))).update("\0");
  }
  return digest.digest("hex");
}

function insideContract(relative, contractPaths) {
  return contractPaths.some((contractPath) =>
    relative === contractPath || relative.startsWith(`${contractPath}/`)
  );
}

export function calculateComponentIdentity({
  platformRoot = PLATFORM_ROOT,
  componentId,
  component
}) {
  const componentRoot = path.join(platformRoot, component.path);
  const files = componentGitFiles(componentRoot);
  const contracts = files.filter((relative) => insideContract(relative, component.contract_paths));
  if (files.length === 0) throw new Error(`component ${componentId} has no source files`);
  if (contracts.length === 0) throw new Error(`component ${componentId} has no contract files`);
  const git = componentGitState(componentRoot);
  const versionFile = JSON.parse(
    fs.readFileSync(path.join(componentRoot, component.version_file), "utf8")
  );
  return Object.freeze({
    component_id: componentId,
    component_version: versionFile.version,
    workspace_revision: git.workspaceRevision,
    component_path: component.path,
    source_revision: git.componentSourceRevision,
    component_tree_revision: git.componentTreeRevision,
    component_source_digest_sha256: digestFiles(componentRoot, files),
    public_contract_digest_sha256: digestFiles(componentRoot, contracts),
    source_file_count: files.length,
    contract_file_count: contracts.length,
    source_worktree_status: git.componentWorktreeStatus,
    workspace_worktree_status: git.workspaceWorktreeStatus
  });
}

export function readIdentityReport(platformRoot = PLATFORM_ROOT) {
  const specification = JSON.parse(
    fs.readFileSync(path.join(platformRoot, "contracts", "component-identity.json"), "utf8")
  );
  const components = Object.fromEntries(
    Object.entries(specification.components).map(([componentId, component]) => [
      componentId,
      calculateComponentIdentity({ platformRoot, componentId, component })
    ])
  );
  return {
    schema: "sts2.ai-platform/component-identity-report-1",
    workspace_revision: componentGitState(platformRoot).workspaceRevision,
    components
  };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  process.stdout.write(`${JSON.stringify(readIdentityReport(), null, 2)}\n`);
}
