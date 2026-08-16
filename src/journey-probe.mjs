import { randomUUID } from "node:crypto";
import { createWriteStream, mkdirSync, readFileSync, writeFileSync } from "node:fs";
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
  requestHostProvenance,
  resolveExperimentalConnectorCanary,
  shippedRuntimeLaunch,
  stopChild,
  waitForExit,
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
  canonicalizeReadResponse,
  canonicalizeSelectedAction,
  canonicalizeSnapshot
} from "./semantic-decision.mjs";
import { publicProfileDescriptor, resolveLaunchProfile } from "./profile-isolation.mjs";
import {
  ProcessResourceSampler,
  summarizeHostPerformance
} from "./process-resource-sampler.mjs";
import {
  analyzeRuntimeDiagnostics,
  evaluateNativeShutdownContainment
} from "./runtime-diagnostics.mjs";
import {
  canonicalizeEpisodeSeed,
  evaluateEpisodeProvenance
} from "./episode-provenance.mjs";
import { resumeProcess, suspendProcess } from "./process-faults.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function orderedActions(actions) {
  return [...actions].sort((left, right) => left.verb.localeCompare(right.verb)
    || left.label.localeCompare(right.label)
    || (left.arguments ?? []).map((argument) => argument.role).join(",")
      .localeCompare((right.arguments ?? []).map((argument) => argument.role).join(",")));
}

export function faultInjectionReady({
  deliveredActions,
  faultAfterDeliveredActions,
  requestedSeed,
  provenanceVerdict
}) {
  return faultAfterDeliveredActions != null
    && deliveredActions >= faultAfterDeliveredActions
    && (requestedSeed == null || provenanceVerdict === "provenance_pass");
}

function actionWithLabel(actions, pattern) {
  return orderedActions(actions).find((action) => pattern.test(action.label));
}

function actionWithVerb(actions, ...verbs) {
  const preference = new Map(verbs.map((verb, index) => [verb, index]));
  return orderedActions(actions).filter((action) => preference.has(action.verb))
    .sort((left, right) => preference.get(left.verb) - preference.get(right.verb)
      || left.label.localeCompare(right.label))[0];
}

function firstSemanticAction(actions) {
  return orderedActions(actions)[0] ?? null;
}

