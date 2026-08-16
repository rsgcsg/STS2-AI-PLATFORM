import { createHash } from "node:crypto";

function compareJson(left, right) {
  return JSON.stringify(left).localeCompare(JSON.stringify(right));
}

function canonicalValue(value, referentIds) {
  if (typeof value === "string" && referentIds.has(value)) return referentIds.get(value);
  if (Array.isArray(value)) return value.map((item) => canonicalValue(item, referentIds));
  if (value == null || typeof value !== "object") return value;
  return Object.fromEntries(Object.keys(value).sort()
    .map((key) => [key, canonicalValue(value[key], referentIds)]));
}

function semanticReferent(referent) {
  return {
    role: referent.role,
    kind: referent.kind,
    label: referent.label ?? null,
    state: canonicalValue(referent.state, new Map()),
    properties_schema: referent.properties_schema ?? null,
    properties: canonicalValue(referent.properties ?? null, new Map())
  };
}

function buildReferentMap(referents) {
  const indexed = referents.map((referent, sourceIndex) => ({
    referent,
    sourceIndex,
    semantic: semanticReferent(referent)
  }));
  indexed.sort((left, right) => compareJson(left.semantic, right.semantic)
    || left.sourceIndex - right.sourceIndex);
  const ids = new Map();
  indexed.forEach(({ referent }, index) => {
    ids.set(referent.referent_id, `referent-${String(index + 1).padStart(4, "0")}`);
  });
  return ids;
}

function canonicalReferents(referents, referentIds) {
  return referents.map((referent) => ({
    canonical_referent_id: referentIds.get(referent.referent_id),
    ...semanticReferent(referent)
  })).sort(compareJson);
}

function canonicalAction(action, referentIds) {
  return {
    verb: action.verb,
    subject_referent_id: action.subject_referent_id == null
      ? null
      : referentIds.get(action.subject_referent_id) ?? "unbound-referent",
    arguments: action.arguments.map((argument) => ({
      role: argument.role,
      referent_id: referentIds.get(argument.referent_id) ?? "unbound-referent"
    })),
    label: action.label
  };
}

export function canonicalizeSnapshot(snapshot) {
  const referents = Array.isArray(snapshot?.referents) ? snapshot.referents : [];
  const referentIds = buildReferentMap(referents);
  const actions = snapshot?.bound_actions?.actions ?? [];
  const reads = snapshot?.reads ?? [];
  return {
    canonical_schema: "sts2.headless/canonical-player-decision-1",
    protocol_version: snapshot?.protocol_version ?? null,
    status: snapshot?.status ?? null,
    persistent: canonicalValue(snapshot?.persistent ?? null, referentIds),
    interaction: snapshot?.interaction == null ? null : {
      kind: snapshot.interaction.kind,
      stage: snapshot.interaction.stage,
      prompt: snapshot.interaction.prompt ?? null,
      content_schema: snapshot.interaction.content_schema,
      content: canonicalValue(snapshot.interaction.content, referentIds),
      capabilities: canonicalValue(snapshot.interaction.capabilities, referentIds)
    },
    referents: canonicalReferents(referents, referentIds),
    bound_actions: snapshot?.bound_actions == null ? null : {
      schema: snapshot.bound_actions.schema,
      status: snapshot.bound_actions.status,
      materialized_count: snapshot.bound_actions.materialized_count,
      total_count: snapshot.bound_actions.total_count,
      limit: snapshot.bound_actions.limit,
      ordering_semantics: snapshot.bound_actions.ordering_semantics,
      actions: actions.map((action) => canonicalAction(action, referentIds)).sort(compareJson)
    },
    reads: reads.map((read) => ({
      kind: read.kind,
      target_referent_id: read.target_referent_id == null
        ? null
        : referentIds.get(read.target_referent_id) ?? "unbound-referent",
      content_schema: read.content_schema,
      visibility_basis: read.visibility_basis,
      snapshot_bound: read.snapshot_bound,
      ordering_semantics: read.ordering_semantics,
      hidden_by_policy: [...read.hidden_by_policy].sort()
    })).sort(compareJson),
    completeness: canonicalValue(snapshot?.completeness ?? null, referentIds),
    information_policy: canonicalValue(snapshot?.information_policy ?? null, referentIds)
  };
}

export function canonicalDecisionDigest(snapshot) {
  return createHash("sha256")
    .update(JSON.stringify(canonicalizeSnapshot(snapshot)))
    .digest("hex");
}

export function canonicalizeSelectedAction(snapshot, boundActionId) {
  const referents = Array.isArray(snapshot?.referents) ? snapshot.referents : [];
  const action = snapshot?.bound_actions?.actions?.find(
    (candidate) => candidate.bound_action_id === boundActionId
  );
  if (!action) return null;
  return canonicalAction(action, buildReferentMap(referents));
}

export function compareCanonicalDecisions(reference, candidate) {
  const expected = canonicalizeSnapshot(reference);
  const actual = canonicalizeSnapshot(candidate);
  const expectedJson = JSON.stringify(expected);
  const actualJson = JSON.stringify(actual);
  return {
    equal: expectedJson === actualJson,
    expected_digest: createHash("sha256").update(expectedJson).digest("hex"),
    actual_digest: createHash("sha256").update(actualJson).digest("hex"),
    expected,
    actual
  };
}
