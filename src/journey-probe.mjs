import { randomUUID } from "node:crypto";
import { createWriteStream, mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { finished } from "node:stream/promises";
import {
  EnvironmentControllerSession,
  PlayerEnvironmentRestClient
} from "@rsgcsg/sts2-connector-client";
import { evaluateHeadlessCapabilities } from "./headless-host.mjs";
import {
  listGameProcesses,
  readJson,
  shippedRuntimeLaunch,
  stopChild,
  waitForEndpoint,
  waitForInteractiveSnapshot
} from "./runtime-probe.mjs";
import { readDiskIdentity } from "./game-installation.mjs";
import { evaluateRuntimeCompatibility } from "./compatibility.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { normalizedDecisionTiming, summarizeDurations } from "./decision-timing.mjs";
import { JsonlRecorder } from "./jsonl-recorder.mjs";
import {
  canonicalDecisionDigest,
  canonicalizeSelectedAction,
  canonicalizeSnapshot
} from "./semantic-decision.mjs";
import { publicProfileDescriptor, resolveLaunchProfile } from "./profile-isolation.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function actionWithLabel(actions, pattern) {
  return actions.find((action) => pattern.test(action.label));
}

function actionWithVerb(actions, ...verbs) {
  return actions.find((action) => verbs.includes(action.verb));
}

export function chooseBoundAction(snapshot, { tutorialPreference = "disable" } = {}) {
  if (snapshot?.status !== "interactive"
      || snapshot?.bound_actions?.status !== "complete"
      || snapshot.bound_actions.actions.length === 0) {
    return null;
  }
  const actions = snapshot.bound_actions.actions;
  const kind = snapshot.interaction.kind;
  const stage = snapshot.interaction.stage;

  if (kind === "main_menu") return actionWithLabel(actions, /single player/i) ?? actions[0];
  if (kind === "singleplayer_menu") {
    return actionWithLabel(actions, /standard|continue|resume/i)
      ?? actions.find((action) => !/back/i.test(action.label))
      ?? null;
  }
  if (kind === "character_select") {
    return actionWithLabel(actions, /embark/i)
      ?? actions.find((action) => action.verb === "select" && !/random/i.test(action.label))
      ?? null;
  }
  if (kind === "tutorial_preference") {
    if (!new Set(["enable", "disable"]).has(tutorialPreference)) {
      throw new Error(`Unsupported tutorial preference: ${tutorialPreference}.`);
    }
    return tutorialPreference === "disable"
      ? actionWithLabel(actions, /cancel|disable|no/i) ?? null
      : actionWithLabel(actions, /confirm|enable|yes/i) ?? null;
  }
  if (kind === "combat_turn") {
    return actionWithVerb(actions, "play")
      ?? actionWithVerb(actions, "end_turn")
      ?? actionWithVerb(actions, "use")
      ?? null;
  }
  if (kind === "shop_room") {
    return actionWithLabel(actions, /leave|map/i) ?? actionWithVerb(actions, "open") ?? null;
  }
  if (kind === "shop_inventory") return actionWithVerb(actions, "cancel", "close") ?? null;
  if (kind === "reward_claim") {
    return actionWithLabel(actions, /skip remaining|continue/i)
      ?? actionWithLabel(actions, /gold|金币/i)
      ?? actions[0];
  }
  if (kind === "card_reward_selection") {
    return actionWithLabel(actions, /skip|跳过/i) ?? actionWithVerb(actions, "select") ?? null;
  }
  if (kind === "rest_site") {
    return actionWithLabel(actions, /rest|heal|休息/i) ?? actions[0];
  }
  if (stage === "preview") return actionWithVerb(actions, "confirm") ?? actionWithVerb(actions, "cancel");

  return actionWithLabel(actions, /continue|proceed/i)
    ?? actionWithVerb(actions, "confirm")
    ?? actionWithVerb(actions, "select")
    ?? actionWithVerb(actions, "activate", "open", "skip")
    ?? actionWithVerb(actions, "end_turn")
    ?? actionWithVerb(actions, "cancel", "close")
    ?? null;
}

