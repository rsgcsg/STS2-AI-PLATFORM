import { spawnSync } from "node:child_process";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import process from "node:process";

import { defaultGameDirectory } from "../../components/annotator/tools/workstation-platform.mjs";
import { componentGitFiles, componentGitState } from "../../tools/component-git.mjs";

const root = path.resolve(import.meta.dirname, "../..");
const connectorRoot = path.join(root, "components/connector");
const annotatorRoot = path.join(root, "components/annotator");
const uiProject = path.join(import.meta.dirname, "STS2PlatformLiveUi.csproj");
const connectorAssembly = path.join(root, "components/connector/host/out/STS2_MCP/STS2_MCP.dll");
const annotatorAssembly = path.join(
  root,
  "components/annotator/src/STS2HumanAnnotator.Mod/bin/Release/net9.0/STS2_HUMAN_ANNOTATOR.dll"
);
const identityTool = path.join(
  root,
  "components/annotator/src/STS2HumanAnnotator.Tool/bin/Release/net9.0/sts2-human-annotator.dll"
);
const uiOutput = path.join(import.meta.dirname, "bin/Release/net9.0");
const uiAssembly = path.join(uiOutput, "STS2_PLATFORM_LIVE_UI.dll");

function run(project, properties = []) {
  const args = [project, "--configuration", "Release", ...properties];
  const result = spawnSync("dotnet", ["build", ...args], {
    cwd: root,
    env: process.env,
    stdio: "inherit"
  });
  if (result.status !== 0)
    process.exit(result.status ?? 1);
}

function runComponentBuild(componentRoot) {
  const result = spawnSync("npm", ["run", "build"], {
    cwd: componentRoot,
    env: process.env,
    stdio: "inherit"
  });
  if (result.status !== 0)
    process.exit(result.status ?? 1);
}

function sha256(file) {
  return crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
}

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8").replace(/^\uFEFF/u, ""));
}

function exactIdentity(file) {
  if (!fs.existsSync(identityTool)) {
    throw new Error("Build the Annotator identity tool before building Platform Live UI.");
  }
  const result = spawnSync("dotnet", [identityTool, "identity", file], {
    cwd: root,
    encoding: "utf8"
  });
  if (result.status !== 0) throw new Error(result.stderr || `identity tool exited with ${result.status}`);
  return JSON.parse(result.stdout);
}

function sourceIdentity() {
  const files = componentGitFiles(import.meta.dirname);
  const digest = crypto.createHash("sha256");
  for (const relative of files) {
    digest.update(relative).update("\0");
    digest.update(fs.readFileSync(path.join(import.meta.dirname, relative))).update("\0");
  }
  try {
    const state = componentGitState(import.meta.dirname);
    return {
      workspace_revision: state.workspaceRevision,
      source_revision: state.componentSourceRevision,
      source_digest_sha256: digest.digest("hex"),
      component_worktree_status: state.componentWorktreeStatus,
      workspace_worktree_status: state.workspaceWorktreeStatus
    };
  } catch {
    const result = spawnSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" });
    if (result.status !== 0) throw new Error("Cannot resolve Platform source identity.");
    return {
      workspace_revision: result.stdout.trim(),
      source_revision: result.stdout.trim(),
      source_digest_sha256: digest.digest("hex"),
      component_worktree_status: "dirty",
      workspace_worktree_status: "dirty"
    };
  }
}

if (process.argv.includes("--ui-only")) {
  process.stderr.write("--ui-only is disabled: Live UI builds must rebuild Connector and Annotator dependencies.\n");
  process.exit(2);
}
const gameDir = path.resolve(process.env.STS2_GAME_DIR || defaultGameDirectory());
if (!fs.existsSync(gameDir)) {
  process.stderr.write(
    `STS2 installation was not found at ${gameDir}; set STS2_GAME_DIR explicitly.\n`
  );
  process.exit(2);
}
const gameProperty = [`-p:STS2GameDir=${gameDir}`];
const source = sourceIdentity();

// Component-owned build entrypoints are the only provenance authorities for
// dependency artifacts. Direct project builds can silently replace a DLL
// without updating its component build record.
runComponentBuild(connectorRoot);
runComponentBuild(annotatorRoot);

run(uiProject, [
  `-p:ConnectorAssembly=${connectorAssembly}`,
  `-p:AnnotatorAssembly=${annotatorAssembly}`,
  `-p:SourceRevision=${source.source_revision}`,
  `-p:SourceDigestSha256=${source.source_digest_sha256}`,
  ...gameProperty
]);

const dataDirectory = process.platform === "darwin"
  ? path.join(gameDir, "SlayTheSpire2.app/Contents/Resources", `data_sts2_macos_${process.arch === "x64" ? "x86_64" : "arm64"}`)
  : process.platform === "win32"
    ? path.join(gameDir, "data_sts2_windows_x86_64")
    : path.join(gameDir, "data_sts2_linuxbsd_x86_64");
const releaseInfo = process.platform === "darwin"
  ? path.join(gameDir, "SlayTheSpire2.app/Contents/Resources/release_info.json")
  : path.join(gameDir, "release_info.json");
const provenance = {
  schema: "sts2.platform/live-ui-build-provenance-1",
  built_at: new Date().toISOString(),
  platform: process.platform,
  architecture: process.arch,
  source,
  game: {
    release: readJson(releaseInfo),
    sts2: exactIdentity(path.join(dataDirectory, "sts2.dll")),
    godotsharp_sha256: sha256(path.join(dataDirectory, "GodotSharp.dll"))
  },
  dependencies: {
    connector: exactIdentity(connectorAssembly),
    annotator: exactIdentity(annotatorAssembly)
  },
  artifact: exactIdentity(uiAssembly)
};
fs.writeFileSync(path.join(uiOutput, "build-provenance.json"), `${JSON.stringify(provenance, null, 2)}\n`);
process.stdout.write(`${JSON.stringify(provenance, null, 2)}\n`);
