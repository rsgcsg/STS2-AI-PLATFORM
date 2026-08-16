import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { readDiskIdentity } from "./game-installation.mjs";
import { canonicalizeEpisodeSeed } from "./episode-provenance.mjs";
import { runBoundedJourney } from "./journey-probe.mjs";
import { instantiateProfileTemplate } from "./profile-template.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { listGameProcesses } from "./runtime-probe.mjs";
import { readSystemIdentity } from "./system-identity.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function readJsonl(file) {
  return readFileSync(file, "utf8").split(/\r?\n/u)
    .filter(Boolean)
    .map((line) => JSON.parse(line));
}

function comparableIdentity(report) {
  return {
    protocol: report?.loaded_identity?.protocol ?? null,
    host_kind: report?.loaded_identity?.host?.host_kind ?? null,
    connector_source: report?.loaded_identity?.host?.implementation?.source_revision ?? null,
    connector_sha256: report?.loaded_identity?.host?.implementation?.artifact_sha256 ?? null,
    connector_mvid: report?.loaded_identity?.host?.implementation?.module_version_id ?? null,
    game_version: report?.loaded_identity?.game?.version ?? null,
    game_commit: report?.loaded_identity?.game?.commit ?? null,
    game_main_assembly_hash: report?.loaded_identity?.game?.main_assembly_hash ?? null,
    modset_fingerprint: report?.loaded_identity?.game?.modset?.fingerprint ?? null
  };
}

function operandSemantics(value, key = null) {
  if (key != null && /(?:^|_)(?:entity|referent)_ids?$/u.test(key)) {
    return Array.isArray(value)
      ? value.map(() => "equivalent-entity")
      : value == null ? null : "equivalent-entity";
  }
  if (Array.isArray(value)) return value.map((item) => operandSemantics(item));
  if (value == null || typeof value !== "object") return value;
  return Object.fromEntries(Object.keys(value).sort()
    .map((childKey) => [childKey, operandSemantics(value[childKey], childKey)]));
}

function semanticEvent(event) {
  if (event.type === "action") {
    const referents = new Map((event.canonical_decision?.referents ?? []).map((referent) => {
      const { canonical_referent_id: id, ...semantics } = referent;
      return [id, operandSemantics(semantics)];
    }));
    const selected = event.canonical_selected_action;
    const selectedSemantics = selected == null ? null : {
      verb: selected.verb,
      subject: selected.subject_referent_id == null
        ? null
        : referents.get(selected.subject_referent_id) ?? "unbound-referent",
      arguments: selected.arguments.map((argument) => ({
        role: argument.role,
        referent: referents.get(argument.referent_id) ?? "unbound-referent"
      })),
      label: selected.label
    };
    return {
      type: "action",
      canonical_decision_digest: event.canonical_decision_digest,
      selected_action_semantics: selectedSemantics,
      delivery: event.delivery,
      reason_code: event.reason_code ?? null
    };
  }
  if (event.type === "read") {
    return {
      type: "read",
      kind: event.kind,
      canonical_read: event.canonical_read ?? null
    };
  }
  if (event.type === "stop" || event.type === "terminal") {
    return {
      type: event.type,
      reason: event.reason,
      canonical_decision: event.canonical_decision ?? null
    };
  }
  return null;
}