export const DEFAULT_JOURNEY_COVERAGE = Object.freeze({
  required_surfaces: Object.freeze([
    "main_menu",
    "map_navigation",
    "combat_turn"
  ]),
  required_surface_groups: Object.freeze([
    Object.freeze({
      id: "run_entry",
      any_of: Object.freeze(["singleplayer_menu", "character_select"])
    }),
    Object.freeze({
      id: "non_combat_decision",
      any_of: Object.freeze([
        "event_option",
        "reward_claim",
        "rest_site",
        "shop_room",
        "treasure_room"
      ])
    })
  ]),
  minimum_combat_deliveries: 3
});

export function evaluateJourneyIntegrity({ terminal, unknownCount, readFailures, successorFailures = 0 }) {
  const errors = [];
  if (unknownCount > 0) errors.push("unknown_delivery_observed");
  if (readFailures > 0) errors.push("advertised_read_failed");
  if (successorFailures > 0) errors.push("stable_successor_missing");
  if (!["coverage_reached", "game_over", "action_limit"].includes(terminal)) {
    errors.push(`terminal:${terminal}`);
  }
  return {
    verdict: errors.length === 0 ? "integrity_pass" : "integrity_incomplete",
    errors,
    unknown_deliveries: unknownCount,
    read_failures: readFailures,
    successor_failures: successorFailures,
    terminal
  };
}

export function terminalForReceipt(receipt) {
  if (receipt?.delivery === "unknown") return "unknown_delivery";
  if (receipt?.delivery === "not_delivered") {
    return `not_delivered:${receipt.reason_code ?? "unspecified"}`;
  }
  return null;
}

export function evaluateSurfaceCoverage({
  surfaces,
  combatDeliveries,
  target = DEFAULT_JOURNEY_COVERAGE
}) {
  const observed = [...new Set(surfaces)].sort();
  const missing = target.required_surfaces.filter((kind) => !observed.includes(kind));
  const missingGroups = (target.required_surface_groups ?? []).filter((group) =>
    !group.any_of.some((kind) => observed.includes(kind)));
  const errors = [];
  if (missing.length > 0) errors.push(`missing_surfaces:${missing.join(",")}`);
  if (missingGroups.length > 0) {
    errors.push(`missing_surface_groups:${missingGroups.map((group) => group.id).join(",")}`);
  }
  if (combatDeliveries < target.minimum_combat_deliveries) {
    errors.push("insufficient_combat_deliveries");
  }
  return {
    verdict: errors.length === 0 ? "coverage_reached" : "coverage_incomplete",
    errors,
    target,
    observed_surfaces: observed,
    missing_surfaces: missing,
    missing_surface_groups: missingGroups,
    combat_deliveries: combatDeliveries
  };
}

export function evaluateBoundedJourney(input) {
  const steps = input.steps ?? [];
  const surfaces = input.surfaces ?? steps.map((step) => step.interaction_kind);
  const deliveredActions = input.deliveredActions
    ?? steps.filter((step) => step.delivery === "delivered").length;
  const combatDeliveries = input.combatDeliveries
    ?? steps.filter((step) => step.interaction_kind === "combat_turn"
      && step.delivery === "delivered").length;
  const integrity = evaluateJourneyIntegrity(input);
  const coverage = evaluateSurfaceCoverage({ surfaces, combatDeliveries, target: input.target });
  return {
    verdict: integrity.verdict !== "integrity_pass"
      ? "h2_incomplete"
      : coverage.verdict === "coverage_reached"
        ? "h2_pass"
        : "h2_integrity_pass_coverage_incomplete",
    integrity,
    coverage,
    delivered_actions: deliveredActions
  };
}

async function waitForSuccessor(client, previousSnapshotId, child, timeoutMs) {
  const started = Date.now();
  let latest = null;
  while (Date.now() - started < timeoutMs) {
    latest = (await client.observe()).data;
    const actionable = latest.status === "interactive"
      && latest.bound_actions?.status === "complete"
      && latest.bound_actions.actions.length > 0;
    if (latest.snapshot_id !== previousSnapshotId
        && (actionable || latest.status === "visible_unsupported")) {
      return latest;
    }
    if (child.exitCode != null || child.signalCode != null) return latest;
    await new Promise((resolve) => setTimeout(resolve, 150));
  }
  return latest;
}

