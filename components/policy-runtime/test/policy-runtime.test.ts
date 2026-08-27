import { describe, expect, it } from "vitest";
import { mkdtemp, readFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import type { PlayerEnvironmentBoundAction, PlayerEnvironmentReceipt, PlayerEnvironmentSnapshot } from "@rsgcsg/sts2-connector-client";
import { admitWholeDecision } from "../src/admission.js";
import { candidateOrderDigest } from "../src/digest.js";
import { PolicyRuntime, admitWholeDecisionBundle } from "../src/runtime.js";
import { ConnectorPolicyClient } from "../src/connector.js";
import { DEFAULT_POLICY_ADAPTER_STARTUP_TIMEOUT_MS, NdjsonPolicyPort } from "../src/policy-port.js";
import { validateAdapterDecision, validatePolicyDecision, validatePolicyManifest, type ConnectorAdapterClient, type DecisionBundle, type PolicyConnector, type PolicyManifest } from "../src/contracts.js";
import { startPolicyRuntimeHttpServer } from "../src/server.js";
import { AgentRunEvidence, verifyEvidenceDirectory } from "../src/evidence.js";

const manifest = (): PolicyManifest => ({
  schema: "sts2.policy-runtime/policy-manifest-1",
  manifest_id: "manifest-test",
  policy: { id: "policy-test", version: "1", provider: "fixture", architecture: "fixture" },
  adapter: { id: "stpd-decision-only", version: "1", protocol: "sts2.policy-runtime/decision-only-ndjson-1", code_sha256: "c".repeat(64) },
  artifact: { id: "artifact-test", path: "artifact.bin", sha256: "a".repeat(64) },
  representation: { id: "snapshot-representation", version: "1", input_schema: "sts2.player-environment/snapshot-1" },
  requirements: { connector_protocol_version: "1.0.0", environment: { host_kind: "test", connector_version: "1", connector_source_revision: "source", connector_artifact_sha256: "b".repeat(64), connector_module_version_id: "mvid", modset_status: "exact", modset_fingerprint: "modset", loaded_mod_ids: ["fixture-mod"] }, reads: [], whole_decision_admission: true, candidate_order_digest: "sha256-json-bound-action-id-order", score_count_matches_candidate_count: true, selected_index: true, successor_required: true },
  support: { game_versions: ["fixture-game"], game_commits: ["fixture-commit"], interaction_kinds: ["test"], action_verbs: ["end_turn"] },
  adapter_config: {},
  claims: { full_run: false, selector: false, catalog_filtered: false, creates_action_authority: false, creates_native_operands: false }
});

const action = (id: string): PlayerEnvironmentBoundAction => ({ bound_action_id: id, verb: "end_turn", interaction_id: "interaction", arguments: [], label: id });
const snapshot = (ids: string[], snapshotId = "snapshot-1", status: PlayerEnvironmentSnapshot["status"] = "interactive", sequence = 1): PlayerEnvironmentSnapshot => ({
  protocol_version: "1.0.0", schema: "sts2.player-environment/snapshot-1", snapshot_id: snapshotId, sequence, observed_at: "2026-08-25T00:00:00.000Z", status, persistent: null,
  interaction: { interaction_id: "interaction", kind: "test", stage: "ready", content_schema: "sts2.player-environment/surface/test-1", content: { surface: { kind: "test" }, context: { kind: "test" } }, capabilities: [] },
  referents: [], bound_actions: { schema: "sts2.player-environment/bound-actions-1", status: "complete", materialized_count: ids.length, total_count: ids.length, limit: ids.length || 1, ordering_semantics: "connector_order", actions: ids.map(action) }, reads: [],
  completeness: { status: "complete", visible_information: "test", interaction_discovery: "test", missing: [], hidden_by_policy: [] }, session: { runtime_instance_id: "runtime", environment_fingerprint: "environment" }, information_policy: { id: "test", scope: "test", includes_hidden_information: false, unknown_field_behavior: "reject" }
});
const bundle = (ids: string[], snapshotId?: string): DecisionBundle => ({ observation: snapshot(ids, snapshotId), reads: [] });

function decisionFor(current: DecisionBundle, selectedIndex: number | null = 0) {
  const digest = candidateOrderDigest(current.observation.bound_actions.actions);
  return { schema: "sts2.policy-runtime/decision-1" as const, decision_id: "decision-1", run_id: "run-1", manifest_id: "manifest-test", snapshot_id: current.observation.snapshot_id, candidate_digest: digest, candidate_count: current.observation.bound_actions.actions.length, scores: current.observation.bound_actions.actions.map((_action, index) => index), selected_index: selectedIndex, disposition: selectedIndex === null ? "abstain" as const : "admit" as const, issued_at: "2026-08-25T00:00:00.000Z" };
}

describe("strict policy contracts", () => {
  it("keeps mode and catalog out of the Policy Manifest", () => {
    const valid = manifest();
    expect(validatePolicyManifest(valid)).toEqual(valid);
    expect(() => validatePolicyManifest({ ...valid, mode: "auto" })).toThrow();
    expect(() => validatePolicyManifest({ ...valid, catalog: [] })).toThrow();
    expect(() => validatePolicyManifest({ ...valid, requirements: { ...valid.requirements, reads: ["run_deck", "run_deck"] } })).toThrow(/duplicates/);
    expect(() => validatePolicyManifest({ ...valid, requirements: { ...valid.requirements, environment: { ...valid.requirements.environment, connector_artifact_sha256: "invalid" } } })).toThrow(/SHA-256/);
    expect(() => validatePolicyManifest({ ...valid, requirements: { ...valid.requirements, environment: { ...valid.requirements.environment, loaded_mod_ids: ["fixture-mod", "fixture-mod"] } } })).toThrow(/duplicates/);
    expect(() => validatePolicyManifest({ ...valid, support: { ...valid.support, action_verbs: [] } })).toThrow(/must not be empty/);
    expect(() => validatePolicyManifest({ ...valid, support: { ...valid.support, interaction_kinds: ["test", "test"] } })).toThrow(/duplicates/);
  });

  it("uses a decision-only shape with no catalog or bound action", () => {
    const value = decisionFor(bundle(["a", "b"]), 1);
    const validated = validatePolicyDecision(value);
    expect(validated.selected_index).toBe(1);
    expect(validated).not.toHaveProperty("catalog");
    expect(validated).not.toHaveProperty("bound_action_id");
  });
});

describe("candidate order admission", () => {
  it("changes digest when the Connector order changes", () => {
    expect(candidateOrderDigest(["a", "b"])).not.toBe(candidateOrderDigest(["b", "a"]));
  });

  it.each([
    ["digest", (d: ReturnType<typeof decisionFor>) => ({ ...d, candidate_digest: "b".repeat(64) })],
    ["count", (d: ReturnType<typeof decisionFor>) => ({ ...d, candidate_count: 1 })],
    ["index", (d: ReturnType<typeof decisionFor>) => ({ ...d, selected_index: 2 })]
  ])("rejects %s drift", (_name, mutate) => {
    const current = bundle(["a", "b"]);
    const decision = mutate(decisionFor(current));
    expect(() => admitWholeDecision(decision, current, manifest(), "run-1")).toThrow();
  });

  it("rejects a reordered current bundle even when the adapter reuses the old digest", () => {
    const original = bundle(["a", "b"]);
    const reordered = bundle(["b", "a"]);
    expect(() => admitWholeDecision(decisionFor(original, 0), reordered, manifest(), "run-1")).toThrow(/digest/);
  });

  it("resolves the selected bound action only from the current bundle order", () => {
    const current = bundle(["a", "b"]);
    const admitted = admitWholeDecision(decisionFor(current, 1), current, manifest(), "run-1");
    expect(admitted.boundAction?.bound_action_id).toBe("b");
  });
});

describe("Connector Read materialization", () => {
  it("fetches only manifest-required advertised Reads and rejects unavailable requirements", async () => {
    const observed = {
      ...snapshot(["a"]),
      reads: [
        { read_id: "read-deck", kind: "run_deck", content_schema: "sts2.player-environment/read/run_deck-1", visibility_basis: "native_visible_fact", snapshot_bound: true as const, ordering_semantics: "native", hidden_by_policy: [] },
        { read_id: "read-piles", kind: "combat_piles", content_schema: "sts2.player-environment/read/combat_piles-1", visibility_basis: "native_visible_fact", snapshot_bound: true as const, ordering_semantics: "native", hidden_by_policy: [] }
      ]
    } satisfies PlayerEnvironmentSnapshot;
    const fetched: string[] = [];
    const client = {
      observe: async () => ({ raw: {}, data: observed }),
      read: async (readId: string, expectedSnapshotId: string) => {
        fetched.push(readId);
        const descriptor = observed.reads.find((read) => read.read_id === readId);
        if (!descriptor) throw new Error("unknown read");
        return { raw: {}, data: {
          protocol_version: "1.0.0", schema: "sts2.player-environment/read-1", read_id: readId,
          expected_snapshot_id: expectedSnapshotId, observed_snapshot_id: expectedSnapshotId,
          observed_at: "2026-08-25T00:00:00.000Z", kind: descriptor.kind,
          visibility_basis: descriptor.visibility_basis, ordering_semantics: descriptor.ordering_semantics,
          content_schema: descriptor.content_schema, content: {},
          completeness: { status: "complete", visible_information: "fixture", interaction_discovery: "fixture", missing: [], hidden_by_policy: [] },
          session: observed.session, information_policy: observed.information_policy
        } };
      }
    } as unknown as ConnectorAdapterClient;
    const connector = new ConnectorPolicyClient(client);
    const selected = await connector.observeBundle(["combat_piles"]);
    expect(fetched).toEqual(["read-piles"]);
    expect(selected.reads.map((read) => read.kind)).toEqual(["combat_piles"]);
    await expect(connector.observeBundle(["shop_catalog"])).rejects.toThrow("required_read_unavailable:shop_catalog");
  });

  it("submits with the latest renewed controller generation", async () => {
    let generation = 1;
    let submittedGeneration: number | null = null;
    const lease = () => ({
      runtime_instance_id: "runtime",
      controller: {
        controller_lease_id: "lease",
        controller_generation: generation,
        client_session_id: "client-session",
        expires_at: new Date(Date.now() + 1_000).toISOString()
      }
    });
    const client = {
      capabilities: async () => ({ raw: {}, data: {
        ...(await new FakeConnector(bundle(["a"])).capabilities()),
        control: { recommended_renewal_ms: 10_000 }
      } }),
      registerClient: async (input: { clientInstanceId: string }) => ({ raw: {}, data: {
        runtime_instance_id: "runtime",
        client: { client_session_id: "client-session", client_instance_id: input.clientInstanceId },
        controller: null
      } }),
      acquireController: async () => ({ raw: {}, data: lease() }),
      renewController: async () => { generation += 1; return { raw: {}, data: lease() }; },
      releaseController: async () => ({ raw: {}, data: { runtime_instance_id: "runtime", controller: null } }),
      submit: async (input: { controllerGeneration: number; requestId: string; boundActionId: string }) => {
        submittedGeneration = input.controllerGeneration;
        return { raw: {}, data: {
          protocol_version: "1.0.0",
          schema: "sts2.player-environment/receipt-1",
          request_id: input.requestId,
          delivery: "not_delivered",
          action: { bound_action_id: input.boundActionId, verb: "end_turn", arguments: [] },
          retry: { allowed: false, reason: "fixture" },
          successor: null
        } };
      }
    } as unknown as ConnectorAdapterClient;
    const connector = new ConnectorPolicyClient(client);

    await connector.acquireController();
    await connector.submit({ requestId: "request", expectedSnapshotId: "snapshot", boundActionId: "a" });

    expect(generation).toBe(2);
    expect(submittedGeneration).toBe(2);
    await connector.releaseController();
  });
});

describe("decision-only process boundary", () => {
  it("allows a bounded model startup window without weakening attestation", () => {
    expect(DEFAULT_POLICY_ADAPTER_STARTUP_TIMEOUT_MS).toBe(30_000);
  });

  it("drains bounded child diagnostics without blocking a decision", async () => {
    const script = [
      "process.stderr.write('x'.repeat(100000));",
      `process.stdout.write(JSON.stringify({schema:'sts2.policy-runtime/policy-port-1',message_type:'ready',adapter:${JSON.stringify(manifest().adapter)}})+'\\n');`,
      "const readline=require('node:readline').createInterface({input:process.stdin});",
      "readline.on('line',(line)=>{const request=JSON.parse(line);process.stdout.write(JSON.stringify({schema:request.schema,message_type:'decision',request_id:request.request_id,output:{candidate_digest:request.input.candidate_digest,scores:Array(request.input.candidate_count).fill(1),selected_index:0}})+'\\n');});"
    ].join("");
    const port = NdjsonPolicyPort.spawn(process.execPath, ["-e", script]);
    try {
      await expect(port.ready()).resolves.toEqual(manifest().adapter);
      const current = bundle(["a"]);
      const digest = candidateOrderDigest(current.observation.bound_actions.actions);
      await expect(port.decide({ run_id: "run-port", manifest: manifest(), bundle: current, candidate_digest: digest, candidate_count: 1 })).resolves.toEqual({ candidate_digest: digest, scores: [1], selected_index: 0 });
    } finally { port.close(); }
  });

  it("rejects adapter startup identity drift before any decision", async () => {
    const drifted = { ...manifest().adapter, code_sha256: "d".repeat(64) };
    const script = `process.stdout.write(JSON.stringify({schema:'sts2.policy-runtime/policy-port-1',message_type:'ready',adapter:${JSON.stringify(drifted)}})+'\\n');setInterval(()=>{},1000);`;
    const port = NdjsonPolicyPort.spawn(process.execPath, ["-e", script]);
    try {
      await expect(port.attest(manifest().adapter)).rejects.toThrow(/differs from Policy Manifest/);
    } finally { port.close(); }
  });
});

class FakeConnector implements PolicyConnector {
  acquireCount = 0;
  releaseCount = 0;
  submitCount = 0;
  observeCount = 0;
  stale = true;
  nextSequence = 2;
  observationQueue: DecisionBundle[] = [];
  requiredReadRequests: string[][] = [];
  receiptRequestIdOverride?: string;
  receiptBoundActionIdOverride?: string;
  constructor(public current: DecisionBundle, private readonly delivery: PlayerEnvironmentReceipt["delivery"] = "delivered", private readonly submitFailure?: Error) {}
  async capabilities() {
    return {
      protocol_version: "1.0.0", snapshot_schema: "sts2.player-environment/snapshot-1", action_schema: "sts2.player-environment/action-1", receipt_schema: "sts2.player-environment/receipt-1", control_schema: "sts2.player-environment/control-1", status: "ready",
      host: { id: "fixture", name: "fixture", version: "1", runtime_instance_id: "runtime", host_kind: "test", implementation: { source_revision: "source", module_version_id: "mvid", artifact_sha256: "b".repeat(64) } },
      game: { version: "fixture-game", commit: "fixture-commit", branch: null, main_assembly_hash: null, compatibility: { status: "exact", observation_allowed: true, detail: "fixture" }, modset: { status: "exact", fingerprint: "modset", scope: "fixture", loaded_mod_ids: ["fixture-mod"], detail: "fixture" } },
      environment_fingerprint: "environment", verbs: ["end_turn"], snapshot_bound: true, single_controller: true, execution_available: true, control: { recommended_renewal_ms: 1000 }, evidence_profiles: [], non_claims: []
    } as Awaited<ReturnType<PolicyConnector["capabilities"]>>;
  }
  async observeBundle(requiredReadKinds: readonly string[]) { this.observeCount += 1; this.requiredReadRequests.push([...requiredReadKinds]); if (this.stale) { this.stale = false; throw Object.assign(new Error("stale_state"), { code: "stale_state" }); } return this.observationQueue.shift() ?? this.current; }
  async acquireController() { this.acquireCount += 1; }
  async releaseController() { this.releaseCount += 1; }
  async submit(input: { requestId: string; expectedSnapshotId: string; boundActionId: string }) {
    this.submitCount += 1;
    if (this.submitFailure) throw this.submitFailure;
    const successor = this.delivery === "delivered" ? snapshot([`next-${this.nextSequence}`], `snapshot-${this.nextSequence}`, "interactive", this.nextSequence) : null;
    if (successor) { this.current = { observation: successor, reads: [] }; this.nextSequence += 1; }
    return { protocol_version: "1.0.0", schema: "sts2.player-environment/receipt-1", request_id: this.receiptRequestIdOverride ?? input.requestId, delivery: this.delivery, action: { bound_action_id: this.receiptBoundActionIdOverride ?? input.boundActionId, verb: "end_turn", arguments: [] }, retry: { allowed: false, reason: "test" }, successor } as PlayerEnvironmentReceipt;
  }
}

describe("runtime integration fake", () => {
  const environmentDriftCases: Array<[string, string, (value: PolicyManifest) => void]> = [
    ["host_kind", "environment_host_kind_drift", (value) => { value.requirements.environment.host_kind = "headless"; }],
    ["connector version", "environment_connector_version_drift", (value) => { value.requirements.environment.connector_version = "different"; }],
    ["connector source", "environment_connector_source_revision_drift", (value) => { value.requirements.environment.connector_source_revision = "different"; }],
    ["connector artifact SHA", "environment_connector_artifact_sha256_drift", (value) => { value.requirements.environment.connector_artifact_sha256 = "c".repeat(64); }],
    ["connector MVID", "environment_connector_module_version_id_drift", (value) => { value.requirements.environment.connector_module_version_id = "different"; }],
    ["modset status", "environment_modset_status_drift", (value) => { value.requirements.environment.modset_status = "different"; }],
    ["modset fingerprint", "environment_modset_fingerprint_drift", (value) => { value.requirements.environment.modset_fingerprint = "different"; }],
    ["loaded mod IDs", "environment_loaded_mod_ids_drift", (value) => { value.requirements.environment.loaded_mod_ids = ["different-mod"]; }]
  ];

  it.each(environmentDriftCases)("fails closed before observe and scoring on %s drift", async (_field, reason, mutate) => {
    const connector = new FakeConnector(bundle(["a"]));
    const pinned = manifest();
    mutate(pinned);
    let scored = false;
    const runtime = new PolicyRuntime({ manifest: pinned, connector, mode: "auto", runId: "run-1", policy: () => { scored = true; return { candidate_digest: "a".repeat(64), scores: [1], selected_index: 0 }; } });
    const result = await runtime.tick();
    expect(result.type).toBe("not_admitted");
    if (result.type === "not_admitted") expect(result.reason).toBe(reason);
    expect(connector.observeCount).toBe(0);
    expect(scored).toBe(false);
    expect(connector.acquireCount).toBe(0);
    expect(connector.submitCount).toBe(0);
  });

  it("publishes host kind and loaded mod IDs in the admitted environment status", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "shadow", runId: "run-identity", staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    expect((await runtime.tick()).type).toBe("shadow");
    expect(runtime.status().environment).toMatchObject({ host_kind: "test", loaded_mod_ids: ["fixture-mod"] });
  });

  it("requests only the Reads declared by the Policy Manifest", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const withReads = manifest();
    withReads.requirements.reads = ["combat_piles"];
    const runtime = new PolicyRuntime({ manifest: withReads, connector, mode: "shadow", runId: "run-1", staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    expect((await runtime.tick()).type).toBe("shadow");
    expect(connector.requiredReadRequests).toEqual([["combat_piles"], ["combat_piles"]]);
  });

  it("scores a Shadow snapshot once and waits for a successor snapshot", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    let scoreCount = 0;
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "shadow", runId: "run-1", staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, policy: (input) => { scoreCount += 1; return { candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }; } });
    expect((await runtime.tick()).type).toBe("shadow");
    const duplicate = await runtime.tick();
    expect(duplicate.type).toBe("not_admitted");
    if (duplicate.type === "not_admitted") expect(duplicate.reason).toBe("snapshot_already_scored");
    expect(scoreCount).toBe(1);
    connector.current = bundle(["b"], "snapshot-2");
    expect((await runtime.tick()).type).toBe("shadow");
    expect(scoreCount).toBe(2);
  });

  it("hands Auto back to Human when the policy abstains", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "auto", runId: "run-1", staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: null }) });
    expect((await runtime.tick()).type).toBe("not_executed");
    expect(runtime.status().mode).toBe("human");
    expect(connector.submitCount).toBe(0);
  });

  it("fails closed before scoring when the exact game identity is unsupported", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const unsupported = manifest();
    unsupported.support.game_commits = ["different-commit"];
    let scored = false;
    const runtime = new PolicyRuntime({ manifest: unsupported, connector, mode: "auto", runId: "run-1", policy: () => { scored = true; return { candidate_digest: "a".repeat(64), scores: [1], selected_index: 0 }; } });
    const result = await runtime.tick();
    expect(result.type).toBe("not_admitted");
    if (result.type === "not_admitted") expect(result.reason).toBe("game_commit_unsupported");
    expect(scored).toBe(false);
    expect(connector.acquireCount).toBe(0);
    expect(connector.submitCount).toBe(0);
  });

  it("refreshes a whole stale bundle and submits only the current indexed action", async () => {
    const connector = new FakeConnector(bundle(["a", "b"]));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "one_step", runId: "run-1", staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, sleep: async () => {}, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [0, 1], selected_index: 1 }) });
    const result = await runtime.tick();
    expect(result.type).toBe("delivered");
    if (result.type === "delivered") expect(result.bound_action.bound_action_id).toBe("b");
    expect(connector.submitCount).toBe(1);
    expect(connector.acquireCount).toBe(1);
    expect(connector.releaseCount).toBe(1);
  });

  it("taints and releases after unknown delivery without retry", async () => {
    const connector = new FakeConnector(bundle(["a"]), "unknown");
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "auto", runId: "run-1", policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    const result = await runtime.tick();
    expect(result.type).toBe("unknown");
    expect(connector.submitCount).toBe(1);
    expect(connector.releaseCount).toBe(1);
    expect(runtime.status().tainted).toBe(true);
    expect((await runtime.tick()).type).toBe("not_admitted");
    expect(connector.submitCount).toBe(1);
    await expect(runtime.setMode("auto")).rejects.toThrow(/tainted/);
  });

  it("treats a mismatched Receipt as unknown and never retries", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    connector.receiptRequestIdOverride = "different-request";
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "auto", runId: "run-receipt-drift", policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });

    const result = await runtime.tick();

    expect(result.type).toBe("unknown");
    expect(runtime.status().tainted).toBe(true);
    expect(runtime.status().taint_reason).toBe("receipt_correlation_failed");
    expect(connector.submitCount).toBe(1);
    expect((await runtime.tick()).type).toBe("not_admitted");
    expect(connector.submitCount).toBe(1);
  });

  it("does not acquire on auto mode and applies support to the whole catalog", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "human", runId: "run-1", policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    await runtime.setMode("auto");
    expect(connector.acquireCount).toBe(0);
    connector.current = { observation: { ...snapshot(["a"]), interaction: { ...snapshot(["a"]).interaction, kind: "unsupported" } }, reads: [] };
    const result = await runtime.tick();
    expect(result.type).toBe("not_admitted");
    expect(runtime.status().mode).toBe("human");
    expect(connector.acquireCount).toBe(0);
    expect(connector.releaseCount).toBe(0);
  });

  it("fails closed on policy error before any controller mutation", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "auto", runId: "run-1", policy: () => { throw new Error("adapter offline"); } });
    const result = await runtime.tick();
    expect(result.type).toBe("not_admitted");
    expect(connector.acquireCount).toBe(0);
    expect(connector.submitCount).toBe(0);
    expect(runtime.status().mode).toBe("human");
    expect(runtime.status().errors.at(-1)).toContain("policy_failed");
  });

  it("cancels an active scored tick when Human mode is requested before submit", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    let resolvePolicy: ((decision: { candidate_digest: string; scores: number[]; selected_index: number }) => void) | undefined;
    let resolveStarted: (() => void) | undefined;
    let expectedDigest = "";
    const policyStarted = new Promise<void>((resolve) => { resolveStarted = resolve; });
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "auto", runId: "run-cancel", policy: (input) => {
      expectedDigest = input.candidate_digest;
      resolveStarted!();
      return new Promise((resolveDecision) => { resolvePolicy = resolveDecision; });
    } });
    const tick = runtime.tick();
    await policyStarted;
    const human = runtime.setMode("human");
    resolvePolicy!({ candidate_digest: expectedDigest, scores: [1], selected_index: 0 });

    const result = await tick;
    await human;

    expect(result.type).toBe("not_admitted");
    if (result.type === "not_admitted") expect(result.reason).toBe("mode_changed_before_submit");
    expect(runtime.status().mode).toBe("human");
    expect(connector.acquireCount).toBe(0);
    expect(connector.submitCount).toBe(0);
  });

  it("fails closed and returns Human when a policy process does not answer", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "auto", runId: "run-timeout", policyTimeoutMs: 5, policy: () => new Promise(() => {}) });
    const result = await runtime.tick();
    expect(result.type).toBe("not_admitted");
    expect(runtime.status().mode).toBe("human");
    expect(runtime.status().errors.at(-1)).toContain("policy decision timed out");
    expect(connector.acquireCount).toBe(0);
    expect(connector.submitCount).toBe(0);
  });

  it("treats submit exceptions as unknown and never retries them", async () => {
    const connector = new FakeConnector(bundle(["a"]), "delivered", new Error("connection lost after dispatch"));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "auto", runId: "run-1", policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    const result = await runtime.tick();
    expect(result.type).toBe("unknown");
    expect(runtime.status().tainted).toBe(true);
    expect(connector.submitCount).toBe(1);
    expect((await runtime.tick()).type).toBe("not_admitted");
    expect(connector.submitCount).toBe(1);
  });

  it("polls past a settling receipt successor before accepting stable readiness", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    connector.observationQueue = [bundle(["a"]), { observation: snapshot(["settling"], "snapshot-2", "settling", 2), reads: [] }, { observation: snapshot(["stable"], "snapshot-3", "interactive", 3), reads: [] }];
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "one_step", runId: "run-1", successorPoll: { maxAttempts: 3, baseBackoffMs: 0 }, staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, sleep: async () => {}, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    const result = await runtime.tick();
    expect(result.type).toBe("delivered");
    if (result.type === "delivered") expect(result.successor.snapshot_id).toBe("snapshot-3");
  });

  it("accepts a distinct non-interactive terminal successor without inventing a next decision", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    connector.observationQueue = [{ observation: snapshot(["a"], "snapshot-initial", "interactive", 1), reads: [] }, { observation: snapshot([], "snapshot-terminal", "observed", 2), reads: [] }];
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "one_step", runId: "run-1", successorPoll: { maxAttempts: 2, baseBackoffMs: 0 }, staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, sleep: async () => {}, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    const result = await runtime.tick();
    expect(result.type).toBe("delivered");
    if (result.type === "delivered") expect(result.successor.status).toBe("observed");
  });

  it("rejects a successor from a different runtime identity as unknown", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const drifted = {
      ...snapshot(["next"], "snapshot-2", "interactive", 2),
      session: { runtime_instance_id: "restarted-runtime", environment_fingerprint: "different-environment" }
    };
    connector.observationQueue = [bundle(["a"]), { observation: drifted, reads: [] }];
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "one_step", runId: "run-successor-drift", successorPoll: { maxAttempts: 2, baseBackoffMs: 0 }, staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, sleep: async () => {}, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });

    const result = await runtime.tick();

    expect(result.type).toBe("unknown");
    expect(runtime.status().taint_reason).toContain("successor_environment_identity_drift");
    expect(connector.submitCount).toBe(1);
  });

  it("serves typed status and bounded sequential auto ticks on loopback", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "human", runId: "run-1", staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, successorPoll: { maxAttempts: 2, baseBackoffMs: 0 }, sleep: async () => {}, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: Array(input.candidate_count).fill(1), selected_index: 0 }) });
    const service = await startPolicyRuntimeHttpServer(runtime, { port: 0, maxAutoTicks: 2 });
    try {
      const modeResponse = await fetch(`${service.address}/mode`, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ mode: "auto" }) });
      expect(modeResponse.status).toBe(200);
      expect((await modeResponse.json()).status.controller).toBe("released");
      const tickResponse = await fetch(`${service.address}/tick`, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ max_ticks: 2 }) });
      expect(tickResponse.status).toBe(200);
      const result = await tickResponse.json() as { results: unknown[] };
      expect(result.results.length).toBe(2);
      expect(connector.submitCount).toBe(2);
    } finally { await service.close(); }
  });

  it("continuously shadows only new Snapshots without acquiring a controller", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    let scoreCount = 0;
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "human", runId: "run-shadow", staleRefresh: { maxAttempts: 2, baseBackoffMs: 0 }, sleep: async () => {}, policy: (input) => { scoreCount += 1; return { candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }; } });
    const service = await startPolicyRuntimeHttpServer(runtime, { port: 0, autoDrive: true, maxAutoTicks: 2, autoIdleMs: 5 });
    try {
      const response = await fetch(`${service.address}/mode`, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ mode: "shadow" }) });
      expect(response.status).toBe(200);
      await eventually(() => scoreCount === 1);
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(scoreCount).toBe(1);
      connector.current = bundle(["b"], "snapshot-2");
      await eventually(() => scoreCount === 2);
      expect(connector.acquireCount).toBe(0);
      expect(connector.submitCount).toBe(0);
    } finally { await service.close(); }
  });

  it("can defer automatic policy work until startup identity is published", async () => {
    const connector = new FakeConnector(bundle(["a"]));
    let scoreCount = 0;
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "shadow", runId: "run-deferred", policy: (input) => { scoreCount += 1; return { candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }; } });
    const service = await startPolicyRuntimeHttpServer(runtime, { port: 0, autoDrive: true, deferAutoDrive: true, autoIdleMs: 5 });
    try {
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(scoreCount).toBe(0);
      service.startDriving();
      await eventually(() => scoreCount === 1);
    } finally { await service.close(); }
  });

  it("seals an immutable Agent run when stopped", async () => {
    const root = await mkdtemp(join(tmpdir(), "sts2-agent-run-"));
    const evidence = await AgentRunEvidence.create({ root, runId: "run-evidence", policyManifest: manifest(), runtimeVersion: "0.1.0-rc.1", runtimeCodeSha256: "e".repeat(64), mode: "shadow" });
    await evidence.attestAdapter(manifest().adapter);
    const runtime = new PolicyRuntime({ manifest: manifest(), connector: new FakeConnector(bundle(["a"])), mode: "shadow", runId: evidence.runId, evidence, runtimeIdentity: { version: "0.1.0-rc.1", code_sha256: "e".repeat(64) }, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    expect((await runtime.tick()).type).toBe("shadow");
    const stopped = await runtime.stop();
    expect(stopped.lifecycle).toBe("stopped");
    await verifyEvidenceDirectory(evidence.directory);
    const runManifest = JSON.parse(await readFile(join(evidence.directory, "manifest.json"), "utf8")) as { status: string; ended_at: string | null };
    expect(runManifest.status).toBe("stopped");
    expect(runManifest.ended_at).not.toBeNull();
    expect(JSON.parse(await readFile(join(evidence.directory, "policy-manifest.json"), "utf8"))).toEqual(manifest());
    const adapterAttestation = JSON.parse(await readFile(join(evidence.directory, "adapter-attestation.json"), "utf8")) as { status: string; expected: unknown; actual: unknown };
    expect(adapterAttestation.status).toBe("attested");
    expect(adapterAttestation.expected).toEqual(manifest().adapter);
    expect(adapterAttestation.actual).toEqual(manifest().adapter);
    const immutableManifest = JSON.parse(await readFile(join(evidence.directory, "evidence-manifest.json"), "utf8")) as { files: Array<{ path: string }> };
    expect(immutableManifest.files.map((entry) => entry.path)).toEqual([
      "adapter-attestation.json",
      "events.jsonl",
      "manifest.json",
      "policy-manifest.json"
    ]);
    expect((await runtime.stop()).lifecycle).toBe("stopped");
  });

  it("serializes concurrent Agent evidence appends before immutable finalization", async () => {
    const root = await mkdtemp(join(tmpdir(), "sts2-agent-concurrent-evidence-"));
    const evidence = await AgentRunEvidence.create({ root, runId: "run-concurrent", policyManifest: manifest(), runtimeVersion: "0.1.0-rc.1", runtimeCodeSha256: "e".repeat(64), mode: "shadow" });
    await evidence.attestAdapter(manifest().adapter);

    await Promise.all(Array.from({ length: 12 }, (_value, index) => evidence.append("parallel", { index })));
    await evidence.finalize({ status: "stopped", tainted: false, mode: "human" });

    const events = (await readFile(join(evidence.directory, "events.jsonl"), "utf8")).trim().split("\n").map((line) => JSON.parse(line) as { sequence: number });
    expect(events.map((event) => event.sequence)).toEqual(Array.from({ length: 12 }, (_value, index) => index + 1));
    await verifyEvidenceDirectory(evidence.directory);
  });

  it("releases an already-held controller when mode evidence fails", async () => {
    const root = await mkdtemp(join(tmpdir(), "sts2-agent-mode-failure-"));
    const evidence = await AgentRunEvidence.create({ root, runId: "run-mode-failure", policyManifest: manifest(), runtimeVersion: "0.1.0-rc.1", runtimeCodeSha256: "e".repeat(64), mode: "auto" });
    await evidence.attestAdapter(manifest().adapter);
    const connector = new FakeConnector(bundle(["a"]));
    const runtime = new PolicyRuntime({ manifest: manifest(), connector, mode: "auto", runId: evidence.runId, evidence, runtimeIdentity: { version: "0.1.0-rc.1", code_sha256: "e".repeat(64) }, policy: (input) => ({ candidate_digest: input.candidate_digest, scores: [1], selected_index: 0 }) });
    expect((await runtime.tick()).type).toBe("delivered");
    expect(runtime.status().controller).toBe("held");
    await evidence.finalize({ status: "stopped", tainted: false, mode: "auto" });

    await expect(runtime.setMode("auto")).rejects.toThrow(/evidence is unavailable/);
    expect(runtime.status().mode).toBe("human");
    expect(runtime.status().controller).toBe("released");
    expect(connector.releaseCount).toBe(1);
  });
});

async function eventually(predicate: () => boolean): Promise<void> {
  const deadline = Date.now() + 500;
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error("condition was not reached before timeout");
    await new Promise((resolve) => setTimeout(resolve, 5));
  }
}
