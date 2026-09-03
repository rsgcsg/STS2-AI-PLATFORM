import assert from "node:assert/strict";
import test from "node:test";
import { SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL } from "@rsgcsg/sts2-connector-client";
import {
  commandOwnsHeadlessRuntime,
  evaluateHeadlessCapabilities,
  processCommand
} from "../src/headless-host.mjs";

test("recognizes only the exact recorded executable with a headless argument", () => {
  const executable = "/games/Slay the Spire 2";
  assert.equal(commandOwnsHeadlessRuntime(
    `${executable} --headless --verbose`,
    executable
  ), true);
  assert.equal(commandOwnsHeadlessRuntime(executable, executable), false);
  assert.equal(commandOwnsHeadlessRuntime(
    "/other/Slay the Spire 2 --headless",
    executable
  ), false);
  assert.equal(commandOwnsHeadlessRuntime(
    '"E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\SlayTheSpire2.exe" --headless --verbose',
    "e:/steamlibrary/steamapps/common/slay the spire 2/slaythespire2.exe"
  ), true);
});

test("does not inspect invalid process identifiers", () => {
  assert.equal(processCommand(0), null);
  assert.equal(processCommand(-1), null);
  assert.equal(processCommand(Number.NaN), null);
});

test("admits only the exact executable Headless Player Environment", () => {
  const capabilities = {
    protocol_version: SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
    execution_available: true,
    host: { host_kind: "headless" },
    game: { modset: { status: "exact_player_environment_only" } }
  };
  assert.deepEqual(evaluateHeadlessCapabilities(capabilities), { ok: true, errors: [] });
  assert.deepEqual(evaluateHeadlessCapabilities({
    ...capabilities,
    game: { modset: { status: "exact_platform_modset" } }
  }), { ok: true, errors: [] });
  assert.deepEqual(evaluateHeadlessCapabilities({
    ...capabilities,
    host: { host_kind: "live_ui" }
  }), { ok: false, errors: ["host_kind_not_headless"] });
  assert.deepEqual(evaluateHeadlessCapabilities({
    ...capabilities,
    game: { modset: { status: "additional_loaded_mods" } }
  }), { ok: false, errors: ["unsupported_modset"] });
});
