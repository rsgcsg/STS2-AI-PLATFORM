#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath, pathToFileURL } from "node:url";
import { readIdentityReport } from "./component-identity.mjs";

const PLATFORM_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const SHA256 = /^[0-9a-f]{64}$/u;
const COMMIT = /^[0-9a-f]{40}$/u;
const MVID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/u;

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8"));
}

function expectEqual(errors, label, actual, expected) {
  if (actual !== expected) errors.push(`${label}: expected ${expected}, got ${actual ?? "missing"}`);
}

function expectPattern(errors, label, value, pattern) {
  if (typeof value !== "string" || !pattern.test(value)) errors.push(`${label}: invalid or missing`);
}

export async function readBomAuthorities(platformRoot = PLATFORM_ROOT) {
  const connectorRelease = readJson(path.join(platformRoot, "components", "connector", "release-manifest.json"));
  const connectorManifest = readJson(path.join(platformRoot, "components", "connector", "host", "mod_manifest.json"));
  const connectorSdk = readJson(path.join(platformRoot, "components", "connector", "sdk", "typescript", "package.json"));
  const hostPackage = readJson(path.join(platformRoot, "components", "host-runtime", "package.json"));
  const annotatorPackage = readJson(path.join(platformRoot, "components", "annotator", "package.json"));
  const annotatorManifest = readJson(path.join(platformRoot, "components", "annotator", "src", "STS2HumanAnnotator.Mod", "mod_manifest.json"));
  const hostReleaseModule = await import(pathToFileURL(
    path.join(platformRoot, "components", "host-runtime", "src", "connector-release.mjs")
  ));
  return {
    identities: readIdentityReport(platformRoot),
    connectorRelease,
    connectorManifest,
    connectorSdk,
    hostPackage,
    annotatorPackage,
    annotatorManifest,
    hostConnectorRelease: hostReleaseModule.CONNECTOR_RELEASE
  };
}

