#!/usr/bin/env node
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parseWorkerCounts } from "../src/capacity-benchmark.mjs";
import { discoverGameDirectory, readDiskIdentity, resolveInstallation } from "../src/game-installation.mjs";
import {
  inspectManagedCandidateBuild,
  loadManagedCandidateManifest,
  prepareManagedCandidate,
  runManagedCandidateCapacity,
  runManagedCandidateProbe
} from "../src/managed-candidate.mjs";
import {
  runManagedPlayerEnvironmentCapacity,
  runManagedPlayerEnvironmentProbe
} from "../src/managed-player-environment-probe.mjs";
import { runManagedNativeBindingGates } from "../src/managed-native-binding-gates.mjs";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const LOCAL = path.join(ROOT, ".local");

function option(args, name, fallback = null) {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
}

function commaList(value) {
  return value == null ? [] : value.split(",").map((item) => item.trim()).filter(Boolean);
}

function diskIdentity() {
  const gameDirectory = discoverGameDirectory();
  if (!gameDirectory) throw new Error("Could not locate STS2; set STS2_GAME_DIR.");
  return readDiskIdentity(resolveInstallation(gameDirectory));
}

async function main() {
  const [command = "help", ...args] = process.argv.slice(2);
  if (command === "prepare") {
    const result = await prepareManagedCandidate({
      root: ROOT,
      localRoot: LOCAL,
      diskIdentity: diskIdentity(),
      candidateDirectory: option(args, "--candidate")
    });
    console.log(JSON.stringify({
      status: result.report.status,
      candidate_directory: result.candidateDirectory,
      report_file: result.reportFile,
      build: result.report.build
    }, null, 2));
    return;
  }
  const candidateDirectory = option(args, "--candidate");
  if (["audit", "probe", "pe-probe", "pe-capacity", "native-gates", "capacity"].includes(command)
      && !candidateDirectory) {
    throw new Error(`${command} requires --candidate <prepared-directory>.`);
  }
  if (command === "audit") {
    const { manifest } = loadManagedCandidateManifest(ROOT);
    console.log(JSON.stringify(await inspectManagedCandidateBuild({
      root: ROOT,
      candidateDirectory,
      manifest
    }), null, 2));
    return;
  }
  if (command === "probe") {
    const result = await runManagedCandidateProbe({
      root: ROOT,
      candidateDirectory,
      diskIdentity: diskIdentity(),
      seed: option(args, "--seed", "H1MANAGEDPROBE01"),
      character: option(args, "--character", "Ironclad"),
      maxActions: Number(option(args, "--max-actions", "200")),
      episodeCount: Number(option(args, "--episodes", "1")),
      resetAtDecisions: commaList(option(args, "--reset-at")),
      requestTimeoutMs: Number(option(args, "--timeout-ms", "10000")),
      evidenceRoot: path.join(LOCAL, "evidence")
    });
    console.log(JSON.stringify({
      status: result.report.status,
      report_file: result.reportFile,
      episode: result.report.episode,
      performance: result.report.performance
    }, null, 2));
    process.exitCode = ["episodes_complete", "terminal_reached"].includes(result.report.status) ? 0 : 2;
    return;
  }
  if (command === "pe-probe") {
    const result = await runManagedPlayerEnvironmentProbe({
      root: ROOT,
      candidateDirectory,
      diskIdentity: diskIdentity(),
      seed: option(args, "--seed", "H1MANAGEDPE01"),
      character: option(args, "--character", "Ironclad"),
      maxActions: Number(option(args, "--max-actions", "200")),
      episodeCount: Number(option(args, "--episodes", "1")),
      requestTimeoutMs: Number(option(args, "--timeout-ms", "10000")),
      evidenceRoot: path.join(LOCAL, "evidence")
    });
    console.log(JSON.stringify({
      status: result.report.status,
      report_file: result.reportFile,
      episode: result.report.episode,
      performance: result.report.performance
    }, null, 2));
    process.exitCode = result.report.status === "candidate_failure" ? 3 : 0;
    return;
  }
  if (command === "pe-capacity") {
    const result = await runManagedPlayerEnvironmentCapacity({
      root: ROOT,
      candidateDirectory,
      diskIdentity: diskIdentity(),
      workerCounts: parseWorkerCounts(option(args, "--workers", "1,2,4")),
      maxActions: Number(option(args, "--max-actions", "300")),
      episodesPerWorker: Number(option(args, "--episodes", "3")),
      seedPrefix: option(args, "--seed-prefix", "H1PECAPACITY"),
      character: option(args, "--character", "Ironclad"),
      requestTimeoutMs: Number(option(args, "--timeout-ms", "10000")),
      evidenceRoot: path.join(LOCAL, "evidence")
    });
    console.log(JSON.stringify({
      status: result.report.status,
      report_file: result.reportFile,
      groups: result.report.groups.map((group) => ({
        worker_count: group.worker_count,
        status: group.status,
        canonical_decisions_per_second:
          group.aggregate_reset_inclusive_canonical_decisions_per_second,
        summed_worker_peak_rss_bytes: group.summed_worker_peak_rss_bytes
      }))
    }, null, 2));
    process.exitCode = result.report.status === "measured_canonical_partial_unqualified" ? 0 : 3;
    return;
  }
  if (command === "native-gates") {
    const result = await runManagedNativeBindingGates({
      root: ROOT,
      candidateDirectory,
      diskIdentity: diskIdentity(),
      seed: option(args, "--seed", "H1NATIVEBINDING01"),
      requestTimeoutMs: Number(option(args, "--timeout-ms", "10000")),
      evidenceRoot: path.join(LOCAL, "evidence")
    });
    console.log(JSON.stringify({
      status: result.report.status,
      report_file: result.reportFile,
      gates: result.report.scenario.gates,
      failure: result.report.scenario.failure
    }, null, 2));
    process.exitCode = result.report.status === "pass" ? 0 : 3;
    return;
  }
  if (command === "capacity") {
    const result = await runManagedCandidateCapacity({
      root: ROOT,
      candidateDirectory,
      diskIdentity: diskIdentity(),
      workerCounts: parseWorkerCounts(option(args, "--workers", "1,2,4")),
      maxActions: Number(option(args, "--max-actions", "200")),
      episodesPerWorker: Number(option(args, "--episodes", "5")),
      seedPrefix: option(args, "--seed-prefix", "H1MANAGED"),
      evidenceRoot: path.join(LOCAL, "evidence")
    });
    console.log(JSON.stringify({ status: result.report.status, report_file: result.reportFile }, null, 2));
    process.exitCode = result.report.status === "measured_raw_candidate" ? 0 : 3;
    return;
  }
  console.log(`Managed exact candidate (experimental, unqualified)

Commands:
  prepare [--candidate DIR]
  audit --candidate DIR
  probe --candidate DIR [--seed SEED] [--episodes N] [--max-actions N] [--reset-at card_select,card_reward]
  pe-probe --candidate DIR [--seed SEED] [--episodes N] [--max-actions N]
  pe-capacity --candidate DIR [--workers 1,2,4] [--episodes N] [--max-actions N]
  native-gates --candidate DIR [--seed SEED]
  capacity --candidate DIR [--workers 1,2,4] [--episodes N] [--max-actions N]

The raw candidate protocol is not the canonical Player Environment. pe-probe
measures a strict but explicitly partial adapter; neither route is cross-host
qualified until the differential corpus passes.`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});
