import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { ProcessResourceSampler } from "./process-resource-sampler.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { readSystemIdentity } from "./system-identity.mjs";
import {
  canonicalizeReadResponse,
  canonicalizeSelectedAction,
  canonicalizeSnapshot
} from "./semantic-decision.mjs";
import { startManagedPlayerEnvironmentSession } from "./managed-player-environment.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function requirePositiveInteger(value, name) {
  if (!Number.isSafeInteger(value) || value < 1) {
    throw new TypeError(`${name} must be a positive integer.`);
  }
}

function ordered(actions) {
  return [...actions].sort((left, right) => left.label.localeCompare(right.label)
    || left.bound_action_id.localeCompare(right.bound_action_id));
}

export function chooseManagedPlayerEnvironmentAction(snapshot) {
  if (snapshot?.status !== "interactive" || snapshot?.bound_actions?.status !== "complete") return null;
  const actions = snapshot.bound_actions.actions;
  const kind = snapshot.interaction.kind;
  if (kind === "combat_turn") {
    return ordered(actions.filter((action) => action.verb === "play"))[0]
      ?? actions.find((action) => action.verb === "end_turn")
      ?? null;
  }
  if (kind === "rest_site") {
    return actions.find((action) => /heal|rest/i.test(action.label)) ?? ordered(actions)[0] ?? null;
  }
  if (kind === "reward_collection") {
    return ordered(actions.filter((action) => /^Take /u.test(action.label)))[0]
      ?? actions.find((action) => action.label === "Proceed")
      ?? ordered(actions)[0]
      ?? null;
  }
  if (kind === "card_reward_selection") {
    return ordered(actions.filter((action) => action.verb === "select"))[0]
      ?? actions.find((action) => action.verb === "skip")
      ?? null;
  }
  if (kind === "treasure_relic_selection") {
    return ordered(actions.filter((action) => action.verb === "select"))[0]
      ?? actions.find((action) => action.verb === "skip")
      ?? null;
  }
  return ordered(actions)[0] ?? null;
}

function episodeSeed(baseSeed, episodeIndex, episodeCount) {
  return episodeCount === 1
    ? baseSeed
    : `${baseSeed}E${String(episodeIndex + 1).padStart(3, "0")}`;
}

