import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { runBoundedJourney } from "./journey-probe.mjs";
import { listGameProcesses } from "./runtime-probe.mjs";
import { instantiateProfileTemplate } from "./profile-template.mjs";
import { readDiskIdentity } from "./game-installation.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { canonicalizeEpisodeSeed } from "./episode-provenance.mjs";
import { readSystemIdentity } from "./system-identity.mjs";

const GIB = 1024 ** 3;

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

export function parseWorkerCounts(value) {
  const counts = String(value).split(",").map((entry) => Number(entry.trim()));
  if (counts.length === 0
      || counts.some((entry) => !Number.isSafeInteger(entry) || entry < 1 || entry > 32)
      || new Set(counts).size !== counts.length) {
    throw new Error("Worker counts must be unique integers from 1 through 32.");
  }
  return counts;
}

function comparableIdentity(report) {
  return {
    protocol: report.loaded_identity.protocol,
    host_kind: report.loaded_identity.host.host_kind,
    connector_source: report.loaded_identity.host.implementation.source_revision,
    connector_sha256: report.loaded_identity.host.implementation.artifact_sha256,
    connector_mvid: report.loaded_identity.host.implementation.module_version_id,
    game_version: report.loaded_identity.game.version,
    game_commit: report.loaded_identity.game.commit,
    game_main_assembly_hash: report.loaded_identity.game.main_assembly_hash,
    modset_fingerprint: report.loaded_identity.game.modset.fingerprint
  };
}

export function summarizeCapacityGroup(results) {
  if (!Array.isArray(results) || results.length === 0) {
    throw new Error("At least one completed worker result is required.");
  }
  const identities = results.map((result) => comparableIdentity(result.report));
  const identity = JSON.stringify(identities[0]);
  if (identities.some((entry) => JSON.stringify(entry) !== identity)) {
    throw new Error("Capacity workers did not load one exact comparable environment identity.");
  }
  const runtimeIds = results.map((result) => result.report.loaded_identity.host.runtime_instance_id);
  if (new Set(runtimeIds).size !== runtimeIds.length) {
    throw new Error("Capacity workers did not report distinct runtime instance IDs.");
  }
  const starts = results.map((result) => result.measurement.decision_window_started_ms);
  const ends = results.map((result) => result.measurement.decision_window_ended_ms);
  if ([...starts, ...ends].some((value) => !Number.isFinite(value))) {
    throw new Error("Every worker must enter and leave a measured decision window.");
  }
  const windowSeconds = (Math.max(...ends) - Math.min(...starts)) / 1000;
  const decisions = results.reduce(
    (sum, result) => sum + result.report.performance.delivered_normalized_semantic_decisions,
    0
  );
  const cpuSeconds = results.reduce((sum, result) => sum + result.report.performance.cpu_seconds, 0);
  const peakRssBytes = results.reduce((sum, result) => sum + result.report.performance.peak_rss_bytes, 0);
  const throughput = windowSeconds > 0 ? decisions / windowSeconds : null;
  const averageCores = windowSeconds > 0 ? cpuSeconds / windowSeconds : null;
  const integrityPass = results.every(
    (result) => result.report.verdict.integrity.verdict === "integrity_pass"
  );
  const sampleErrors = results.flatMap((result) => result.report.performance.sample_errors);
  const seeds = results.map((result) => result.report.episode_provenance?.actual_seed ?? null);
  const provenancePass = results.every(
    (result) => result.report.episode_provenance?.verdict === "provenance_pass"
  ) && new Set(seeds).size === 1 && seeds[0] != null;
  const shutdownContainmentVerdicts = results.map(
    (result) => result.report.shutdown_containment?.verdict ?? "not_evaluated"
  );
  const shutdownContainmentBounded = shutdownContainmentVerdicts.every(
    (verdict) => verdict === "clean_shutdown" || verdict === "bounded_containment_candidate"
  );
  return {
    status: integrityPass && provenancePass && sampleErrors.length === 0
      ? "measured"
      : "measurement_incomplete",
    worker_count: results.length,
    exact_identity: identities[0],
    runtime_instance_ids: runtimeIds,
    delivered_normalized_semantic_decisions: decisions,
    common_decision_window_seconds: windowSeconds,
    aggregate_normalized_semantic_decisions_per_second: throughput,
    total_cpu_seconds: cpuSeconds,
    average_cpu_cores: averageCores,
    summed_worker_peak_rss_bytes: peakRssBytes,
    normalized_semantic_decisions_per_second_per_core:
      throughput != null && averageCores > 0 ? throughput / averageCores : null,
    normalized_semantic_decisions_per_second_per_gib:
      throughput != null && peakRssBytes > 0 ? throughput / (peakRssBytes / GIB) : null,
    integrity_pass: integrityPass,
    episode_seed: seeds[0] ?? null,
    episode_provenance_pass: provenancePass,
    shutdown_containment_bounded: shutdownContainmentBounded,
    shutdown_containment_verdicts: shutdownContainmentVerdicts,
    sample_errors: sampleErrors,
    workers: results.map((result) => ({
      worker_id: result.report.worker.worker_id,
      profile: result.report.profile,
      endpoint: result.report.command.connector.endpoint,
      runtime_instance_id: result.report.loaded_identity.host.runtime_instance_id,
      decisions: result.report.performance.delivered_normalized_semantic_decisions,
      decision_window_seconds: result.report.performance.decision_window_seconds,
      decisions_per_second: result.report.performance.normalized_semantic_decisions_per_second,
      peak_rss_bytes: result.report.performance.peak_rss_bytes,
      report_file: result.reportFile,
      verdict: result.report.verdict,
      shutdown_containment: result.report.shutdown_containment ?? null,
      runtime_diagnostics: result.report.runtime_diagnostics ?? null
    }))
  };
}

