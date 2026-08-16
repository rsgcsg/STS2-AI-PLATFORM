import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { evaluateRuntimeCompatibility } from "./compatibility.mjs";
import { readDiskIdentity } from "./game-installation.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { readSystemIdentity } from "./system-identity.mjs";

const FULL_RUNTIME_GATES = Object.freeze([
  "connector_candidate_build",
  "isolated_profile_bootstrap_and_reset",
  "h0_boot_and_identity",
  "h1_control_stale_idempotency_and_successor",
  "seed_and_episode_provenance",
  "targeted_semantic_differential",
  "bounded_journey",
  "capacity_and_resource_measurement",
  "crash_hang_and_restart_recovery",
  "bounded_soak_and_shared_profile_sentinel",
  "manual_support_manifest_review"
]);

const SOURCE_FIELDS = new Set([
  "gameVersion",
  "gameCommit",
  "runtimeMainAssemblyHash",
  "sts2AssemblySha256"
]);

const HOST_FIELDS = new Set([
  "platform",
  "architecture",
  "executableSha256",
  "godotSharpAssemblySha256"
]);

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

export function planRuntimeRequalification(identity, options = {}) {
  const compatibility = evaluateRuntimeCompatibility(identity, options);
  if (compatibility.status === "supported_exact") {
    return {
      status: "supported_identity_unchanged",
      authority: "supported_exact",
      support_id: compatibility.support_id,
      identity_mismatches: [],
      required_gates: [],
      source_audit_required: false,
      host_qualification_required: false,
      automatic_promotion: false
    };
  }

  const mismatches = compatibility.mismatches;
  const sourceAuditRequired = mismatches.some((field) => SOURCE_FIELDS.has(field));
  const hostQualificationRequired = mismatches.some((field) => HOST_FIELDS.has(field));
  const requiredGates = [
    ...(sourceAuditRequired
      ? ["exact_assembly_inventory_and_decompilation", "source_impact_review"]
      : []),
    ...(hostQualificationRequired
      ? ["host_bootstrap_and_process_lifecycle_review"]
      : []),
    ...FULL_RUNTIME_GATES
  ];
  return {
    status: compatibility.status === "known_experimental"
      ? "known_experimental_qualification_required"
      : "identity_drift_requalification_required",
    authority: "fail_closed",
    support_id: compatibility.support_id,
    identity_mismatches: mismatches,
    nearest_identity: compatibility.expected,
    observed_identity: compatibility.actual,
    required_gates: [...new Set(requiredGates)],
    source_audit_required: sourceAuditRequired,
    host_qualification_required: hostQualificationRequired,
    automatic_promotion: false
  };
}

export function runRequalificationDrill({ installation, evidenceRoot }) {
  const diskIdentity = readDiskIdentity(installation);
  const plan = planRuntimeRequalification(diskIdentity);
  const evidenceDirectory = path.join(evidenceRoot, `requalification-${safeTimestamp()}`);
  const reportFile = path.join(evidenceDirectory, "report.json");
  mkdirSync(evidenceDirectory, { recursive: true });
  const report = {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    status: plan.status,
    headless: readProjectIdentity(),
    system_identity: readSystemIdentity(),
    disk_identity: diskIdentity,
    plan,
    non_claims: [
      "A no-drift result is not new runtime qualification.",
      "A generated plan is not evidence that any listed gate passed.",
      "A fixture mutation proves fail-closed planning, not a real game-update drill."
    ]
  };
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  return { report, reportFile, evidenceDirectory };
}
