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
  const evidencePackage = readJson(path.join(platformRoot, "components", "evidence", "package.json"));
  const workbenchPackage = readJson(path.join(platformRoot, "apps", "workbench", "package.json"));
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
    evidencePackage,
    workbenchPackage,
    hostConnectorRelease: hostReleaseModule.CONNECTOR_RELEASE
  };
}

export function validatePlatformBom(bom, authorities) {
  const errors = [];
  expectEqual(errors, "schema", bom.schema, "sts2.ai-platform/bom-1");
  const componentMap = {
    connector: "connector",
    host_runtime: "host-runtime",
    annotator: "annotator",
    evidence: "evidence",
    workbench: "workbench"
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
  expectEqual(errors, "Evidence package version", bom.components?.evidence?.version, authorities.evidencePackage.version);
  expectEqual(errors, "Workbench package version", bom.components?.workbench?.version, authorities.workbenchPackage.version);
  const dependency = authorities.annotatorManifest.dependencies?.find(({ id }) => id === "STS2_MCP");
  expectEqual(errors, "Annotator Connector dependency", dependency?.min_version, authorities.connectorManifest.version);

  const publicConnector = bom.public_packages?.connector_host;
  const pinnedConnector = authorities.hostConnectorRelease;
  expectEqual(errors, "public Connector release", publicConnector?.release, `connector/v${pinnedConnector.version}`);
  expectEqual(errors, "public Connector asset", publicConnector?.asset, pinnedConnector.archive);
  expectEqual(errors, "public Connector archive SHA", publicConnector?.sha256, pinnedConnector.archiveSha256);
  expectEqual(errors, "public Connector artifact SHA", publicConnector?.artifact_sha256, pinnedConnector.artifactSha256);
  expectEqual(errors, "public Connector artifact MVID", publicConnector?.artifact_mvid, pinnedConnector.artifactMvid);
  expectEqual(errors, "Host Connector protocol pin", bom.components?.player_environment_protocol, pinnedConnector.protocol);

  expectEqual(errors, "public Host release", bom.public_packages?.host_runtime?.release, `host-runtime/v${authorities.hostPackage.version}`);
  expectEqual(errors, "public Host asset", bom.public_packages?.host_runtime?.asset, `rsgcsg-sts2-host-runtime-${authorities.hostPackage.version}.tgz`);
  expectEqual(errors, "runtime Connector source", bom.exact_runtime_candidate?.connector?.source_revision, pinnedConnector.sourceRevision);
  expectEqual(errors, "runtime Connector SHA", bom.exact_runtime_candidate?.connector?.artifact_sha256, pinnedConnector.artifactSha256);
  expectEqual(errors, "runtime Connector MVID", bom.exact_runtime_candidate?.connector?.artifact_mvid, pinnedConnector.artifactMvid);
  expectEqual(errors, "runtime protocol", bom.exact_runtime_candidate?.connector?.protocol, pinnedConnector.protocol);
  expectEqual(errors, "V1 runtime generation", bom.exact_runtime_candidate?.evidence_generation, "v1_runtime_seal_predecessor");

  const v2 = bom.current_v2_candidate;
  expectEqual(errors, "V2 status", v2?.status,
    "native_human_read_rich_combat_verified_selector_pending");
  expectEqual(errors, "V2 Connector source", v2?.connector?.source_revision, bom.components?.connector?.source_revision);
  expectEqual(errors, "V2 Connector protocol", v2?.connector?.protocol, bom.components?.player_environment_protocol);
  expectPattern(errors, "V2 loaded Annotator source", v2?.annotator?.source_revision, COMMIT);
  expectPattern(errors, "V2 loaded Annotator digest", v2?.annotator?.source_digest_sha256, SHA256);
  expectEqual(errors, "V2 current Annotator source", v2?.annotator?.current_component_source_revision,
    bom.components?.annotator?.source_revision);
  expectEqual(errors, "V2 current Annotator digest", v2?.annotator?.current_component_source_digest_sha256,
    bom.components?.annotator?.component_source_digest_sha256);
  expectEqual(errors, "V2 Annotator source relation", v2?.annotator?.source_relation,
    "loaded_native_artifact_precedes_cli_only_evidence_path_and_closeout_docs");
  for (const component of ["connector", "annotator"])
    for (const level of ["build", "installed", "loaded"])
      expectEqual(errors, `V2 ${component} ${level}`, v2?.[component]?.[level], "pass");
  expectEqual(errors, "V2 observation canary", v2?.runtime?.observation_canary, "pass");
  expectEqual(errors, "V2 observer mutation boundary", v2?.runtime?.mutation, "disabled_by_observer_modset");
  expectEqual(errors, "workspace entrypoint source gate",
    v2?.automated_gates?.workspace_entrypoints_tracked, "pass");
  const v2Human = v2?.native_human_gate;
  expectEqual(errors, "V2 human gate", v2Human?.status, "partial_pass");
  expectEqual(errors, "V2 human runtime", v2Human?.runtime_instance_id, v2?.runtime?.runtime_instance_id);
  expectEqual(errors, "V2 human audit", v2Human?.audit_status, "pass");
  expectEqual(errors, "V2 human records", v2Human?.admitted_records, 30);
  expectEqual(errors, "V2 human invalidations", v2Human?.invalidations, 5);
  expectEqual(errors, "V2 human origin", v2Human?.human_origin, "owner_attested_not_machine_proven");
  expectEqual(errors, "V2 ordinary combat", v2Human?.ordinary_combat?.status, "pass");
  expectEqual(errors, "V2 targeted play", v2Human?.ordinary_combat?.targeted_play, 7);
  expectEqual(errors, "V2 untargeted play", v2Human?.ordinary_combat?.untargeted_play, 16);
  expectEqual(errors, "V2 end turn", v2Human?.ordinary_combat?.end_turn, 7);
  expectEqual(errors, "V2 interactive successors", v2Human?.ordinary_combat?.interactive_successors, 30);
  expectEqual(errors, "V2 run-deck Reads", v2Human?.reads?.run_deck, 60);
  expectEqual(errors, "V2 combat-pile Reads", v2Human?.reads?.combat_piles, 60);
  expectEqual(errors, "V2 Read failures", v2Human?.reads?.failed, 0);
  expectEqual(errors, "V2 bundle schema", v2Human?.bundle?.schema,
    "sts2.human-annotator/session-bundle-2");
  expectEqual(errors, "V2 transfer promotion", v2Human?.transfer?.initial_status, "promoted");
  expectEqual(errors, "V2 transfer retry", v2Human?.transfer?.retry_status, "reused");
  expectEqual(errors, "V2 transfer findings", v2Human?.transfer?.findings, 0);
  expectEqual(errors, "V2 STPD source", v2Human?.stpd_import?.source_revision,
    bom.external_consumer_cutovers?.stpd);
  expectEqual(errors, "V2 STPD import", v2Human?.stpd_import?.status, "pass");
  expectEqual(errors, "V2 STPD accepted", v2Human?.stpd_import?.accepted, 30);
  expectEqual(errors, "V2 STPD rejected", v2Human?.stpd_import?.rejected, 0);
  expectEqual(errors, "V2 selector source/test", v2Human?.generated_card_choice?.source_and_test_status, "pass");
  expectEqual(errors, "V2 selector runtime", v2Human?.generated_card_choice?.runtime_status, "not_exercised");

  for (const [label, value] of [
    ["public Connector archive SHA", publicConnector?.sha256],
    ["public Connector artifact SHA", publicConnector?.artifact_sha256],
    ["public SDK SHA", bom.public_packages?.typescript_sdk?.sha256],
    ["public Host SHA", bom.public_packages?.host_runtime?.sha256],
    ["public Host content digest", bom.public_packages?.host_runtime?.package_content_digest_sha256],
    ["runtime game executable SHA", bom.exact_runtime_candidate?.game?.executable_sha256],
    ["runtime game assembly SHA", bom.exact_runtime_candidate?.game?.main_assembly_sha256],
    ["runtime Annotator SHA", bom.exact_runtime_candidate?.annotator?.artifact_sha256],
    ["V2 Connector SHA", v2?.connector?.artifact_sha256],
    ["V2 Annotator SHA", v2?.annotator?.artifact_sha256],
    ["V2 loaded Annotator source digest", v2?.annotator?.source_digest_sha256],
    ["V2 current Annotator source digest", v2?.annotator?.current_component_source_digest_sha256],
    ["V2 bundle content ID", v2Human?.bundle?.content_id],
    ["V2 capture profile SHA", v2Human?.bundle?.capture_profile_sha256],
    ["V2 export SHA", v2Human?.bundle?.export_sha256],
    ["V2 checksums SHA", v2Human?.bundle?.checksums_sha256],
    ["V2 transfer manifest SHA", v2Human?.transfer?.manifest_sha256],
    ["H0 report SHA", bom.exact_runtime_candidate?.gates?.h0?.report_sha256],
    ["H1 report SHA", bom.exact_runtime_candidate?.gates?.h1?.report_sha256],
    ["H2 report SHA", bom.exact_runtime_candidate?.gates?.h2?.report_sha256],
    ["human export SHA", bom.exact_runtime_candidate?.gates?.annotator_human?.export_sha256]
  ]) expectPattern(errors, label, value, SHA256);
  expectPattern(errors, "runtime game MVID", bom.exact_runtime_candidate?.game?.main_assembly_mvid, MVID);
  expectPattern(errors, "runtime Connector MVID", bom.exact_runtime_candidate?.connector?.artifact_mvid, MVID);
  expectPattern(errors, "runtime Annotator MVID", bom.exact_runtime_candidate?.annotator?.artifact_mvid, MVID);
  expectPattern(errors, "V2 game executable SHA", v2?.game?.executable_sha256, SHA256);
  expectPattern(errors, "V2 game assembly SHA", v2?.game?.main_assembly_sha256, SHA256);
  expectPattern(errors, "V2 game MVID", v2?.game?.main_assembly_mvid, MVID);
  expectPattern(errors, "V2 Connector MVID", v2?.connector?.artifact_mvid, MVID);
  expectPattern(errors, "V2 Annotator MVID", v2?.annotator?.artifact_mvid, MVID);
  expectPattern(errors, "V2 loaded Annotator source", v2?.annotator?.source_revision, COMMIT);
  expectPattern(errors, "V2 current Annotator source", v2?.annotator?.current_component_source_revision, COMMIT);
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
  expectEqual(errors, "support level", bom.support_level,
    "human_evidence_v2_read_rich_combat_verified_selector_pending");
  if (!bom.non_claims?.includes("human_origin_owner_attested_not_machine_proven"))
    errors.push("human-origin epistemic-boundary non-claim is missing");
  if (!bom.non_claims?.includes("v2_generated_card_choice_not_exercised"))
    errors.push("V2 generated-card-choice non-claim is missing");
  if (!bom.non_claims?.includes("v2_corpus_and_training_not_authorized"))
    errors.push("V2 corpus/training authorization non-claim is missing");
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