function compactStep(snapshot, action, receipt, timing) {
  return {
    at: new Date().toISOString(),
    snapshot_id: snapshot.snapshot_id,
    interaction_kind: snapshot.interaction.kind,
    interaction_stage: snapshot.interaction.stage,
    bound_action_count: snapshot.bound_actions.actions.length,
    action: {
      bound_action_id: action.bound_action_id,
      verb: action.verb,
      label: action.label
    },
    canonical_decision_digest: canonicalDecisionDigest(snapshot),
    canonical_decision: canonicalizeSnapshot(snapshot),
    canonical_selected_action: canonicalizeSelectedAction(snapshot, action.bound_action_id),
    request_id: receipt.request_id,
    delivery: receipt.delivery,
    reason_code: receipt.reason_code ?? null,
    successor_snapshot_id: receipt.successor?.snapshot_id ?? null,
    successor_status: receipt.successor?.status ?? null,
    timing
  };
}

export async function runBoundedJourney({
  installation,
  localRoot,
  endpoint = "http://127.0.0.1:15526",
  timeoutMs = 90_000,
  actionTimeoutMs = 20_000,
  maxActions = 40,
  tutorialPreference = "disable",
  evidenceRoot,
  sharedProfileAcknowledged = false,
  isolatedProfileId = null,
  experimentalBuildAcknowledged = false
}) {
  const launchProfile = resolveLaunchProfile({
    localRoot,
    isolatedProfileId,
    sharedProfileAcknowledged
  });
  const running = listGameProcesses();
  if (running.length > 0) throw new Error(`Refusing to launch beside an existing STS2 process:\n${running.join("\n")}`);
  const endpointBefore = await readJson(endpoint, "/api/player-environment/capabilities", 1000);
  if (endpointBefore.ok) throw new Error("The Connector endpoint is already owned by another process.");

  const diskIdentity = readDiskIdentity(installation);
  const compatibility = evaluateRuntimeCompatibility(diskIdentity);
  const headlessIdentity = readProjectIdentity();
  if (compatibility.status !== "supported_exact" && !experimentalBuildAcknowledged) {
    throw new Error(
      `Unsupported STS2 runtime (${compatibility.mismatches.join(", ")}); `
      + "pass --experimental-build only to collect non-support evidence."
    );
  }

  const evidenceDirectory = path.join(evidenceRoot, `bounded-journey-${safeTimestamp()}`);
  mkdirSync(evidenceDirectory, { recursive: true });
  const eventsFile = path.join(evidenceDirectory, "events.jsonl");
  const reportFile = path.join(evidenceDirectory, "report.json");
  const stdoutFile = path.join(evidenceDirectory, "stdout.log");
  const stderrFile = path.join(evidenceDirectory, "stderr.log");
  const recorder = new JsonlRecorder(eventsFile, { flushEvery: 1 });
  let eventCount = 0;
  const record = (event) => {
    recorder.append(event);
    eventCount += 1;
  };
  const { child, args } = shippedRuntimeLaunch(installation, { launchProfile });
  const stdoutStream = createWriteStream(stdoutFile);
  const stderrStream = createWriteStream(stderrFile);
  child.stdout.pipe(stdoutStream);
  child.stderr.pipe(stderrStream);
  let session = null;
  let capabilities = null;
  let terminal = "not_started";
  let unknownCount = 0;
  let readFailures = 0;
  let successorFailures = 0;
  let deliveredActions = 0;
  let combatDeliveries = 0;
  const surfaces = new Set();
  const semanticDecisionDurations = [];
  const readKinds = new Set();

  try {
    const capabilitiesResult = await waitForEndpoint(endpoint, timeoutMs, child);
    if (!capabilitiesResult.ok) throw new Error(`Connector endpoint did not become ready: ${capabilitiesResult.error}`);
    capabilities = capabilitiesResult.value;
    const capabilityGate = evaluateHeadlessCapabilities(capabilities);
    if (!capabilityGate.ok) throw new Error(`Headless capability gate failed: ${capabilityGate.errors.join(", ")}`);
    const observed = await waitForInteractiveSnapshot(endpoint, timeoutMs, child);
    let snapshot = observed.at(-1)?.value;
    if (!snapshot) throw new Error("No interactive Player Environment snapshot mounted.");

    const client = new PlayerEnvironmentRestClient(endpoint, 30_000);
    session = new EnvironmentControllerSession(client, {
      productId: "sts2-headless-bounded-journey",
      productName: "STS2 Headless Bounded Journey",
      productVersion: headlessIdentity.version
    });
    await session.register(capabilities.host, capabilities.control);

    for (let index = 0; index < maxActions; index += 1) {
      const snapshotReadyMs = performance.now();
      if (snapshot.status === "visible_unsupported") {
        record({
          type: "stop",
          reason: "visible_unsupported",
          at: new Date().toISOString(),
          snapshot_id: snapshot.snapshot_id,
          canonical_decision: canonicalizeSnapshot(snapshot)
        });
        terminal = "visible_unsupported";
        break;
      }
      if (snapshot.interaction.kind === "game_over") {
        record({
          type: "terminal",
          reason: "game_over",
          at: new Date().toISOString(),
          snapshot_id: snapshot.snapshot_id,
          canonical_decision: canonicalizeSnapshot(snapshot)
        });
        terminal = "game_over";
        break;
      }
      for (const read of snapshot.reads) {
        if (readKinds.has(read.kind)) continue;
        try {
          const result = (await client.read(read.read_id, snapshot.snapshot_id)).data;
          readKinds.add(read.kind);
          record({
            at: new Date().toISOString(),
            type: "read",
            snapshot_id: snapshot.snapshot_id,
            kind: read.kind,
            observed_snapshot_id: result.observed_snapshot_id,
            completeness: result.completeness.status
          });
        } catch (error) {
          readFailures += 1;
          record({
            at: new Date().toISOString(),
            type: "read_failure",
            snapshot_id: snapshot.snapshot_id,
            kind: read.kind,
            error: error instanceof Error ? error.message : String(error)
          });
        }
      }

      const action = chooseBoundAction(snapshot, { tutorialPreference });
      const policySelectedMs = performance.now();
      if (!action) {
        record({
          type: "stop",
          reason: "no_safe_probe_action",
          at: new Date().toISOString(),
          snapshot_id: snapshot.snapshot_id,
          canonical_decision: canonicalizeSnapshot(snapshot)
        });
        terminal = snapshot.status === "interactive" ? "no_safe_probe_action" : snapshot.status;
        break;
      }
      const credentials = await session.credentials();
      const submitStartedMs = performance.now();
      const receipt = (await client.submit({
        requestId: `headless-journey-${randomUUID()}`,
        expectedSnapshotId: snapshot.snapshot_id,
        boundActionId: action.bound_action_id,
        clientSessionId: credentials.clientSessionId,
        controllerLeaseId: credentials.controllerLeaseId,
        controllerGeneration: credentials.controllerGeneration
      })).data;
      const receiptMs = performance.now();
      const receiptTerminal = terminalForReceipt(receipt);
      if (receiptTerminal) {
        if (receipt.delivery === "unknown") unknownCount += 1;
        record({
          type: "action",
          ...compactStep(snapshot, action, receipt, null)
        });
        terminal = receiptTerminal;
        break;
      }
      let successor = receipt.successor;
      if (!successor || successor.snapshot_id === snapshot.snapshot_id || successor.status === "settling") {
        successor = await waitForSuccessor(client, snapshot.snapshot_id, child, actionTimeoutMs);
      }
      if (!successor) {
        successorFailures += 1;
        record({
          type: "action",
          ...compactStep(snapshot, action, receipt, null)
        });
        terminal = "successor_timeout";
        break;
      }
      const successorReadyMs = performance.now();
      const timing = normalizedDecisionTiming({
        snapshotReadyMs,
        policySelectedMs,
        submitStartedMs,
        receiptMs,
        successorReadyMs
      });
      semanticDecisionDurations.push(timing.semantic_decision_ms);
      record({ type: "action", ...compactStep(snapshot, action, receipt, timing) });
      surfaces.add(snapshot.interaction.kind);
      if (receipt.delivery === "delivered") {
        deliveredActions += 1;
        if (snapshot.interaction.kind === "combat_turn") combatDeliveries += 1;
      }
      snapshot = successor;

      if (combatDeliveries >= 3
          && ["main_menu", "singleplayer_menu", "event_option", "map_navigation", "combat_turn"]
            .every((kind) => surfaces.has(kind))) {
        terminal = "coverage_reached";
        break;
      }
    }
    if (terminal === "not_started") terminal = "action_limit";

    const verdict = evaluateBoundedJourney({
      surfaces: [...surfaces],
      deliveredActions,
      combatDeliveries,
      terminal,
      unknownCount,
      readFailures,
      successorFailures
    });
    const report = {
      schema_version: 1,
      generated_at: new Date().toISOString(),
      headless: headlessIdentity,
      route: "shipped_godot_headless",
      command: { executable: installation.executable, args },
      profile: publicProfileDescriptor(launchProfile),
      disk_identity: diskIdentity,
      compatibility,
      evidence_mode: compatibility.status === "supported_exact" ? "supported" : "experimental",
      probe_policy: {
        kind: "deterministic_test_consumer",
        tutorial_preference: tutorialPreference
      },
      loaded_identity: {
        protocol: capabilities.protocol_version,
        host: capabilities.host,
        game: capabilities.game
      },
      read_kinds_exercised: [...readKinds].sort(),
      event_count: eventCount,
      events_files: recorder.files.map((file) => path.basename(file)),
      timing: { semantic_decision_ms: summarizeDurations(semanticDecisionDurations) },
      verdict,
      non_claims: [
        "The deterministic policy is a test consumer, not a gameplay agent.",
        "H2 does not prove full-run completion, semantic parity, save isolation, determinism, or performance.",
        launchProfile.mode === "shared_steam_profile"
          ? "The active Steam profile was explicitly used; profile isolation was not exercised."
          : "The isolated profile path is source-backed experimental until runtime sentinel and Cloud checks pass."
      ]
    };
    recorder.close();
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    return { report, reportFile, eventsFile, evidenceDirectory };
  } catch (error) {
    terminal = terminal === "not_started" ? "probe_error" : terminal;
    const message = error instanceof Error ? error.message : String(error);
    const report = {
      schema_version: 1,
      generated_at: new Date().toISOString(),
      headless: headlessIdentity,
      route: "shipped_godot_headless",
      command: { executable: installation.executable, args },
      profile: publicProfileDescriptor(launchProfile),
      disk_identity: diskIdentity,
      compatibility,
      evidence_mode: compatibility.status === "supported_exact" ? "supported" : "experimental",
      probe_policy: {
        kind: "deterministic_test_consumer",
        tutorial_preference: tutorialPreference
      },
      loaded_identity: capabilities
        ? { protocol: capabilities.protocol_version, host: capabilities.host, game: capabilities.game }
        : null,
      read_kinds_exercised: [...readKinds].sort(),
      event_count: eventCount,
      events_files: recorder.files.map((file) => path.basename(file)),
      error: message,
      verdict: evaluateBoundedJourney({
        surfaces: [...surfaces],
        deliveredActions,
        combatDeliveries,
        terminal,
        unknownCount,
        readFailures,
        successorFailures
      })
    };
    recorder.close();
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    throw new Error(`${message}; evidence: ${reportFile}`);
  } finally {
    recorder.close();
    try {
      await session?.close();
    } catch {
      // Cleanup must never prevent termination of the real game process.
    }
    await stopChild(child);
    await Promise.allSettled([finished(stdoutStream), finished(stderrStream)]);
  }
}
