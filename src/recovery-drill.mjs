import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { readDiskIdentity } from "./game-installation.mjs";
import { runBoundedJourney } from "./journey-probe.mjs";
import { instantiateProfileTemplate } from "./profile-template.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { listGameProcesses, readJson } from "./runtime-probe.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function comparableRuntimeIdentity(report) {
  return {
    protocol: report?.loaded_identity?.protocol ?? null,
    connector_source: report?.loaded_identity?.host?.implementation?.source_revision ?? null,
    connector_sha256: report?.loaded_identity?.host?.implementation?.artifact_sha256 ?? null,
    connector_mvid: report?.loaded_identity?.host?.implementation?.module_version_id ?? null,
    game_version: report?.loaded_identity?.game?.version ?? null,
    game_commit: report?.loaded_identity?.game?.commit ?? null,
    game_main_assembly_hash: report?.loaded_identity?.game?.main_assembly_hash ?? null,
    modset_fingerprint: report?.loaded_identity?.game?.modset?.fingerprint ?? null
  };
}

export function evaluateRecoveryCycle({
  faultProfile,
  recoveryProfile,
  faultReport,
  recoveryReport,
  remainingProcesses = [],
  endpointReleased = true
}) {
  const errors = [];
  if (faultProfile?.generation_id === recoveryProfile?.generation_id) {
    errors.push("hard_reset_did_not_advance_generation");
  }
  if (faultProfile?.template_payload_sha256 !== recoveryProfile?.template_payload_sha256) {
    errors.push("hard_reset_template_changed");
  }
  if (faultReport?.verdict?.integrity?.terminal !== "injected_process_crash") {
    errors.push("expected_process_fault_not_observed");
  }
  if ((faultReport?.verdict?.delivered_actions ?? 0) < 1) {
    errors.push("fault_was_not_injected_after_a_semantic_decision");
  }
  if (recoveryReport?.verdict?.integrity?.verdict !== "integrity_pass") {
    errors.push("recovery_journey_integrity_failed");
  }
  const diagnosticFindings = [];
  if (faultReport?.runtime_diagnostics?.status !== "clean") {
    diagnosticFindings.push("fault_process_diagnostics_observed");
  }
  if (recoveryReport?.runtime_diagnostics?.status !== "clean") {
    diagnosticFindings.push("recovered_process_shutdown_diagnostics_observed");
  }
  const faultIdentity = comparableRuntimeIdentity(faultReport);
  const recoveryIdentity = comparableRuntimeIdentity(recoveryReport);
  if (Object.values(faultIdentity).some((value) => value == null)
      || JSON.stringify(faultIdentity) !== JSON.stringify(recoveryIdentity)) {
    errors.push("recovery_environment_identity_changed");
  }
  const faultRuntime = faultReport?.loaded_identity?.host?.runtime_instance_id ?? null;
  const recoveryRuntime = recoveryReport?.loaded_identity?.host?.runtime_instance_id ?? null;
  if (faultRuntime == null || recoveryRuntime == null || faultRuntime === recoveryRuntime) {
    errors.push("recovery_runtime_instance_not_replaced");
  }
  if (remainingProcesses.length > 0) errors.push("game_process_remained_after_recovery_cycle");
  if (!endpointReleased) errors.push("connector_endpoint_remained_owned_after_recovery_cycle");
  return {
    verdict: errors.length === 0 ? "recovery_cycle_pass" : "recovery_cycle_incomplete",
    errors,
    diagnostic_findings: diagnosticFindings,
    shutdown_quality: diagnosticFindings.includes("recovered_process_shutdown_diagnostics_observed")
      ? "diagnostics_observed"
      : "clean",
    exact_identity: recoveryIdentity,
    fault_runtime_instance_id: faultRuntime,
    recovery_runtime_instance_id: recoveryRuntime,
    fault_generation_id: faultProfile?.generation_id ?? null,
    recovery_generation_id: recoveryProfile?.generation_id ?? null
  };
}

