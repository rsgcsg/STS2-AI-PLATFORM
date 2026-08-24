import { randomUUID } from "node:crypto";
import { createWriteStream, mkdirSync } from "node:fs";
import path from "node:path";
import { createServer } from "node:net";
import { finished } from "node:stream/promises";
import {
  EnvironmentControllerSession,
  PlayerEnvironmentRestClient
} from "@rsgcsg/sts2-connector-client";
import { evaluateRuntimeCompatibility } from "./compatibility.mjs";
import { canonicalizeEpisodeSeed, evaluateEpisodeProvenance } from "./episode-provenance.mjs";
import { readDiskIdentity } from "./game-installation.mjs";
import { evaluateHeadlessCapabilities } from "./headless-host.mjs";
import { chooseBoundAction } from "./journey-probe.mjs";
import { readProjectIdentity } from "./project-identity.mjs";
import { resolveLaunchProfile } from "./profile-isolation.mjs";
import { instantiateProfileTemplate } from "./profile-template.mjs";
import {
  listGameProcesses,
  requestHostProvenance,
  resolveExperimentalConnectorCanary,
  shippedRuntimeLaunch,
  stopChild,
  waitForEndpoint,
  waitForInteractiveSnapshot
} from "./runtime-probe.mjs";

function safeTimestamp() {
  return new Date().toISOString().replaceAll(":", "-").replaceAll(".", "-");
}

export async function allocateReferenceEndpoint() {
  const server = createServer();
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  const address = server.address();
  await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
  if (address == null || typeof address === "string") {
    throw new Error("Could not allocate an isolated Reference loopback endpoint.");
  }
  return `http://127.0.0.1:${address.port}`;
}

function childReference(child) {
  return {
    get exitCode() {
      return child.exitCode;
    },
    get signalCode() {
      return child.signalCode;
    }
  };
}

function stableSuccessor(snapshot, expectedSnapshotId) {
  return snapshot != null
    && snapshot.snapshot_id !== expectedSnapshotId
    && snapshot.status !== "settling";
}

export async function settleReferenceReceipt({
  receipt,
  expectedSnapshotId,
  observe,
  child,
  timeoutMs,
  pollIntervalMs = 100
}) {
  if (receipt?.delivery !== "delivered") return receipt;
  if (stableSuccessor(receipt.successor, expectedSnapshotId)) return receipt;
  const started = Date.now();
  let latest = receipt.successor ?? null;
  while (Date.now() - started < timeoutMs) {
    if (child.exitCode != null || child.signalCode != null) break;
    latest = await observe();
    if (stableSuccessor(latest, expectedSnapshotId)) {
      return {
        ...receipt,
        successor: latest,
        successor_observation: "driver_observed_after_delivery"
      };
    }
    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }
  return {
    ...receipt,
    successor: null,
    successor_observation: "timeout_after_delivered_input",
    last_observed_successor: latest
  };
}

async function closeStreams(streams) {
  for (const stream of streams) stream.end();
  await Promise.allSettled(streams.map((stream) => finished(stream)));
}

