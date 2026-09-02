#!/usr/bin/env node

import { createHash } from "node:crypto";
import { createReadStream, readFileSync } from "node:fs";
import { access, readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import readline from "node:readline";
import { fileURLToPath } from "node:url";

const CONTRACT = Object.freeze({
  state: "authoritative fair-player state consumed by the executed Human action",
  action_space: "typed same-boundary native semantic catalog when present; otherwise the complete public catalog for direct UI decisions",
  action: "exact Human/native action present exactly once in the authoritative execution action space",
  successor: "next authoritative state after the action and its causally owned continuation",
  non_claims: [
    "human_observation_is_not_semantic_state",
    "acceptance_is_not_execution",
    "interactive_status_alone_does_not_prove_successor",
    "legacy_v2_admission_does_not_prove_canonical_transition",
    "public_delivery_catalog_is_not_native_semantic_legality"
  ]
});

async function* jsonLines(file) {
  const input = createReadStream(file, { encoding: "utf8" });
  const lines = readline.createInterface({ input, crlfDelay: Infinity });
  let number = 0;
  for await (const line of lines) {
    number++;
    if (!line.trim()) continue;
    yield { number, value: JSON.parse(line) };
  }
}

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) =>
      `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function increment(target, key) {
  target[key] = (target[key] ?? 0) + 1;
}

function sameRef(left, right) {
  return Boolean(left && right
    && left.content_sha256 === right.content_sha256
    && left.snapshot_id === right.snapshot_id);
}

function normalizedArguments(action) {
  if (!action) return null;
  if (Array.isArray(action.arguments)) {
    return Object.fromEntries(action.arguments.map((argument) =>
      [argument.role, argument.referent_id]));
  }
  if (action.arguments && typeof action.arguments === "object")
    return action.arguments;
  return {};
}

function samePublicAction(catalogAction, selectedAction) {
  if (!catalogAction || !selectedAction) return false;
  return catalogAction.bound_action_id === selectedAction.bound_action_id
    && catalogAction.verb === selectedAction.verb
    && (catalogAction.subject_referent_id ?? null)
      === (selectedAction.subject_referent_id ?? null)
    && canonical(normalizedArguments(catalogAction))
      === canonical(normalizedArguments(selectedAction));
}

function frameStatus(frame, action) {
  const snapshot = frame?.snapshot;
  const catalog = snapshot?.bound_actions;
  const actions = Array.isArray(catalog?.actions) ? catalog.actions : [];
  const matches = action ? actions.filter((candidate) => samePublicAction(candidate, action)) : [];
  const complete = Boolean(
    frame
    && snapshot?.completeness?.status === "complete"
    && catalog?.status === "complete"
    && frame.catalog_count === actions.length
    && catalog.materialized_count === actions.length);
  return {
    complete,
    catalog_status: catalog?.status ?? "missing",
    catalog_count: actions.length,
    action_match_count: matches.length
  };
}

function semanticActionSpaceStatus(value, action, actionWitnessId) {
  const actions = Array.isArray(value?.actions) ? value.actions : [];
  const observedKey = value?.observed_action_key;
  const selected = actions.filter((candidate) => candidate?.key === observedKey);
  const current = value?.schema === "sts2.human-annotator/execution-semantic-action-space-2"
    && value?.schema_version === 2;
  const legacy = value?.schema === "sts2.human-annotator/execution-semantic-action-space-1"
    && value?.schema_version === 1;
  const matches = current
    ? selected.filter(() => value?.human_bound_action_id === action?.bound_action_id)
    : selected.filter((candidate) =>
      candidate?.verb === action?.verb
      && (candidate?.subject_referent_id ?? null) === (action?.subject_referent_id ?? null)
      && canonical(candidate?.arguments ?? {}) === canonical(normalizedArguments(action)));
  const complete = Boolean(
    value
    && (current || legacy)
    && value.action_witness_id === actionWitnessId
    && ["before_execution", "before_native_action_admission"].includes(value.phase)
    && value.status === "captured"
    && value.scope !== "unavailable"
    && value.semantic_state
    && typeof value.semantic_state_digest === "string"
    && value.semantic_state_digest.length > 0
    && typeof value.semantic_catalog_digest === "string"
    && value.semantic_catalog_digest.length > 0
    && value.observed_membership === "exact_once"
    && value.observed_match_count === 1
    && selected.length === 1
    && Array.isArray(value.native_evidence)
    && value.native_evidence.length > 0
    && (!current
      || typeof value.human_bound_action_id === "string"
        && value.human_bound_action_id.length > 0));
  return {
    complete,
    catalog_status: complete ? "complete" : "missing_or_incomplete",
    catalog_count: actions.length,
    action_match_count: matches.length
  };
}

async function discoverRunFiles(root) {
  return (await readdir(root))
    .filter((name) => /^run-.*\.jsonl$/.test(name))
    .sort()
    .map((name) => path.join(root, name));
}

function disposition(events) {
  const terminal = events.filter((event) =>
    event.kind === "transition_proved"
    || event.kind === "transition_unknown"
    || event.kind === "action_aborted_before_commit"
    || event.kind === "action_cancelled_before_start"
    || event.kind === "action_cancelled_after_start");
  return terminal;
}

function actionFromSources(accepted, ledger, legacy) {
  return accepted?.action?.bound_action
    ?? ledger?.bound_action
    ?? legacy?.action
    ?? null;
}

function sourceForAction(accepted, ledger, legacy) {
  if (accepted?.action?.bound_action) return "semantic_trace";
  if (ledger?.bound_action) return "native_ledger";
  if (legacy?.action) return "legacy_v2_record";
  return "missing";
}

function causalSuccessorStatus({ proved, events, actionsById, loadFrame, loadActionSpace }) {
  if (!proved) return { valid: false, reason: "successor_not_proved" };
  const successor = loadFrame(proved.successor_ref);
  if (!successor)
    return { valid: false, reason: "successor_frame_missing" };
  const successorStatus = frameStatus(successor, null);
  if (successor?.snapshot?.completeness?.status !== "complete")
    return { valid: false, reason: "successor_state_incomplete" };

  if (proved.proof_status === "proved_execution_handoff_boundary") {
    const next = actionsById.get(proved.related_action_witness_id);
    const nextBoundary = next?.events.find((event) =>
      event.kind === "boundary_observed"
      && event.boundary?.immediately_consumed_by_action_witness_id === next.id);
    if (!nextBoundary || !sameRef(proved.successor_ref, nextBoundary.execution_pre_ref))
      return { valid: false, reason: "handoff_does_not_equal_next_execution_pre" };
    const nextAction = next.action;
    const nextActionSpace = loadActionSpace(next?.events.find((event) =>
      event.execution_semantic_action_space_ref)?.execution_semantic_action_space_ref);
    const nextMembership = nextActionSpace
      ? semanticActionSpaceStatus(nextActionSpace, nextAction, next.id)
      : frameStatus(successor, nextAction);
    if (!nextMembership.complete || nextMembership.action_match_count !== 1)
      return { valid: false, reason: "handoff_next_action_not_in_same_state_catalog" };
    return { valid: true, reason: "execution_handoff_exact" };
  }

  if (proved.proof_status === "proved_player_choice_boundary") {
    const paused = events.some((event) => event.kind === "action_paused_for_player_choice");
    if (!paused)
      return { valid: false, reason: "player_choice_boundary_without_native_pause" };
    return successorStatus.complete
      ? { valid: true, reason: "native_player_choice_pause" }
      : { valid: false, reason: "successor_state_action_space_incomplete" };
  }

  if (proved.proof_status === "proved_native_post_commit_boundary") {
    if (proved.boundary?.witness_kind !== "after_native_ui_commit")
      return { valid: false, reason: "native_post_commit_witness_missing" };
    return successorStatus.complete
      ? { valid: true, reason: "native_post_commit_exact" }
      : { valid: false, reason: "successor_state_action_space_incomplete" };
  }

  if (proved.proof_status === "proved_native_commit_then_owner_boundary") {
    if (proved.boundary?.witness_kind !== "native_decision_owner_ready")
      return { valid: false, reason: "native_owner_boundary_witness_missing" };
    return successorStatus.complete
      ? { valid: true, reason: "native_commit_then_owner_boundary_exact" }
      : { valid: false, reason: "successor_state_action_space_incomplete" };
  }

  if (proved.proof_status === "proved_native_commit_then_execution_handoff") {
    const next = actionsById.get(proved.related_action_witness_id);
    const nextBoundary = next?.events.find((event) =>
      event.kind === "boundary_observed"
      && event.boundary?.immediately_consumed_by_action_witness_id === next.id);
    if (!nextBoundary || !sameRef(proved.successor_ref, nextBoundary.execution_pre_ref))
      return { valid: false, reason: "native_commit_handoff_does_not_equal_next_execution_pre" };
    const nextActionSpace = loadActionSpace(next?.events.find((event) =>
      event.execution_semantic_action_space_ref)?.execution_semantic_action_space_ref);
    const nextMembership = nextActionSpace
      ? semanticActionSpaceStatus(nextActionSpace, next.action, next.id)
      : frameStatus(successor, next.action);
    if (!nextMembership.complete || nextMembership.action_match_count !== 1)
      return { valid: false, reason: "native_commit_handoff_action_not_in_same_state_catalog" };
    return { valid: true, reason: "native_commit_then_execution_handoff_exact" };
  }

  return { valid: false, reason: "interactive_polling_is_not_causal_successor_proof" };
}

function mechanicallyLegacyUsable(record) {
  const action = record?.action;
  const pre = record?.pre;
  const successor = record?.successor;
  const preStatus = frameStatus(pre, action);
  const successorStatus = frameStatus(
    successor?.snapshot ? { snapshot: successor.snapshot, catalog_count:
      successor.snapshot.bound_actions?.materialized_count } : null,
    null);
  return record?.eligibility?.status === "admitted"
    && preStatus.complete
    && preStatus.action_match_count === 1
    && successor?.status === "interactive"
    && successor?.snapshot_id !== pre?.snapshot_id
    && successorStatus.complete;
}

export async function calibrate(recordingDirectory) {
  const root = path.resolve(recordingDirectory);
  const tracePath = path.join(root, "semantic-boundary-trace.jsonl");
  const ledgerPath = path.join(root, "native-action-ledger.jsonl");
  const manifest = JSON.parse(await readFile(path.join(root, "recording-manifest.json"), "utf8"));

  const actionsById = new Map();
  let traceSha;
  {
    const traceBytes = await readFile(tracePath);
    traceSha = sha256(traceBytes);
  }
  for await (const { value } of jsonLines(tracePath)) {
    const id = value.action?.action_witness_id;
    if (!id) throw new Error("Semantic event is missing action_witness_id");
    let state = actionsById.get(id);
    if (!state) {
      state = { id, events: [], action: null };
      actionsById.set(id, state);
    }
    state.events.push(value);
    if (value.action?.bound_action) state.action = value.action.bound_action;
  }

  const ledgerAccepted = new Map();
  let ledgerSha;
  {
    const ledgerBytes = await readFile(ledgerPath);
    ledgerSha = sha256(ledgerBytes);
  }
  for await (const { value } of jsonLines(ledgerPath)) {
    if (value.kind === "accepted") ledgerAccepted.set(value.action_witness_id, value);
  }

  const legacyByRecord = new Map();
  let legacyUsable = 0;
  for (const runFile of await discoverRunFiles(root)) {
    for await (const { value } of jsonLines(runFile)) {
      if (value.record_id) legacyByRecord.set(value.record_id, value);
      if (mechanicallyLegacyUsable(value)) legacyUsable++;
    }
  }

  const durableCanonicalByWitness = new Map();
  let legacyDurableCanonical = 0;
  const canonicalPath = path.join(root, "canonical-transitions.jsonl");
  try {
    await access(canonicalPath);
    for await (const { value } of jsonLines(canonicalPath)) {
      if (value.schema_version === 2
        && value.schema === "sts2.human-annotator/canonical-transition-evidence-2"
        && value.collection_mode === "causal_human_native_observation"
        && value.action_witness_id) {
        durableCanonicalByWitness.set(value.action_witness_id, value);
      } else if (value.schema_version === 1) {
        legacyDurableCanonical++;
      }
    }
  } catch {
    // Older recordings predate the additive canonical stream.
  }

  const frameCache = new Map();
  function loadObject(reference) {
    if (!reference?.object_ref) return null;
    const relative = path.normalize(reference.object_ref);
    if (path.isAbsolute(relative) || relative.startsWith("..") || relative.includes(`..${path.sep}`))
      throw new Error(`Unsafe semantic frame reference: ${reference.object_ref}`);
    if (!frameCache.has(relative)) {
      const file = path.join(root, relative);
      const encoded = readFileSync(file, "utf8");
      const digest = sha256(encoded);
      if (digest !== reference.content_sha256)
        throw new Error(`Semantic frame digest mismatch: ${relative}`);
      frameCache.set(relative, JSON.parse(encoded));
    }
    return frameCache.get(relative);
  }
  const loadFrame = loadObject;
  const loadActionSpace = loadObject;

  const classifications = {};
  const proofReasons = {};
  const byNativeType = {};
  const details = [];
  let rapidRebindValid = 0;
  let futureActionChainCandidate = 0;

  const ordered = [...actionsById.values()].sort((left, right) => {
    const l = left.events.find((event) => event.kind === "action_accepted")?.action?.action_sequence ?? 0;
    const r = right.events.find((event) => event.kind === "action_accepted")?.action?.action_sequence ?? 0;
    return l - r || left.id.localeCompare(right.id);
  });

  for (const state of ordered) {
    const accepted = state.events.find((event) => event.kind === "action_accepted");
    if (!accepted) throw new Error(`Action has no accepted event: ${state.id}`);
    const terminals = disposition(state.events);
    const nativeType = accepted.action.native_action_type;
    const ledger = ledgerAccepted.get(state.id);
    const legacy = legacyByRecord.get(accepted.action.record_id);
    const selected = actionFromSources(accepted, ledger, legacy);
    state.action = selected;
    const selectedSource = sourceForAction(accepted, ledger, legacy);
    const terminal = terminals[0];
    const proved = terminals.find((event) => event.kind === "transition_proved");
    const preReference = terminal?.execution_pre_ref
      ?? state.events.find((event) => event.execution_pre_ref)?.execution_pre_ref;
    const pre = loadFrame(preReference);
    const actionSpaceReference = state.events.find((event) =>
      event.execution_semantic_action_space_ref)?.execution_semantic_action_space_ref;
    const semanticActionSpace = loadActionSpace(actionSpaceReference);
    const publicFallbackAllowed = accepted.action.native_mechanism === "direct_ui_commit";
    const actionSpaceAuthority = semanticActionSpace
      ? "native_semantic_execution"
      : publicFallbackAllowed
        ? "public_bound_actions"
        : "missing_execution_semantic";
    const preStatus = semanticActionSpace
      ? semanticActionSpaceStatus(semanticActionSpace, selected, state.id)
      : publicFallbackAllowed
        ? frameStatus(pre, selected)
        : {
          complete: false,
          catalog_status: "missing",
          catalog_count: 0,
          action_match_count: 0
        };
    const humanReference = accepted.human_observation_ref;
    const human = loadFrame(humanReference) ?? ledger?.decision_pre ?? legacy?.pre ?? null;
    const humanStatus = frameStatus(human, selected);
    const successor = causalSuccessorStatus({
      proved,
      events: state.events,
      actionsById,
      loadFrame,
      loadActionSpace
    });

    let classification;
    let reason;
    if (terminals.length !== 1) {
      classification = "rejected";
      reason = terminals.length === 0 ? "missing_unique_disposition" : "multiple_dispositions";
    } else if (terminal.kind === "action_cancelled_before_start"
      || terminal.kind === "action_cancelled_after_start"
      || terminal.kind === "action_aborted_before_commit") {
      classification = "rejected";
      reason = terminal.kind;
    } else if (!selected) {
      classification = "rejected";
      reason = "exact_action_unavailable";
    } else if (!preStatus.complete || preStatus.action_match_count !== 1) {
      classification = "state_action_space_unresolved";
      reason = !preStatus.complete
        ? "execution_state_action_space_incomplete"
        : `executed_action_match_count_${preStatus.action_match_count}`;
    } else if (!successor.valid) {
      classification = "successor_unresolved";
      reason = successor.reason;
    } else {
      classification = "semantic_candidate_s_a_s_prime";
      reason = successor.reason;
    }

    if (humanStatus.complete && humanStatus.action_match_count === 1
      && classification === "state_action_space_unresolved")
      futureActionChainCandidate++;
    if (classification === "semantic_candidate_s_a_s_prime"
      && reason === "execution_handoff_exact")
      rapidRebindValid++;

    increment(classifications, classification);
    increment(proofReasons, reason);
    byNativeType[nativeType] ??= {};
    increment(byNativeType[nativeType], classification);
    details.push({
      action_sequence: accepted.action.action_sequence,
      action_witness_id: state.id,
      record_id: accepted.action.record_id,
      native_action_type: nativeType,
      native_mechanism: accepted.action.native_mechanism,
      classification,
      reason,
      action_source: selectedSource,
      execution_pre_snapshot_id: preReference?.snapshot_id ?? null,
      execution_catalog_status: preStatus.catalog_status,
      execution_catalog_count: preStatus.catalog_count,
      execution_action_match_count: preStatus.action_match_count,
      execution_action_space_authority: actionSpaceAuthority,
      execution_semantic_state_digest:
        semanticActionSpace?.semantic_state_digest ?? null,
      execution_semantic_catalog_digest:
        semanticActionSpace?.semantic_catalog_digest ?? null,
      successor_snapshot_id: proved?.successor_ref?.snapshot_id ?? null,
      semantic_proof_status: proved?.proof_status ?? terminal?.proof_status ?? null,
      human_action_match_count: humanStatus.action_match_count,
      durable_canonical: durableCanonicalByWitness.has(state.id)
    });
  }

  const acceptedCount = ordered.length;
  return {
    schema_version: 2,
    schema: "sts2.human-annotator/semantic-training-calibration-2",
    contract: CONTRACT,
    session: {
      session_id: manifest.session_id,
      timeline_id: manifest.timeline_id,
      recorder_version: manifest.recorder_version,
      recorder_source_revision: manifest.recorder_source_revision,
      platform: manifest.platform,
      capture_profile_id: manifest.capture_profile_id,
      capture_profile_sha256: manifest.capture_profile_sha256,
      source_directory: root,
      semantic_trace_sha256: traceSha,
      native_ledger_sha256: ledgerSha
    },
    summary: {
      accepted_actions: acceptedCount,
      uniquely_classified_actions: details.length,
      classifications: Object.fromEntries(Object.entries(classifications).sort()),
      proof_reasons: Object.fromEntries(Object.entries(proofReasons).sort()),
      legacy_usable: legacyUsable,
      semantic_candidate_s_a: (classifications.semantic_candidate_s_a_s_prime ?? 0)
        + (classifications.successor_unresolved ?? 0),
      semantic_candidate_s_a_s_prime:
        classifications.semantic_candidate_s_a_s_prime ?? 0,
      durable_canonical: durableCanonicalByWitness.size,
      legacy_durable_canonical: legacyDurableCanonical,
      semantic_candidate_without_durable_canonical: details.filter((value) =>
        value.classification === "semantic_candidate_s_a_s_prime"
        && !value.durable_canonical).length,
      rapid_rebind_valid: rapidRebindValid,
      state_action_space_unresolved: classifications.state_action_space_unresolved ?? 0,
      successor_unresolved: classifications.successor_unresolved ?? 0,
      future_action_chain_candidate: futureActionChainCandidate,
      rejected: classifications.rejected ?? 0
    },
    by_native_action_type: Object.fromEntries(Object.entries(byNativeType).sort()),
    actions: details
  };
}

async function main() {
  const args = process.argv.slice(2);
  const directory = args.find((arg) => !arg.startsWith("--"));
  const outputIndex = args.indexOf("--output");
  const output = outputIndex >= 0 ? args[outputIndex + 1] : null;
  if (!directory || outputIndex >= 0 && !output)
    throw new Error("Usage: calibrate-semantic-training.mjs <recording-directory> [--output <report.json>]");
  const result = await calibrate(directory);
  const encoded = `${JSON.stringify(result, null, 2)}\n`;
  if (output) await writeFile(output, encoded, { flag: "wx" });
  else process.stdout.write(encoded);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  });
}
