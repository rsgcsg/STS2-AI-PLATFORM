import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { summarizeCapacityGroup } from "./capacity-benchmark.mjs";
import { canonicalizeEpisodeSeed } from "./episode-provenance.mjs";
import { readDiskIdentity } from "./game-installation.mjs";
import { runBoundedJourney } from "./journey-probe.mjs";
import { instantiateProfileTemplate } from "./profile-template.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { listGameProcesses, readJson } from "./runtime-probe.mjs";
import { readSystemIdentity } from "./system-identity.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

export function episodeSeed(baseSeed, episode) {
  if (!Number.isSafeInteger(episode) || episode < 1) {
    throw new Error("Soak episode must be a positive integer.");
  }
  return canonicalizeEpisodeSeed(`${baseSeed}${String(episode).padStart(6, "0")}`);
}

export function summarizeSoakEpisodes(episodes, requestedEpisodes, workerCount) {
  const completed = episodes.filter((episode) => episode.status === "measured");
  const failures = episodes.flatMap((episode) => episode.failures ?? []);
  const runtimeIds = completed.flatMap((episode) => episode.runtime_instance_ids);
  const generationIds = completed.flatMap((episode) => episode.generation_ids);
  const errors = [];
  if (completed.length !== requestedEpisodes) errors.push("episode_count_incomplete");
  if (failures.length > 0) errors.push("worker_failures_observed");
  if (new Set(runtimeIds).size !== runtimeIds.length) errors.push("runtime_instance_reused");
  if (new Set(generationIds).size !== generationIds.length) errors.push("profile_generation_reused");
  if (episodes.some((episode) => episode.remaining_processes?.length > 0)) {
    errors.push("process_leak_observed");
  }
  if (episodes.some((episode) => episode.endpoint_release_pass !== true)) {
    errors.push("endpoint_leak_observed");
  }
  const decisions = completed.reduce(
    (sum, episode) => sum + episode.delivered_normalized_semantic_decisions,
    0
  );
  const decisionWindowSeconds = completed.reduce(
    (sum, episode) => sum + episode.common_decision_window_seconds,
    0
  );
  return {
    verdict: errors.length === 0 ? "soak_smoke_pass" : "soak_incomplete",
    errors,
    requested_episodes: requestedEpisodes,
    completed_episodes: completed.length,
    worker_count: workerCount,
    successful_worker_runs: completed.length * workerCount,
    worker_failures: failures.length,
    unique_runtime_instances: new Set(runtimeIds).size,
    unique_profile_generations: new Set(generationIds).size,
    delivered_normalized_semantic_decisions: decisions,
    measured_decision_window_seconds: decisionWindowSeconds,
    aggregate_normalized_semantic_decisions_per_second:
      decisionWindowSeconds > 0 ? decisions / decisionWindowSeconds : null
  };
}

