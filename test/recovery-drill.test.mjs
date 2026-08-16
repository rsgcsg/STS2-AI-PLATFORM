import assert from "node:assert/strict";
import test from "node:test";
import { evaluateRecoveryCycle } from "../src/recovery-drill.mjs";

function report(runtimeId, terminal, integrity, delivered = 1) {
  return {
    runtime_diagnostics: { status: "clean" },
    loaded_identity: {
      protocol: "1.0",
      host: {
        runtime_instance_id: runtimeId,
        implementation: {
          source_revision: "source",
          artifact_sha256: "sha",
          module_version_id: "mvid"
        }
      },
      game: {
        version: "v0.test",
        commit: "commit",
        main_assembly_hash: 1,
        modset: { fingerprint: "modset" }
      }
    },
    verdict: {
      delivered_actions: delivered,
      integrity: { terminal, verdict: integrity }
    }
  };
}

test("accepts an exact reset and distinct recovered runtime", () => {
  const result = evaluateRecoveryCycle({
    faultProfile: { generation_id: "old", template_payload_sha256: "template" },
    recoveryProfile: { generation_id: "new", template_payload_sha256: "template" },
    faultReport: report("fault", "injected_process_crash", "integrity_incomplete"),
    recoveryReport: report("recovery", "action_limit", "integrity_pass"),
    remainingProcesses: [],
    endpointReleased: true
  });
  assert.equal(result.verdict, "recovery_cycle_pass");
  assert.deepEqual(result.errors, []);
});

test("rejects reused generations, runtimes, and incomplete recovery", () => {
  const result = evaluateRecoveryCycle({
    faultProfile: { generation_id: "same", template_payload_sha256: "a" },
    recoveryProfile: { generation_id: "same", template_payload_sha256: "b" },
    faultReport: report("runtime", "action_limit", "integrity_pass", 0),
    recoveryReport: report("runtime", "action_limit", "integrity_incomplete"),
    remainingProcesses: ["game"],
    endpointReleased: false
  });
  assert.equal(result.verdict, "recovery_cycle_incomplete");
  assert.ok(result.errors.length >= 7);
});
