import assert from "node:assert/strict";
import test from "node:test";
import { compareSemanticTrajectories } from "../src/semantic-differential.mjs";

function report(runtime, seed = "SEED01", sha = "artifact") {
  return {
    loaded_identity: {
      protocol: "1.0-rc.2",
      host: {
        host_kind: "headless",
        runtime_instance_id: runtime,
        implementation: {
          source_revision: "source",
          artifact_sha256: sha,
          module_version_id: "mvid"
        }
      },
      game: {
        version: "v0.111.0",
        commit: "41cef1ea",
        main_assembly_hash: 222455745,
        modset: { fingerprint: "modset" }
      }
    },
    episode_provenance: { verdict: "provenance_pass", actual_seed: seed },
    verdict: { integrity: { verdict: "integrity_pass" } }
  };
}

const profiles = {
  referenceProfile: { template_payload_sha256: "template", generation_id: "generation-a" },
  candidateProfile: { template_payload_sha256: "template", generation_id: "generation-b" }
};

function action(digest = "decision-a", verb = "select", referentId = "referent-0001") {
  return {
    type: "action",
    canonical_decision_digest: digest,
    canonical_decision: {
      referents: [{
        canonical_referent_id: referentId,
        role: "card",
        kind: "entity",
        label: "Defend",
        state: { visible: true },
        properties_schema: "card-1",
        properties: { definition_id: "DEFEND", cost: "1" }
      }]
    },
    canonical_selected_action: { verb, subject_referent_id: referentId, arguments: [], label: verb },
    delivery: "delivered",
    reason_code: null
  };
}

test("same-artifact independent seeded trajectories admit a semantic match", () => {
  const result = compareSemanticTrajectories({
    referenceReport: report("runtime-a"),
    candidateReport: report("runtime-b"),
    referenceEvents: [action()],
    candidateEvents: [action()],
    ...profiles
  });
  assert.equal(result.verdict, "semantic_match");
  assert.equal(result.first_divergence, null);
});

test("semantic differential reports the first changed decision or action", () => {
  const result = compareSemanticTrajectories({
    referenceReport: report("runtime-a"),
    candidateReport: report("runtime-b"),
    referenceEvents: [action(), action("decision-b", "confirm")],
    candidateEvents: [action(), action("decision-c", "cancel")],
    ...profiles
  });
  assert.equal(result.verdict, "semantic_mismatch");
  assert.equal(result.first_divergence.semantic_event_index, 1);
  assert.equal(result.first_divergence.reference.selected_action_semantics.verb, "confirm");
  assert.equal(result.first_divergence.candidate.selected_action_semantics.verb, "cancel");
});

test("indistinguishable operands remain exact at execution but equivalent in cross-runtime measurement", () => {
  const result = compareSemanticTrajectories({
    referenceReport: report("runtime-a"),
    candidateReport: report("runtime-b"),
    referenceEvents: [action("same-decision", "play", "referent-0002")],
    candidateEvents: [action("same-decision", "play", "referent-0004")],
    ...profiles
  });
  assert.equal(result.verdict, "semantic_match");
});

test("semantic differential rejects different artifacts and seeds before qualification", () => {
  const result = compareSemanticTrajectories({
    referenceReport: report("runtime-a", "SEED01", "artifact-a"),
    candidateReport: report("runtime-b", "SEED02", "artifact-b"),
    referenceEvents: [action()],
    candidateEvents: [action()],
    ...profiles
  });
  assert.equal(result.verdict, "semantic_mismatch");
  assert.ok(result.errors.includes("environment_identity_not_comparable"));
  assert.ok(result.errors.includes("episode_seed_not_comparable"));
});
