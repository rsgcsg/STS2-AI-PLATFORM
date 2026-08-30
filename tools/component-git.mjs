import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

function git(root, args, options = {}) {
  return execFileSync("git", args, {
    cwd: root,
    encoding: options.encoding ?? "utf8",
    stdio: ["ignore", "pipe", "ignore"]
  });
}

export function componentGitState(componentRoot) {
  const root = path.resolve(componentRoot);
  const workspaceRoot = git(root, ["rev-parse", "--show-toplevel"]).trim();
  const prefix = git(root, ["rev-parse", "--show-prefix"]).trim().replace(/\/$/u, "");
  const workspaceRevision = git(root, ["rev-parse", "HEAD"]).trim();
  const discoveredSourceRevision = prefix
    ? git(workspaceRoot, ["log", "-1", "--format=%H", "--", prefix]).trim()
    : workspaceRevision;
  const componentSourceRevision = discoveredSourceRevision || workspaceRevision;
  let componentTreeRevision;
  try {
    componentTreeRevision = prefix
      ? git(root, ["rev-parse", `HEAD:${prefix}`]).trim()
      : git(root, ["rev-parse", "HEAD"]).trim();
  } catch {
    // A new component has no HEAD tree until its first commit. The worktree
    // digest remains authoritative for local candidate identity.
    componentTreeRevision = "uncommitted";
  }
  const componentStatus = git(root, ["status", "--porcelain", "--", "."]).trim();
  const workspaceStatus = git(workspaceRoot, ["status", "--porcelain"]).trim();
  return Object.freeze({
    workspaceRoot,
    workspaceRevision,
    componentPath: prefix || ".",
    componentSourceRevision,
    componentTreeRevision,
    componentWorktreeStatus: componentStatus ? "dirty" : "clean",
    workspaceWorktreeStatus: workspaceStatus ? "dirty" : "clean"
  });
}

export function componentGitFiles(componentRoot) {
  const root = path.resolve(componentRoot);
  const output = git(
    root,
    ["ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", "."],
    { encoding: "buffer" }
  );
  return output.toString("utf8")
    .split("\0")
    .filter(Boolean)
    .filter((relative) => fs.statSync(path.join(root, relative), { throwIfNoEntry: false })?.isFile())
    .sort();
}
