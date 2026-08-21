import { randomUUID } from "node:crypto";
import { createWriteStream, mkdirSync } from "node:fs";
import path from "node:path";
import { finished } from "node:stream/promises";
import {
  EnvironmentControllerSession,
  PlayerEnvironmentRestClient
} from "@rsgcsg/sts2-connector-client";
import { evaluateRuntimeCompatibility } from "./compatibility.mjs";
import { canonicalizeEpisodeSeed, evaluateEpisodeProvenance } from "./episode-provenance.mjs";
import { readDiskIdentity } from "./game-installation.mjs";
import { evaluateHeadlessCapabilities } from "./headless-host.mjs";
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
  endpoint = "http://127.0.0.1:15830",
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
    connectorEndpoint: endpoint,
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
      endpoint,
      hostControlToken: launch.hostControlToken,
      expectedRuntimeInstanceId: capabilities?.host?.runtime_instance_id ?? null
    });
    await closeStreams([stdoutStream, stderrStream]);
    return exit;
  };

  try {
    const endpointResult = await waitForEndpoint(endpoint, timeoutMs, childReference(child));
    if (!endpointResult.ok) {
      throw new Error(`Reference Connector endpoint did not become ready: ${endpointResult.error}`);
    }
    capabilities = endpointResult.value;
    const gate = evaluateHeadlessCapabilities(capabilities);
    if (!gate.ok) throw new Error(`Reference capability gate failed: ${gate.errors.join(", ")}`);
    const observations = await waitForInteractiveSnapshot(endpoint, timeoutMs, childReference(child));
    const snapshot = observations.at(-1)?.value;
    if (snapshot == null) throw new Error("Reference runtime did not mount an interactive snapshot.");

    const client = new PlayerEnvironmentRestClient(endpoint, requestTimeoutMs);
    controller = new EnvironmentControllerSession(client, {
      productId: "sts2-headless-reference-driver",
      productName: "STS2 Headless Reference Driver",
      productVersion: readProjectIdentity().version
    });
    await controller.register(capabilities.host, capabilities.control);
    const provenanceResponse = await requestHostProvenance({
      endpoint,
      hostControlToken: launch.hostControlToken,
      expectedRuntimeInstanceId: capabilities.host.runtime_instance_id,
      timeoutMs: requestTimeoutMs
    });
    const provenance = evaluateEpisodeProvenance({
      requestedSeed: canonicalSeed,
      expectedRuntimeInstanceId: capabilities.host.runtime_instance_id,
      response: provenanceResponse
    });
    if (provenance.verdict !== "provenance_pass") {
      throw new Error(`Reference episode provenance failed: ${provenance.errors.join(", ")}`);
    }

    return {
      snapshot,
      identity: {
        protocol: capabilities.protocol_version,
        host: capabilities.host,
        game: capabilities.game,
        disk: diskIdentity,
        profile,
        episode_provenance: provenance,
        evidence_directory: evidenceDirectory
      },
      observe: async () => (await client.observe()).data,
      read: async ({ readId, expectedSnapshotId }) =>
        (await client.read(readId, expectedSnapshotId)).data,
      submit: async ({ requestId, expectedSnapshotId, boundActionId }) => {
        const credentials = await controller.credentials();
        return (await client.submit({
          requestId,
          expectedSnapshotId,
          boundActionId,
          clientSessionId: credentials.clientSessionId,
          controllerLeaseId: credentials.controllerLeaseId,
          controllerGeneration: credentials.controllerGeneration
        })).data;
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