export async function runReferenceSoak({
  installation,
  localRoot,
  evidenceRoot,
  templateId = "vanilla-clean",
  workerCount = 2,
  episodes = 2,
  actionsPerWorker = 8,
  basePort = 15800,
  baseSeed = "H1SOAK",
  timeoutMs = 90_000,
  actionTimeoutMs = 20_000,
  maxWorkerFailures = 0,
  experimentalBuildAcknowledged = false
}) {
  if (!Number.isSafeInteger(workerCount) || workerCount < 1 || workerCount > 32) {
    throw new Error("Soak worker count must be an integer from 1 through 32.");
  }
  if (!Number.isSafeInteger(episodes) || episodes < 1 || episodes > 100_000) {
    throw new Error("Soak episodes must be an integer from 1 through 100000.");
  }
  if (!Number.isSafeInteger(actionsPerWorker) || actionsPerWorker < 1) {
    throw new Error("Soak actions per worker must be a positive integer.");
  }
  if (!Number.isSafeInteger(maxWorkerFailures) || maxWorkerFailures < 0) {
    throw new Error("Soak worker failure budget must be a non-negative integer.");
  }
  if (!Number.isSafeInteger(basePort) || basePort < 1024 || basePort + workerCount > 65535) {
    throw new Error("Soak port range is invalid.");
  }
  const existing = listGameProcesses();
  if (existing.length > 0) {
    throw new Error(`Reference soak requires a clean process baseline:\n${existing.join("\n")}`);
  }
  const diskIdentity = readDiskIdentity(installation);
  const evidenceDirectory = path.join(evidenceRoot, `reference-soak-${safeTimestamp()}`);
  const reportFile = path.join(evidenceDirectory, "report.json");
  mkdirSync(evidenceDirectory, { recursive: true });
  const report = {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    status: "running",
    route: "reference_shipped_bounded_soak",
    headless: readProjectIdentity(),
    system_identity: readSystemIdentity(),
    disk_identity: diskIdentity,
    template_id: templateId,
    worker_count: workerCount,
    requested_episodes: episodes,
    actions_per_worker: actionsPerWorker,
    base_port: basePort,
    base_seed: canonicalizeEpisodeSeed(baseSeed),
    max_worker_failures: maxWorkerFailures,
    episodes: [],
    non_claims: [
      "A bounded smoke is not the 72-hour or 10-million-decision H1.0 soak gate.",
      "Infrastructure truncation is not a gameplay loss or terminal state.",
      "The deterministic policy is a test consumer, not a gameplay or learning agent."
    ]
  };
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  let failuresObserved = 0;

  for (let episode = 1; episode <= episodes; episode += 1) {
    const seed = episodeSeed(report.base_seed, episode);
    const workers = Array.from({ length: workerCount }, (_, index) => {
      const worker = index + 1;
      const workerId = `soak-e${String(episode).padStart(6, "0")}-w${String(worker).padStart(2, "0")}`;
      const profileId = `soak-w${String(worker).padStart(2, "0")}`;
      return {
        worker_id: workerId,
        profile_id: profileId,
        endpoint: `http://127.0.0.1:${basePort + index}`,
        profile: instantiateProfileTemplate({
          localRoot,
          templateId,
          profileId,
          expectedGameIdentity: diskIdentity
        })
      };
    });
    const settled = await Promise.allSettled(workers.map((worker) => runBoundedJourney({
      installation,
      localRoot,
      endpoint: worker.endpoint,
      timeoutMs,
      actionTimeoutMs,
      maxActions: actionsPerWorker,
      tutorialPreference: "disable",
      evidenceRoot,
      isolatedProfileId: worker.profile_id,
      experimentalBuildAcknowledged,
      allowConcurrentProcesses: true,
      evidenceLabel: worker.worker_id,
      runSeed: seed
    })));
    const failures = settled.flatMap((entry, index) => entry.status === "rejected"
      ? [{
          worker_id: workers[index].worker_id,
          classification: "infrastructure_truncated",
          error: entry.reason instanceof Error ? entry.reason.message : String(entry.reason)
        }]
      : []);
    failuresObserved += failures.length;
    const remainingProcesses = listGameProcesses();
    const endpointStates = await Promise.all(workers.map(async (worker) => ({
      endpoint: worker.endpoint,
      released: !(await readJson(
        worker.endpoint,
        "/api/player-environment/capabilities",
        1_000
      )).ok
    })));
    const completed = settled.filter((entry) => entry.status === "fulfilled").map((entry) => entry.value);
    let episodeReport;
    if (failures.length === 0) {
      const measured = summarizeCapacityGroup(completed);
      episodeReport = {
        episode,
        seed,
        ...measured,
        generation_ids: workers.map((worker) => worker.profile.generation_id),
        profiles: workers.map((worker) => worker.profile),
        endpoint_states: endpointStates,
        endpoint_release_pass: endpointStates.every((state) => state.released),
        remaining_processes: remainingProcesses,
        failures
      };
    } else {
      episodeReport = {
        episode,
        seed,
        status: "worker_failure",
        generation_ids: workers.map((worker) => worker.profile.generation_id),
        runtime_instance_ids: completed.map(
          (result) => result.report.loaded_identity?.host?.runtime_instance_id ?? null
        ).filter(Boolean),
        endpoint_states: endpointStates,
        endpoint_release_pass: endpointStates.every((state) => state.released),
        remaining_processes: remainingProcesses,
        failures
      };
    }
    report.episodes.push(episodeReport);
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    if (failuresObserved > maxWorkerFailures
        || remainingProcesses.length > 0
        || !episodeReport.endpoint_release_pass) {
      break;
    }
  }

  report.summary = summarizeSoakEpisodes(report.episodes, episodes, workerCount);
  report.status = report.summary.verdict;
  report.completed_at = new Date().toISOString();
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  return { report, reportFile, evidenceDirectory };
}
