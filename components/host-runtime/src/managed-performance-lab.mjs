import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { startManagedCandidateRuntime } from "./managed-candidate.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { readSystemIdentity } from "./system-identity.mjs";

const PROFILES = Object.freeze({
  qualification: Object.freeze({
    profileName: "qualification",
    identityMode: "crypto",
    validateSdk: true,
    eagerReads: true,
    canonicalEvidence: true,
    resourceSamplingIntervalMs: 250,
    quietDiagnostics: false
  }),
  training: Object.freeze({
    profileName: "training",
    identityMode: "sequence",
    validateSdk: false,
    eagerReads: false,
    canonicalEvidence: false,
    resourceSamplingIntervalMs: null,
    quietDiagnostics: true
  }),
  "training-validated": Object.freeze({
    profileName: "training-validated",
    identityMode: "sequence",
    validateSdk: true,
    eagerReads: false,
    canonicalEvidence: false,
    resourceSamplingIntervalMs: null,
    quietDiagnostics: true
  }),
  reliability: Object.freeze({
    profileName: "reliability",
    identityMode: "sequence",
    validateSdk: true,
    eagerReads: false,
    canonicalEvidence: false,
    resourceSamplingIntervalMs: 1000,
    quietDiagnostics: true
  }),
  "qualification-no-sampler": Object.freeze({
    profileName: "qualification-no-sampler",
    identityMode: "crypto",
    validateSdk: true,
    eagerReads: true,
    canonicalEvidence: true,
    resourceSamplingIntervalMs: null,
    quietDiagnostics: false
  }),
  "qualification-sequence": Object.freeze({
    profileName: "qualification-sequence",
    identityMode: "sequence",
    validateSdk: true,
    eagerReads: true,
    canonicalEvidence: true,
    resourceSamplingIntervalMs: null,
    quietDiagnostics: false
  }),
  "qualification-lazy-reads": Object.freeze({
    profileName: "qualification-lazy-reads",
    identityMode: "sequence",
    validateSdk: true,
    eagerReads: false,
    canonicalEvidence: true,
    resourceSamplingIntervalMs: null,
    quietDiagnostics: false
  }),
  "qualification-no-evidence": Object.freeze({
    profileName: "qualification-no-evidence",
    identityMode: "sequence",
    validateSdk: true,
    eagerReads: false,
    canonicalEvidence: false,
    resourceSamplingIntervalMs: null,
    quietDiagnostics: false
  })
});

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

export function managedPerformanceProfile(name) {
  const profile = PROFILES[name];
  if (profile == null) {
    throw new Error(`Unknown managed performance profile ${name}. Expected ${Object.keys(PROFILES).join(", ")}.`);
  }
  return { ...profile };
}

export async function runManagedEngineBenchmark({
  root,
  candidateDirectory,
  diskIdentity,
  episodes = 5,
  warmupEpisodes = 1,
  maxActions = 600,
  seedPrefix = "H1ENGINE",
  character = "Ironclad",
  serializeEachDecision = false,
  requestTimeoutMs = 120_000,
  evidenceRoot = null
}) {
  const processStarted = performance.now();
  const runtime = await startManagedCandidateRuntime({
    root,
    candidateDirectory,
    diskIdentity,
    requestTimeoutMs,
    quietDiagnostics: true
  });
  let benchmark;
  let before;
  let after;
  try {
    before = await runtime.process.request({ cmd: "process_metrics" }, requestTimeoutMs);
    benchmark = await runtime.process.request({
      cmd: "benchmark_engine",
      episodes,
      warmup_episodes: warmupEpisodes,
      max_actions: maxActions,
      seed_prefix: seedPrefix,
      character,
      serialize_each_decision: serializeEachDecision
    }, requestTimeoutMs);
    after = await runtime.process.request({ cmd: "process_metrics" }, requestTimeoutMs);
  } finally {
    await runtime.process.stop({ request: { cmd: "quit" }, timeoutMs: 5_000 });
  }
  const report = {
    schema: "sts2.headless/managed-engine-performance-1",
    generated_at: new Date().toISOString(),
    status: benchmark?.status === "measured_exact_game_owned_loop" ? "measured" : "failed",
    headless: readProjectIdentity(root),
    system_identity: readSystemIdentity(),
    game_identity: {
      version: diskIdentity.release.version,
      commit: diskIdentity.release.commit,
      runtime_main_assembly_hash: diskIdentity.runtime_main_assembly_hash,
      sts2_dll_sha256: diskIdentity.sts2_assembly.sha256
    },
    candidate: {
      manifest: runtime.manifest,
      build: runtime.build,
      runtime_identity: runtime.runtimeIdentity
    },
    process_lifecycle_wall_ms: performance.now() - processStarted,
    benchmark,
    process_delta: {
      cpu_ms: after.cpu_total_ms - before.cpu_total_ms,
      allocated_bytes: after.allocated_bytes_total - before.allocated_bytes_total,
      gc_collections: {
        gen0: after.gen0_collections - before.gen0_collections,
        gen1: after.gen1_collections - before.gen1_collections,
        gen2: after.gen2_collections - before.gen2_collections
      },
      final_working_set_bytes: after.working_set_bytes,
      final_private_bytes: after.private_bytes,
      final_managed_heap_bytes: after.managed_heap_bytes
    },
    non_claims: [
      "This engine lab includes the managed candidate lifecycle and decision detector, not the shipped Reference Host.",
      "A deterministic bounded loop is not semantic qualification, reliability, learner, or transfer evidence.",
      "The JSON shadow measures serialization work but does not include pipes, parsing, Node projection, or SDK validation."
    ]
  };
  let reportFile = null;
  if (evidenceRoot != null) {
    const directory = path.join(evidenceRoot, `managed-engine-performance-${safeTimestamp()}`);
    mkdirSync(directory, { recursive: true });
    reportFile = path.join(directory, "report.json");
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  }
  return { report, reportFile };
}