export async function runRecoveryDrill({
  installation,
  localRoot,
  evidenceRoot,
  templateId,
  profileId = "recovery-worker",
  endpoint = "http://127.0.0.1:15700",
  cycles = 3,
  faultAfterDeliveredActions = 1,
  recoveryActions = 3,
  timeoutMs = 90_000,
  actionTimeoutMs = 20_000,
  experimentalBuildAcknowledged = false
}) {
  if (!Number.isSafeInteger(cycles) || cycles < 1 || cycles > 20) {
    throw new Error("Recovery cycles must be an integer from 1 through 20.");
  }
  if (!Number.isSafeInteger(recoveryActions) || recoveryActions < 1) {
    throw new Error("Recovery actions must be a positive integer.");
  }
  const existing = listGameProcesses();
  if (existing.length > 0) {
    throw new Error(`Recovery drill requires a clean process baseline:\n${existing.join("\n")}`);
  }
  const exactGameIdentity = readDiskIdentity(installation);
  const evidenceDirectory = path.join(evidenceRoot, `recovery-${safeTimestamp()}`);
  const reportFile = path.join(evidenceDirectory, "report.json");
  mkdirSync(evidenceDirectory, { recursive: true });
  const report = {
    schema_version: 1,
    generated_at: new Date().toISOString(),
    status: "running",
    route: "reference_shipped_crash_recovery",
    headless: readProjectIdentity(),
    disk_identity: exactGameIdentity,
    template_id: templateId,
    profile_id: profileId,
    endpoint,
    requested_cycles: cycles,
    fault_after_delivered_actions: faultAfterDeliveredActions,
    recovery_actions: recoveryActions,
    cycles: [],
    non_claims: [
      "A bounded recovery drill is not long-duration soak evidence.",
      "Recovery uses a verified profile template and does not prove in-place save recovery.",
      "The deterministic policy is a test consumer, not a gameplay agent."
    ]
  };
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);

  for (let index = 0; index < cycles; index += 1) {
    const cycleId = String(index + 1).padStart(2, "0");
    const faultProfile = instantiateProfileTemplate({
      localRoot,
      templateId,
      profileId,
      expectedGameIdentity: exactGameIdentity
    });
    const fault = await runBoundedJourney({
      installation,
      localRoot,
      endpoint,
      timeoutMs,
      actionTimeoutMs,
      maxActions: faultAfterDeliveredActions,
      tutorialPreference: "disable",
      evidenceRoot,
      isolatedProfileId: profileId,
      experimentalBuildAcknowledged,
      evidenceLabel: `recovery-${cycleId}-fault`,
      faultAfterDeliveredActions
    });
    const recoveryProfile = instantiateProfileTemplate({
      localRoot,
      templateId,
      profileId,
      expectedGameIdentity: exactGameIdentity
    });
    const recovery = await runBoundedJourney({
      installation,
      localRoot,
      endpoint,
      timeoutMs,
      actionTimeoutMs,
      maxActions: recoveryActions,
      tutorialPreference: "disable",
      evidenceRoot,
      isolatedProfileId: profileId,
      experimentalBuildAcknowledged,
      evidenceLabel: `recovery-${cycleId}-restart`
    });
    const remainingProcesses = listGameProcesses();
    const endpointReleased = !(await readJson(
      endpoint,
      "/api/player-environment/capabilities",
      1000
    )).ok;
    const verdict = evaluateRecoveryCycle({
      faultProfile,
      recoveryProfile,
      faultReport: fault.report,
      recoveryReport: recovery.report,
      remainingProcesses,
      endpointReleased
    });
    report.cycles.push({
      cycle: index + 1,
      fault_profile: faultProfile,
      recovery_profile: recoveryProfile,
      fault_report_file: fault.reportFile,
      recovery_report_file: recovery.reportFile,
      remaining_processes: remainingProcesses,
      endpoint_released: endpointReleased,
      verdict
    });
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    if (verdict.verdict !== "recovery_cycle_pass") break;
  }
  const operationalPass = report.cycles.length === cycles
    && report.cycles.every((cycle) => cycle.verdict.verdict === "recovery_cycle_pass");
  const cleanShutdown = operationalPass
    && report.cycles.every((cycle) => cycle.verdict.shutdown_quality === "clean");
  report.status = !operationalPass
    ? "recovery_incomplete"
    : cleanShutdown
      ? "recovery_pass_clean_shutdown"
      : "recovery_operational_pass_shutdown_diagnostics_observed";
  report.completed_at = new Date().toISOString();
  writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  return { report, reportFile, evidenceDirectory };
}