export async function startShippedPlayerEnvironmentEpisode({
  installation,
  localRoot,
  evidenceRoot,
  seed,
  templateId = "vanilla-clean",
  endpoint = null,
  timeoutMs = 90_000,
  requestTimeoutMs = 30_000,
  experimentalBuildAcknowledged = false,
  experimentalConnectorAcknowledged = false
}) {
  const canonicalSeed = canonicalizeEpisodeSeed(seed);
  if (canonicalSeed == null) throw new Error("Reference episodes require one explicit canonical seed.");
  const running = listGameProcesses();
  if (running.length > 0) {
    throw new Error(`Reference Player Environment requires a clean process baseline:\n${running.join("\n")}`);
  }

  const diskIdentity = readDiskIdentity(installation);
  const runtimeEndpoint = endpoint ?? await allocateReferenceEndpoint();
  const compatibility = evaluateRuntimeCompatibility(diskIdentity);
  if (compatibility.status !== "supported_exact" && !experimentalBuildAcknowledged) {
    throw new Error(
      `Unsupported STS2 runtime (${compatibility.mismatches.join(", ")}); `
      + "explicit experimental acknowledgement is required."
    );
  }
  const profileId = `reference-driver-${randomUUID().slice(0, 12)}`;
  const profile = instantiateProfileTemplate({
    localRoot,
    templateId,
    profileId,
    expectedGameIdentity: diskIdentity
  });
  const launchProfile = resolveLaunchProfile({ localRoot, isolatedProfileId: profileId });
  const connectorCanary = resolveExperimentalConnectorCanary({
    installation,
    compatibility,
    acknowledged: experimentalBuildAcknowledged || experimentalConnectorAcknowledged
  });
  const evidenceDirectory = path.join(evidenceRoot, `reference-driver-${safeTimestamp()}-${profileId.slice(-12)}`);
  mkdirSync(evidenceDirectory, { recursive: true });
  const stdoutStream = createWriteStream(path.join(evidenceDirectory, "stdout.log"));
  const stderrStream = createWriteStream(path.join(evidenceDirectory, "stderr.log"));
  const launch = shippedRuntimeLaunch(installation, {
    launchProfile,
    connectorEndpoint: runtimeEndpoint,
    runSeed: canonicalSeed,
    connectorCanary
  });
  const { child } = launch;
  child.stdout.pipe(stdoutStream);
  child.stderr.pipe(stderrStream);
  let controller = null;
  let capabilities = null;
  let closed = false;

  const close = async () => {
    if (closed) return null;
    closed = true;
    await controller?.close().catch(() => null);
    const exit = await stopChild(child, {
      endpoint: runtimeEndpoint,
      hostControlToken: launch.hostControlToken,
      expectedRuntimeInstanceId: capabilities?.host?.runtime_instance_id ?? null
    });
    await closeStreams([stdoutStream, stderrStream]);
    return exit;
  };

  try {
    const endpointResult = await waitForEndpoint(runtimeEndpoint, timeoutMs, childReference(child));
    if (!endpointResult.ok) {
      throw new Error(`Reference Connector endpoint did not become ready: ${endpointResult.error}`);
    }
    capabilities = endpointResult.value;
    const gate = evaluateHeadlessCapabilities(capabilities);
    if (!gate.ok) throw new Error(`Reference capability gate failed: ${gate.errors.join(", ")}`);
    const observations = await waitForInteractiveSnapshot(runtimeEndpoint, timeoutMs, childReference(child));
    let snapshot = observations.at(-1)?.value;
    if (snapshot == null) throw new Error("Reference runtime did not mount an interactive snapshot.");

    const client = new PlayerEnvironmentRestClient(runtimeEndpoint, requestTimeoutMs);
    controller = new EnvironmentControllerSession(client, {
      productId: "sts2-headless-reference-driver",
      productName: "STS2 Headless Reference Driver",
      productVersion: readProjectIdentity().version
    });
    await controller.register(capabilities.host, capabilities.control);
    const provenanceResponse = await requestHostProvenance({
      endpoint: runtimeEndpoint,
      hostControlToken: launch.hostControlToken,
      expectedRuntimeInstanceId: capabilities.host.runtime_instance_id,
      timeoutMs: requestTimeoutMs
    });
    let provenance = evaluateEpisodeProvenance({
      requestedSeed: canonicalSeed,
      expectedRuntimeInstanceId: capabilities.host.runtime_instance_id,
      response: provenanceResponse
    });

    const refreshProvenance = async () => {
      const response = await requestHostProvenance({
        endpoint: runtimeEndpoint,
        hostControlToken: launch.hostControlToken,
        expectedRuntimeInstanceId: capabilities.host.runtime_instance_id,
        timeoutMs: requestTimeoutMs
      });
      provenance = evaluateEpisodeProvenance({
        requestedSeed: canonicalSeed,
        expectedRuntimeInstanceId: capabilities.host.runtime_instance_id,
        response
      });
      return provenance;
    };

    const bootstrapTrace = [];
    const runEntryKinds = new Set([
      "main_menu",
      "singleplayer_menu",
      "character_select",
      "tutorial",
      "tutorial_preference"
    ]);
    for (let index = 0; runEntryKinds.has(snapshot.interaction.kind) && index < 16; index += 1) {
      const action = chooseBoundAction(snapshot, { tutorialPreference: "disable" });
      if (action == null) {
        throw new Error(`Reference reset cannot safely advance ${snapshot.interaction.kind}.`);
      }
      const credentials = await controller.credentials();
      const receipt = (await client.submit({
        requestId: `reference-reset-${String(index + 1).padStart(2, "0")}-${randomUUID()}`,
        expectedSnapshotId: snapshot.snapshot_id,
        boundActionId: action.bound_action_id,
        clientSessionId: credentials.clientSessionId,
        controllerLeaseId: credentials.controllerLeaseId,
        controllerGeneration: credentials.controllerGeneration
      })).data;
      const settled = await settleReferenceReceipt({
        receipt,
        expectedSnapshotId: snapshot.snapshot_id,
        observe: async () => (await client.observe()).data,
        child,
        timeoutMs: requestTimeoutMs
      });
      bootstrapTrace.push({
        interaction_kind: snapshot.interaction.kind,
        verb: action.verb,
        label: action.label,
        delivery: settled.delivery,
        reason_code: settled.reason_code ?? null,
        successor_kind: settled.successor?.interaction?.kind ?? null
      });
      if (settled.delivery !== "delivered" || settled.successor == null) {
        throw new Error(
          `Reference reset input was not followed by a stable successor: `
          + `${settled.delivery}:${settled.reason_code ?? settled.successor_observation ?? "unspecified"}.`
        );
      }
      snapshot = settled.successor;
      await refreshProvenance();
    }
    if (runEntryKinds.has(snapshot.interaction.kind)) {
      throw new Error("Reference reset exceeded the bounded run-entry action budget.");
    }
    if (snapshot.status !== "interactive" || snapshot.interaction.kind !== "map_navigation") {
      throw new Error(
        `Reference reset expected the first map decision, observed ${snapshot.status}:${snapshot.interaction.kind}.`
      );
    }
    await refreshProvenance();
    if (provenance.verdict !== "provenance_pass") {
      throw new Error(`Reference run seed was not proven after bootstrap: ${provenance.errors.join(", ")}`);
    }

    return {
      snapshot,
      identity: {
        protocol: capabilities.protocol_version,
        host: capabilities.host,
        game: capabilities.game,
        disk: diskIdentity,
        profile,
        get episode_provenance() {
          return provenance;
        },
        bootstrap_trace: bootstrapTrace,
        evidence_directory: evidenceDirectory,
        endpoint: runtimeEndpoint
      },
      observe: async () => (await client.observe()).data,
      read: async ({ readId, expectedSnapshotId }) =>
        (await client.read(readId, expectedSnapshotId)).data,
      provenance: refreshProvenance,
      submit: async ({ requestId, expectedSnapshotId, boundActionId }) => {
        const credentials = await controller.credentials();
        const receipt = (await client.submit({
          requestId,
          expectedSnapshotId,
          boundActionId,
          clientSessionId: credentials.clientSessionId,
          controllerLeaseId: credentials.controllerLeaseId,
          controllerGeneration: credentials.controllerGeneration
        })).data;
        const settled = await settleReferenceReceipt({
          receipt,
          expectedSnapshotId,
          observe: async () => (await client.observe()).data,
          child,
          timeoutMs: requestTimeoutMs
        });
        await refreshProvenance();
        return settled;
      },
      close
    };
  } catch (error) {
    await close();
    throw error;
  }
}

export class ShippedPlayerEnvironmentSession {
  constructor(options, { startEpisode = startShippedPlayerEnvironmentEpisode } = {}) {
    this.options = options;
    this.startEpisode = startEpisode;
    this.episode = null;
    this.lastIdentity = null;
  }

  async reset(seed) {
    await this.closeEpisode();
    this.episode = await this.startEpisode({ ...this.options, seed });
    this.lastIdentity = this.episode.identity;
    return this.episode.snapshot;
  }

  requireEpisode() {
    if (this.episode == null) throw new Error("Reference Player Environment must be reset before use.");
    return this.episode;
  }

  observe() {
    return this.requireEpisode().observe();
  }

  read(input) {
    return this.requireEpisode().read(input);
  }

  submit(input) {
    return this.requireEpisode().submit(input);
  }

  async provenance() {
    const episode = this.requireEpisode();
    const episodeProvenance = await episode.provenance();
    this.lastIdentity = {
      ...this.lastIdentity,
      episode_provenance: episodeProvenance
    };
    return this.lastIdentity;
  }

  async closeEpisode() {
    if (this.episode == null) return null;
    const current = this.episode;
    this.episode = null;
    return current.close();
  }

  close() {
    return this.closeEpisode();
  }
}
