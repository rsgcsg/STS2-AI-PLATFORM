import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");

test("nested npm Host entrypoints preserve the script argument boundary", () => {
  const workspace = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
  const hostScripts = Object.entries(workspace.scripts)
    .filter(([name]) => name.startsWith("host:"));

  assert.ok(hostScripts.length > 0);
  for (const [name, command] of hostScripts) {
    if (command.startsWith("npm --prefix components/host-runtime run ")) {
      assert.match(command, / --$/u, `${name} must forward caller arguments after npm's -- separator`);
    }
  }
});

test("exact-game validation builds the Connector before the dependent Annotator", () => {
  const workspace = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
  const command = workspace.scripts["check:exact-game"];
  const connectorBuild = command.indexOf("npm --prefix components/connector run build");
  const annotatorCheck = command.indexOf("npm --prefix components/annotator run check");

  assert.ok(connectorBuild >= 0, "exact-game validation must produce the exact Connector artifact");
  assert.ok(annotatorCheck > connectorBuild,
    "Annotator validation must consume the already-built Connector artifact");
});