export function validatePlatformBom(bom, authorities) {
  const errors = [];
  expectEqual(errors, "schema", bom.schema, "sts2.ai-platform/bom-1");
  const componentMap = {
    connector: "connector",
    host_runtime: "host-runtime",
    annotator: "annotator"
  };
  for (const [bomKey, identityKey] of Object.entries(componentMap)) {
    const component = bom.components?.[bomKey];
    const identity = authorities.identities.components[identityKey];
    expectEqual(errors, `${bomKey}.version`, component?.version, identity.component_version);
    expectEqual(errors, `${bomKey}.source_revision`, component?.source_revision, identity.source_revision);
    expectEqual(errors, `${bomKey}.component_tree_revision`, component?.component_tree_revision, identity.component_tree_revision);
    expectEqual(errors, `${bomKey}.component_source_digest_sha256`, component?.component_source_digest_sha256, identity.component_source_digest_sha256);
  }

  expectEqual(errors, "connector release version", bom.components?.connector?.version, authorities.connectorRelease.release.version);
  expectEqual(errors, "connector Mod version", authorities.connectorManifest.version, authorities.connectorRelease.release.version);
  expectEqual(errors, "Player Environment protocol", bom.components?.player_environment_protocol, authorities.connectorRelease.player_environment.protocol);
  expectEqual(errors, "TypeScript SDK version", bom.components?.typescript_sdk, authorities.connectorSdk.version);
  expectEqual(errors, "Host Runtime version", bom.components?.host_runtime?.version, authorities.hostPackage.version);
  expectEqual(errors, "Annotator package version", bom.components?.annotator?.version, authorities.annotatorPackage.version);
  expectEqual(errors, "Annotator Mod version", authorities.annotatorManifest.version, authorities.annotatorPackage.version);
  const dependency = authorities.annotatorManifest.dependencies?.find(({ id }) => id === "STS2_MCP");
  expectEqual(errors, "Annotator Connector dependency", dependency?.min_version, authorities.connectorManifest.version);

  const publicConnector = bom.public_packages?.connector_host;
  const pinnedConnector = authorities.hostConnectorRelease;
  expectEqual(errors, "public Connector release", publicConnector?.release, `connector/v${pinnedConnector.version}`);
  expectEqual(errors, "public Connector asset", publicConnector?.asset, pinnedConnector.archive);
  expectEqual(errors, "public Connector archive SHA", publicConnector?.sha256, pinnedConnector.archiveSha256);
  expectEqual(errors, "public Connector artifact SHA", publicConnector?.artifact_sha256, pinnedConnector.artifactSha256);
  expectEqual(errors, "public Connector artifact MVID", publicConnector?.artifact_mvid, pinnedConnector.artifactMvid);
  expectEqual(errors, "Host Connector source pin", bom.components?.connector?.source_revision, pinnedConnector.sourceRevision);
  expectEqual(errors, "Host Connector protocol pin", bom.components?.player_environment_protocol, pinnedConnector.protocol);

  expectEqual(errors, "public Host release", bom.public_packages?.host_runtime?.release, `host-runtime/v${authorities.hostPackage.version}`);
  expectEqual(errors, "public Host asset", bom.public_packages?.host_runtime?.asset, `rsgcsg-sts2-host-runtime-${authorities.hostPackage.version}.tgz`);
  expectEqual(errors, "runtime Connector source", bom.exact_runtime_candidate?.connector?.source_revision, pinnedConnector.sourceRevision);
  expectEqual(errors, "runtime Connector SHA", bom.exact_runtime_candidate?.connector?.artifact_sha256, pinnedConnector.artifactSha256);
  expectEqual(errors, "runtime Connector MVID", bom.exact_runtime_candidate?.connector?.artifact_mvid, pinnedConnector.artifactMvid);
  expectEqual(errors, "runtime protocol", bom.exact_runtime_candidate?.connector?.protocol, pinnedConnector.protocol);
  expectEqual(errors, "runtime Annotator source", bom.exact_runtime_candidate?.annotator?.source_revision, bom.components?.annotator?.source_revision);
  expectEqual(errors, "runtime Annotator digest", bom.exact_runtime_candidate?.annotator?.source_digest_sha256, bom.components?.annotator?.component_source_digest_sha256);

  for (const [label, value] of [
    ["public Connector archive SHA", publicConnector?.sha256],
    ["public Connector artifact SHA", publicConnector?.artifact_sha256],
    ["public SDK SHA", bom.public_packages?.typescript_sdk?.sha256],
    ["public Host SHA", bom.public_packages?.host_runtime?.sha256],
    ["public Host content digest", bom.public_packages?.host_runtime?.package_content_digest_sha256],
    ["runtime game executable SHA", bom.exact_runtime_candidate?.game?.executable_sha256],
    ["runtime game assembly SHA", bom.exact_runtime_candidate?.game?.main_assembly_sha256],
    ["runtime Annotator SHA", bom.exact_runtime_candidate?.annotator?.artifact_sha256],
    ["H0 report SHA", bom.exact_runtime_candidate?.gates?.h0?.report_sha256],
    ["H1 report SHA", bom.exact_runtime_candidate?.gates?.h1?.report_sha256],
    ["H2 report SHA", bom.exact_runtime_candidate?.gates?.h2?.report_sha256],
    ["human export SHA", bom.exact_runtime_candidate?.gates?.annotator_human?.export_sha256]
  ]) expectPattern(errors, label, value, SHA256);
  expectPattern(errors, "runtime game MVID", bom.exact_runtime_candidate?.game?.main_assembly_mvid, MVID);
  expectPattern(errors, "runtime Connector MVID", bom.exact_runtime_candidate?.connector?.artifact_mvid, MVID);
  expectPattern(errors, "runtime Annotator MVID", bom.exact_runtime_candidate?.annotator?.artifact_mvid, MVID);
  expectPattern(errors, "STPD cutover", bom.external_consumer_cutovers?.stpd, COMMIT);
  for (const gate of ["h0", "h1", "h2", "annotator_loaded", "annotator_human"])
    expectEqual(errors, `${gate} gate`, bom.exact_runtime_candidate?.gates?.[gate]?.status, "pass");
  const humanGate = bom.exact_runtime_candidate?.gates?.annotator_human;
  expectEqual(errors, "human gate runtime", humanGate?.runtime_instance_id,
    bom.exact_runtime_candidate?.gates?.annotator_loaded?.runtime_instance_id);
  expectEqual(errors, "human gate audit", humanGate?.audit_status, "pass");
  expectEqual(errors, "human gate records", humanGate?.admitted_records, 30);
  expectEqual(errors, "human origin boundary", humanGate?.human_origin,
    "owner_attested_not_machine_proven");
  expectEqual(errors, "support level", bom.support_level, "runtime_seal_candidate_human_gate_passed");
  if (!bom.non_claims?.includes("human_origin_owner_attested_not_machine_proven"))
    errors.push("human-origin epistemic-boundary non-claim is missing");
  return errors;
}

export async function checkPlatformBom(platformRoot = PLATFORM_ROOT) {
  const bom = readJson(path.join(platformRoot, "platform-bom.json"));
  const authorities = await readBomAuthorities(platformRoot);
  const errors = validatePlatformBom(bom, authorities);
  return { status: errors.length === 0 ? "pass" : "fail", errors };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const report = await checkPlatformBom();
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
  if (report.errors.length > 0) process.exitCode = 1;
}
