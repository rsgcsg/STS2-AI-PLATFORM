#!/usr/bin/env node
// Build-only Git authority. Installed tools verify their pinned bytes, never a checkout.
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { componentGitState } from "../../../tools/component-git.mjs";

export function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  if (value !== null && typeof value === "object")
    return `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
  return JSON.stringify(value);
}

export function inventory(directory) {
  const files = [];
  function visit(current, prefix = "") {
    for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
      const name = prefix + entry.name;
      if (entry.isSymbolicLink()) throw new Error("Collection tool release cannot contain symlinks");
      if (entry.isDirectory()) visit(path.join(current, entry.name), `${name}/`);
      else if (entry.isFile()) {
        const bytes = fs.readFileSync(path.join(current, entry.name));
        files.push({ path: name, bytes: bytes.length, sha256: crypto.createHash("sha256").update(bytes).digest("hex") });
      } else throw new Error("Unsupported collection tool file type");
    }
  }
  visit(directory);
  return files.sort((a, b) => a.path < b.path ? -1 : a.path > b.path ? 1 : 0);
}

export function publishCollectionTool(componentRoot, output, { dotnet = "dotnet" } = {}) {
  const state = componentGitState(componentRoot);
  if (state.workspaceWorktreeStatus !== "clean")
    throw new Error("Collection tool release requires an exact clean workspace; commit first.");
  const destination = path.resolve(output);
  if (fs.existsSync(destination)) throw new Error("Collection tool output already exists; releases are immutable.");
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  const staging = fs.mkdtempSync(path.join(path.dirname(destination), ".collection-tool-"));
  try {
    execFileSync(dotnet, ["publish", path.join(componentRoot, "src/STS2HumanAnnotator.Tool/STS2HumanAnnotator.Tool.csproj"),
      "-c", "Release", "-o", staging, "--self-contained", "false", "-p:UseAppHost=false"],
    { stdio: "inherit" });
    fs.copyFileSync(path.join(state.workspaceRoot, "platform-bom.json"), path.join(staging, "platform-bom.json"));
    if (componentGitState(componentRoot).workspaceWorktreeStatus !== "clean"
        || componentGitState(componentRoot).workspaceRevision !== state.workspaceRevision)
      throw new Error("Source identity changed during collection tool publication");
    const identity = {
      product: "STS2 Platform Collection Tool", version: 1, worktree: "clean",
      workspace_revision: state.workspaceRevision,
      source_revision: state.componentSourceRevision,
      component_tree_revision: state.componentTreeRevision,
      entrypoint: "sts2-human-annotator.dll",
      supported_recording_schema: "sts2.human-annotator/recording-manifest-2",
      output_schema: "sts2.human-annotator/session-bundle-3", files: inventory(staging)
    };
    const release_id = crypto.createHash("sha256").update(canonical(identity)).digest("hex");
    fs.writeFileSync(path.join(staging, "collection-tool.json"),
      `${canonical({ schema: "sts2.evidence/collection-tool-1", release_id, identity })}\n`);
    fs.renameSync(staging, destination);
    return { status: "published", directory: destination, release_id, identity };
  } catch (error) {
    fs.rmSync(staging, { recursive: true, force: true });
    throw error;
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  if (args.length !== 2 || args[0] !== "--output")
    throw new Error("usage: publish-collection-tool.mjs --output /absolute/new-release-directory");
  console.log(JSON.stringify(publishCollectionTool(path.resolve(path.dirname(fileURLToPath(import.meta.url)), ".."), args[1]), null, 2));
}
