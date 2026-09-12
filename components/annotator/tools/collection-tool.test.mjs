import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { canonical, inventory, publishCollectionTool } from "./publish-collection-tool.mjs";

test("release identity sorts nested keys and binds exact dependency bytes", () => {
  assert.equal(canonical({ z: [{ b: 2, a: 1 }], a: "你好" }), '{"a":"你好","z":[{"a":1,"b":2}]}');
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "collection-tool-"));
  try {
    fs.writeFileSync(path.join(directory, "tool.dll"), "original");
    const first = inventory(directory);
    fs.writeFileSync(path.join(directory, "tool.dll"), "replaced");
    assert.notEqual(inventory(directory)[0].sha256, first[0].sha256);
  } finally { fs.rmSync(directory, { recursive: true, force: true }); }
});

test("release publication refuses dirty source before invoking any build", () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "collection-source-"));
  const git = (...args) => execFileSync("git", args, { cwd: directory, stdio: "pipe" });
  try {
    git("init"); git("config", "user.name", "Fixture"); git("config", "user.email", "fixture@example.invalid");
    fs.writeFileSync(path.join(directory, "source"), "one");
    git("add", "source"); git("commit", "-m", "fixture");
    fs.writeFileSync(path.join(directory, "source"), "two");
    assert.throws(() => publishCollectionTool(directory, path.join(directory, "out"), { dotnet: "must-not-run" }), /clean workspace/);
    assert.equal(fs.existsSync(path.join(directory, "out")), false);
  } finally { fs.rmSync(directory, { recursive: true, force: true }); }
});
