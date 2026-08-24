import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { chooseManagedPlayerEnvironmentAction } from "./managed-player-environment-probe.mjs";
import { startManagedPlayerEnvironmentSession } from "./managed-player-environment.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { readSystemIdentity } from "./system-identity.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function assertMounted(runIdentity, seed) {
  const pending = [
    "action_executor_running",
    "pending_host_operation",
    "pending_card_selection",
    "pending_card_reward",
    "pending_reward_set",
    "pending_bundle"
  ].filter((name) => runIdentity?.[name] !== false);
  if (runIdentity?.type !== "run_identity"
      || runIdentity.active !== true
      || runIdentity.seed !== seed
      || pending.length > 0) {
    throw new Error(`Managed runtime mount is not quiescent for seed ${seed}: ${pending.join(",")}`);
  }
}

function runtimeEvidence(started) {
  return {
    pid: started.runtime.process.pid,
    adapter_runtime_instance_id: started.runtime.adapterRuntimeInstanceId,
    environment_fingerprint: started.environmentFingerprint,
    runtime_identity: started.runtime.runtimeIdentity,
    build: started.runtime.build
  };
}

export async function runManagedRecoveryProbe({
  root,
  candidateDirectory,
  diskIdentity,
  seed = "H1MANAGEDRECOVERY01",
  character = "Ironclad",
  requestTimeoutMs = 10_000,
  evidenceRoot = null
}) {
  if (process.platform === "win32") {
    throw new Error("The deterministic suspend/write/kill recovery probe currently requires POSIX signals.");
  }
  if (typeof seed !== "string" || seed.length === 0) throw new TypeError("seed must be a non-empty string.");
  const report = {
    schema: "sts2.headless/managed-recovery-probe-1",
    generated_at: new Date().toISOString(),
    status: "running",
    headless: readProjectIdentity(root),
    system_identity: readSystemIdentity(),
    game_identity: {
      version: diskIdentity.release.version,
      commit: diskIdentity.release.commit,
      runtime_main_assembly_hash: diskIdentity.runtime_main_assembly_hash,
      original_sts2_sha256: diskIdentity.sts2_assembly.sha256
    },
    seed,
    fault_profile: "process_suspended_before_request_write_then_killed_after_write",
    fault_runtime: null,
    replacement_runtime: null,
    gates: {},
    failure: null,
    non_claims: [
      "The transport accepted the request bytes before process loss; this does not prove that native Commit occurred.",
      "A bounded replacement drill is not long-soak or million-decision reliability evidence."
    ]
  };
  let fault = null;
  let replacement = null;
  try {
    fault = await startManagedPlayerEnvironmentSession({
      root,
      candidateDirectory,
      diskIdentity,
      character,
      requestTimeoutMs
    });
    report.fault_runtime = runtimeEvidence(fault);
    const before = await fault.session.mount({ seed, timeoutMs: requestTimeoutMs });
    assertMounted(await fault.runtime.process.request({ cmd: "run_identity" }, requestTimeoutMs), seed);
    const selected = chooseManagedPlayerEnvironmentAction(before);
    if (selected == null) throw new Error("Fault runtime did not publish a complete action.");
    const request = {
      requestId: "managed-recovery-ambiguous",
      expectedSnapshotId: before.snapshot_id,
      boundActionId: selected.bound_action_id,
      timeoutMs: requestTimeoutMs
    };
    if (!fault.runtime.process.sendSignal("SIGSTOP")) throw new Error("Could not suspend fault runtime.");
    await new Promise((resolve) => setTimeout(resolve, 25));
    const pendingReceipt = fault.session.submit(request);
    await new Promise((resolve) => setTimeout(resolve, 25));
    if (!fault.runtime.process.sendSignal("SIGKILL")) throw new Error("Could not kill fault runtime after request write.");
    const unknown = await pendingReceipt;
    if (unknown.delivery !== "unknown" || unknown.retry?.allowed !== false || unknown.successor != null) {
      throw new Error("Ambiguous transport loss did not return a terminal unknown receipt.");
    }
    const replay = await fault.session.submit(request);
    if (JSON.stringify(replay) !== JSON.stringify(unknown)) {
      throw new Error("Duplicate ambiguous request did not replay the exact unknown receipt.");
    }
    const refused = await fault.session.submit({
      ...request,
      requestId: "managed-recovery-after-unknown"
    });
    if (refused.delivery !== "not_delivered"
        || refused.reason_code !== "runtime_tainted_after_unknown"
        || refused.retry?.allowed !== false) {
      throw new Error("Tainted runtime accepted authority after unknown delivery.");
    }
    const faultExit = await fault.runtime.process.exit;
    report.gates.unknown_no_retry = {
      verdict: "pass",
      delivery: unknown.delivery,
      reason_code: unknown.reason_code,
      exact_duplicate_receipt: true,
      subsequent_authority_closed: true,
      process_exit: faultExit
    };

    replacement = await startManagedPlayerEnvironmentSession({
      root,
      candidateDirectory,
      diskIdentity,
      character,
      requestTimeoutMs
    });
    report.replacement_runtime = runtimeEvidence(replacement);
    if (replacement.runtime.process.pid === fault.runtime.process.pid
        || replacement.runtime.adapterRuntimeInstanceId === fault.runtime.adapterRuntimeInstanceId
        || replacement.environmentFingerprint !== fault.environmentFingerprint) {
      throw new Error("Replacement process/runtime identity is not a fresh equivalent environment.");
    }
    const recovered = await replacement.session.mount({ seed, timeoutMs: requestTimeoutMs });
    assertMounted(await replacement.runtime.process.request({ cmd: "run_identity" }, requestTimeoutMs), seed);
    const recoveredAction = chooseManagedPlayerEnvironmentAction(recovered);
    if (recoveredAction == null) throw new Error("Replacement runtime did not publish a complete action.");
    const recoveredReceipt = await replacement.session.submit({
      requestId: "managed-recovery-replacement-action",
      expectedSnapshotId: recovered.snapshot_id,
      boundActionId: recoveredAction.bound_action_id,
      timeoutMs: requestTimeoutMs
    });
    if (recoveredReceipt.delivery !== "delivered" || recoveredReceipt.successor == null) {
      throw new Error("Replacement runtime did not deliver an action with a successor.");
    }
    report.gates.process_replacement = {
      verdict: "pass",
      distinct_pid: true,
      distinct_adapter_runtime_instance: true,
      exact_environment_preserved: true,
      requested_seed_reestablished: true,
      delivered_action_with_successor: true
    };
    report.status = "managed_recovery_pass";
  } catch (error) {
    report.status = "managed_recovery_incomplete";
    report.failure = error instanceof Error ? error.message : String(error);
  } finally {
    if (fault != null) await fault.session.close().catch(() => null);
    if (replacement != null) await replacement.session.close().catch(() => null);
  }
  let reportFile = null;
  if (evidenceRoot != null) {
    const directory = path.join(evidenceRoot, `managed-recovery-${safeTimestamp()}`);
    mkdirSync(directory, { recursive: true });
    reportFile = path.join(directory, "report.json");
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
  }
  return { report, reportFile };
}
