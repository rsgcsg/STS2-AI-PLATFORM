import { describe, expect, it, vi } from "vitest";
import {
  decodePlayerSnapshot,
  EnvironmentControllerSession,
  PlayerEnvironmentRestClient,
  prefetchPlayerEnvironmentDecisionBundle,
  SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL
} from "../src/index.js";

function snapshot() {
  return {
    protocol_version: SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
    schema: "sts2.player-environment/snapshot-1",
    snapshot_id: "snapshot-1",
    sequence: 1,
    observed_at: "2026-08-13T00:00:00Z",
    status: "interactive",
    persistent: null,
    interaction: {
      interaction_id: "interaction-1",
      kind: "menu",
      stage: "ready",
      prompt: "Choose",
      content_schema: "sts2.player-environment/surface/menu-1",
      content: { surface: { kind: "menu" }, context: { kind: "menu" } },
      capabilities: [{
        verb: "activate",
        subject_role: "control",
        arguments: [],
        availability_basis: "current_native_interaction"
      }]
    },
    referents: [{
      referent_id: "control-1",
      role: "control",
      kind: "control",
      label: "Continue",
      state: { visible: true, enabled: true, observation_basis: "native_visible_fact" },
      properties_schema: null,
      properties: null
    }],
    bound_actions: {
      schema: "sts2.player-environment/bound-actions-1",
      status: "complete",
      materialized_count: 1,
      total_count: 1,
      limit: 512,
      ordering_semantics: "deterministic",
      actions: [{
        bound_action_id: "bound-1",
        verb: "activate",
        interaction_id: "interaction-1",
        subject_referent_id: "control-1",
        arguments: [],
        label: "Continue"
      }]
    },
    reads: [],
    completeness: {
      status: "complete",
      visible_information: "complete",
      interaction_discovery: "exact",
      missing: [],
      hidden_by_policy: ["hidden_rng"]
    },
    session: { runtime_instance_id: "runtime-1", environment_fingerprint: "environment-1" },
    information_policy: {
      id: "player_visible",
      scope: "player_environment",
      includes_hidden_information: false,
      unknown_field_behavior: "fail_closed"
    }
  };
}

