import type { PlayerEnvironmentBoundAction } from "@rsgcsg/sts2-connector-client";
import { candidateOrderDigest } from "./digest.js";
import { validateAdapterDecision, validatePolicyDecision, type DecisionBundle, type PolicyDecision, type PolicyManifest } from "./contracts.js";

export class PolicyAdmissionError extends Error {
  readonly code = "policy_admission_failed" as const;
  constructor(message: string) { super(message); this.name = "PolicyAdmissionError"; }
}

export interface AdmittedDecision {
  readonly decision: PolicyDecision;
  readonly bundle: DecisionBundle;
  readonly boundAction: PlayerEnvironmentBoundAction | null;
  readonly candidateDigest: string;
}

/** Validate the adapter echo against the current complete bundle, without catalog filtering. */
export function admitWholeDecision(value: unknown, bundle: DecisionBundle, manifest: PolicyManifest, expectedRunId: string): AdmittedDecision {
  let decision: PolicyDecision;
  try { decision = validatePolicyDecision(value); } catch (error) { throw new PolicyAdmissionError(error instanceof Error ? error.message : String(error)); }
  if (decision.manifest_id !== manifest.manifest_id || decision.run_id !== expectedRunId || decision.snapshot_id !== bundle.observation.snapshot_id) throw new PolicyAdmissionError("Policy Decision identity does not match the current Agent Run, Manifest, or Snapshot");
  const actions = bundle.observation.bound_actions.actions;
  if (bundle.observation.status !== "interactive" || bundle.observation.completeness.status !== "complete" || bundle.observation.bound_actions.status !== "complete" || bundle.observation.bound_actions.materialized_count !== bundle.observation.bound_actions.total_count || actions.length === 0) throw new PolicyAdmissionError("Policy Decision requires a complete whole decision bundle");
  if (!manifest.support.interaction_kinds.includes(bundle.observation.interaction.kind)) throw new PolicyAdmissionError("current interaction is outside Manifest support");
  if (actions.some((action) => !manifest.support.action_verbs.includes(action.verb))) throw new PolicyAdmissionError("current action catalog is outside Manifest support");
  const digest = candidateOrderDigest(actions);
  try { validateAdapterDecision({ candidate_digest: decision.candidate_digest, scores: decision.scores, selected_index: decision.selected_index }, digest, actions.length); } catch (error) { throw new PolicyAdmissionError(error instanceof Error ? error.message : String(error)); }
  if (decision.candidate_count !== actions.length) throw new PolicyAdmissionError("Policy Decision candidate count drift");
  if (decision.selected_index === null && decision.disposition !== "abstain") throw new PolicyAdmissionError("null selected_index must abstain");
  if (decision.selected_index !== null && decision.disposition !== "admit") throw new PolicyAdmissionError("selected_index must admit");
  return { decision, bundle, boundAction: decision.selected_index === null ? null : actions[decision.selected_index] ?? null, candidateDigest: digest };
}
