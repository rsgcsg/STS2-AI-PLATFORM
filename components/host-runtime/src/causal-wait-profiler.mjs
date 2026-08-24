import { summarizeDurations } from "./decision-timing.mjs";

const SELECTOR_KINDS = /(?:selection|selector|card_bundle|combat_pile)/u;

function finiteNonNegative(value) {
  return Number.isFinite(value) && value >= 0 ? value : null;
}

function sum(values) {
  return values.reduce((total, value) => total + (finiteNonNegative(value) ?? 0), 0);
}

export function classifyObservedWaitBoundary(event) {
  const source = event?.interaction_kind ?? null;
  const successor = event?.observed_successor_interaction_kind ?? null;
  if (source === "character_select") return "run_mount_boundary";
  if (source === "map_navigation") return "room_mount_boundary";
  if (source === "combat_turn") return "combat_resolution_boundary";
  if (SELECTOR_KINDS.test(source ?? "") || SELECTOR_KINDS.test(successor ?? "")) {
    return "selector_handoff_boundary";
  }
  if (source != null && successor != null && source !== successor) return "surface_handoff_boundary";
  return "same_surface_or_unclassified_boundary";
}

function waitStateKey(observation) {
  return [
    observation?.status ?? "unknown",
    observation?.interaction_kind ?? "none",
    observation?.interaction_stage ?? "none",
    observation?.bound_action_status ?? "none",
    observation?.bound_action_count ?? "none"
  ].join("/");
}

function summarizeGroup(events) {
  const receiptWaits = events.map((event) => event.timing?.receipt_to_successor_ms);
  const semanticWaits = events.map((event) => event.timing?.semantic_decision_ms);
  return {
    count: events.length,
    receipt_to_successor: summarizeDurations(receiptWaits),
    semantic_decision: summarizeDurations(semanticWaits),
    receipt_wait_total_ms: sum(receiptWaits),
    semantic_decision_total_ms: sum(semanticWaits)
  };
}

function grouped(events, keyForEvent) {
  const groups = new Map();
  for (const event of events) {
    const key = keyForEvent(event);
    const existing = groups.get(key) ?? [];
    existing.push(event);
    groups.set(key, existing);
  }
  return Object.fromEntries([...groups.entries()]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([key, values]) => [key, summarizeGroup(values)]));
}

export function summarizeCausalWaits(events, { topLimit = 10 } = {}) {
  if (!Array.isArray(events)) throw new TypeError("Wait profiler events must be an array.");
  if (!Number.isSafeInteger(topLimit) || topLimit < 0) {
    throw new TypeError("Wait profiler topLimit must be a non-negative integer.");
  }
  const actions = events.filter((event) => event?.type === "action" && event?.timing != null);
  const semanticTotalMs = sum(actions.map((event) => event.timing.semantic_decision_ms));
  const receiptWaitTotalMs = sum(actions.map((event) => event.timing.receipt_to_successor_ms));
  const measuredNonReceiptMs = Math.max(0, semanticTotalMs - receiptWaitTotalMs);
  const stateCounts = new Map();
  for (const event of actions) {
    for (const observation of event.successor_wait?.observations ?? []) {
      const key = waitStateKey(observation);
      stateCounts.set(key, (stateCounts.get(key) ?? 0) + (observation.sample_count ?? 1));
    }
  }
  const counterfactualRate = measuredNonReceiptMs > 0
    ? actions.length / (measuredNonReceiptMs / 1000)
    : null;
  const topWaits = [...actions]
    .sort((left, right) => (right.timing.receipt_to_successor_ms ?? 0)
      - (left.timing.receipt_to_successor_ms ?? 0))
    .slice(0, topLimit)
    .map((event) => ({
      interaction_kind: event.interaction_kind ?? null,
      interaction_stage: event.interaction_stage ?? null,
      action_verb: event.action?.verb ?? null,
      action_label: event.action?.label ?? null,
      observed_successor_interaction_kind: event.observed_successor_interaction_kind ?? null,
      observed_successor_interaction_stage: event.observed_successor_interaction_stage ?? null,
      boundary_category: classifyObservedWaitBoundary(event),
      receipt_to_successor_ms: finiteNonNegative(event.timing.receipt_to_successor_ms),
      semantic_decision_ms: finiteNonNegative(event.timing.semantic_decision_ms),
      successor_wait_terminal: event.successor_wait?.terminal ?? "legacy_event_without_wait_trace",
      successor_wait_poll_count: event.successor_wait?.poll_count ?? null,
      observed_state_sequence: (event.successor_wait?.observations ?? []).map(waitStateKey)
    }));

  return {
    schema: "sts2.headless/causal-wait-profile-1",
    action_count: actions.length,
    measured: {
      semantic_decision_total_ms: semanticTotalMs,
      receipt_to_successor_total_ms: receiptWaitTotalMs,
      receipt_to_successor_fraction: semanticTotalMs > 0 ? receiptWaitTotalMs / semanticTotalMs : null,
      semantic_decision: summarizeDurations(
        actions.map((event) => event.timing.semantic_decision_ms)
      ),
      receipt_to_successor: summarizeDurations(
        actions.map((event) => event.timing.receipt_to_successor_ms)
      )
    },
    counterfactual_zero_observed_receipt_wait: {
      measured_non_receipt_total_ms: measuredNonReceiptMs,
      normalized_decisions_per_second: counterfactualRate,
      interpretation: "Arithmetic upper bound over recorded action spans only; it is not a proven Host throughput ceiling."
    },
    by_observed_boundary: grouped(actions, classifyObservedWaitBoundary),
    by_source_surface: grouped(actions, (event) => event.interaction_kind ?? "unknown"),
    observed_wait_states: [...stateCounts.entries()]
      .sort((left, right) => right[1] - left[1] || left[0].localeCompare(right[0]))
      .map(([state, samples]) => ({ state, samples })),
    top_waits: topWaits,
    non_claims: [
      "Boundary categories describe where the Player Environment waited; they do not prove an internal engine cause.",
      "Polling resolution and REST overhead remain inside the measured receipt-to-successor duration.",
      "Removing observed waits can expose new bottlenecks, so the arithmetic counterfactual is not qualification evidence."
    ]
  };
}
