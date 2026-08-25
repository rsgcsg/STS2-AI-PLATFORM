import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import {
  componentGitFiles,
  componentGitState
} from "../../tools/component-git.mjs";
import {
  playerEnvironmentSourceIdentity,
  sourceRevisionForFiles
} from "../../components/connector/tools/connector-provenance.mjs";

function digestFiles(componentRoot, files) {
  const digest = crypto.createHash("sha256");
  for (const relative of files) {
    digest.update(relative).update("\0");
    digest.update(fs.readFileSync(path.join(componentRoot, relative))).update("\0");
  }
  return digest.digest("hex");
}

function componentIdentity(componentRoot, filter = () => true) {
  const state = componentGitState(componentRoot);
  const files = componentGitFiles(componentRoot).filter(filter);
  const sourceRevision = sourceRevisionForFiles(componentRoot, files);
  if (!sourceRevision) throw new Error(`Source revision is unavailable for ${componentRoot}.`);
  return {
    source_revision: sourceRevision,
    source_digest_sha256: digestFiles(componentRoot, files),
    component_tree_revision: state.componentTreeRevision,
    component_worktree_status: state.componentWorktreeStatus,
    file_count: files.length
  };
}

export function sourceSetIdentity(platformRoot) {
  const connectorRoot = path.join(platformRoot, "components/connector");
  const connector = playerEnvironmentSourceIdentity(connectorRoot);
  if (!connector) throw new Error("Connector native source identity is unavailable.");
  const workspace = componentGitState(platformRoot);
  const annotator = componentIdentity(
    path.join(platformRoot, "components/annotator"),
    (relative) => /^(?:src\/STS2HumanAnnotator\.Core|src\/STS2HumanAnnotator\.Mod)\//u.test(relative)
      && [".cs", ".csproj", ".json"].includes(path.extname(relative))
  );
  const liveUi = componentIdentity(
    path.join(platformRoot, "apps/ingame-ui"),
    (relative) => path.extname(relative) === ".cs"
  );
  const gameMod = componentIdentity(
    path.join(platformRoot, "apps/game-mod"),
    (relative) => [
      "UnifiedPlatformMod.cs",
      "STS2Platform.GameMod.csproj",
      "mod_manifest.json"
    ].includes(relative)
  );
  const components = {
    connector: {
      source_revision: connector.revision,
      source_digest_sha256: connector.sourceDigest,
      component_tree_revision: connector.componentTreeRevision,
      component_worktree_status: connector.worktreeStatus,
      file_count: connector.fileCount
    },
    annotator,
    live_ui: liveUi,
    game_mod: gameMod
  };
  const platformDigest = crypto.createHash("sha256")
    .update(JSON.stringify(components))
    .digest("hex");
  return {
    platform: {
      source_revision: gameMod.source_revision,
      source_digest_sha256: platformDigest,
      workspace_revision: workspace.workspaceRevision,
      workspace_worktree_status: workspace.workspaceWorktreeStatus
    },
    components
  };
}

export function sourceSetMatches(left, right) {
  if (!left || !right) return false;
  return left.platform?.source_revision === right.platform?.source_revision
    && left.platform?.source_digest_sha256 === right.platform?.source_digest_sha256
    && Object.keys(right.components).every((name) =>
      left.components?.[name]?.source_revision === right.components[name].source_revision
      && left.components?.[name]?.source_digest_sha256 === right.components[name].source_digest_sha256);
}