export function compareSemanticTrajectories({
  referenceReport,
  candidateReport,
  referenceEvents,
  candidateEvents,
  referenceProfile,
  candidateProfile
}) {
  const errors = [];
  const referenceIdentity = comparableIdentity(referenceReport);
  const candidateIdentity = comparableIdentity(candidateReport);
  if (Object.values(referenceIdentity).some((value) => value == null)
      || JSON.stringify(referenceIdentity) !== JSON.stringify(candidateIdentity)) {
    errors.push("environment_identity_not_comparable");
  }
  const referenceRuntime = referenceReport?.loaded_identity?.host?.runtime_instance_id ?? null;
  const candidateRuntime = candidateReport?.loaded_identity?.host?.runtime_instance_id ?? null;
  if (referenceRuntime == null || candidateRuntime == null || referenceRuntime === candidateRuntime) {
    errors.push("runtime_instances_not_independent");
  }
  if (referenceProfile?.template_payload_sha256 !== candidateProfile?.template_payload_sha256) {
    errors.push("profile_template_changed");
  }
  if (referenceProfile?.generation_id === candidateProfile?.generation_id) {
    errors.push("profile_generations_not_independent");
  }
  const referenceProvenance = referenceReport?.episode_provenance;
  const candidateProvenance = candidateReport?.episode_provenance;
  if (referenceProvenance?.verdict !== "provenance_pass"
      || candidateProvenance?.verdict !== "provenance_pass"
      || referenceProvenance?.actual_seed !== candidateProvenance?.actual_seed) {
    errors.push("episode_seed_not_comparable");
  }
  if (referenceReport?.verdict?.integrity?.verdict !== "integrity_pass"
      || candidateReport?.verdict?.integrity?.verdict !== "integrity_pass") {
    errors.push("trajectory_integrity_incomplete");
  }

  const expected = referenceEvents.map(semanticEvent).filter(Boolean);
  const actual = candidateEvents.map(semanticEvent).filter(Boolean);
  let firstDivergence = null;
  const length = Math.max(expected.length, actual.length);
  for (let index = 0; index < length; index += 1) {
    if (JSON.stringify(expected[index] ?? null) !== JSON.stringify(actual[index] ?? null)) {
      firstDivergence = {
        semantic_event_index: index,
        reference: expected[index] ?? null,
        candidate: actual[index] ?? null
      };
      break;
    }
  }
  if (firstDivergence != null) errors.push("semantic_trajectory_diverged");

  return {
    verdict: errors.length === 0 ? "semantic_match" : "semantic_mismatch",
    errors,
    exact_identity: referenceIdentity,
    seed: referenceProvenance?.actual_seed ?? null,
    reference_runtime_instance_id: referenceRuntime,
    candidate_runtime_instance_id: candidateRuntime,
    reference_semantic_event_count: expected.length,
    candidate_semantic_event_count: actual.length,
    first_divergence: firstDivergence
  };
}

export async function runReferenceRepeatability({
  installation,
  localRoot,
  evidenceRoot,
  templateId = "vanilla-clean",
  profilePrefix = "reference-differential",
  endpoint = "http://127.0.0.1:15710",
  runSeed,
  maxActions = 12,
  timeoutMs = 90_000,
  actionTimeoutMs = 20_000,
  experimentalBuildAcknowledged = false
}) {
  const canonicalSeed = canonicalizeEpisodeSeed(runSeed);
  if (canonicalSeed == null) throw new Error("Reference differential requires one explicit seed.");
  const existing = listGameProcesses();
  if (existing.length > 0) {
    throw new Error(`Reference differential requires a clean process baseline:\n${existing.join("\n")}`);
  }
  const diskIdentity = readDiskIdentity(installation);
  const evidenceDirectory = path.join(evidenceRoot, `reference-differential-${safeTimestamp()}`);
  const reportFile = path.join(evidenceDirectory, "report.json");
  mkdirSync(evidenceDirectory, { recursive: true });
  const profiles = ["reference", "candidate"].map((side) => instantiateProfileTemplate({
    localRoot,
    templateId,
    profileId: `${profilePrefix}-${side}`,
    expectedGameIdentity: diskIdentity
  }));
  const report = {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    status: "running",
    route: "same_artifact_reference_repeatability",
    headless: readProjectIdentity(),
    system_identity: readSystemIdentity(),
    disk_identity: diskIdentity,
    template_id: templateId,
    requested_seed: canonicalSeed,
    max_actions: maxActions,
    profiles,
    runs: [],
    non_claims: [
      "Same-artifact repeatability is a prerequisite, not cross-Host semantic qualification.",
      "A bounded deterministic trajectory is not full-game determinism or Training Ready evidence.",
      "Canonicalization is measurement-only and never creates gameplay authority."
    ]
  };
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);

  try {
    for (let index = 0; index < profiles.length; index += 1) {
      const side = index === 0 ? "reference" : "candidate";
      const result = await runBoundedJourney({
        installation,
        localRoot,
        endpoint,
        timeoutMs,
        actionTimeoutMs,
        maxActions,
        tutorialPreference: "disable",
        evidenceRoot,
        isolatedProfileId: profiles[index].profile_id,
        experimentalBuildAcknowledged,
        evidenceLabel: `differential-${side}`,
        runSeed: canonicalSeed
      });
      report.runs.push({ side, report_file: result.reportFile, events_file: result.eventsFile });
      writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    }
    const referenceReport = JSON.parse(readFileSync(report.runs[0].report_file, "utf8"));
    const candidateReport = JSON.parse(readFileSync(report.runs[1].report_file, "utf8"));
    report.comparison = compareSemanticTrajectories({
      referenceReport,
      candidateReport,
      referenceEvents: readJsonl(report.runs[0].events_file),
      candidateEvents: readJsonl(report.runs[1].events_file),
      referenceProfile: profiles[0],
      candidateProfile: profiles[1]
    });
    report.status = report.comparison.verdict;
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
