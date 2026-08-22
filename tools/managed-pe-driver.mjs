#!/usr/bin/env node
import readline from "node:readline";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { discoverGameDirectory, readDiskIdentity, resolveInstallation } from "../src/game-installation.mjs";
import { canonicalizeEpisodeSeed } from "../src/episode-provenance.mjs";
import { startManagedPlayerEnvironmentSession } from "../src/managed-player-environment.mjs";
import { readProjectIdentity } from "../src/project-identity.mjs";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function option(args, name, fallback = null) {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
}

function write(value) {
  process.stdout.write(`${JSON.stringify(value)}\n`);
}

const args = process.argv.slice(2);
const candidateDirectory = option(args, "--candidate");
if (candidateDirectory == null) throw new Error("managed-pe-driver requires --candidate <prepared-directory>.");
const gameDirectory = option(args, "--game-dir", discoverGameDirectory());
if (gameDirectory == null) throw new Error("Could not locate STS2; set --game-dir or STS2_GAME_DIR.");
const requestTimeoutMs = Number(option(args, "--timeout-ms", "10000"));
const started = await startManagedPlayerEnvironmentSession({
  root: ROOT,
  candidateDirectory,
  diskIdentity: readDiskIdentity(resolveInstallation(gameDirectory)),
  character: option(args, "--character", "Ironclad"),
  requestTimeoutMs,
  quietDiagnostics: args.includes("--quiet-diagnostics")
});
let mounted = false;
let closed = false;
let requestedEpisodeSeed = null;

write({
  type: "ready",
  protocol: "sts2.headless/managed-player-environment-driver-1",
  headless: readProjectIdentity(ROOT),
  candidate_build: started.runtime.build,
  runtime_identity: started.runtime.runtimeIdentity,
  adapter_runtime_instance_id: started.runtime.adapterRuntimeInstanceId,
  environment_fingerprint: started.environmentFingerprint
});

async function handle(request) {
  const requestId = request?.request_id ?? null;
  switch (request?.command) {
    case "reset": {
      requestedEpisodeSeed = canonicalizeEpisodeSeed(request.seed);
      const snapshot = await started.session.mount({
        seed: requestedEpisodeSeed,
        reset: mounted,
        timeoutMs: requestTimeoutMs
      });
      mounted = true;
      return { type: "reset_result", request_id: requestId, snapshot };
    }
    case "observe":
      return { type: "observe_result", request_id: requestId, snapshot: started.session.observe() };
    case "read":
      return {
        type: "read_result",
        request_id: requestId,
        read: started.session.read({
          readId: request.read_id,
          expectedSnapshotId: request.expected_snapshot_id
        })
      };
    case "step":
      return {
        type: "step_result",
        request_id: requestId,
        receipt: await started.session.submit({
          requestId: request.mutation_request_id,
          expectedSnapshotId: request.expected_snapshot_id,
          boundActionId: request.bound_action_id,
          timeoutMs: requestTimeoutMs
        })
      };
    case "episode_identity": {
      const runIdentity = await started.runtime.process.request({ cmd: "run_identity" }, requestTimeoutMs);
      return {
        type: "episode_identity_result",
        request_id: requestId,
        identity: {
          candidate_build: started.runtime.build,
          runtime_identity: started.runtime.runtimeIdentity,
          adapter_runtime_instance_id: started.runtime.adapterRuntimeInstanceId,
          environment_fingerprint: started.environmentFingerprint,
          episode_provenance: {
            verdict: runIdentity?.type === "run_identity"
              && runIdentity.active === true
              && runIdentity.seed === requestedEpisodeSeed
              ? "provenance_pass"
              : "provenance_incomplete",
            requested_seed: requestedEpisodeSeed,
            actual_seed: runIdentity?.seed ?? null,
            runtime_instance_id: started.runtime.adapterRuntimeInstanceId
          }
        }
      };
    }
    case "close":
      closed = true;
      return { type: "close_result", request_id: requestId, exit: await started.session.close() };
    default:
      throw new Error(`Unsupported driver command: ${String(request?.command)}`);
  }
}

const input = readline.createInterface({ input: process.stdin });
let queue = Promise.resolve();
input.on("line", (line) => {
  queue = queue.then(async () => {
    let request;
    try {
      request = JSON.parse(line);
      const response = await handle(request);
      write(response);
      if (closed) input.close();
    } catch (error) {
      write({
        type: "error",
        request_id: request?.request_id ?? null,
        code: "driver_request_failed",
        message: error instanceof Error ? error.message : String(error)
      });
    }
  });
});
input.on("close", async () => {
  await queue;
  if (!closed) await started.session.close().catch(() => null);
});