describe("Player Environment client", () => {
  it("strictly decodes a complete finite current action projection", () => {
    expect(decodePlayerSnapshot(snapshot()).data.bound_actions.actions[0]?.bound_action_id)
      .toBe("bound-1");
  });

  it("rejects action operands that are not current visible referents", () => {
    const value = snapshot();
    value.bound_actions.actions[0]!.subject_referent_id = "hidden-control";
    expect(() => decodePlayerSnapshot(value)).toThrow(/current referent/u);
  });

  it("sends state-bound read requests without creating game semantics", async () => {
    const fetchImpl = vi.fn(async () => new Response(JSON.stringify({
      protocol_version: SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
      schema: "sts2.player-environment/read-1",
      read_id: "read:run_deck",
      expected_snapshot_id: "snapshot-1",
      observed_snapshot_id: "snapshot-1",
      observed_at: "2026-08-13T00:00:00Z",
      kind: "run_deck",
      target_referent_id: null,
      visibility_basis: "player_openable_run_deck_view",
      ordering_semantics: "unordered_multiset",
      content_schema: "sts2.player-environment/read/run_deck-1",
      content: { kind: "run_deck", card_count: 0, cards: [] },
      completeness: {
        status: "complete",
        visible_information: "complete",
        interaction_discovery: "read_only",
        missing: [],
        hidden_by_policy: []
      },
      session: { runtime_instance_id: "runtime-1", environment_fingerprint: "environment-1" },
      information_policy: {
        id: "player_visible",
        scope: "player_environment",
        includes_hidden_information: false,
        unknown_field_behavior: "fail_closed"
      }
    }), { status: 200, headers: { "content-type": "application/json" } }));
    const client = new PlayerEnvironmentRestClient("http://127.0.0.1:15526", 1000, fetchImpl as typeof fetch);
    await client.read("read:run_deck", "snapshot-1");
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://127.0.0.1:15526/api/player-environment/reads/read%3Arun_deck?expected_snapshot_id=snapshot-1",
      expect.objectContaining({ method: "GET" })
    );
  });

  it("aggregates only coherent advertised Reads for memoryless consumers", async () => {
    const observation = decodePlayerSnapshot({
      ...snapshot(),
      reads: [{
        read_id: "read:run_deck",
        kind: "run_deck",
        target_referent_id: null,
        content_schema: "sts2.player-environment/read/run_deck-1",
        visibility_basis: "player_openable_run_deck_view",
        snapshot_bound: true,
        ordering_semantics: "unordered_multiset",
        hidden_by_policy: []
      }]
    }).data;
    const coherentRead = {
      protocol_version: SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
      schema: "sts2.player-environment/read-1" as const,
      read_id: "read:run_deck",
      expected_snapshot_id: "snapshot-1",
      observed_snapshot_id: "snapshot-1",
      observed_at: "2026-08-13T00:00:00Z",
      kind: "run_deck",
      target_referent_id: null,
      visibility_basis: "player_openable_run_deck_view",
      ordering_semantics: "unordered_multiset",
      content_schema: "sts2.player-environment/read/run_deck-1",
      content: { kind: "run_deck", card_count: 0, cards: [] },
      completeness: {
        status: "complete" as const,
        visible_information: "complete",
        interaction_discovery: "read_only",
        missing: [],
        hidden_by_policy: []
      },
      session: { runtime_instance_id: "runtime-1", environment_fingerprint: "environment-1" },
      information_policy: {
        id: "player_visible",
        scope: "player_environment",
        includes_hidden_information: false as const,
        unknown_field_behavior: "fail_closed"
      }
    };

    const bundle = await prefetchPlayerEnvironmentDecisionBundle(
      observation,
      async () => coherentRead
    );
    expect(bundle.reads).toEqual([coherentRead]);
    await expect(prefetchPlayerEnvironmentDecisionBundle(
      observation,
      async () => ({ ...coherentRead, observed_snapshot_id: "snapshot-stale" })
    )).rejects.toThrow(/not coherent/u);
  });

  it("decodes a stale not-delivered receipt returned with HTTP 409", async () => {
    const successor = snapshot();
    successor.snapshot_id = "snapshot-2";
    successor.sequence = 2;
    const fetchImpl = vi.fn(async () => new Response(JSON.stringify({
      protocol_version: SUPPORTED_PLAYER_ENVIRONMENT_PROTOCOL,
      schema: "sts2.player-environment/receipt-1",
      request_id: "request-stale",
      delivery: "not_delivered",
      action: {
        bound_action_id: "bound-1",
        verb: "activate",
        subject_referent_id: "control-1",
        arguments: []
      },
      reason_code: "stale_snapshot",
      detail: "Obtain a fresh snapshot.",
      retry: { allowed: true, reason: "fresh_snapshot_required" },
      successor
    }), {
      status: 409,
      headers: { "content-type": "application/json" }
    }));
    const client = new PlayerEnvironmentRestClient(
      "http://127.0.0.1:15526",
      1000,
      fetchImpl as typeof fetch
    );

    const receipt = await client.submit({
      requestId: "request-stale",
      expectedSnapshotId: "snapshot-1",
      boundActionId: "bound-1",
      clientSessionId: "client-1",
      controllerLeaseId: "lease-1",
      controllerGeneration: 1
    });

    expect(receipt.data.delivery).toBe("not_delivered");
    expect(receipt.data.reason_code).toBe("stale_snapshot");
    expect(receipt.data.successor?.snapshot_id).toBe("snapshot-2");
  });

  it("still rejects an ordinary HTTP 409 error payload", async () => {
    const fetchImpl = vi.fn(async () => new Response(JSON.stringify({
      error: "controller_lease_held"
    }), {
      status: 409,
      headers: { "content-type": "application/json" }
    }));
    const client = new PlayerEnvironmentRestClient(
      "http://127.0.0.1:15526",
      1000,
      fetchImpl as typeof fetch
    );

    await expect(client.submit({
      requestId: "request-conflict",
      expectedSnapshotId: "snapshot-1",
      boundActionId: "bound-1",
      clientSessionId: "client-1",
      controllerLeaseId: "lease-1",
      controllerGeneration: 1
    })).rejects.toMatchObject({ statusCode: 409 });
  });

  it("requires the consumer to own its controller identity", async () => {
    const environment = {
      registerClient: vi.fn(async (input: { clientInstanceId: string }) => ({
        raw: {},
        data: {
          runtime_instance_id: "runtime-1",
          client: {
            client_session_id: "session-1",
            client_instance_id: input.clientInstanceId
          },
          controller: null
        }
      })),
      acquireController: vi.fn(),
      renewController: vi.fn(),
      releaseController: vi.fn()
    };
    const controller = new EnvironmentControllerSession(environment, {
      productId: "test-consumer",
      productName: "Test Consumer",
      productVersion: "1.2.3",
      clientInstanceId: "test-consumer-instance"
    });
    await controller.register(
      { runtime_instance_id: "runtime-1" },
      { recommended_renewal_ms: 10_000 }
    );
    expect(environment.registerClient).toHaveBeenCalledWith({
      clientInstanceId: "test-consumer-instance",
      productId: "test-consumer",
      productName: "Test Consumer",
      productVersion: "1.2.3"
    });
    await controller.close();
  });
});
