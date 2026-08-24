import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { componentGitState } from "../../../tools/component-git.mjs";

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
  const git = componentGitState(root);
  const digest = calculateSourceDigest(root);
  return {
    product: "sts2-host-runtime",
    version: workspace.version,
    workspace_revision: git.workspaceRevision,
    component_path: git.componentPath,
    source_revision: git.componentSourceRevision,
    component_tree_revision: git.componentTreeRevision,
    source_worktree_status: git.componentWorktreeStatus,
    workspace_worktree_status: git.workspaceWorktreeStatus,
    source_digest_sha256: digest.sha256,
    source_file_count: digest.file_count
  };
}