export async function runCapacityBenchmark({
  installation,
  localRoot,
  evidenceRoot,
  templateId,
  workerCounts = [1, 2, 4],
  basePort = 15600,
  maxActions = 12,
  timeoutMs = 90_000,
  actionTimeoutMs = 20_000,
  runSeed = "H1CAPACITY01"
}) {
  const canonicalRunSeed = canonicalizeEpisodeSeed(runSeed);
  const counts = parseWorkerCounts(workerCounts.join(","));
  if (!Number.isSafeInteger(basePort) || basePort < 1024 || basePort + Math.max(...counts) > 65535) {
    throw new Error("The capacity port range is invalid.");
  }
  const existing = listGameProcesses();
  if (existing.length > 0) {
    throw new Error(`Capacity measurement requires a clean process baseline:\n${existing.join("\n")}`);
  }
  const evidenceDirectory = path.join(evidenceRoot, `capacity-${safeTimestamp()}`);
  mkdirSync(evidenceDirectory, { recursive: true });
  const reportFile = path.join(evidenceDirectory, "report.json");
  const report = {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    status: "running",
    route: "reference_shipped_multi_process",
    headless: readProjectIdentity(),
    system_identity: readSystemIdentity(),
    disk_identity: readDiskIdentity(installation),
    template_id: templateId,
    worker_counts: counts,
    base_port: basePort,
    max_actions_per_worker: maxActions,
    requested_seed: canonicalRunSeed,
    groups: [],
    non_claims: [
      "Capacity measurement is not semantic qualification or Training Ready evidence.",
      "Summed worker peak RSS is conservative and is not a synchronized system-wide peak.",
      "The deterministic bounded policy is a test consumer, not a trained policy."
    ]
  };
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  const exactGameIdentity = report.disk_identity;

  try {
    for (const workerCount of counts) {
      const workers = [];
      for (let index = 0; index < workerCount; index += 1) {
        const workerNumber = index + 1;
        const workerId = `capacity-${workerCount}-w${String(workerNumber).padStart(2, "0")}`;
        const profileId = `capacity-w${String(workerNumber).padStart(2, "0")}`;
        const endpoint = `http://127.0.0.1:${basePort + workerNumber - 1}`;
        const profile = instantiateProfileTemplate({
          localRoot,
          templateId,
          profileId,
          expectedGameIdentity: exactGameIdentity
        });
        workers.push({ workerId, profileId, endpoint, profile });
      }
      const groupStartedMs = performance.now();
      const settled = await Promise.allSettled(workers.map((worker) => runBoundedJourney({
        installation,
        localRoot,
        endpoint: worker.endpoint,
        timeoutMs,
        actionTimeoutMs,
        maxActions,
        tutorialPreference: "disable",
        evidenceRoot,
        isolatedProfileId: worker.profileId,
        experimentalBuildAcknowledged: true,
        allowConcurrentProcesses: true,
        evidenceLabel: worker.workerId,
        runSeed: canonicalRunSeed
      })));
      const failures = settled
        .filter((entry) => entry.status === "rejected")
        .map((entry) => entry.reason instanceof Error ? entry.reason.message : String(entry.reason));
      if (failures.length > 0) {
        report.groups.push({ status: "worker_failure", worker_count: workerCount, failures, workers });
        throw new Error(`Capacity worker failure: ${failures.join(" | ")}`);
      }
      const results = settled.map((entry) => entry.value);
      const summary = summarizeCapacityGroup(results);
      report.groups.push({
        ...summary,
        group_wall_seconds: (performance.now() - groupStartedMs) / 1000,
        template_instances: workers.map((worker) => worker.profile)
      });
      const leaked = listGameProcesses();
      if (leaked.length > 0) {
        throw new Error(`Capacity workers did not terminate cleanly:\n${leaked.join("\n")}`);
      }
      writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    }
    report.status = report.groups.every((group) => group.status === "measured")
      ? "measured"
      : "measurement_incomplete";
    report.completed_at = new Date().toISOString();
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    return { report, reportFile, evidenceDirectory };
  } catch (error) {
    report.status = "failed";
    report.completed_at = new Date().toISOString();
    report.error = error instanceof Error ? error.message : String(error);
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    throw new Error(`${report.error}; evidence: ${reportFile}`);
  }
}
