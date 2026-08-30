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
