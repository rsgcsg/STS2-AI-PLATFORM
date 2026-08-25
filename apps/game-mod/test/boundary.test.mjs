import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

import { compiledSourceDigest } from "../source-identity.mjs";

const root = path.resolve(import.meta.dirname, "../../..");
const read = (relative) => fs.readFileSync(path.join(root, relative), "utf8");

test("production game Mod has one manifest, assembly and explicit initializer", () => {
  const manifest = JSON.parse(read("apps/game-mod/mod_manifest.json"));
  const project = read("apps/game-mod/STS2Platform.GameMod.csproj");
  const initializer = read("apps/game-mod/UnifiedPlatformMod.cs");

  assert.equal(manifest.id, "STS2_PLATFORM");
  assert.deepEqual(manifest.dependencies, []);
  assert.match(project, /<AssemblyName>STS2_PLATFORM<\/AssemblyName>/u);
  assert.match(project, /STS2_PLATFORM_UNIFIED/u);
  assert.match(project, /<SourceRevisionId>\$\(PlatformSourceRevision\)<\/SourceRevisionId>/u);
  assert.match(initializer, /ConnectorMod\.Initialize\(\);[\s\S]*RecorderMod\.Initialize\(\);[\s\S]*PlatformLiveUiMod\.Initialize\(\);/u);
});

test("compiled identity ignores repository-only provenance", () => {
  const component = {
    source_revision: "a".repeat(40),
    source_digest_sha256: "b".repeat(64),
    component_tree_revision: "c".repeat(40),
    component_worktree_status: "clean",
    file_count: 3
  };
  const baseline = compiledSourceDigest({ game_mod: component });
  const repositoryOnlyDrift = compiledSourceDigest({
    game_mod: {
      ...component,
      component_tree_revision: "d".repeat(40),
      component_worktree_status: "dirty",
      file_count: 99
    }
  });
  const nativeDrift = compiledSourceDigest({
    game_mod: { ...component, source_digest_sha256: "e".repeat(64) }
  });

  assert.equal(repositoryOnlyDrift, baseline);
  assert.notEqual(nativeDrift, baseline);
});

test("component initializers are disabled only in the unified build", () => {
  for (const file of [
    "components/connector/host/ConnectorMod.cs",
    "components/annotator/src/STS2HumanAnnotator.Mod/RecorderMod.cs",
    "apps/ingame-ui/PlatformLiveUiMod.cs"
  ]) {
    const source = read(file);
    assert.match(source, /#if !STS2_PLATFORM_UNIFIED\s+\[ModInitializer\("Initialize"\)\]\s+#endif/u);
  }
});

test("Live UI uses K from the SceneTree signal and logs readiness", () => {
  const source = read("apps/ingame-ui/PlatformLiveUiMod.cs");
  assert.match(source, /internal sealed class PlatformLivePanel : IDisposable/u);
  assert.match(source, /tree\.ProcessFrame \+= _processFrameHandler/u);
  assert.match(source, /Input\.IsKeyPressed\(Key\.K\) \|\| Input\.IsPhysicalKeyPressed\(Key\.K\)/u);
  assert.doesNotMatch(source, /class PlatformLivePanel : Control/u);
  assert.doesNotMatch(source, /override void _(Ready|Process|Input)/u);
  assert.match(source, /adding layer to SceneTree root/u);
  assert.match(source, /panel mount failed/u);
  assert.match(source, /panel ready; input=K/u);
  assert.doesNotMatch(source, /Key\.F\d+/u);
});

test("single-Mod deploy retires every legacy production manifest and DLL", () => {
  const lifecycle = read("apps/game-mod/lifecycle.mjs");
  for (const name of [
    "STS2_MCP.dll",
    "STS2_MCP.json",
    "STS2_HUMAN_ANNOTATOR.dll",
    "STS2_HUMAN_ANNOTATOR.json",
    "STS2_PLATFORM_LIVE_UI.dll",
    "STS2_PLATFORM_LIVE_UI.json"
  ]) assert.match(lifecycle, new RegExp(name.replace(".", "\\."), "u"));
});

test("loaded verification never promotes an input canary to owner evidence", () => {
  const lifecycle = read("apps/game-mod/lifecycle.mjs");
  assert.match(lifecycle, /ui_toggle_runtime_canary/u);
  assert.match(lifecycle, /owner_ui_visibility: "pending human runtime evidence"/u);
  assert.match(lifecycle, /input_canary_is_not_owner_visibility_evidence/u);
  assert.doesNotMatch(lifecycle, /owner_ui_toggle/u);
});

test("record settlement accepts only shared exact Modsets and never retries an unknown evidence commit", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const validator = read("components/annotator/src/STS2HumanAnnotator.Core/RecordValidation.cs");

  assert.match(runtime, /RecordingEnvironmentAdmission\.IsExactModset/u);
  assert.match(validator, /RecordingEnvironmentAdmission\.IsExactModset/u);
  assert.match(runtime, /decision_persistence_unknown/u);
  assert.match(runtime, /evidence_commit_unknown/u);
  assert.match(runtime, /ClearPendingWithInvalidation\([\s\S]*decision_persistence_unknown/u);
});

test("native card staging permits transient snapshot advance without weakening identity guards", () => {
  const runtime = read("components/annotator/src/STS2HumanAnnotator.Mod/RecorderRuntime.cs");
  const guard = read("components/annotator/src/STS2HumanAnnotator.Core/RecordingApplication.cs");

  assert.match(runtime, /StagedCardPlayGuard\.IsContinuous/u);
  assert.doesNotMatch(runtime, /cached\.Frame\.Snapshot\.SnapshotId,[\s\S]*current\.Snapshot\.SnapshotId/u);
  assert.match(guard, /currentSequence >= stagedSequence/u);
  assert.match(guard, /stagedRuntimeInstanceId/u);
  assert.match(guard, /stagedEnvironmentFingerprint/u);
  assert.match(guard, /stagedInteractionId/u);
  assert.match(guard, /externalControllerActive/u);
});