export async function runManagedPlayerEnvironmentProbe({
  root,
  candidateDirectory,
  diskIdentity,
  seed,
  character = "Ironclad",
  maxActions = 200,
  episodeCount = 1,
  requestTimeoutMs = 10_000,
  evidenceRoot = null
}) {
  requirePositiveInteger(maxActions, "maxActions");
  requirePositiveInteger(episodeCount, "episodeCount");
  requirePositiveInteger(requestTimeoutMs, "requestTimeoutMs");
  if (typeof seed !== "string" || seed.length === 0) throw new TypeError("seed must be a non-empty string.");

  const processStarted = performance.now();
  const started = await startManagedPlayerEnvironmentSession({
    root,
    candidateDirectory,
    diskIdentity,
    character,
    requestTimeoutMs
  });
  const sampler = new ProcessResourceSampler(started.runtime.process.pid, { intervalMs: 250 });
  await sampler.start();
  const events = [];
  let snapshot = null;
  let runIdentity = null;
  let stopReason = null;
  let failure = null;
  let decisionStarted = null;
  let decisionEnded = null;
  const episodes = [];
  try {
    for (let episodeIndex = 0; episodeIndex < episodeCount; episodeIndex += 1) {
      const requestedSeed = episodeSeed(seed, episodeIndex, episodeCount);
      const eventStart = events.length;
      const mountStarted = performance.now();
      snapshot = await started.session.mount({
        seed: requestedSeed,
        reset: episodeIndex > 0,
        timeoutMs: requestTimeoutMs
      });
      runIdentity = await started.runtime.process.request({ cmd: "run_identity" }, requestTimeoutMs);
      if (runIdentity?.type !== "run_identity"
          || runIdentity.active !== true
          || runIdentity.seed !== requestedSeed) {
        throw new Error(`Managed Player Environment did not prove requested game seed ${requestedSeed}.`);
      }
      const mountedAt = performance.now();
      decisionStarted ??= mountedAt;
      events.push({
        type: "episode_provenance",
        episode_index: episodeIndex,
        requested_seed: requestedSeed,
        actual_seed: runIdentity.seed,
        verdict: "provenance_pass",
        mount_wall_ms: mountedAt - mountStarted
      });

      let episodeStopReason = null;
      for (let actionIndex = 0; actionIndex < maxActions; actionIndex += 1) {
        for (const descriptor of snapshot.reads) {
          try {
            const value = started.session.read({
              readId: descriptor.read_id,
              expectedSnapshotId: snapshot.snapshot_id
            });
            events.push({
              type: "read",
              episode_index: episodeIndex,
              action_index: actionIndex,
              canonical_read: canonicalizeReadResponse(value)
            });
          } catch (error) {
            episodeStopReason = `read_failure:${descriptor.kind}:${error instanceof Error ? error.message : String(error)}`;
            break;
          }
        }
        if (episodeStopReason != null) break;
        const selected = chooseManagedPlayerEnvironmentAction(snapshot);
        if (selected == null) {
          episodeStopReason = snapshot.interaction.kind === "game_over"
            ? "game_over"
            : `visible_unsupported:${snapshot.interaction.kind}:${snapshot.completeness.missing.join(",")}`;
          break;
        }
        const before = performance.now();
        const value = await started.session.submit({
          requestId: `managed-pe-${String(episodeIndex + 1).padStart(4, "0")}-${String(actionIndex + 1).padStart(8, "0")}`,
          expectedSnapshotId: snapshot.snapshot_id,
          boundActionId: selected.bound_action_id,
          timeoutMs: requestTimeoutMs
        });
        const after = performance.now();
        events.push({
          type: "action",
          episode_index: episodeIndex,
          action_index: actionIndex,
          canonical_decision: canonicalizeSnapshot(snapshot),
          canonical_action: canonicalizeSelectedAction(snapshot, selected.bound_action_id),
          delivery: value.delivery,
          reason_code: value.reason_code ?? null,
          detail: value.detail ?? null,
          retry: value.retry ?? null,
          delivery_wall_ms: after - before,
          successor_kind: value.successor?.interaction?.kind ?? null,
          successor_status: value.successor?.status ?? null
        });
        if (value.delivery === "unknown") {
          episodeStopReason = `unknown:${value.reason_code ?? "unspecified"}`;
          break;
        }
        if (value.delivery !== "delivered" || value.successor == null) {
          episodeStopReason = `not_delivered:${value.reason_code ?? "unspecified"}`;
          break;
        }
        snapshot = value.successor;
      }
      episodeStopReason ??= "action_limit";
      const episodeEvents = events.slice(eventStart);
      const episodeActions = episodeEvents.filter((event) => event.type === "action");
      episodes.push({
        episode_index: episodeIndex,
        requested_seed: requestedSeed,
        game_reported_seed: runIdentity.seed,
        seed_provenance: "game_reported_match",
        terminal: episodeStopReason,
        canonical_actions_attempted: episodeActions.length,
        canonical_actions_delivered: episodeActions.filter((event) => event.delivery === "delivered").length,
        canonical_reads_completed: episodeEvents.filter((event) => event.type === "read").length
      });
      if (episodeStopReason !== "game_over" && episodeStopReason !== "action_limit") {
        stopReason = episodeStopReason;
        break;
      }
    }
    decisionEnded = performance.now();
    if (stopReason == null) {
      stopReason = episodeCount === 1 ? episodes[0].terminal : "episodes_complete";
    }
  } catch (error) {
    failure = error instanceof Error ? error.message : String(error);
    stopReason ??= `exception:${failure}`;
    decisionEnded = performance.now();
  }
  const resources = await sampler.stop();
  const exit = await started.session.close();
  const actions = events.filter((event) => event.type === "action");
  const reads = events.filter((event) => event.type === "read");
  const delivered = actions.filter((event) => event.delivery === "delivered").length;
  const windowSeconds = decisionStarted == null || decisionEnded == null
    ? null
    : (decisionEnded - decisionStarted) / 1000;
  const peakRssBytes = resources.samples.length === 0
    ? null
    : Math.max(...resources.samples.map((sample) => sample.rss_bytes));
  const report = {
    schema: "sts2.headless/managed-player-environment-probe-2",
    generated_at: new Date().toISOString(),
    status: failure != null
      ? "candidate_failure"
      : stopReason === "action_limit" || stopReason === "game_over" || stopReason === "episodes_complete"
        ? "bounded_partial_player_environment_measured"
        : "fail_closed",
    headless: readProjectIdentity(root),
    system_identity: readSystemIdentity(),
    candidate: {
      manifest: started.runtime.manifest,
      build: started.runtime.build,
      runtime_identity: started.runtime.runtimeIdentity,
      adapter_runtime_instance_id: started.runtime.adapterRuntimeInstanceId,
      environment_fingerprint: started.environmentFingerprint
    },
    game_identity: {
      version: diskIdentity.release.version,
      commit: diskIdentity.release.commit,
      runtime_main_assembly_hash: diskIdentity.runtime_main_assembly_hash,
      original_sts2_sha256: diskIdentity.sts2_assembly.sha256
    },
    episode: {
      requested_seed: seed,
      game_reported_seed: episodeCount === 1 ? runIdentity?.seed ?? null : null,
      seed_provenance: episodes.length === episodeCount
        && episodes.every((episode) => episode.seed_provenance === "game_reported_match")
        ? "game_reported_match"
        : "incomplete",
      character,
      episodes_requested: episodeCount,
      episodes_completed: episodes.length,
      terminal: stopReason,
      failure,
      canonical_actions_attempted: actions.length,
      canonical_actions_delivered: delivered,
      canonical_reads_completed: reads.length,
      episodes,
      final_snapshot: snapshot == null ? null : {
        status: snapshot.status,
        interaction_kind: snapshot.interaction.kind,
        completeness: snapshot.completeness
      }
    },
    performance: {
      unit: "canonical_player_environment_decision_partial_unqualified",
      process_startup_seconds: decisionStarted == null ? null : (decisionStarted - processStarted) / 1000,
      decision_window_started_ms: decisionStarted,
      decision_window_ended_ms: decisionEnded,
      decision_window_seconds: windowSeconds,
      reset_inclusive_decision_window_seconds: windowSeconds,
      delivered_decisions_per_second: windowSeconds > 0 ? delivered / windowSeconds : null,
      peak_rss_bytes: peakRssBytes,
      resource_samples: resources.samples,
      resource_sample_errors: resources.errors
    },
    process: {
      pid: started.runtime.process.pid,
      exit,
      diagnostics: started.runtime.process.diagnostics
    },
    events,
    verdict: {
      hard_shell: actions.some((event) => event.delivery === "unknown")
        ? "unknown_delivery_fail_closed"
        : failure == null ? "bounded_integrity_pass" : "integrity_incomplete",
      semantic_conformance: "not_evaluated",
      h1_admission: "not_evaluated"
    },
    non_claims: [
      "A strict SDK-valid partial projection is not Player Environment semantic conformance.",
      "The managed decision source still contains manual projection and semantic shims.",
      "This bounded deterministic policy is not gameplay, training, or transfer evidence.",
      "Raw managed speed is not H1.0 speed until cross-Host semantic qualification succeeds."
    ]
  };
  let reportFile = null;
  if (evidenceRoot != null) {
    const directory = path.join(evidenceRoot, `managed-player-environment-${safeTimestamp()}`);
    mkdirSync(directory, { recursive: true });
    reportFile = path.join(directory, "report.json");
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  }
  return { report, reportFile };
}

