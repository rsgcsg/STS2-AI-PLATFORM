import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { componentGitState } from "../../../tools/component-git.mjs";

export function canonicalSourceBytes(contents) {
  return Buffer.from(contents.toString("utf8").replace(/\r\n/gu, "\n"), "utf8");
}

export function playerEnvironmentSourceIdentity(workspace) {
  let gitState;
  try {
    gitState = componentGitState(workspace);
  } catch {
    return null;
  }
  const revision = gitState.componentSourceRevision;
  if (!/^[0-9a-f]{40}$/u.test(revision)) return null;

  const filesResult = spawnSync("git", [
    "ls-files", "--cached", "--others", "--exclude-standard", "--deduplicate", "--", "host"
  ], {
    cwd: workspace,
    encoding: "utf8",
    stdio: "pipe"
  });
  if (filesResult.status !== 0) return null;
  const excludedPrefixes = [
    "host/bin/",
    "host/obj/",
    "host/out/",
    "host/tests/",
    "host/tools/"
  ];
  const sourceExtensions = new Set([".cs", ".csproj", ".json", ".props", ".targets"]);
  const files = filesResult.stdout.split("\n")
    .filter(Boolean)
    .filter((file) => !excludedPrefixes.some((prefix) => file.startsWith(prefix)))
    .filter((file) => sourceExtensions.has(path.extname(file).toLowerCase()))
    .filter((file) => existsSync(path.join(workspace, file)))
    .sort();
  const digest = createHash("sha256");
  for (const file of files) {
    digest.update(file)
      .update("\0")
      .update(canonicalSourceBytes(readFileSync(path.join(workspace, file))))
      .update("\0");
  }
  const statusResult = spawnSync("git", ["status", "--porcelain", "--", ...files], {
    cwd: workspace,
    encoding: "utf8",
    stdio: "pipe"
  });
  return {
    revision,
    workspaceRevision: gitState.workspaceRevision,
    componentTreeRevision: gitState.componentTreeRevision,
    sourceDigest: digest.digest("hex"),
    worktreeStatus: gitState.componentWorktreeStatus,
    workspaceWorktreeStatus: gitState.workspaceWorktreeStatus,
    fileCount: files.length
  };
}

export function readOptionalJson(file) {
  if (!existsSync(file)) return null;
  try {
    return JSON.parse(readFileSync(file, "utf8"));
  } catch {
    return null;
  }
}

export function evaluateBuildProvenance({
  currentSource,
  sourceProtocol: currentProtocol,
  builtSha,
  builtMvid,
  buildMetadata,
  installedSha,
  installedMvid,
  installedMetadata
}) {
  const errors = [];
  if (builtSha && !buildMetadata) errors.push("build_provenance_missing");
  if (buildMetadata) {
    if (buildMetadata.source_revision !== currentSource?.revision) {
      errors.push("source_build_revision_mismatch");
    }
    if (buildMetadata.player_environment_source_digest !== currentSource?.sourceDigest) {
      errors.push("source_build_digest_mismatch");
    }
    if (buildMetadata.source_protocol !== currentProtocol) {
      errors.push("source_build_protocol_mismatch");
    }
    if (buildMetadata.artifact_sha256 !== builtSha) errors.push("build_provenance_sha_mismatch");
    if (buildMetadata.artifact_mvid !== builtMvid) errors.push("build_provenance_mvid_mismatch");
  }
  if (installedSha && !installedMetadata) errors.push("installed_provenance_missing");
  if (installedMetadata) {
    if (installedMetadata.artifact_sha256 !== installedSha) {
      errors.push("installed_provenance_sha_mismatch");
    }
    if (installedMetadata.artifact_mvid !== installedMvid) {
      errors.push("installed_provenance_mvid_mismatch");
    }
    if (buildMetadata
        && installedMetadata.player_environment_source_digest
          !== buildMetadata.player_environment_source_digest) {
      errors.push("build_installed_provenance_mismatch");
    }
    if (buildMetadata
        && installedMetadata.source_revision !== buildMetadata.source_revision) {
      errors.push("build_installed_revision_mismatch");
    }
  }
  return { ok: errors.length === 0, errors };
}