function combatPlayAction(snapshot, actions) {
  const hand = snapshot.interaction?.content?.context?.player?.hand;
  const enemies = snapshot.interaction?.content?.context?.enemies;
  if (!Array.isArray(hand)) return actionWithVerb(actions, "play");

  const enemyOrder = new Map((Array.isArray(enemies) ? enemies : [])
    .map((enemy, index) => [enemy.entity_id, index]));
  for (const card of hand) {
    const matching = actions.filter((action) => action.verb === "play"
      && action.subject_referent_id === card.entity_id);
    if (matching.length === 0) continue;
    return [...matching].sort((left, right) => {
      const leftTarget = left.arguments?.find((argument) => argument.role === "target")?.referent_id;
      const rightTarget = right.arguments?.find((argument) => argument.role === "target")?.referent_id;
      return (enemyOrder.get(leftTarget) ?? Number.MAX_SAFE_INTEGER)
        - (enemyOrder.get(rightTarget) ?? Number.MAX_SAFE_INTEGER)
        || left.label.localeCompare(right.label);
    })[0];
  }
  return null;
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

  if (kind === "main_menu") {
    return actionWithLabel(actions, /single player/i) ?? firstSemanticAction(actions);
  }
  if (kind === "singleplayer_menu") {
    return actionWithLabel(actions, /standard|continue|resume/i)
      ?? orderedActions(actions).find((action) => !/back/i.test(action.label))
      ?? null;
  }
  if (kind === "character_select") {
    return actionWithLabel(actions, /embark/i)
      ?? orderedActions(actions).find((action) => action.verb === "select" && !/random/i.test(action.label))
      ?? null;
  }
  if (kind === "tutorial" || kind === "tutorial_preference") {
    const tutorialId = snapshot.interaction?.content?.surface?.tutorial_id ?? null;
    if (kind === "tutorial" && tutorialId === "combat_rules_ftue") {
      return actionWithLabel(actions, /next|finish|continue|advance/i) ?? null;
    }
    if (kind === "tutorial" && tutorialId !== "accept_tutorials_ftue") return null;
    if (!new Set(["enable", "disable"]).has(tutorialPreference)) {
      throw new Error(`Unsupported tutorial preference: ${tutorialPreference}.`);
    }
    return tutorialPreference === "disable"
      ? actionWithLabel(actions, /cancel|disable|no/i) ?? null
      : actionWithLabel(actions, /confirm|enable|yes/i) ?? null;
  }
  if (kind === "combat_turn") {
    return combatPlayAction(snapshot, actions)
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
      ?? firstSemanticAction(actions);
  }
  if (kind === "card_reward_selection") {
    return actionWithLabel(actions, /skip|跳过/i) ?? actionWithVerb(actions, "select") ?? null;
  }
  if (kind === "rest_site") {
    return actionWithLabel(actions, /rest|heal|休息/i) ?? firstSemanticAction(actions);
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

export function evaluateJourneyIntegrity({
  terminal,
  unknownCount,
  readFailures,
  successorFailures = 0,
  provenanceFailures = 0
}) {
  const errors = [];
  if (unknownCount > 0) errors.push("unknown_delivery_observed");
  if (readFailures > 0) errors.push("advertised_read_failed");
  if (successorFailures > 0) errors.push("stable_successor_missing");
  if (provenanceFailures > 0) errors.push("episode_provenance_unverified");
  if (!["coverage_reached", "game_over", "action_limit"].includes(terminal)) {
    errors.push(`terminal:${terminal}`);
  }
  return {
    verdict: errors.length === 0 ? "integrity_pass" : "integrity_incomplete",
    errors,
    unknown_deliveries: unknownCount,
    read_failures: readFailures,
    successor_failures: successorFailures,
    provenance_failures: provenanceFailures,
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

export function isRefreshableStaleReceipt(receipt) {
  return receipt?.delivery === "not_delivered"
    && receipt?.reason_code === "stale_snapshot"
    && receipt?.successor?.snapshot_id != null;
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

function successorObservation(snapshot, elapsedMs) {
  return {
    first_elapsed_ms: elapsedMs,
    last_elapsed_ms: elapsedMs,
    sample_count: 1,
    first_snapshot_id: snapshot?.snapshot_id ?? null,
    last_snapshot_id: snapshot?.snapshot_id ?? null,
    status: snapshot?.status ?? null,
    interaction_kind: snapshot?.interaction?.kind ?? null,
    interaction_stage: snapshot?.interaction?.stage ?? null,
    bound_action_status: snapshot?.bound_actions?.status ?? null,
    bound_action_count: Array.isArray(snapshot?.bound_actions?.actions)
      ? snapshot.bound_actions.actions.length
      : null
  };
}

function successorObservationKey(observation) {
  return JSON.stringify({
    status: observation.status,
    interaction_kind: observation.interaction_kind,
    interaction_stage: observation.interaction_stage,
    bound_action_status: observation.bound_action_status,
    bound_action_count: observation.bound_action_count
  });
}

function appendSuccessorObservation(observations, snapshot, elapsedMs) {
  const next = successorObservation(snapshot, elapsedMs);
  const previous = observations.at(-1);
  if (previous != null && successorObservationKey(previous) === successorObservationKey(next)) {
    previous.last_elapsed_ms = elapsedMs;
    previous.sample_count += 1;
    previous.last_snapshot_id = next.last_snapshot_id;
    return;
  }
  observations.push(next);
}

function stableSuccessor(snapshot, previousSnapshotId) {
  const actionable = snapshot?.status === "interactive"
    && snapshot?.bound_actions?.status === "complete"
    && snapshot.bound_actions.actions.length > 0;
  return snapshot?.snapshot_id !== previousSnapshotId
    && (actionable || snapshot?.status === "visible_unsupported");
}

function immediateSuccessorWait(previousSnapshotId, snapshot, terminal = "receipt_successor") {
  const observations = [];
  appendSuccessorObservation(observations, snapshot, 0);
  return {
    schema: "sts2.headless/successor-wait-1",
    previous_snapshot_id: previousSnapshotId,
    terminal,
    poll_count: 0,
    elapsed_ms: 0,
    observations
  };
}

export async function waitForSuccessor(
  client,
  previousSnapshotId,
  child,
  timeoutMs,
  { initialSnapshot = null, pollIntervalMs = 150 } = {}
) {
  const started = performance.now();
  const observations = [];
  if (initialSnapshot != null) appendSuccessorObservation(observations, initialSnapshot, 0);
  let latest = initialSnapshot;
  let terminal = "timeout";
  let pollCount = 0;
  while (performance.now() - started < timeoutMs) {
    latest = (await client.observe()).data;
    pollCount += 1;
    const elapsedMs = performance.now() - started;
    appendSuccessorObservation(observations, latest, elapsedMs);
    if (stableSuccessor(latest, previousSnapshotId)) {
      terminal = "stable_successor";
      break;
    }
    if (child.exitCode != null || child.signalCode != null) {
      terminal = "child_exit";
      break;
    }
    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }
  return {
    snapshot: latest,
    profile: {
      schema: "sts2.headless/successor-wait-1",
      previous_snapshot_id: previousSnapshotId,
      terminal,
      poll_count: pollCount,
      elapsed_ms: performance.now() - started,
      observations
    }
  };
}

function compactStep(
  snapshot,
  action,
  receipt,
  timing,
  { successorWait = null, observedSuccessor = receipt.successor } = {}
) {
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
    observed_successor_snapshot_id: observedSuccessor?.snapshot_id ?? null,
    observed_successor_status: observedSuccessor?.status ?? null,
    observed_successor_interaction_kind: observedSuccessor?.interaction?.kind ?? null,
    observed_successor_interaction_stage: observedSuccessor?.interaction?.stage ?? null,
    successor_wait: successorWait,
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
  experimentalBuildAcknowledged = false,
  allowConcurrentProcesses = false,
  evidenceLabel = null,
  faultAfterDeliveredActions = null,
  faultMode = "process_crash",
  shutdownDrainMs = 2_000,
  runSeed = null,
  maxConsecutiveStale = 8
}) {
  const canonicalRunSeed = canonicalizeEpisodeSeed(runSeed);
  const launchProfile = resolveLaunchProfile({
    localRoot,
    isolatedProfileId,
    sharedProfileAcknowledged
  });
  if (allowConcurrentProcesses
      && (launchProfile.mode !== "isolated_local_profile" || sharedProfileAcknowledged)) {
    throw new Error("Concurrent probes require one isolated local profile per process.");
  }
  if (evidenceLabel != null && !/^[a-z0-9][a-z0-9._-]{0,63}$/u.test(evidenceLabel)) {
    throw new Error("Evidence labels must be bounded lowercase identifiers.");
  }
  if (faultAfterDeliveredActions != null
      && (!Number.isSafeInteger(faultAfterDeliveredActions) || faultAfterDeliveredActions < 1)) {
    throw new Error("Fault injection requires a positive delivered-action count.");
  }
  if (!new Set(["process_crash", "process_hang"]).has(faultMode)) {
    throw new Error("Fault mode must be process_crash or process_hang.");
  }
  if (!Number.isSafeInteger(shutdownDrainMs) || shutdownDrainMs < 0 || shutdownDrainMs > 10_000) {
    throw new Error("Shutdown drain must be an integer from 0 through 10000 milliseconds.");
  }
  if (!Number.isSafeInteger(maxConsecutiveStale)
      || maxConsecutiveStale < 0
      || maxConsecutiveStale > 100) {
    throw new Error("Maximum consecutive stale refusals must be an integer from 0 through 100.");
  }
  const running = listGameProcesses();
  if (running.length > 0 && !allowConcurrentProcesses) {
    throw new Error(`Refusing to launch beside an existing STS2 process:\n${running.join("\n")}`);
  }
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
  const connectorCanary = resolveExperimentalConnectorCanary({
    installation,
    compatibility,
    acknowledged: experimentalBuildAcknowledged
  });

  const evidenceDirectory = path.join(
    evidenceRoot,
    `bounded-journey-${safeTimestamp()}${evidenceLabel == null ? "" : `-${evidenceLabel}`}`
  );
  mkdirSync(evidenceDirectory, { recursive: true });
  const eventsFile = path.join(evidenceDirectory, "events.jsonl");
  const resourcesFile = path.join(evidenceDirectory, "resources.jsonl");
  const reportFile = path.join(evidenceDirectory, "report.json");
  const stdoutFile = path.join(evidenceDirectory, "stdout.log");
  const stderrFile = path.join(evidenceDirectory, "stderr.log");
  const recorder = new JsonlRecorder(eventsFile, { flushEvery: 1 });
  const resourceRecorder = new JsonlRecorder(resourcesFile, { flushEvery: 1 });
  let eventCount = 0;
  const record = (event) => {
    recorder.append(event);
    eventCount += 1;
  };
  const processStartedMs = performance.now();
  const { child, args, connector, hostControlToken, hostConfiguration } = shippedRuntimeLaunch(installation, {
    launchProfile,
    connectorEndpoint: endpoint,
    runSeed: canonicalRunSeed,
    connectorCanary
  });
  const resourceSampler = new ProcessResourceSampler(child.pid, {
    onSample: (sample) => resourceRecorder.append({ type: "process_resource", ...sample })
  });
  await resourceSampler.start();
  const stdoutStream = createWriteStream(stdoutFile);
  const stderrStream = createWriteStream(stderrFile);
  child.stdout.pipe(stdoutStream);
  child.stderr.pipe(stderrStream);
  const stderrBeforeNativeShutdown = [];
  const stderrAfterNativeShutdown = [];
  let nativeShutdownStarted = false;
  child.stderr.on("data", (chunk) => {
    (nativeShutdownStarted ? stderrAfterNativeShutdown : stderrBeforeNativeShutdown)
      .push(Buffer.from(chunk));
  });
  let session = null;
  let capabilities = null;
  let endpointReadyMs = null;
  let interactiveReadyMs = null;
  let decisionWindowStartedMs = null;
  let decisionWindowEndedMs = null;
  let resourceSamplingResult = null;
  let completedReport = null;
  let terminal = "not_started";
  let unknownCount = 0;
  let readFailures = 0;
  let successorFailures = 0;
  let episodeProvenance = evaluateEpisodeProvenance({
    requestedSeed: canonicalRunSeed,
    expectedRuntimeInstanceId: null,
    response: null
  });
  let deliveredActions = 0;
  let combatDeliveries = 0;
  let staleRefusals = 0;
  let consecutiveStale = 0;
  const surfaces = new Set();
  const semanticDecisionDurations = [];
  const readKinds = new Set();
  const stopResourceSampling = async () => {
    if (resourceSamplingResult == null) resourceSamplingResult = await resourceSampler.stop();
    resourceRecorder.close();
    return resourceSamplingResult;
  };
  let lastProvenanceEvent = null;
  const refreshEpisodeProvenance = async () => {
    if (canonicalRunSeed == null
        || capabilities == null
        || episodeProvenance.verdict === "provenance_pass") {
      return episodeProvenance;
    }
    const provenanceResponse = await requestHostProvenance({
      endpoint,
      hostControlToken,
      expectedRuntimeInstanceId: capabilities.host.runtime_instance_id
    });
    episodeProvenance = evaluateEpisodeProvenance({
      requestedSeed: canonicalRunSeed,
      expectedRuntimeInstanceId: capabilities.host.runtime_instance_id,
      response: provenanceResponse
    });
    const fingerprint = JSON.stringify(episodeProvenance);
    if (fingerprint !== lastProvenanceEvent) {
      record({ type: "episode_provenance", at: new Date().toISOString(), ...episodeProvenance });
      lastProvenanceEvent = fingerprint;
    }
    return episodeProvenance;
  };

  try {
    const capabilitiesResult = await waitForEndpoint(endpoint, timeoutMs, child);
    if (!capabilitiesResult.ok) throw new Error(`Connector endpoint did not become ready: ${capabilitiesResult.error}`);
    endpointReadyMs = performance.now();
    capabilities = capabilitiesResult.value;
    const capabilityGate = evaluateHeadlessCapabilities(capabilities);
    if (!capabilityGate.ok) throw new Error(`Headless capability gate failed: ${capabilityGate.errors.join(", ")}`);
    const observed = await waitForInteractiveSnapshot(endpoint, timeoutMs, child);
    let snapshot = observed.at(-1)?.value;
    if (!snapshot) throw new Error("No interactive Player Environment snapshot mounted.");
    interactiveReadyMs = performance.now();

    const client = new PlayerEnvironmentRestClient(endpoint, 30_000);
    session = new EnvironmentControllerSession(client, {
      productId: "sts2-headless-bounded-journey",
      productName: "STS2 Headless Bounded Journey",
      productVersion: headlessIdentity.version
    });
    await session.register(capabilities.host, capabilities.control);

    for (let index = 0; index < maxActions; index += 1) {
      const snapshotReadyMs = performance.now();
      decisionWindowStartedMs ??= snapshotReadyMs;
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
            completeness: result.completeness.status,
            canonical_read: canonicalizeReadResponse(result)
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
      if (isRefreshableStaleReceipt(receipt)) {
        staleRefusals += 1;
        consecutiveStale += 1;
        if (consecutiveStale > maxConsecutiveStale) {
          record({
            type: "stale_refusal",
            ...compactStep(snapshot, action, receipt, null),
            consecutive_stale: consecutiveStale
          });
          terminal = "stale_livelock";
          break;
        }
        let refreshed = receipt.successor;
        let successorWait = immediateSuccessorWait(
          snapshot.snapshot_id,
          refreshed,
          "receipt_stale_successor"
        );
        if (refreshed.status === "settling") {
          const waitResult = await waitForSuccessor(
            client,
            refreshed.snapshot_id,
            child,
            actionTimeoutMs,
            { initialSnapshot: refreshed }
          );
          refreshed = waitResult.snapshot;
          successorWait = waitResult.profile;
        }
        record({
          type: "stale_refusal",
          ...compactStep(snapshot, action, receipt, null, {
            successorWait,
            observedSuccessor: refreshed
          }),
          consecutive_stale: consecutiveStale
        });
        if (!refreshed) {
          successorFailures += 1;
          terminal = "stale_successor_timeout";
          break;
        }
        snapshot = refreshed;
        index -= 1;
        continue;
      }
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
      let successorWait = immediateSuccessorWait(snapshot.snapshot_id, successor);
      if (!successor || successor.snapshot_id === snapshot.snapshot_id || successor.status === "settling") {
        const waitResult = await waitForSuccessor(
          client,
          snapshot.snapshot_id,
          child,
          actionTimeoutMs,
          { initialSnapshot: successor }
        );
        successor = waitResult.snapshot;
        successorWait = waitResult.profile;
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
      decisionWindowEndedMs = successorReadyMs;
      const timing = normalizedDecisionTiming({
        snapshotReadyMs,
        policySelectedMs,
        submitStartedMs,
        receiptMs,
        successorReadyMs
      });
      semanticDecisionDurations.push(timing.semantic_decision_ms);
      record({
        type: "action",
        ...compactStep(snapshot, action, receipt, timing, {
          successorWait,
          observedSuccessor: successor
        })
      });
      surfaces.add(snapshot.interaction.kind);
      if (receipt.delivery === "delivered") {
        consecutiveStale = 0;
        deliveredActions += 1;
        if (snapshot.interaction.kind === "combat_turn") combatDeliveries += 1;
      }
      snapshot = successor;
      await refreshEpisodeProvenance();

      if (faultInjectionReady({
        deliveredActions,
        faultAfterDeliveredActions,
        requestedSeed: canonicalRunSeed,
        provenanceVerdict: episodeProvenance.verdict
      })) {
        const requestedAt = new Date().toISOString();
        if (faultMode === "process_crash") {
          const signalAccepted = child.kill("SIGKILL");
          const processExit = await waitForExit(child, 5_000);
          record({
            type: "fault_injection",
            at: requestedAt,
            kind: "process_kill_after_stable_successor",
            delivered_actions: deliveredActions,
            signal_accepted: signalAccepted,
            process_exit: processExit
          });
          terminal = "injected_process_crash";
        } else {
          const suspension = suspendProcess(child.pid);
          const endpointWhileSuspended = await readJson(
            endpoint,
            "/api/player-environment/capabilities",
            1_000
          );
          const processRemained = child.exitCode == null && child.signalCode == null;
          const hangObserved = suspension.status === "applied"
            && !endpointWhileSuspended.ok
            && processRemained;
          const signalAccepted = child.kill("SIGKILL");
          let processExit = await waitForExit(child, 5_000);
          let resume = null;
          if (processExit == null) {
            resume = resumeProcess(child.pid);
            child.kill("SIGKILL");
            processExit = await waitForExit(child, 5_000);
          }
          record({
            type: "fault_injection",
            at: requestedAt,
            kind: "process_suspend_after_stable_successor",
            delivered_actions: deliveredActions,
            suspension,
            endpoint_while_suspended: endpointWhileSuspended,
            process_remained_while_endpoint_unresponsive: processRemained,
            hang_observed: hangObserved,
            signal_accepted: signalAccepted,
            resume,
            process_exit: processExit
          });
          terminal = hangObserved ? "injected_process_hang" : "hang_injection_incomplete";
        }
        break;
      }

      if (evaluateSurfaceCoverage({
        surfaces: [...surfaces],
        combatDeliveries
      }).verdict === "coverage_reached") {
        terminal = "coverage_reached";
        break;
      }
    }
    if (terminal === "not_started") terminal = "action_limit";

    await refreshEpisodeProvenance();

    const verdict = evaluateBoundedJourney({
      surfaces: [...surfaces],
      deliveredActions,
      combatDeliveries,
      terminal,
      unknownCount,
      readFailures,
      successorFailures,
      provenanceFailures: episodeProvenance.verdict === "provenance_incomplete" ? 1 : 0
    });
    const resources = await stopResourceSampling();
    const performanceSummary = summarizeHostPerformance({
      samples: resources.samples,
      sampleErrors: resources.errors,
      decisionWindowStartedMs,
      decisionWindowEndedMs,
      deliveredDecisions: deliveredActions
    });
    const report = {
      schema_version: 1,
      generated_at: new Date().toISOString(),
      headless: headlessIdentity,
      route: "shipped_godot_headless",
      worker: evidenceLabel == null ? null : { worker_id: evidenceLabel, concurrent: allowConcurrentProcesses },
      command: { executable: installation.executable, args, connector },
      host_configuration: hostConfiguration,
      profile: publicProfileDescriptor(launchProfile),
      disk_identity: diskIdentity,
      compatibility,
      evidence_mode: compatibility.status === "supported_exact" ? "supported" : "experimental",
      probe_policy: {
        kind: "deterministic_test_consumer",
        tutorial_preference: tutorialPreference,
        max_consecutive_stale: maxConsecutiveStale,
        fault_after_delivered_actions: faultAfterDeliveredActions,
        requested_seed: canonicalRunSeed
      },
      loaded_identity: {
        protocol: capabilities.protocol_version,
        host: capabilities.host,
        game: capabilities.game
      },
      read_kinds_exercised: [...readKinds].sort(),
      stale_refusals: staleRefusals,
      event_count: eventCount,
      events_files: recorder.files.map((file) => path.basename(file)),
      resources_files: resourceRecorder.files.map((file) => path.basename(file)),
      timing: {
        boot: {
          process_start_to_endpoint_ms: endpointReadyMs == null ? null : endpointReadyMs - processStartedMs,
          process_start_to_interactive_ms:
            interactiveReadyMs == null ? null : interactiveReadyMs - processStartedMs
        },
        semantic_decision_ms: summarizeDurations(semanticDecisionDurations)
      },
      episode_provenance: episodeProvenance,
      performance: performanceSummary,
      verdict,
      non_claims: [
        "The deterministic policy is a test consumer, not a gameplay agent.",
        "One bounded journey does not prove full-run completion, semantic parity, determinism, capacity, or support.",
        launchProfile.mode === "shared_steam_profile"
          ? "The active Steam profile was explicitly used; profile isolation was not exercised."
          : "The isolated profile path is source-backed experimental until runtime sentinel and Cloud checks pass."
      ]
    };
    completedReport = report;
    recorder.close();
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    return {
      report,
      reportFile,
      eventsFile,
      evidenceDirectory,
      measurement: {
        process_started_ms: processStartedMs,
        decision_window_started_ms: decisionWindowStartedMs,
        decision_window_ended_ms: decisionWindowEndedMs
      }
    };
  } catch (error) {
    terminal = terminal === "not_started" ? "probe_error" : terminal;
    const message = error instanceof Error ? error.message : String(error);
    const resources = await stopResourceSampling();
    const performanceSummary = summarizeHostPerformance({
      samples: resources.samples,
      sampleErrors: resources.errors,
      decisionWindowStartedMs,
      decisionWindowEndedMs,
      deliveredDecisions: deliveredActions
    });
    const report = {
      schema_version: 1,
      generated_at: new Date().toISOString(),
      headless: headlessIdentity,
      route: "shipped_godot_headless",
      worker: evidenceLabel == null ? null : { worker_id: evidenceLabel, concurrent: allowConcurrentProcesses },
      command: { executable: installation.executable, args, connector },
      host_configuration: hostConfiguration,
      profile: publicProfileDescriptor(launchProfile),
      disk_identity: diskIdentity,
      compatibility,
      evidence_mode: compatibility.status === "supported_exact" ? "supported" : "experimental",
      probe_policy: {
        kind: "deterministic_test_consumer",
        tutorial_preference: tutorialPreference,
        max_consecutive_stale: maxConsecutiveStale,
        fault_after_delivered_actions: faultAfterDeliveredActions,
        requested_seed: canonicalRunSeed
      },
      loaded_identity: capabilities
        ? { protocol: capabilities.protocol_version, host: capabilities.host, game: capabilities.game }
        : null,
      episode_provenance: episodeProvenance,
      stale_refusals: staleRefusals,
      read_kinds_exercised: [...readKinds].sort(),
      event_count: eventCount,
      events_files: recorder.files.map((file) => path.basename(file)),
      resources_files: resourceRecorder.files.map((file) => path.basename(file)),
      timing: {
        boot: {
          process_start_to_endpoint_ms: endpointReadyMs == null ? null : endpointReadyMs - processStartedMs,
          process_start_to_interactive_ms:
            interactiveReadyMs == null ? null : interactiveReadyMs - processStartedMs
        },
        semantic_decision_ms: summarizeDurations(semanticDecisionDurations)
      },
      performance: performanceSummary,
      error: message,
      verdict: evaluateBoundedJourney({
        surfaces: [...surfaces],
        deliveredActions,
        combatDeliveries,
        terminal,
        unknownCount,
        readFailures,
        successorFailures,
        provenanceFailures: episodeProvenance.verdict === "provenance_incomplete" ? 1 : 0
      })
    };
    completedReport = report;
    recorder.close();
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    throw new Error(`${message}; evidence: ${reportFile}`);
  } finally {
    recorder.close();
    await stopResourceSampling();
    try {
      await session?.close();
    } catch {
      // Cleanup must never prevent termination of the real game process.
    }
    if (faultAfterDeliveredActions == null && child.exitCode == null && child.signalCode == null
        && shutdownDrainMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, shutdownDrainMs));
    }
    nativeShutdownStarted = true;
    const processCleanup = await stopChild(child, {
      endpoint,
      hostControlToken,
      expectedRuntimeInstanceId: capabilities?.host?.runtime_instance_id ?? null
    });
    await Promise.allSettled([finished(stdoutStream), finished(stderrStream)]);
    if (completedReport != null) {
      completedReport.process_cleanup = processCleanup;
      completedReport.runtime_diagnostics = analyzeRuntimeDiagnostics({
        stdout: readFileSync(stdoutFile, "utf8"),
        stderr: readFileSync(stderrFile, "utf8"),
        beforeNativeShutdownStderr: Buffer.concat(stderrBeforeNativeShutdown).toString("utf8"),
        afterNativeShutdownStderr: Buffer.concat(stderrAfterNativeShutdown).toString("utf8")
      });
      completedReport.shutdown_containment = evaluateNativeShutdownContainment({
        diagnostics: completedReport.runtime_diagnostics,
        processCleanup
      });
      writeFileSync(reportFile, `${JSON.stringify(completedReport, null, 2)}\n`);
    }
  }
}
