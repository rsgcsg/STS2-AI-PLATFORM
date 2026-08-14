import { randomUUID } from "node:crypto";
import { createWriteStream, existsSync, mkdirSync, writeFileSync } from "node:fs";
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

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

function actionWithLabel(actions, pattern) {
  return actions.find((action) => pattern.test(action.label));
}

function actionWithVerb(actions, ...verbs) {
  return actions.find((action) => verbs.includes(action.verb));
}

export function chooseBoundAction(snapshot) {
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

export function evaluateBoundedJourney({ steps, terminal, unknownCount, readFailures }) {
  const surfaces = [...new Set(steps.map((step) => step.interaction_kind))];
  const combatDeliveries = steps.filter((step) =>
    step.interaction_kind === "combat_turn" && step.delivery === "delivered").length;
  const required = ["main_menu", "singleplayer_menu", "event_option", "map_navigation", "combat_turn"];
  const missing = required.filter((kind) => !surfaces.includes(kind));
  const errors = [];
  if (missing.length > 0) errors.push(`missing_surfaces:${missing.join(",")}`);
  if (combatDeliveries < 3) errors.push("insufficient_combat_deliveries");
  if (unknownCount > 0) errors.push("unknown_delivery_observed");
  if (readFailures > 0) errors.push("advertised_read_failed");
  if (!["coverage_reached", "game_over"].includes(terminal)) errors.push(`terminal:${terminal}`);
  return {
    verdict: errors.length === 0 ? "h2_pass" : "h2_incomplete",
    errors,
    surfaces,
    delivered_actions: steps.filter((step) => step.delivery === "delivered").length,
    combat_deliveries: combatDeliveries,
    unknown_deliveries: unknownCount,
    read_failures: readFailures,
    terminal
  };
}

async function waitForSuccessor(client, previousSnapshotId, child, timeoutMs) {
  const started = Date.now();
  let latest = null;
  while (Date.now() - started < timeoutMs) {
    latest = (await client.observe()).data;
    if (latest.snapshot_id !== previousSnapshotId
        && (latest.status === "interactive" || latest.status === "visible_unsupported")) {
      return latest;
    }
    if (child.exitCode != null || child.signalCode != null) return latest;
    await new Promise((resolve) => setTimeout(resolve, 150));
  }
  return latest;
}

function compactStep(snapshot, action, receipt) {
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
    request_id: receipt.request_id,
    delivery: receipt.delivery,
    reason_code: receipt.reason_code ?? null,
    successor_snapshot_id: receipt.successor?.snapshot_id ?? null,
    successor_status: receipt.successor?.status ?? null
  };
}

