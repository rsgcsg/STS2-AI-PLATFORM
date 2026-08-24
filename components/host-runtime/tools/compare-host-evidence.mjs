#!/usr/bin/env node
import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import {
  compareCrossHostTrajectories,
  journeyEvidenceRun,
  loadJourneyEvidence
} from "../src/semantic-differential.mjs";
import { canonicalizeEpisodeSeed } from "../src/episode-provenance.mjs";

function option(args, name, fallback = null) {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
}

function required(args, name) {
  const value = option(args, name);
  if (value == null) throw new Error(`${name} is required.`);
  return path.resolve(value);
}

const args = process.argv.slice(2);
const referenceDirectory = required(args, "--reference");
const candidateDirectory = required(args, "--candidate");
const seed = canonicalizeEpisodeSeed(option(args, "--seed"));
if (seed == null) throw new Error("--seed is required.");
const maxActions = Number(option(args, "--max-actions", "21"));
if (!Number.isSafeInteger(maxActions) || maxActions < 1) {
  throw new Error("--max-actions must be a positive integer.");
}

const semanticTarget = {
  schema: "sts2.headless/semantic-target-1",
  target_id: option(args, "--target-id", "sts2-v0.111.0-player-visible-v1"),
  protocol_version: option(args, "--protocol", "1.0.0"),
  game_build: {
    version: option(args, "--game-version", "v0.111.0"),
    commit: option(args, "--game-commit", "41cef1ea"),
    main_assembly_hash: Number(option(args, "--main-assembly-hash", "1010476334"))
  },
  content_policy_id: option(args, "--content-policy", "vanilla_connector_only_v1"),
  information_policy_id: option(args, "--information-policy", "player_visible_v1")
};
const scenario = {
  schema: "sts2.headless/scenario-1",
  scenario_id: option(args, "--scenario-id", "bounded-reference-journey"),
  seed,
  policy_id: option(args, "--policy-id", "deterministic-probe-1"),
  max_actions: maxActions
};
const referenceEvidence = loadJourneyEvidence(referenceDirectory);
const candidateEvidence = loadJourneyEvidence(candidateDirectory);
const referenceRun = journeyEvidenceRun({
  evidence: referenceEvidence,
  semanticTarget,
  scenario,
  driverId: "reference-evidence"
});
const candidateRun = journeyEvidenceRun({
  evidence: candidateEvidence,
  semanticTarget,
  scenario,
  driverId: "candidate-evidence"
});
const comparison = compareCrossHostTrajectories({ referenceRun, candidateRun });
const report = {
  generated_at: new Date().toISOString(),
  reference: referenceEvidence.evidence,
  candidate: candidateEvidence.evidence,
  comparison
};
const output = path.resolve(option(
  args,
  "--output",
  path.join(candidateDirectory, "cross-host-comparison.json")
));
mkdirSync(path.dirname(output), { recursive: true });
writeFileSync(output, `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ output, verdict: comparison.verdict, errors: comparison.errors }, null, 2));
process.exitCode = comparison.verdict === "cross_host_semantic_match" ? 0 : 10;