function capacityIdentity(report) {
  return {
    candidate_id: report.candidate.manifest.candidate_id,
    source_patch_sha256: report.candidate.build.source_patch_sha256,
    host_artifact_sha256: report.candidate.build.artifact_sha256,
    runtime_sts2_sha256: report.candidate.build.runtime_sts2_sha256,
    environment_fingerprint: report.candidate.environment_fingerprint,
    game_identity: report.game_identity
  };
}

export function summarizeManagedPlayerEnvironmentCapacityGroup(results, groupWallSeconds) {
  if (!Array.isArray(results) || results.length === 0) {
    throw new TypeError("At least one managed Player Environment worker result is required.");
  }
  if (!Number.isFinite(groupWallSeconds) || groupWallSeconds <= 0) {
    throw new TypeError("groupWallSeconds must be positive.");
  }
  const reports = results.map((result) => result.report);
  const identities = reports.map(capacityIdentity);
  const expectedIdentity = JSON.stringify(identities[0]);
  if (identities.some((identity) => JSON.stringify(identity) !== expectedIdentity)) {
    throw new Error("Canonical capacity workers did not use one exact comparable artifact and game identity.");
  }
  const runtimeIds = reports.map((report) => report.candidate.adapter_runtime_instance_id);
  if (new Set(runtimeIds).size !== runtimeIds.length) {
    throw new Error("Canonical capacity workers did not report distinct runtime instance IDs.");
  }
  const starts = reports.map((report) => report.performance.decision_window_started_ms);
  const ends = reports.map((report) => report.performance.decision_window_ended_ms);
  const comparableWindow = [...starts, ...ends].every(Number.isFinite);
  const commonWindowSeconds = comparableWindow
    ? (Math.max(...ends) - Math.min(...starts)) / 1000
    : null;
  const decisions = reports.reduce(
    (sum, report) => sum + report.episode.canonical_actions_delivered,
    0
  );
  const reads = reports.reduce(
    (sum, report) => sum + report.episode.canonical_reads_completed,
    0
  );
  const peakRssBytes = reports.reduce(
    (sum, report) => sum + (report.performance.peak_rss_bytes ?? 0),
    0
  );
  const workersMeasured = reports.every((report) =>
    report.status === "bounded_partial_player_environment_measured"
    && report.episode.failure == null
    && report.episode.seed_provenance === "game_reported_match"
    && report.episode.episodes_completed === report.episode.episodes_requested
    && report.episode.canonical_actions_attempted === report.episode.canonical_actions_delivered
    && report.episode.episodes.every((episode) =>
      episode.terminal === "game_over" || episode.terminal === "action_limit"));
  return {
    status: workersMeasured && commonWindowSeconds > 0
      ? "measured_canonical_partial_unqualified"
      : "measurement_incomplete",
    worker_count: reports.length,
    exact_identity: identities[0],
    runtime_instance_ids: runtimeIds,
    group_wall_seconds: groupWallSeconds,
    common_reset_inclusive_decision_window_seconds: commonWindowSeconds,
    delivered_canonical_decisions: decisions,
    completed_canonical_reads: reads,
    process_lifecycle_inclusive_canonical_decisions_per_second: decisions / groupWallSeconds,
    aggregate_reset_inclusive_canonical_decisions_per_second:
      commonWindowSeconds > 0 ? decisions / commonWindowSeconds : null,
    summed_worker_peak_rss_bytes: peakRssBytes,
    workers: reports.map((report) => ({
      status: report.status,
      runtime_instance_id: report.candidate.adapter_runtime_instance_id,
      episode: report.episode,
      performance: {
        process_startup_seconds: report.performance.process_startup_seconds,
        reset_inclusive_decision_window_seconds:
          report.performance.reset_inclusive_decision_window_seconds,
        delivered_decisions_per_second: report.performance.delivered_decisions_per_second,
        peak_rss_bytes: report.performance.peak_rss_bytes,
        resource_sample_errors: report.performance.resource_sample_errors
      },
      process_exit: report.process.exit,
      diagnostic_count: report.process.diagnostics.length,
      last_action: (() => {
        const action = report.events.findLast((event) => event.type === "action");
        return action == null ? null : {
          episode_index: action.episode_index,
          action_index: action.action_index,
          canonical_action: action.canonical_action,
          delivery: action.delivery,
          reason_code: action.reason_code,
          detail: action.detail
        };
      })()
    }))
  };
}