export async function runBoundedJourney({
  installation,
  endpoint = "http://127.0.0.1:15526",
  timeoutMs = 90_000,
  actionTimeoutMs = 20_000,
  maxActions = 40,
  evidenceRoot,
  sharedProfileAcknowledged = false,
  experimentalBuildAcknowledged = false
}) {
  if (!sharedProfileAcknowledged) {
    throw new Error("The bounded journey mutates the active Steam profile; pass --shared-profile to acknowledge this.");
  }
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
  const events = [];
  const { child, args } = shippedRuntimeLaunch(installation);
  const stdoutStream = createWriteStream(stdoutFile);
  const stderrStream = createWriteStream(stderrFile);
  child.stdout.pipe(stdoutStream);
  child.stderr.pipe(stderrStream);
  let session = null;
  let capabilities = null;
  let terminal = "not_started";
  let unknownCount = 0;
  let readFailures = 0;
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
      if (snapshot.status === "visible_unsupported") {
        terminal = "visible_unsupported";
        break;
      }
      if (snapshot.interaction.kind === "game_over") {
        terminal = "game_over";
        break;
      }
      for (const read of snapshot.reads) {
        if (readKinds.has(read.kind)) continue;
        try {
          const result = (await client.read(read.read_id, snapshot.snapshot_id)).data;
          readKinds.add(read.kind);
          events.push({
            at: new Date().toISOString(),
            type: "read",
            snapshot_id: snapshot.snapshot_id,
            kind: read.kind,
            observed_snapshot_id: result.observed_snapshot_id,
            completeness: result.completeness.status
          });
        } catch (error) {
          readFailures += 1;
          events.push({
            at: new Date().toISOString(),
            type: "read_failure",
            snapshot_id: snapshot.snapshot_id,
            kind: read.kind,
            error: error instanceof Error ? error.message : String(error)
          });
        }
      }

      const action = chooseBoundAction(snapshot);
      if (!action) {
        terminal = snapshot.status === "interactive" ? "no_safe_probe_action" : snapshot.status;
        break;
      }
      const credentials = await session.credentials();
      const receipt = (await client.submit({
        requestId: `headless-journey-${randomUUID()}`,
        expectedSnapshotId: snapshot.snapshot_id,
        boundActionId: action.bound_action_id,
        clientSessionId: credentials.clientSessionId,
        controllerLeaseId: credentials.controllerLeaseId,
        controllerGeneration: credentials.controllerGeneration
      })).data;
      events.push({ type: "action", ...compactStep(snapshot, action, receipt) });
      if (receipt.delivery === "unknown") {
        unknownCount += 1;
        terminal = "unknown_delivery";
        break;
      }
      let successor = receipt.successor;
      if (!successor || successor.snapshot_id === snapshot.snapshot_id || successor.status === "settling") {
        successor = await waitForSuccessor(client, snapshot.snapshot_id, child, actionTimeoutMs);
      }
      if (!successor) {
        terminal = "successor_timeout";
        break;
      }
      snapshot = successor;

      const combatDeliveries = events.filter((event) =>
        event.type === "action"
        && event.interaction_kind === "combat_turn"
        && event.delivery === "delivered").length;
      const surfaces = new Set(events.filter((event) => event.type === "action")
        .map((event) => event.interaction_kind));
      if (combatDeliveries >= 3
          && ["main_menu", "singleplayer_menu", "event_option", "map_navigation", "combat_turn"]
            .every((kind) => surfaces.has(kind))) {
        terminal = "coverage_reached";
        break;
      }
    }
    if (terminal === "not_started") terminal = "action_limit";

    const steps = events.filter((event) => event.type === "action");
    const verdict = evaluateBoundedJourney({ steps, terminal, unknownCount, readFailures });
    const report = {
      schema_version: 1,
      generated_at: new Date().toISOString(),
      headless: headlessIdentity,
      route: "shipped_godot_headless",
      command: { executable: installation.executable, args },
      profile: { mode: "shared_steam_profile", isolation: "not_proven", acknowledged: true },
      disk_identity: diskIdentity,
      compatibility,
      evidence_mode: compatibility.status === "supported_exact" ? "supported" : "experimental",
      loaded_identity: {
        protocol: capabilities.protocol_version,
        host: capabilities.host,
        game: capabilities.game
      },
      read_kinds_exercised: [...readKinds].sort(),
      event_count: events.length,
      verdict,
      non_claims: [
        "The deterministic policy is a test consumer, not a gameplay agent.",
        "H2 does not prove full-run completion, semantic parity, save isolation, determinism, or performance.",
        "The active Steam profile was explicitly used; profile isolation remains unproven."
      ]
    };
    writeFileSync(eventsFile, events.map((event) => JSON.stringify(event)).join("\n") + "\n");
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    return { report, reportFile, eventsFile, evidenceDirectory };
  } catch (error) {
    terminal = terminal === "not_started" ? "probe_error" : terminal;
    const message = error instanceof Error ? error.message : String(error);
    const steps = events.filter((event) => event.type === "action");
    const report = {
      schema_version: 1,
      generated_at: new Date().toISOString(),
      headless: headlessIdentity,
      route: "shipped_godot_headless",
      command: { executable: installation.executable, args },
      profile: { mode: "shared_steam_profile", isolation: "not_proven", acknowledged: true },
      disk_identity: diskIdentity,
      compatibility,
      evidence_mode: compatibility.status === "supported_exact" ? "supported" : "experimental",
      loaded_identity: capabilities
        ? { protocol: capabilities.protocol_version, host: capabilities.host, game: capabilities.game }
        : null,
      read_kinds_exercised: [...readKinds].sort(),
      event_count: events.length,
      error: message,
      verdict: evaluateBoundedJourney({ steps, terminal, unknownCount, readFailures })
    };
    writeFileSync(eventsFile, events.map((event) => JSON.stringify(event)).join("\n") + "\n");
    writeFileSync(reportFile, `${JSON.stringify(report, null, 2)}\n`);
    throw new Error(`${message}; evidence: ${reportFile}`);
  } finally {
    try {
      await session?.close();
    } catch {
      // Cleanup must never prevent termination of the real game process.
    }
    await stopChild(child);
    await Promise.allSettled([finished(stdoutStream), finished(stderrStream)]);
    if (!existsSync(reportFile) && events.length > 0) {
      writeFileSync(eventsFile, events.map((event) => JSON.stringify(event)).join("\n") + "\n");
    }
  }
}
