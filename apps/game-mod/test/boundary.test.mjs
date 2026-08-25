import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

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
  assert.match(initializer, /ConnectorMod\.Initialize\(\);[\s\S]*RecorderMod\.Initialize\(\);[\s\S]*PlatformLiveUiMod\.Initialize\(\);/u);
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

test("Live UI uses K at the early input stage and logs readiness", () => {
  const source = read("apps/ingame-ui/PlatformLiveUiMod.cs");
  assert.match(source, /SetProcessInput\(true\)/u);
  assert.match(source, /public override void _Input\(InputEvent @event\)/u);
  assert.match(source, /key\.Keycode == Key\.K \|\| key\.PhysicalKeycode == Key\.K/u);
  assert.match(source, /NativeUiMainThread\.Run/u);
  assert.match(source, /private static async Task MountOnMainThread\(CanvasLayer layer\)/u);
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
