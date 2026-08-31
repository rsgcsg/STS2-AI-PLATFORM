import assert from "node:assert/strict";
import test from "node:test";

import { evaluateLoadedEvidence, extractGameProcessIds } from "../loaded-evidence.mjs";

function fixture() {
  const artifact = { sha256: "artifact", module_version_id: "mvid" };
  return {
    installed: {
      artifact,
      source: {
        platform: { source_revision: "platform", source_digest_sha256: "platform-digest" },
        components: {
          connector: { source_revision: "connector" },
          annotator: { source_revision: "annotator" },
          live_ui: { source_revision: "live-ui", source_digest_sha256: "live-ui-digest" }
        }
      }
    },
    status: {
      status: "ready",
      observed_at: "2026-08-29T08:34:12.705Z",
      process_id: 454,
      session_id: "none"
    },
    capabilities: {
      execution_available: true,
      host: {
        implementation: {
          artifact_sha256: "artifact",
          module_version_id: "mvid",
          source_revision: "connector"
        }
      },
      game: {
        modset: { status: "exact_platform_modset", loaded_mod_ids: ["STS2_PLATFORM"] }
      }
    },
    platformIdentity: {
      loaded_at: "2026-08-29T08:34:12.649Z",
      artifact_sha256: "artifact",
      module_version_id: "mvid",
      platform_source_revision: "platform",
      platform_source_digest_sha256: "platform-digest",
      connector_source_revision: "connector",
      annotator_source_revision: "annotator",
      live_ui_source_revision: "live-ui"
    },
    liveUiIdentity: {
      artifact_sha256: "artifact",
      module_version_id: "mvid",
      source_revision: "live-ui",
      source_digest_sha256: "live-ui-digest"
    },
    uiPanelReady: true,
    gameProcessIds: ["454"]
  };
}

test("Ready without a recording session is coherent loaded evidence", () => {
  assert.deepEqual(evaluateLoadedEvidence(fixture()), { ready: true, errors: [] });
});

test("process records expose exact PIDs on Unix and Windows", () => {
  assert.deepEqual(extractGameProcessIds([
    "454 /Applications/Slay the Spire 2",
    "not-a-process"
  ], "darwin"), ["454"]);
  assert.deepEqual(extractGameProcessIds([
    '"SlayTheSpire2.exe","4242","Console","1","123,456 K"'
  ], "win32"), ["4242"]);
});

test("runtime status is bound to the loaded generation rather than a heartbeat age", () => {
  const evidence = fixture();
  evidence.status.observed_at = "2026-08-29T08:34:12.648Z";
  assert.deepEqual(evaluateLoadedEvidence(evidence), {
    ready: false,
    errors: ["annotator_runtime_generation_mismatch"]
  });
});

test("runtime status must belong to a current game process", () => {
  const evidence = fixture();
  evidence.gameProcessIds = ["900"];
  assert.deepEqual(evaluateLoadedEvidence(evidence), {
    ready: false,
    errors: ["annotator_runtime_process_mismatch"]
  });
});

test("a present recording environment is an additional exact consistency gate", () => {
  const evidence = fixture();
  evidence.status.environment = {
    connector: { sha256: "old", module_version_id: "mvid", source_revision: "connector" },
    annotator: { sha256: "artifact", module_version_id: "mvid", source_revision: "annotator" },
    modset_status: "exact_platform_modset"
  };
  const result = evaluateLoadedEvidence(evidence);
  assert.equal(result.ready, false);
  assert.deepEqual(result.errors, ["recording_connector_artifact_mismatch"]);
});

test("source, execution and exact Modset authorities fail closed independently", () => {
  const evidence = fixture();
  evidence.capabilities.host.implementation.source_revision = "other";
  evidence.capabilities.execution_available = false;
  evidence.capabilities.game.modset.loaded_mod_ids = ["STS2_PLATFORM", "OTHER"];
  assert.deepEqual(evaluateLoadedEvidence(evidence), {
    ready: false,
    errors: [
      "connector_capabilities_source_revision_mismatch",
      "connector_execution_not_available",
      "unified_modset_membership_mismatch"
    ]
  });
});
