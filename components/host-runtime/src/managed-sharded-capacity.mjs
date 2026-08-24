import { execFile } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";
import { summarizeManagedPlayerEnvironmentCapacityGroup } from "./managed-player-environment-probe.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { readSystemIdentity } from "./system-identity.mjs";

const execFileAsync = promisify(execFile);
const WORKER = fileURLToPath(new URL("../tools/managed-pe-worker.mjs", import.meta.url));

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

async function runWorker(configFile, timeoutMs) {
  const { stdout, stderr } = await execFileAsync(process.execPath, [WORKER, configFile], {
    encoding: "utf8",
    timeout: timeoutMs,
    maxBuffer: 64 * 1024 * 1024
  });
  return { ...JSON.parse(stdout), stderr };
}

export async function runManagedPlayerEnvironmentShardedCapacity({
  root,
  candidateDirectory,
  gameDirectory,
  workerCounts,
  maxActions,
  episodesPerWorker,
  seedPrefix,
  character,
  requestTimeoutMs,
  evidenceRoot,
  profile
}) {
  const outputDirectory = path.join(evidenceRoot, `managed-player-environment-sharded-${safeTimestamp()}`);
  mkdirSync(outputDirectory, { recursive: true });
  const temporary = mkdtempSync(path.join(os.tmpdir(), "sts2-headless-shards-"));
  const groups = [];
  try {
    for (const workerCount of workerCounts) {
      const configs = Array.from({ length: workerCount }, (_, index) => {
        const file = path.join(temporary, `worker-${workerCount}-${index}.json`);
        writeFileSync(file, JSON.stringify({
          gameDirectory,
          probe: {
            root,
            candidateDirectory,
            seed: seedPrefix,
            character,
            maxActions,
            episodeCount: episodesPerWorker,
            requestTimeoutMs,
            ...profile
          }
        }));
        return file;
      });
      const groupStarted = performance.now();
      const coordinatorCpuStarted = process.cpuUsage();
      const workers = await Promise.all(configs.map((file) => runWorker(
        file,
        requestTimeoutMs * Math.max(10, episodesPerWorker * maxActions)
      )));
      const groupWallSeconds = (performance.now() - groupStarted) / 1000;
      const coordinatorCpu = process.cpuUsage(coordinatorCpuStarted);
      const coordinatorCpuSeconds = (coordinatorCpu.user + coordinatorCpu.system) / 1_000_000;
      const workerNodeCpuSeconds = workers.reduce((sum, worker) => sum + worker.worker_node.cpu_seconds, 0);
      const summary = summarizeManagedPlayerEnvironmentCapacityGroup(
        workers.map((worker) => ({ report: worker.report })),
        groupWallSeconds,
        {
          topology: "independent_node_supervisor_per_managed_runtime",
          coordinator_cpu_seconds: coordinatorCpuSeconds,
          worker_node_cpu_seconds: workerNodeCpuSeconds,
          total_node_cpu_seconds: coordinatorCpuSeconds + workerNodeCpuSeconds,
          coordinator_final_rss_bytes: process.memoryUsage().rss,
          summed_worker_node_final_rss_bytes: workers.reduce(
            (sum, worker) => sum + worker.worker_node.final_rss_bytes,
            0
          ),
          stderr_bytes: workers.reduce((sum, worker) => sum + Buffer.byteLength(worker.stderr), 0)
        }
      );
      groups.push(summary);
    }
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
  const report = {
    schema: "sts2.headless/managed-player-environment-sharded-capacity-1",
    generated_at: new Date().toISOString(),
    status: groups.every((group) => group.status === "measured_canonical_partial_unqualified")
      ? "measured_canonical_partial_unqualified"
      : "measurement_incomplete",
    headless: readProjectIdentity(root),
    system_identity: readSystemIdentity(),
    topology: "sharded_node_supervisors",
    worker_counts: workerCounts,
    episodes_per_worker: episodesPerWorker,
    max_actions_per_episode: maxActions,
    seed_prefix: seedPrefix,
    character,
    profile,
    groups,
    non_claims: [
      "Sharding changes orchestration only; it does not qualify managed gameplay semantics.",
      "This bounded deterministic workload is not a real learner resource envelope or long-run reliability evidence."
    ]
  };
  const reportFile = path.join(outputDirectory, "report.json");
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  return { report, reportFile, outputDirectory };
}