export async function runManagedPlayerEnvironmentCapacity({
  root,
  candidateDirectory,
  diskIdentity,
  workerCounts = [1, 2, 4],
  maxActions = 300,
  episodesPerWorker = 3,
  seedPrefix = "H1PECAPACITY",
  character = "Ironclad",
  requestTimeoutMs = 10_000,
  evidenceRoot
}) {
  requirePositiveInteger(maxActions, "maxActions");
  requirePositiveInteger(episodesPerWorker, "episodesPerWorker");
  requirePositiveInteger(requestTimeoutMs, "requestTimeoutMs");
  if (!Array.isArray(workerCounts) || workerCounts.length === 0) {
    throw new TypeError("workerCounts must contain at least one positive integer.");
  }
  workerCounts.forEach((workerCount) => requirePositiveInteger(workerCount, "workerCount"));
  if (typeof seedPrefix !== "string" || seedPrefix.length === 0) {
    throw new TypeError("seedPrefix must be a non-empty string.");
  }
  if (typeof evidenceRoot !== "string" || evidenceRoot.length === 0) {
    throw new TypeError("evidenceRoot must be a non-empty path.");
  }

  const outputDirectory = path.join(evidenceRoot, `managed-player-environment-capacity-${safeTimestamp()}`);
  mkdirSync(outputDirectory, { recursive: true });
  const groups = [];
  for (const workerCount of workerCounts) {
    const groupStarted = performance.now();
    const workers = await Promise.all(Array.from({ length: workerCount }, () =>
      runManagedPlayerEnvironmentProbe({
        root,
        candidateDirectory,
        diskIdentity,
        seed: seedPrefix,
        character,
        maxActions,
        episodeCount: episodesPerWorker,
        requestTimeoutMs,
        evidenceRoot: null
      })));
    groups.push(summarizeManagedPlayerEnvironmentCapacityGroup(
      workers,
      (performance.now() - groupStarted) / 1000
    ));
  }
  const report = {
    schema: "sts2.headless/managed-player-environment-capacity-1",
    generated_at: new Date().toISOString(),
    status: groups.every((group) => group.status === "measured_canonical_partial_unqualified")
      ? "measured_canonical_partial_unqualified"
      : "measurement_incomplete",
    headless: readProjectIdentity(root),
    system_identity: readSystemIdentity(),
    worker_counts: workerCounts,
    episodes_per_worker: episodesPerWorker,
    max_actions_per_episode: maxActions,
    seed_prefix: seedPrefix,
    character,
    groups,
    non_claims: [
      "Canonical adapter throughput is not cross-Host semantic conformance.",
      "A short reset-and-capacity ladder is not long-run reset or million-step reliability evidence.",
      "The managed projection remains partial and the candidate remains experimental_unqualified.",
      "This deterministic policy is not gameplay quality, training, or transfer evidence."
    ]
  };
  const reportFile = path.join(outputDirectory, "report.json");
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  return { report, reportFile, outputDirectory };
}
