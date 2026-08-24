import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const PROJECT_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const IGNORED_DIRECTORIES = new Set([
  ".git",
  ".local",
  ".mypy_cache",
  ".pytest_cache",
  ".venv",
  "__pycache__",
  "bin",
  "node_modules",
  "obj"
]);

function git(root, args) {
  return execFileSync("git", args, {
    cwd: root,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });
}

function readGitIdentity(root) {
  try {
    const workspaceRoot = git(root, ["rev-parse", "--show-toplevel"]).trim();
    const componentPath = git(root, ["rev-parse", "--show-prefix"])
      .trim()
      .replace(/\/$/u, "");
    const workspaceRevision = git(root, ["rev-parse", "HEAD"]).trim();
    const sourceRevision = componentPath
      ? git(workspaceRoot, ["log", "-1", "--format=%H", "--", componentPath]).trim()
      : workspaceRevision;
    const treeRevision = componentPath
      ? git(root, ["rev-parse", `HEAD:${componentPath}`]).trim()
      : git(root, ["rev-parse", "HEAD^{tree}"]).trim();
    const componentStatus = git(root, ["status", "--porcelain", "--", "."]).trim();
    const workspaceStatus = git(workspaceRoot, ["status", "--porcelain"]).trim();
    return {
      kind: "git_checkout",
      workspace_revision: workspaceRevision,
      component_path: componentPath || ".",
      source_revision: sourceRevision,
      component_tree_revision: treeRevision,
      source_worktree_status: componentStatus ? "dirty" : "clean",
      workspace_worktree_status: workspaceStatus ? "dirty" : "clean"
    };
  } catch {
    return {
      kind: "installed_package",
      workspace_revision: null,
      component_path: ".",
      source_revision: null,
      component_tree_revision: null,
      source_worktree_status: "not_applicable",
      workspace_worktree_status: "not_applicable"
    };
  }
}

function gitSourceFiles(root) {
  try {
    const output = execFileSync(
      "git",
      ["ls-files", "-z", "--cached", "--others", "--exclude-standard"],
      { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }
    );
    return output
      .split("\0")
      .filter(Boolean)
      .sort()
      .map((relative) => path.join(root, relative))
      .filter((file) => statSync(file, { throwIfNoEntry: false })?.isFile() === true);
  } catch {
    return null;
  }
}

function sourceFiles(root) {
  const gitFiles = gitSourceFiles(root);
  if (gitFiles != null) return gitFiles;
  const files = [];
  function walk(directory) {
    for (const entry of readdirSync(directory).sort()) {
      if (IGNORED_DIRECTORIES.has(entry)) continue;
      const file = path.join(directory, entry);
      if (statSync(file).isDirectory()) walk(file);
      else if (!entry.endsWith(".pyc")) files.push(file);
    }
  }
  walk(root);
  return files;
}

export function calculateSourceDigest(root = PROJECT_ROOT) {
  const hash = createHash("sha256");
  const files = sourceFiles(root);
  for (const file of files) {
    hash.update(path.relative(root, file));
    hash.update("\0");
    hash.update(readFileSync(file));
    hash.update("\0");
  }
  return { sha256: hash.digest("hex"), file_count: files.length };
}

export function readProjectIdentity(root = PROJECT_ROOT) {
  const workspace = JSON.parse(readFileSync(path.join(root, "package.json"), "utf8"));
  const source = readGitIdentity(root);
  const digest = calculateSourceDigest(root);
  return {
    product: "sts2-host-runtime",
    package_name: workspace.name,
    version: workspace.version,
    distribution_kind: source.kind,
    workspace_revision: source.workspace_revision,
    component_path: source.component_path,
    source_revision: source.source_revision,
    component_tree_revision: source.component_tree_revision,
    source_worktree_status: source.source_worktree_status,
    workspace_worktree_status: source.workspace_worktree_status,
    source_digest_sha256: digest.sha256,
    source_file_count: digest.file_count
  };
}
