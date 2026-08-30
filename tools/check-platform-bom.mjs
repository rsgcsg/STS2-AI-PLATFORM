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
const LOADED_SCHEMA3_ANNOTATOR = Object.freeze({
  sourceRevision: "54efe38d6d2f49051e04248072acb548feddfe9a",
  workspaceRevisionAtBuild: "750315b3b998a04507439869c96ba78d280787fb",
  componentTreeRevision: "e07fa1ad8b4939cbc5e6435d818acacc4565b57d",
  componentSourceDigest: "b95e8c8e630f793b90f7328c3f1ea8374a6c252c0cfff55560cab43619da3a54",
  buildSourceDigest: "a601e6d9ee85c54fbf1841535dc11c59e7d224a20f0d6b003493a7e1b53aa622",
  artifactSha: "4fa6757045b6d5c2b137e78b1e96e7163c2a5c64372a41955682257d6a6a1056",
  artifactMvid: "51c7c37b-3305-4286-b2bc-52cd5725ac76",
  runtimeInstance: "7bcc19e7fb614eedad563db93310adc7",
  environment: "15177b88c13f87fac1c4b676aee2529a643411952eeda50b82ca67837be1f15f",
  modset: "2263e3958c03544a5a43ed462be1f85406a9a1c0fba8bf981a0c4c69fe54b544",
  rollback: "apps/game-mod/.local/deployments/2026-08-28T16-46-50.719Z"
});
const POTION_OWNER_CANARY = Object.freeze({
  sourceRevision: "e1d88e3582d3d51a383d366d5ede517ca6a98e40",
  componentSourceDigest: "724aafeb04bdb7980585dc3973e3d4199252b3774b504fc2a1f3cd75074e600b",
  artifactSha: "be1a96ec762139de7bcda8ec5f4898a482c6dc03cf4fd18e20be41585eb22380",
  artifactMvid: "79354979-0488-42c3-bd83-8b90d6bbf9e4",
  runtimeInstance: "1ad1e3f83e9545ab911bd75f85262a96",
  environment: "94b59c951d4b0004d85bba9de2a35c3fd28b12d35b9ed2e996c8b81fc8c3fafc",
  modset: "35c367613f6caf041842a02850582477edd1dfba018316dbf599f2f79aa81915",
  rollback: "apps/game-mod/.local/deployments/2026-08-27T16-03-47.774Z"
});
const NATIVE_SEMANTIC_DISCRIMINATOR = Object.freeze({
  sourceRevision: "05d9e8e859a26b306f23ae5de188347d0570781b",
  connectorTree: "42206612cbb07f18ec048286486534edcde5b4f2",
  connectorDigest: "7e96c7a8fc31fdf2f198d50bc674b54547b051dd533c8e089b800cad178ef9a9",
  annotatorTree: "a849c967e0b361cef10a1ee0ea3bb17070d12e85",
  annotatorDigest: "d0eb10bf40d381fde88fcf5e0cc146aa9429a126c7cce3f9345acebb2d0e6e0b",
  artifactSha: "d3b59bed5453b62e0f6e7b1efc3d0414748ec354d1205bf06edbd46ee1e301c1",
  artifactMvid: "04acd691-90f8-4105-93d8-b260f4726315",
  runtimeInstance: "f015b026a0d043538e4a3f8403476056",
  environment: "190234e4a3b270d4447e13598c45aa205d40766b98f6efa2b99c3790b77386d2",
  modset: "968a30c304f7ba8befd459f279a1f36957eb5dbebed94e8016111bce3389288c",
  sessionId: "session-20260830T064823Z-ed1d683fe0b44e1db312c7489cda7fba",
  timelineId: "timeline-bc4ee13a1bdd400bbec356e5a0abdbdc",
  auditCloseoutSourceRevision: "193861ad9f8e1e7c058a292942b5cf5729aad413"
});

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
  const nativeFoundationComponent = readJson(path.join(platformRoot, "components", "native-foundation", "component.json"));
  const connectorRelease = readJson(path.join(platformRoot, "components", "connector", "release-manifest.json"));
  const connectorManifest = readJson(path.join(platformRoot, "components", "connector", "host", "mod_manifest.json"));
  const connectorSdk = readJson(path.join(platformRoot, "components", "connector", "sdk", "typescript", "package.json"));
  const hostPackage = readJson(path.join(platformRoot, "components", "host-runtime", "package.json"));
  const annotatorPackage = readJson(path.join(platformRoot, "components", "annotator", "package.json"));
  const annotatorManifest = readJson(path.join(platformRoot, "components", "annotator", "src", "STS2HumanAnnotator.Mod", "mod_manifest.json"));
  const evidencePackage = readJson(path.join(platformRoot, "components", "evidence", "package.json"));
  const policyRuntimePackage = readJson(path.join(platformRoot, "components", "policy-runtime", "package.json"));
  const workbenchPackage = readJson(path.join(platformRoot, "apps", "workbench", "package.json"));
  const liveUiPackage = readJson(path.join(platformRoot, "apps", "ingame-ui", "package.json"));
  const gameModPackage = readJson(path.join(platformRoot, "apps", "game-mod", "package.json"));
  const hostReleaseModule = await import(pathToFileURL(
    path.join(platformRoot, "components", "host-runtime", "src", "connector-release.mjs")
  ));
  return {
    identities: readIdentityReport(platformRoot),
    nativeFoundationComponent,
    connectorRelease,
    connectorManifest,
    connectorSdk,
    hostPackage,
    annotatorPackage,
    annotatorManifest,
    evidencePackage,
    policyRuntimePackage,
    workbenchPackage,
    liveUiPackage,
    gameModPackage,
    hostConnectorRelease: hostReleaseModule.CONNECTOR_RELEASE
  };
}

export function validatePlatformBom(bom, authorities) {
  const errors = [];
  expectEqual(errors, "schema", bom.schema, "sts2.ai-platform/bom-1");
  const componentMap = {
    native_foundation: "native-foundation",
    connector: "connector",
    host_runtime: "host-runtime",
    annotator: "annotator",
    evidence: "evidence",
    policy_runtime: "policy-runtime",
    workbench: "workbench",
    live_ui: "live-ui",
    game_mod: "game-mod"
  };
  for (const [bomKey, identityKey] of Object.entries(componentMap)) {
    const component = bom.components?.[bomKey];
    const identity = authorities.identities.components[identityKey];
    expectEqual(errors, `${bomKey}.version`, component?.version, identity.component_version);
    expectEqual(errors, `${bomKey}.source_revision`, component?.source_revision, identity.source_revision);
    expectEqual(errors, `${bomKey}.component_tree_revision`, component?.component_tree_revision, identity.component_tree_revision);
    expectEqual(errors, `${bomKey}.component_source_digest_sha256`, component?.component_source_digest_sha256, identity.component_source_digest_sha256);
  }
  expectEqual(errors, "Native Foundation version", bom.components?.native_foundation?.version,
    authorities.nativeFoundationComponent.version);
  expectEqual(errors, "connector release version", bom.components?.connector?.version, authorities.connectorRelease.release.version);
  expectEqual(errors, "connector Mod version", authorities.connectorManifest.version, authorities.connectorRelease.release.version);
  expectEqual(errors, "Player Environment protocol", bom.components?.player_environment_protocol, authorities.connectorRelease.player_environment.protocol);
  expectEqual(errors, "TypeScript SDK version", bom.components?.typescript_sdk, authorities.connectorSdk.version);
  expectEqual(errors, "Host Runtime version", bom.components?.host_runtime?.version, authorities.hostPackage.version);
  expectEqual(errors, "Annotator package version", bom.components?.annotator?.version, authorities.annotatorPackage.version);
  expectEqual(errors, "Annotator Mod version", authorities.annotatorManifest.version, authorities.annotatorPackage.version);
  expectEqual(errors, "Evidence package version", bom.components?.evidence?.version, authorities.evidencePackage.version);
  expectEqual(errors, "Policy Runtime package version", bom.components?.policy_runtime?.version, authorities.policyRuntimePackage.version);
  expectEqual(errors, "Workbench package version", bom.components?.workbench?.version, authorities.workbenchPackage.version);
  expectEqual(errors, "Live UI package version", bom.components?.live_ui?.version, authorities.liveUiPackage.version);
  expectEqual(errors, "Game Mod package version", bom.components?.game_mod?.version, authorities.gameModPackage.version);
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
  expectPattern(errors, "V2 loaded Connector source", v2?.connector?.source_revision, COMMIT);
  expectEqual(errors, "V2 current Connector source", v2?.connector?.current_component_source_revision,
    bom.components?.connector?.source_revision);
  expectEqual(errors, "V2 current Connector digest", v2?.connector?.current_component_source_digest_sha256,
    bom.components?.connector?.component_source_digest_sha256);
  expectEqual(errors, "V2 Connector source relation", v2?.connector?.source_relation,
    "loaded_human_v2_artifact_precedes_unified_platform_source");
  expectEqual(errors, "V2 Connector protocol", v2?.connector?.protocol, bom.components?.player_environment_protocol);
  expectPattern(errors, "V2 loaded Annotator source", v2?.annotator?.source_revision, COMMIT);
  expectPattern(errors, "V2 loaded Annotator digest", v2?.annotator?.source_digest_sha256, SHA256);
  expectEqual(errors, "V2 current Annotator source", v2?.annotator?.current_component_source_revision,
    bom.components?.annotator?.source_revision);
  expectEqual(errors, "V2 current Annotator digest", v2?.annotator?.current_component_source_digest_sha256,
    bom.components?.annotator?.component_source_digest_sha256);
  expectEqual(errors, "V2 Annotator source relation", v2?.annotator?.source_relation,
    "loaded_human_v2_artifact_precedes_unified_platform_source");
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
  expectPattern(errors, "V2 STPD source", v2Human?.stpd_import?.source_revision, COMMIT);
  expectEqual(errors, "V2 STPD import", v2Human?.stpd_import?.status, "pass");
  expectEqual(errors, "V2 STPD accepted", v2Human?.stpd_import?.accepted, 30);
  expectEqual(errors, "V2 STPD rejected", v2Human?.stpd_import?.rejected, 0);
  expectEqual(errors, "V2 selector source/test", v2Human?.generated_card_choice?.source_and_test_status, "pass");
  expectEqual(errors, "V2 selector runtime", v2Human?.generated_card_choice?.runtime_status, "not_exercised");

  const policyCandidate = bom.unified_platform_runtime_candidate;
  expectEqual(errors, "unified Platform candidate status", policyCandidate?.status,
    "native_semantic_discriminator_bounded_human_pass");
  const discriminatorCandidate = policyCandidate?.native_semantic_discriminator_source_candidate;
  expectEqual(errors, "native discriminator candidate status", discriminatorCandidate?.status,
    "human_canary_bounded_semantic_lane_supported");
  expectEqual(errors, "native discriminator workspace",
    discriminatorCandidate?.workspace_revision_at_build,
    NATIVE_SEMANTIC_DISCRIMINATOR.sourceRevision);
  expectEqual(errors, "native discriminator Connector source",
    discriminatorCandidate?.connector_source_revision,
    NATIVE_SEMANTIC_DISCRIMINATOR.sourceRevision);
  expectEqual(errors, "native discriminator Connector tree",
    discriminatorCandidate?.connector_component_tree_revision,
    NATIVE_SEMANTIC_DISCRIMINATOR.connectorTree);
  expectEqual(errors, "native discriminator Connector digest",
    discriminatorCandidate?.connector_component_source_digest_sha256,
    NATIVE_SEMANTIC_DISCRIMINATOR.connectorDigest);
  expectEqual(errors, "native discriminator Annotator source",
    discriminatorCandidate?.annotator_source_revision,
    NATIVE_SEMANTIC_DISCRIMINATOR.sourceRevision);
  expectEqual(errors, "native discriminator Annotator tree",
    discriminatorCandidate?.annotator_component_tree_revision,
    NATIVE_SEMANTIC_DISCRIMINATOR.annotatorTree);
  expectEqual(errors, "native discriminator Annotator digest",
    discriminatorCandidate?.annotator_component_source_digest_sha256,
    NATIVE_SEMANTIC_DISCRIMINATOR.annotatorDigest);
  expectEqual(errors, "native discriminator artifact",
    discriminatorCandidate?.artifact_sha256, NATIVE_SEMANTIC_DISCRIMINATOR.artifactSha);
  expectEqual(errors, "native discriminator MVID",
    discriminatorCandidate?.artifact_mvid, NATIVE_SEMANTIC_DISCRIMINATOR.artifactMvid);
  expectEqual(errors, "native discriminator build", discriminatorCandidate?.built,
    "pass_clean_source");
  expectEqual(errors, "native discriminator install", discriminatorCandidate?.installed, "pass");
  expectEqual(errors, "native discriminator load", discriminatorCandidate?.loaded, "pass");
  expectEqual(errors, "native discriminator Human runtime",
    discriminatorCandidate?.human_runtime, "pass_bounded_owner_canary");
  expectEqual(errors, "native discriminator runtime",
    discriminatorCandidate?.runtime_instance_id, NATIVE_SEMANTIC_DISCRIMINATOR.runtimeInstance);
  expectEqual(errors, "native discriminator environment",
    discriminatorCandidate?.environment_fingerprint, NATIVE_SEMANTIC_DISCRIMINATOR.environment);
  expectEqual(errors, "native discriminator Modset status",
    discriminatorCandidate?.modset_status, "exact_platform_modset");
  expectEqual(errors, "native discriminator Modset",
    discriminatorCandidate?.modset_fingerprint, NATIVE_SEMANTIC_DISCRIMINATOR.modset);
  if (!Array.isArray(discriminatorCandidate?.loaded_mod_ids)
    || discriminatorCandidate.loaded_mod_ids.length !== 1
    || discriminatorCandidate.loaded_mod_ids[0] !== "STS2_PLATFORM")
    errors.push("native discriminator loaded Mods: expected only STS2_PLATFORM");
  expectEqual(errors, "native discriminator runtime status",
    discriminatorCandidate?.runtime_status, "recording_closed_process_exited");
  const discriminatorHuman = discriminatorCandidate?.owner_canary;
  expectEqual(errors, "native discriminator Human session", discriminatorHuman?.session_id,
    NATIVE_SEMANTIC_DISCRIMINATOR.sessionId);
  expectEqual(errors, "native discriminator Human timeline", discriminatorHuman?.timeline_id,
    NATIVE_SEMANTIC_DISCRIMINATOR.timelineId);
  expectEqual(errors, "native discriminator Human origin", discriminatorHuman?.human_origin,
    "owner_attested_not_machine_proven");
  expectEqual(errors, "native discriminator Human audit", discriminatorHuman?.audit_status,
    "pass_after_audit_aggregation_fix");
  expectEqual(errors, "native discriminator audit source",
    discriminatorHuman?.audit_closeout_source_revision,
    NATIVE_SEMANTIC_DISCRIMINATOR.auditCloseoutSourceRevision);
  for (const field of [
    "manifest_sha256",
    "decision_v2_sha256",
    "native_ledger_sha256",
    "native_semantic_discriminator_sha256"
  ]) expectPattern(errors, `native discriminator Human ${field}`, discriminatorHuman?.[field], SHA256);
  for (const [field, expected] of Object.entries({
    valid_decision_v2: 40,
    invalid_decision_v2: 0,
    native_accepted: 41,
    native_successful: 41,
    native_cancelled: 0,
    native_aborted: 0,
    native_unknown: 0,
    semantic_exact_once_membership: 41,
    play_card: 30,
    end_turn: 10,
    use_potion: 1,
    player_choice_pauses: 2,
    player_choice_resumes: 2,
    ordinary_execution_handoff_candidates: 40,
    overlapping_acceptance: 0,
    ui_frame_not_authoritative_at_execution: 34,
    ui_complete_catalog_zero_membership: 7,
    legacy_strict_transition_admitted: 40,
    legacy_strict_transition_invalidated_on_close: 1
  })) expectEqual(errors, `native discriminator Human ${field}`, discriminatorHuman?.[field], expected);
  expectEqual(errors, "native discriminator route verdict", discriminatorHuman?.route_verdict,
    "FEASIBLE_FULL_RUN_NATIVE_SEMANTIC_RECORDER_EXISTS");
  expectEqual(errors, "native discriminator predecessor transfer",
    discriminatorCandidate?.evidence_transfer_from_predecessor, false);
  expectPattern(errors, "native discriminator rollback", discriminatorCandidate?.rollback,
    /^apps\/game-mod\/\.local\/deployments\/[0-9TZ.:-]+$/u);
  const serializedCandidate = policyCandidate?.serialized_canonical_loaded_candidate;
  expectEqual(errors, "serialized candidate status", serializedCandidate?.status,
    "loaded_pending_owner_human_canary");
  expectPattern(errors, "serialized candidate workspace", serializedCandidate?.workspace_revision_at_build,
    COMMIT);
  expectPattern(errors, "serialized candidate Platform source", serializedCandidate?.platform_source_revision,
    COMMIT);
  expectPattern(errors, "serialized candidate Platform digest",
    serializedCandidate?.platform_source_digest_sha256, SHA256);
  expectPattern(errors, "serialized candidate Connector source",
    serializedCandidate?.connector_source_revision, COMMIT);
  expectPattern(errors, "serialized candidate Connector digest",
    serializedCandidate?.connector_source_digest_sha256, SHA256);
  expectPattern(errors, "serialized candidate Annotator source",
    serializedCandidate?.annotator_source_revision, COMMIT);
  expectPattern(errors, "serialized candidate Annotator digest",
    serializedCandidate?.annotator_source_digest_sha256, SHA256);
  expectPattern(errors, "serialized candidate artifact", serializedCandidate?.artifact_sha256, SHA256);
  expectPattern(errors, "serialized candidate MVID", serializedCandidate?.artifact_mvid, MVID);
  expectEqual(errors, "serialized candidate protocol", serializedCandidate?.player_environment_protocol,
    "1.0.0");
  expectPattern(errors, "serialized candidate runtime", serializedCandidate?.runtime_instance_id,
    /^[0-9a-f]{32}$/u);
  expectPattern(errors, "serialized candidate environment", serializedCandidate?.environment_fingerprint,
    SHA256);
  expectEqual(errors, "serialized candidate Modset status", serializedCandidate?.modset_status,
    "exact_platform_modset");
  expectPattern(errors, "serialized candidate Modset", serializedCandidate?.modset_fingerprint, SHA256);
  if (!Array.isArray(serializedCandidate?.loaded_mod_ids)
    || serializedCandidate.loaded_mod_ids.length !== 1
    || serializedCandidate.loaded_mod_ids[0] !== "STS2_PLATFORM")
    errors.push("serialized candidate loaded Mods: expected only STS2_PLATFORM");
  for (const level of ["built", "installed", "loaded"])
    expectEqual(errors, `serialized candidate ${level}`, serializedCandidate?.[level], "pass");
  expectEqual(errors, "serialized candidate installed identity",
    serializedCandidate?.installed_identity, "verified_unified_platform_sidecar");
  expectEqual(errors, "serialized candidate runtime status", serializedCandidate?.runtime_status,
    "ready_no_session");
  expectEqual(errors, "serialized candidate Human runtime", serializedCandidate?.human_runtime,
    "not_exercised");
  expectEqual(errors, "serialized candidate canonical stream", serializedCandidate?.canonical_stream,
    "not_exercised");
  expectEqual(errors, "serialized candidate after latency", serializedCandidate?.after_latency,
    "not_measured");
  const serializedHost = serializedCandidate?.host_automation;
  expectEqual(errors, "serialized candidate isolated Host bootstrap",
    serializedHost?.isolated_profile_bootstrap, "pass_shared_profile_unchanged");
  expectEqual(errors, "serialized candidate same-artifact prefix",
    serializedHost?.same_artifact_prefix_9, "semantic_match");
  expectEqual(errors, "serialized candidate same-artifact rapid trajectory",
    serializedHost?.same_artifact_rapid_12, "semantic_mismatch_native_effect_timing");
  expectEqual(errors, "serialized candidate Managed actions", serializedHost?.managed_exact_actions, 80);
  expectEqual(errors, "serialized candidate Managed reads", serializedHost?.managed_exact_reads, 158);
  expectEqual(errors, "serialized candidate Managed authority gates",
    serializedHost?.managed_exact_reset_stale_idempotency, "pass");
  if (!(serializedHost?.managed_exact_qualification_decisions_per_second > 0))
    errors.push("serialized candidate Managed performance: expected positive measured throughput");
  expectEqual(errors, "serialized candidate cross-Host rapid trajectory",
    serializedHost?.cross_host_rapid_prefix,
    "semantic_mismatch_reference_pre_effect_vs_managed_post_effect");
  expectEqual(errors, "serialized candidate predecessor transfer",
    serializedCandidate?.evidence_transfer_from_predecessor, false);
  expectPattern(errors, "serialized candidate rollback", serializedCandidate?.rollback,
    /^apps\/game-mod\/\.local\/deployments\/[0-9TZ.:-]+$/u);
  expectEqual(errors, "candidate STPD source", policyCandidate?.external_policy?.stpd_source_revision,
    bom.external_consumer_cutovers?.stpd);
  expectEqual(errors, "candidate policy checkpoint", policyCandidate?.external_policy?.checkpoint_status,
    "absent");
  expectEqual(errors, "candidate current Annotator source",
    policyCandidate?.annotator?.current_component_source_revision,
    bom.components?.annotator?.source_revision);
  expectEqual(errors, "candidate current Annotator digest",
    policyCandidate?.annotator?.current_component_source_digest_sha256,
    bom.components?.annotator?.component_source_digest_sha256);
  expectEqual(errors, "candidate current Live UI source",
    policyCandidate?.live_ui?.current_component_source_revision,
    bom.components?.live_ui?.source_revision);
  expectEqual(errors, "candidate current Live UI digest",
    policyCandidate?.live_ui?.current_component_source_digest_sha256,
    bom.components?.live_ui?.component_source_digest_sha256);
  expectPattern(errors, "candidate loaded Connector source", policyCandidate?.connector?.source_revision, COMMIT);
  expectEqual(errors, "candidate current Connector source",
    policyCandidate?.connector?.current_component_source_revision,
    bom.components?.connector?.source_revision);
  expectEqual(errors, "candidate current Connector digest",
    policyCandidate?.connector?.current_component_source_digest_sha256,
    bom.components?.connector?.component_source_digest_sha256);
  expectEqual(errors, "candidate Connector source relation", policyCandidate?.connector?.source_relation,
    "loaded_native_source_precedes_current_discriminator_source");
  expectEqual(errors, "candidate Annotator source relation", policyCandidate?.annotator?.source_relation,
    "loaded_native_source_precedes_current_audit_closeout_source");
  const semanticTimeline = policyCandidate?.semantic_timeline_source_candidate;
  expectEqual(errors, "semantic timeline source status", semanticTimeline?.status,
    "human_canary_bounded_live_proved");
  expectPattern(errors, "semantic timeline proved Annotator source",
    semanticTimeline?.annotator_source_revision, COMMIT);
  expectEqual(errors, "semantic timeline trace schema", semanticTimeline?.trace_schema,
    "sts2.human-annotator/semantic-boundary-trace-event-2");
  expectPattern(errors, "semantic timeline workspace at build",
    semanticTimeline?.workspace_revision_at_build, COMMIT);
  expectPattern(errors, "semantic timeline source digest",
    semanticTimeline?.annotator_source_digest_sha256, SHA256);
  expectPattern(errors, "semantic timeline artifact", semanticTimeline?.artifact_sha256, SHA256);
  expectPattern(errors, "semantic timeline MVID", semanticTimeline?.artifact_mvid, MVID);
  expectEqual(errors, "semantic timeline built", semanticTimeline?.built, "pass");
  expectEqual(errors, "semantic timeline installed", semanticTimeline?.installed, "pass");
  expectEqual(errors, "semantic timeline loaded", semanticTimeline?.loaded, "pass");
  expectEqual(errors, "semantic timeline Human runtime", semanticTimeline?.human_runtime,
    "pass_bounded_owner_canary");
  expectEqual(errors, "semantic timeline protocol", semanticTimeline?.player_environment_protocol,
    bom.components?.player_environment_protocol);
  expectPattern(errors, "semantic timeline runtime", semanticTimeline?.runtime_instance_id,
    /^[0-9a-f]{32}$/u);
  expectPattern(errors, "semantic timeline environment", semanticTimeline?.environment_fingerprint,
    SHA256);
  expectEqual(errors, "semantic timeline Modset status", semanticTimeline?.modset_status,
    "exact_platform_modset");
  expectPattern(errors, "semantic timeline Modset", semanticTimeline?.modset_fingerprint, SHA256);
  expectEqual(errors, "semantic timeline loaded Mods",
    JSON.stringify(semanticTimeline?.loaded_mod_ids), JSON.stringify(["STS2_PLATFORM"]));
  const timelineCanary = semanticTimeline?.owner_canary;
  expectPattern(errors, "semantic timeline owner session", timelineCanary?.session_id,
    /^session-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{32}$/u);
  expectPattern(errors, "semantic timeline owner timeline", timelineCanary?.timeline_id,
    /^timeline-[0-9a-f]{32}$/u);
  expectEqual(errors, "semantic timeline owner audit", timelineCanary?.audit_status, "pass");
  expectEqual(errors, "semantic timeline owner origin", timelineCanary?.human_origin,
    "owner_attested_not_machine_proven");
  for (const [label, actual, expected] of [
    ["valid records", timelineCanary?.valid_records, 11],
    ["invalid records", timelineCanary?.invalid_records, 0],
    ["invalidations", timelineCanary?.invalidations, 38],
    ["Reads", timelineCanary?.reads_materialized, 3116],
    ["Read failures", timelineCanary?.reads_failed, 0],
    ["ledger accepted", timelineCanary?.ledger_accepted, 30],
    ["ledger started", timelineCanary?.ledger_started, 21],
    ["ledger finished", timelineCanary?.ledger_finished, 18],
    ["ledger cancelled", timelineCanary?.ledger_cancelled, 12],
    ["ledger admitted", timelineCanary?.ledger_strict_admitted, 10],
    ["ledger invalidated", timelineCanary?.ledger_strict_invalidated, 20],
    ["ledger unresolved", timelineCanary?.ledger_unresolved, 0],
    ["semantic accepted", timelineCanary?.semantic_accepted, 31],
    ["semantic started", timelineCanary?.semantic_started, 22],
    ["semantic proved", timelineCanary?.semantic_proved, 19],
    ["semantic standalone unknown", timelineCanary?.semantic_standalone_unknown, 0],
    ["semantic cancelled before start", timelineCanary?.semantic_cancelled_before_start, 9],
    ["semantic cancelled after start", timelineCanary?.semantic_cancelled_after_start_unknown, 3],
    ["semantic abort", timelineCanary?.semantic_aborted_before_commit, 0],
    ["semantic unresolved", timelineCanary?.semantic_unresolved, 0],
    ["intervening Human start", timelineCanary?.proved_with_intervening_human_start, 0],
    ["pre/execution mismatch", timelineCanary?.proved_pre_execution_boundary_mismatch, 0],
    ["execution handoff proved", timelineCanary?.execution_handoff_proved, 1],
    ["execution handoff mismatch", timelineCanary?.execution_handoff_mismatch, 0],
    ["complete execution boundaries", timelineCanary?.execution_boundary_complete_state_reads, 22]
  ]) expectEqual(errors, `semantic timeline ${label}`, actual, expected);
  for (const [label, value] of [
    ["Decision V2 SHA", timelineCanary?.decision_v2_sha256],
    ["invalidations SHA", timelineCanary?.invalidations_sha256],
    ["ledger SHA", timelineCanary?.ledger_sha256],
    ["trace SHA", timelineCanary?.semantic_trace_sha256],
    ["RunJournal SHA", timelineCanary?.run_journal_sha256]
  ]) expectPattern(errors, `semantic timeline ${label}`, value, SHA256);
  expectEqual(errors, "semantic timeline ledger schema", timelineCanary?.ledger_schema,
    "sts2.human-annotator/native-action-ledger-event-2");
  expectEqual(errors, "semantic timeline generated choice", timelineCanary?.generated_card_select,
    "pass");
  expectEqual(errors, "semantic timeline generated skip", timelineCanary?.generated_card_skip,
    "not_exercised");
  expectEqual(errors, "semantic timeline current exact reorder",
    timelineCanary?.exact_execution_order_rebind, "not_exercised_on_schema2_artifact");
  expectEqual(errors, "semantic timeline catalog-incomplete handoff",
    timelineCanary?.catalog_incomplete_handoff, "not_exercised");
  expectEqual(errors, "semantic timeline pending Close",
    timelineCanary?.close_pending_edge_to_proof, "not_exercised");
  expectEqual(errors, "semantic timeline predecessor evidence transfer",
    semanticTimeline?.evidence_transfer_from_predecessor, false);
  expectPattern(errors, "semantic timeline rollback", semanticTimeline?.rollback,
    /^apps\/game-mod\/\.local\/deployments\//u);
  if (semanticTimeline?.artifact_sha256 === policyCandidate?.annotator?.artifact_sha256)
    errors.push("Semantic timeline source candidate must not reuse predecessor artifact identity");
  const fullRun = policyCandidate?.full_run_semantic_source_candidate;
  expectEqual(errors, "Full-Run source status", fullRun?.status,
    "semantic_evidence_schema3_bounded_human_pass");
  expectPattern(errors, "Full-Run build Annotator source",
    fullRun?.annotator_source_revision, COMMIT);
  expectEqual(errors, "Full-Run current Annotator source",
    fullRun?.annotator_source_revision, LOADED_SCHEMA3_ANNOTATOR.sourceRevision);
  expectEqual(errors, "Full-Run current Annotator component tree",
    fullRun?.annotator_component_tree_revision, LOADED_SCHEMA3_ANNOTATOR.componentTreeRevision);
  expectPattern(errors, "Full-Run build Annotator component digest",
    fullRun?.annotator_component_source_digest_sha256, SHA256);
  expectEqual(errors, "Full-Run current Annotator component digest",
    fullRun?.annotator_component_source_digest_sha256,
    LOADED_SCHEMA3_ANNOTATOR.componentSourceDigest);
  expectEqual(errors, "Full-Run build Annotator provenance",
    fullRun?.annotator_build_source_digest_sha256, LOADED_SCHEMA3_ANNOTATOR.buildSourceDigest);
  expectEqual(errors, "Full-Run workspace at build",
    fullRun?.workspace_revision_at_build, LOADED_SCHEMA3_ANNOTATOR.workspaceRevisionAtBuild);
  expectPattern(errors, "Full-Run build source digest",
    fullRun?.annotator_build_source_digest_sha256, SHA256);
  expectEqual(errors, "Full-Run trace schema", fullRun?.trace_schema,
    "sts2.human-annotator/semantic-evidence-event-3");
  expectPattern(errors, "Full-Run workspace at build",
    fullRun?.workspace_revision_at_build, COMMIT);
  expectPattern(errors, "Full-Run artifact", fullRun?.artifact_sha256, SHA256);
  expectEqual(errors, "Full-Run current artifact", fullRun?.artifact_sha256,
    LOADED_SCHEMA3_ANNOTATOR.artifactSha);
  expectPattern(errors, "Full-Run MVID", fullRun?.artifact_mvid, MVID);
  expectEqual(errors, "Full-Run current MVID", fullRun?.artifact_mvid,
    LOADED_SCHEMA3_ANNOTATOR.artifactMvid);
  expectEqual(errors, "Full-Run protocol", fullRun?.player_environment_protocol,
    bom.components?.player_environment_protocol);
  expectEqual(errors, "Full-Run slices", JSON.stringify(fullRun?.implemented_slices),
    JSON.stringify([
      "lethal_combat_to_reward",
      "reward_claim",
      "reward_proceed",
      "card_reward_select",
      "map_travel",
      "combat_hand_select",
      "combat_hand_deselect",
      "combat_hand_confirm",
      "potion_use_target_cancel"
    ]));
  expectEqual(errors, "Full-Run tests", fullRun?.annotator_core_tests, 82);
  expectEqual(errors, "Full-Run build", fullRun?.built, "pass");
  expectEqual(errors, "Full-Run install", fullRun?.installed, "pass");
  expectEqual(errors, "Full-Run loaded", fullRun?.loaded, "pass");
  expectEqual(errors, "Full-Run Human runtime", fullRun?.human_runtime,
    "pass_bounded_schema3");
  expectPattern(errors, "Full-Run runtime", fullRun?.runtime_instance_id,
    /^[0-9a-f]{32}$/u);
  expectEqual(errors, "Full-Run current runtime", fullRun?.runtime_instance_id,
    LOADED_SCHEMA3_ANNOTATOR.runtimeInstance);
  expectPattern(errors, "Full-Run environment", fullRun?.environment_fingerprint, SHA256);
  expectEqual(errors, "Full-Run Modset status", fullRun?.modset_status,
    "exact_platform_modset");
  expectPattern(errors, "Full-Run Modset", fullRun?.modset_fingerprint, SHA256);
  expectEqual(errors, "Full-Run current environment", fullRun?.environment_fingerprint,
    LOADED_SCHEMA3_ANNOTATOR.environment);
  expectEqual(errors, "Full-Run current Modset", fullRun?.modset_fingerprint,
    LOADED_SCHEMA3_ANNOTATOR.modset);
  expectEqual(errors, "Full-Run loaded Mods", JSON.stringify(fullRun?.loaded_mod_ids),
    JSON.stringify(["STS2_PLATFORM"]));
  const schema3Canary = fullRun?.owner_canary;
  expectEqual(errors, "schema-3 owner session", schema3Canary?.session_id,
    "session-20260829T052157Z-e549d3601e7640f997b6f475180b2dfe");
  expectEqual(errors, "schema-3 owner timeline", schema3Canary?.timeline_id,
    "timeline-53a417ad759941c99a6ba9e138115453");
  expectEqual(errors, "schema-3 owner audit", schema3Canary?.audit_status, "pass");
  expectEqual(errors, "schema-3 owner origin", schema3Canary?.human_origin,
    "owner_attested_not_machine_proven");
  expectEqual(errors, "schema-3 Decision V2 valid", schema3Canary?.decision_v2_valid, 188);
  expectEqual(errors, "schema-3 Decision V2 invalid", schema3Canary?.decision_v2_invalid, 0);
  expectEqual(errors, "schema-3 accepted", schema3Canary?.semantic_accepted, 333);
  expectEqual(errors, "schema-3 started", schema3Canary?.semantic_started, 333);
  expectEqual(errors, "schema-3 finished", schema3Canary?.semantic_finished, 333);
  expectEqual(errors, "schema-3 proved", schema3Canary?.semantic_proved, 333);
  expectEqual(errors, "schema-3 unknown", schema3Canary?.semantic_unknown, 0);
  expectEqual(errors, "schema-3 cancelled", schema3Canary?.semantic_cancelled, 0);
  expectEqual(errors, "schema-3 aborted", schema3Canary?.semantic_aborted, 0);
  expectEqual(errors, "schema-3 unresolved", schema3Canary?.semantic_unresolved, 0);
  expectEqual(errors, "schema-3 performance profile", schema3Canary?.performance_profile,
    "not_present_in_loaded_artifact");
  const fullRunCanary = fullRun?.prior_full_run_canary;
  expectEqual(errors, "Full-Run owner audit", fullRunCanary?.audit_status, "pass");
  expectEqual(errors, "Full-Run owner origin", fullRunCanary?.human_origin,
    "owner_attested_not_machine_proven");
  expectEqual(errors, "Full-Run accepted", fullRunCanary?.semantic_accepted, 250);
  expectEqual(errors, "Full-Run proved", fullRunCanary?.semantic_proved, 248);
  expectEqual(errors, "Full-Run cancelled", fullRunCanary?.semantic_cancelled_before_start, 2);
  expectEqual(errors, "Full-Run unknown", fullRunCanary?.semantic_unknown, 0);
  expectEqual(errors, "Full-Run unresolved", fullRunCanary?.semantic_unresolved, 0);
  expectEqual(errors, "Full-Run native accounting", fullRunCanary?.native_accounting, "pass");
  const subsequent = fullRun?.potion_owner_canary;
  expectEqual(errors, "Full-Run potion canary source", subsequent?.annotator_source_revision,
    POTION_OWNER_CANARY.sourceRevision);
  expectEqual(errors, "Full-Run potion canary digest",
    subsequent?.annotator_component_source_digest_sha256,
    POTION_OWNER_CANARY.componentSourceDigest);
  expectEqual(errors, "Full-Run subsequent source/test", subsequent?.source_test_status, "pass");
  expectEqual(errors, "Full-Run subsequent tests", subsequent?.annotator_core_tests, 80);
  expectEqual(errors, "Full-Run subsequent slices", JSON.stringify(subsequent?.implemented_slices),
    JSON.stringify(["potion_use_target_cancel"]));
  for (const level of ["built", "installed", "loaded"])
    expectEqual(errors, `Full-Run subsequent ${level}`, subsequent?.[level], "pass");
  expectEqual(errors, "Full-Run potion canary artifact", subsequent?.artifact_sha256,
    POTION_OWNER_CANARY.artifactSha);
  expectEqual(errors, "Full-Run potion canary MVID", subsequent?.artifact_mvid,
    POTION_OWNER_CANARY.artifactMvid);
  expectEqual(errors, "Full-Run potion canary runtime", subsequent?.runtime_instance_id,
    POTION_OWNER_CANARY.runtimeInstance);
  expectEqual(errors, "Full-Run potion canary environment", subsequent?.environment_fingerprint,
    POTION_OWNER_CANARY.environment);
  expectEqual(errors, "Full-Run potion canary Modset status", subsequent?.modset_status,
    "exact_platform_modset");
  expectEqual(errors, "Full-Run potion canary Modset", subsequent?.modset_fingerprint,
    POTION_OWNER_CANARY.modset);
  expectEqual(errors, "Full-Run potion canary rollback", subsequent?.rollback,
    POTION_OWNER_CANARY.rollback);
  expectEqual(errors, "Full-Run potion canary Human", subsequent?.human_runtime,
    "pass_overall_accounting_potion_gate_failed");
  const potionCanary = subsequent?.owner_canary;
  expectEqual(errors, "Full-Run potion canary audit", potionCanary?.audit_status, "pass");
  expectEqual(errors, "Full-Run potion canary Human origin", potionCanary?.human_origin,
    "owner_attested_not_machine_proven");
  for (const [label, actual, expected] of [
    ["Decision V2 valid", potionCanary?.decision_v2_valid, 219],
    ["Decision V2 invalid", potionCanary?.decision_v2_invalid, 0],
    ["legacy invalidations", potionCanary?.legacy_invalidations, 287],
    ["Reads", potionCanary?.reads_materialized, 17954],
    ["Read failures", potionCanary?.reads_failed, 0],
    ["accepted", potionCanary?.semantic_accepted, 627],
    ["proved", potionCanary?.semantic_proved, 625],
    ["cancel before start", potionCanary?.semantic_cancelled_before_start, 1],
    ["cancel after start unknown", potionCanary?.semantic_cancelled_after_start_unknown, 1],
    ["unresolved", potionCanary?.semantic_unresolved, 0],
    ["enemy-target potion proved", potionCanary?.potion_enemy_target_proved, 1],
    ["self-target potion invalidations", potionCanary?.potion_self_target_mapping_invalidations, 3],
    ["self-target potion unaccounted", potionCanary?.potion_self_target_unaccounted, 1]
  ]) expectEqual(errors, `Full-Run potion canary ${label}`, actual, expected);
  expectEqual(errors, "Full-Run potion target cancel",
    potionCanary?.potion_target_picker_cancel, "not_exercised");
  expectEqual(errors, "Full-Run potion canary evidence transfer",
    subsequent?.evidence_transfer_from_repair_artifact, false);
  expectEqual(errors, "Full-Run current evidence transfer from potion canary",
    fullRun?.evidence_transfer_from_potion_owner_canary, false);
  expectEqual(errors, "Full-Run predecessor evidence transfer",
    fullRun?.evidence_transfer_from_schema2_predecessor, false);
  expectEqual(errors, "Full-Run predecessor canary",
    fullRun?.predecessor_owner_canary, "failed_semantic_accounting");
  expectEqual(errors, "Full-Run predecessor missing semantic roots",
    fullRun?.predecessor_missing_semantic_native_roots, 546);
  expectPattern(errors, "Full-Run rollback", fullRun?.rollback,
    /^apps\/game-mod\/\.local\/deployments\//u);
  if (fullRun?.annotator_source_revision === semanticTimeline?.annotator_source_revision)
    errors.push("Full-Run source candidate must not reuse the proved schema-2 source identity");
  if (fullRun?.artifact_sha256 === semanticTimeline?.artifact_sha256)
    errors.push("Full-Run source candidate must not reuse the proved schema-2 artifact identity");
  expectEqual(errors, "candidate Live UI source relation", policyCandidate?.live_ui?.source_relation,
    "loaded_native_source_scope_matches_current_component");
  expectEqual(errors, "candidate Connector protocol", policyCandidate?.connector?.protocol,
    bom.components?.player_environment_protocol);
  expectEqual(errors, "candidate Policy Runtime source", policyCandidate?.policy_runtime?.source_revision,
    bom.components?.policy_runtime?.source_revision);
  expectEqual(errors, "candidate Policy Runtime digest", policyCandidate?.policy_runtime?.source_digest_sha256,
    bom.components?.policy_runtime?.component_source_digest_sha256);
  expectEqual(errors, "candidate Policy Runtime version", policyCandidate?.policy_runtime?.version,
    bom.components?.policy_runtime?.version);
  expectEqual(errors, "candidate Game Mod version", policyCandidate?.game_mod?.version,
    bom.components?.game_mod?.version);
  expectEqual(errors, "candidate current Game Mod source",
    policyCandidate?.game_mod?.current_component_source_revision,
    bom.components?.game_mod?.source_revision);
  expectEqual(errors, "candidate current Game Mod digest",
    policyCandidate?.game_mod?.current_component_source_digest_sha256,
    bom.components?.game_mod?.component_source_digest_sha256);
  for (const level of ["installed", "loaded"])
    expectEqual(errors, `candidate Game Mod ${level}`, policyCandidate?.game_mod?.[level], "pass");
  expectEqual(errors, "candidate rollback", policyCandidate?.game_mod?.rollback_available, true);
  expectEqual(errors, "candidate UI panel", policyCandidate?.ui_panel_ready, "pass");
  expectEqual(errors, "candidate UI input canary", policyCandidate?.ui_toggle_runtime_canary,
    "not_observed");
  expectEqual(errors, "candidate owner UI visibility", policyCandidate?.owner_ui_visibility,
    "pending_human_runtime_evidence");
  expectEqual(errors, "candidate recording controls",
    policyCandidate?.human_recording_controls_exercised,
    "not_exercised_on_current_ledger_v2_artifact");
  const recordingValidation = policyCandidate?.recording_application_owner_validation;
  expectPattern(errors, "recording predecessor lifecycle runtime",
    recordingValidation?.predecessor_runtime_instance_id, /^[0-9a-f]{32}$/u);
  expectEqual(errors, "recording lifecycle sessions", recordingValidation?.session_ids?.length, 2);
  expectEqual(errors, "recording lifecycle timelines", recordingValidation?.distinct_timelines, 2);
  expectEqual(errors, "recording lifecycle pause/resume", recordingValidation?.pause_resume, "pass");
  expectEqual(errors, "recording lifecycle pending Close", recordingValidation?.pending_close, "pass");
  expectEqual(errors, "recording lifecycle second session",
    recordingValidation?.second_session_same_process, "pass");
  expectEqual(errors, "recording failed admitted records", recordingValidation?.admitted_records, 0);
  expectEqual(errors, "recording failed invalidations", recordingValidation?.invalidations, 18);
  expectEqual(errors, "recording failed audit", recordingValidation?.audit_status,
    "fail_no_decision_file");
  expectEqual(errors, "recording root cause", recordingValidation?.root_cause,
    "unified_modset_validator_drift");
  expectEqual(errors, "recording repair loaded", recordingValidation?.repair_loaded, true);
  expectEqual(errors, "recording repair artifact", recordingValidation?.repair_artifact_sha256,
    policyCandidate?.recording_application_decision_gate?.artifact_sha256);
  expectEqual(errors, "recording repair Human decision",
    recordingValidation?.repair_human_decision, "pass_end_turn_only");
  expectEqual(errors, "recording evidence transfer",
    recordingValidation?.evidence_transfer_to_repair, false);
  const decisionGate = policyCandidate?.recording_application_decision_gate;
  expectPattern(errors, "recording decision gate runtime", decisionGate?.runtime_instance_id,
    /^[0-9a-f]{32}$/u);
  expectPattern(errors, "recording decision gate artifact", decisionGate?.artifact_sha256, SHA256);
  expectPattern(errors, "recording decision gate MVID", decisionGate?.artifact_mvid, MVID);
  expectPattern(errors, "recording decision gate session", decisionGate?.session_id,
    /^session-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{32}$/u);
  expectEqual(errors, "recording decision gate audit", decisionGate?.audit_status, "pass");
  expectEqual(errors, "recording decision valid records", decisionGate?.valid_records, 6);
  expectEqual(errors, "recording decision invalid records", decisionGate?.invalid_records, 0);
  expectEqual(errors, "recording decision end turns", decisionGate?.ordinary_combat_end_turn, 6);
  expectEqual(errors, "recording decision card plays", decisionGate?.ordinary_combat_play_card, 0);
  expectEqual(errors, "recording decision card failures", decisionGate?.play_card_failed_closed, 21);
  expectEqual(errors, "recording decision Reads", decisionGate?.reads_materialized, 24);
  expectEqual(errors, "recording decision Read failures", decisionGate?.reads_failed, 0);
  expectEqual(errors, "recording first Close runtime", decisionGate?.close_runtime_status,
    "closed_on_first_request");
  expectEqual(errors, "recording stale Close UI", decisionGate?.close_ui_status,
    "stale_intermediate_until_second_click");
  expectEqual(errors, "recording current repair loaded", decisionGate?.current_repair_loaded, true);
  expectPattern(errors, "recording current repair artifact",
    decisionGate?.current_repair_artifact_sha256, SHA256);
  expectPattern(errors, "recording current repair MVID", decisionGate?.current_repair_artifact_mvid, MVID);
  expectPattern(errors, "recording current repair runtime",
    decisionGate?.current_repair_runtime_instance_id, /^[0-9a-f]{32}$/u);
  expectEqual(errors, "recording current repair owner validation",
    decisionGate?.current_repair_owner_validation, "pass_card_play_end_turn_close_scope_ui");
  expectEqual(errors, "recording current repair sessions",
    decisionGate?.current_repair_sessions?.length, 2);
  expectEqual(errors, "recording current repair valid records",
    decisionGate?.current_repair_valid_records, 39);
  expectEqual(errors, "recording current repair invalid records",
    decisionGate?.current_repair_invalid_records, 0);
  expectEqual(errors, "recording current repair card plays",
    decisionGate?.current_repair_play_card, 25);
  expectEqual(errors, "recording current repair end turns",
    decisionGate?.current_repair_end_turn, 14);
  expectEqual(errors, "recording current repair invalidations",
    decisionGate?.current_repair_invalidations, 43);
  expectEqual(errors, "recording current repair Reads",
    decisionGate?.current_repair_reads_materialized, 158);
  expectEqual(errors, "recording current repair Read failures",
    decisionGate?.current_repair_reads_failed, 0);
  expectEqual(errors, "recording pending Close",
    decisionGate?.current_repair_pending_close, "closed_after_bounded_successor_timeout");
  expectEqual(errors, "accepted-only failure accounting loaded",
    decisionGate?.accepted_failure_accounting_loaded, true);
  expectPattern(errors, "accepted-only artifact",
    decisionGate?.accepted_failure_accounting_artifact_sha256, SHA256);
  expectPattern(errors, "accepted-only MVID",
    decisionGate?.accepted_failure_accounting_artifact_mvid, MVID);
  expectPattern(errors, "accepted-only runtime",
    decisionGate?.accepted_failure_accounting_runtime_instance_id, /^[0-9a-f]{32}$/u);
  expectEqual(errors, "accepted-only owner validation",
    decisionGate?.accepted_failure_accounting_owner_validation,
    "pass_owner_attested_card_play_end_turn_cancel_close_scope_ui");
  expectEqual(errors, "accepted-only sessions",
    decisionGate?.accepted_failure_accounting_session_ids?.length, 1);
  expectEqual(errors, "accepted-only audit",
    decisionGate?.accepted_failure_accounting_audit_status, "pass");
  expectEqual(errors, "accepted-only valid records",
    decisionGate?.accepted_failure_accounting_valid_records, 19);
  expectEqual(errors, "accepted-only invalid records",
    decisionGate?.accepted_failure_accounting_invalid_records, 0);
  expectEqual(errors, "accepted-only card plays",
    decisionGate?.accepted_failure_accounting_play_card, 10);
  expectEqual(errors, "accepted-only end turns",
    decisionGate?.accepted_failure_accounting_end_turn, 9);
  expectEqual(errors, "accepted-only invalidations",
    decisionGate?.accepted_failure_accounting_invalidations, 16);
  expectEqual(errors, "accepted-only pre-frame failures",
    decisionGate?.accepted_failure_accounting_pre_frame_failures, 15);
  expectEqual(errors, "accepted-only overlapping actions",
    decisionGate?.accepted_failure_accounting_overlapping_actions, 1);
  expectEqual(errors, "accepted-only Reads",
    decisionGate?.accepted_failure_accounting_reads_materialized, 78);
  expectEqual(errors, "accepted-only Read failures",
    decisionGate?.accepted_failure_accounting_reads_failed, 0);
  expectEqual(errors, "accepted-only Close status",
    decisionGate?.accepted_failure_accounting_close_status, "closed_on_first_request");
  expectEqual(errors, "accepted-only Close latency",
    decisionGate?.accepted_failure_accounting_close_ms, 5.153);
  expectEqual(errors, "accepted-only cancelled attempt boundary",
    decisionGate?.native_rejected_cancelled_attempt,
    "owner_attested_no_record_or_invalidation_not_machine_attributable");
  expectEqual(errors, "accepted-only Human origin",
    decisionGate?.human_origin, "owner_attested_not_machine_proven");
  expectEqual(errors, "accepted-only evidence transfer",
    decisionGate?.evidence_transfer_to_accepted_failure_accounting, false);
  if (decisionGate?.artifact_sha256 === decisionGate?.current_repair_artifact_sha256)
    errors.push("Recording decision evidence must not transfer to the current repair artifact");
  if (decisionGate?.current_repair_artifact_sha256 ===
      decisionGate?.accepted_failure_accounting_artifact_sha256)
    errors.push("Predecessor recording evidence must not transfer to accepted-only accounting");
  expectEqual(errors, "recording current repair evidence transfer",
    decisionGate?.evidence_transfer_to_current_repair, false);
  const rapidGate = policyCandidate?.rapid_input_ledger_v1_owner_validation;
  expectPattern(errors, "rapid ledger artifact", rapidGate?.artifact_sha256, SHA256);
  expectPattern(errors, "rapid ledger MVID", rapidGate?.artifact_mvid, MVID);
  expectPattern(errors, "rapid ledger loaded Annotator source",
    rapidGate?.annotator_source_revision, COMMIT);
  expectPattern(errors, "rapid ledger runtime", rapidGate?.runtime_instance_id,
    /^[0-9a-f]{32}$/u);
  expectPattern(errors, "rapid ledger session", rapidGate?.session_id,
    /^session-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{32}$/u);
  expectEqual(errors, "rapid ledger audit", rapidGate?.audit_status, "pass");
  expectEqual(errors, "rapid ledger valid records", rapidGate?.valid_records, 12);
  expectEqual(errors, "rapid ledger invalid records", rapidGate?.invalid_records, 0);
  expectEqual(errors, "rapid ledger invalidations", rapidGate?.invalidations, 29);
  expectEqual(errors, "rapid ledger Reads", rapidGate?.reads_materialized, 94);
  expectEqual(errors, "rapid ledger Read failures", rapidGate?.reads_failed, 0);
  expectEqual(errors, "rapid ledger schema", rapidGate?.ledger_schema,
    "sts2.human-annotator/native-action-ledger-event-1");
  expectEqual(errors, "rapid ledger event count", rapidGate?.ledger_events, 140);
  expectEqual(errors, "rapid ledger accepted", rapidGate?.accepted, 35);
  expectEqual(errors, "rapid ledger started", rapidGate?.started, 35);
  expectEqual(errors, "rapid ledger finished", rapidGate?.finished, 35);
  expectEqual(errors, "rapid ledger cancelled", rapidGate?.cancelled, 0);
  expectEqual(errors, "rapid ledger player-choice pause",
    rapidGate?.paused_for_player_choice, 0);
  expectEqual(errors, "rapid ledger strict admitted", rapidGate?.strict_admitted, 12);
  expectEqual(errors, "rapid ledger strict invalidated", rapidGate?.strict_invalidated, 23);
  expectEqual(errors, "rapid ledger unresolved", rapidGate?.unresolved, 0);
  expectEqual(errors, "rapid ledger play-card actions", rapidGate?.play_card_actions, 23);
  expectEqual(errors, "rapid ledger end-turn actions", rapidGate?.end_turn_actions, 12);
  expectEqual(errors, "rapid ledger Human origin", rapidGate?.human_origin,
    "owner_attested_not_machine_proven");
  expectEqual(errors, "rapid ledger decision payload scope", rapidGate?.decision_payload_scope,
    "ledger_v1_does_not_retain_invalidated_frozen_decision");
  expectEqual(errors, "rapid ledger evidence transfer", rapidGate?.evidence_transfer_to_ledger_v2,
    false);
  if (decisionGate?.accepted_failure_accounting_artifact_sha256 === rapidGate?.artifact_sha256)
    errors.push("Accepted-only predecessor evidence must not replace rapid ledger evidence");
  const rapidV2 = policyCandidate?.rapid_input_ledger_v2_loaded_candidate;
  expectEqual(errors, "rapid ledger v2 status", rapidV2?.status,
    "loaded_pending_owner_validation");
  expectEqual(errors, "rapid ledger v2 artifact", rapidV2?.artifact_sha256,
    "df5d2c61304be5dfbbfe8f608a5832539a723f0330c93e7330f48fc97d0a3d0e");
  expectEqual(errors, "rapid ledger v2 MVID", rapidV2?.artifact_mvid,
    "9072e515-69f2-4131-957b-417d80008b04");
  expectEqual(errors, "rapid ledger v2 source", rapidV2?.annotator_source_revision,
    "de5e55fcf1bd8f17af3d1a3c871781b1702b99cf");
  expectEqual(errors, "rapid ledger v2 source digest", rapidV2?.annotator_source_digest_sha256,
    "c8f12204c45519c007ee0c2b6b890e5f1fe705510150978ffa8a37426d233780");
  expectPattern(errors, "rapid ledger v2 workspace", rapidV2?.workspace_revision, COMMIT);
  expectEqual(errors, "rapid ledger v2 runtime", rapidV2?.runtime_instance_id,
    "ebe7a9fc27c344baa82980825895081d");
  expectEqual(errors, "rapid ledger v2 environment", rapidV2?.environment_fingerprint,
    "a7171bb99ab28f686f65b8dbe2a1d4c2b0440f0f83abc0adfc025d0ffa69ada7");
  expectEqual(errors, "rapid ledger v2 Modset", rapidV2?.modset_fingerprint,
    "20b2de1a468fcc24d9f1e61037e5d3f72271d11821016f5ace68fe05c8afae51");
  expectEqual(errors, "rapid ledger v2 schema", rapidV2?.ledger_schema,
    "sts2.human-annotator/native-action-ledger-event-2");
  for (const level of ["build", "installed", "loaded"])
    expectEqual(errors, `rapid ledger v2 ${level}`, rapidV2?.[level], "pass");
  expectEqual(errors, "rapid ledger v2 runtime status", rapidV2?.runtime_status,
    "ready_no_session");
  expectEqual(errors, "rapid ledger v2 owner canary", rapidV2?.owner_rapid_input,
    "not_exercised");
  expectEqual(errors, "rapid ledger v2 evidence transfer",
    rapidV2?.evidence_transfer_from_ledger_v1, false);
  expectEqual(errors, "rapid ledger v2 rollback", rapidV2?.rollback,
    "apps/game-mod/.local/deployments/2026-08-26T06-50-54.021Z");
  if (rapidGate?.artifact_sha256 === rapidV2?.artifact_sha256)
    errors.push("Ledger v1 Live evidence must not transfer to the ledger v2 artifact");
  const semanticCandidate = policyCandidate?.semantic_execution_order_loaded_candidate;
  expectEqual(errors, "semantic execution candidate status", semanticCandidate?.status,
    "human_canary_exact_rebind_live_proved");
  expectEqual(errors, "semantic execution candidate artifact", semanticCandidate?.artifact_sha256,
    policyCandidate?.game_mod?.artifact_sha256);
  expectEqual(errors, "semantic execution candidate MVID", semanticCandidate?.artifact_mvid,
    policyCandidate?.game_mod?.artifact_mvid);
  expectEqual(errors, "semantic execution candidate source", semanticCandidate?.annotator_source_revision,
    policyCandidate?.annotator?.source_revision);
  expectEqual(errors, "semantic execution candidate source digest",
    semanticCandidate?.annotator_source_digest_sha256,
    policyCandidate?.annotator?.source_digest_sha256);
  expectPattern(errors, "semantic execution candidate workspace",
    semanticCandidate?.workspace_revision, COMMIT);
  expectEqual(errors, "semantic execution candidate runtime", semanticCandidate?.runtime_instance_id,
    policyCandidate?.runtime?.runtime_instance_id);
  expectEqual(errors, "semantic execution candidate environment",
    semanticCandidate?.environment_fingerprint,
    policyCandidate?.runtime?.environment_fingerprint);
  expectEqual(errors, "semantic execution candidate Modset", semanticCandidate?.modset_fingerprint,
    policyCandidate?.runtime?.modset_fingerprint);
  for (const level of ["build", "installed", "loaded"])
    expectEqual(errors, `semantic execution candidate ${level}`, semanticCandidate?.[level], "pass");
  expectEqual(errors, "semantic execution candidate runtime status",
    semanticCandidate?.runtime_status, "recording_closed");
  expectEqual(errors, "semantic execution candidate owner canary",
    semanticCandidate?.owner_semantic_execution_order,
    "pass_exact_reorder_rebind_live_proved");
  const semanticCanary = semanticCandidate?.owner_canary;
  expectPattern(errors, "semantic execution owner session", semanticCanary?.session_id,
    /^session-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{32}$/u);
  expectEqual(errors, "semantic execution owner audit", semanticCanary?.audit_status, "pass");
  expectEqual(errors, "semantic execution Human origin", semanticCanary?.human_origin,
    "owner_attested_not_machine_proven");
  expectEqual(errors, "semantic execution valid records", semanticCanary?.valid_records, 3);
  expectEqual(errors, "semantic execution invalid records", semanticCanary?.invalid_records, 0);
  expectEqual(errors, "semantic execution invalidations", semanticCanary?.invalidations, 32);
  expectEqual(errors, "semantic execution Reads", semanticCanary?.reads_materialized, 64);
  expectEqual(errors, "semantic execution Read failures", semanticCanary?.reads_failed, 0);
  for (const [label, value] of [
    ["semantic execution Decision V2 SHA", semanticCanary?.decision_v2_sha256],
    ["semantic execution invalidations SHA", semanticCanary?.invalidations_sha256],
    ["semantic execution ledger SHA", semanticCanary?.ledger_sha256],
    ["semantic execution trace SHA", semanticCanary?.semantic_trace_sha256],
    ["semantic execution RunJournal SHA", semanticCanary?.run_journal_sha256]
  ]) expectPattern(errors, label, value, SHA256);
  expectEqual(errors, "semantic execution ledger schema", semanticCanary?.ledger_schema,
    "sts2.human-annotator/native-action-ledger-event-2");
  expectEqual(errors, "semantic execution ledger accepted", semanticCanary?.ledger_accepted, 28);
  expectEqual(errors, "semantic execution ledger started", semanticCanary?.ledger_started, 24);
  expectEqual(errors, "semantic execution ledger finished", semanticCanary?.ledger_finished, 16);
  expectEqual(errors, "semantic execution ledger cancelled", semanticCanary?.ledger_cancelled, 12);
  expectEqual(errors, "semantic execution ledger admitted", semanticCanary?.ledger_strict_admitted, 3);
  expectEqual(errors, "semantic execution ledger invalidated",
    semanticCanary?.ledger_strict_invalidated, 25);
  expectEqual(errors, "semantic execution ledger unresolved", semanticCanary?.ledger_unresolved, 0);
  expectEqual(errors, "semantic execution player-choice lifecycle",
    semanticCanary?.player_choice_pause_resume, "pass");
  expectEqual(errors, "semantic execution accepted", semanticCanary?.semantic_accepted, 29);
  expectEqual(errors, "semantic execution started", semanticCanary?.semantic_started, 25);
  expectEqual(errors, "semantic execution proved", semanticCanary?.semantic_proved, 6);
  expectEqual(errors, "semantic execution unknown", semanticCanary?.semantic_unknown, 11);
  expectEqual(errors, "semantic execution unknown incomplete execution boundary",
    semanticCanary?.semantic_unknown_execution_boundary_incomplete, 6);
  expectEqual(errors, "semantic execution unknown incomplete successor boundary",
    semanticCanary?.semantic_unknown_successor_boundary_incomplete, 4);
  expectEqual(errors, "semantic execution unknown Close before boundary",
    semanticCanary?.semantic_unknown_recording_closed_before_boundary, 1);
  expectEqual(errors, "semantic execution cancel before start",
    semanticCanary?.semantic_cancelled_before_start, 4);
  expectEqual(errors, "semantic execution cancel after start",
    semanticCanary?.semantic_cancelled_after_start, 8);
  expectEqual(errors, "semantic execution abort before Commit",
    semanticCanary?.semantic_aborted_before_commit, 0);
  expectEqual(errors, "semantic execution unresolved", semanticCanary?.semantic_unresolved, 0);
  expectEqual(errors, "semantic execution intervening-start proofs",
    semanticCanary?.proved_with_intervening_human_start, 0);
  expectEqual(errors, "semantic execution pre-boundary mismatches",
    semanticCanary?.proved_pre_execution_boundary_mismatch, 0);
  expectEqual(errors, "semantic execution exact rebind count",
    semanticCanary?.exact_execution_order_rebinds, 2);
  expectEqual(errors, "semantic execution exact rebind proved transitions",
    semanticCanary?.exact_rebind_proved_transitions, 1);
  expectEqual(errors, "semantic execution exact rebind native cancellations",
    semanticCanary?.exact_rebind_native_cancellations, 1);
  expectEqual(errors, "semantic execution generated-card select",
    semanticCanary?.generated_card_select, "pass");
  expectEqual(errors, "semantic execution exact reorder claim",
    semanticCanary?.exact_reorder_rebind, "pass_live_proved");
  expectEqual(errors, "semantic execution candidate evidence transfer",
    semanticCandidate?.evidence_transfer_from_predecessor, false);
  expectEqual(errors, "semantic execution candidate rollback", semanticCandidate?.rollback,
    policyCandidate?.game_mod?.rollback);
  if (rapidV2?.artifact_sha256 === semanticCandidate?.artifact_sha256)
    errors.push("Rapid ledger v2 evidence must not transfer to the semantic execution artifact");
  expectEqual(errors, "candidate compatibility", policyCandidate?.runtime?.compatibility_status, "canary_exact");
  expectEqual(errors, "candidate Modset status", policyCandidate?.runtime?.modset_status,
    "exact_platform_modset");
  expectEqual(errors, "candidate loaded Mods", JSON.stringify(policyCandidate?.runtime?.loaded_mod_ids),
    JSON.stringify(["STS2_PLATFORM"]));
  expectEqual(errors, "candidate execution", policyCandidate?.runtime?.execution_available, true);
  for (const component of ["connector", "annotator", "live_ui"])
    expectEqual(errors, `candidate common artifact SHA (${component})`,
      policyCandidate?.[component]?.artifact_sha256, policyCandidate?.game_mod?.artifact_sha256);
  for (const component of ["connector", "annotator", "live_ui"])
    expectEqual(errors, `candidate common artifact MVID (${component})`,
      policyCandidate?.[component]?.artifact_mvid, policyCandidate?.game_mod?.artifact_mvid);
  expectEqual(errors, "candidate model modes",
    policyCandidate?.policy_shadow_one_step_auto_exercised,
    "not_exercised_checkpoint_absent");
  const predecessorHuman = policyCandidate?.predecessor_human_session;
  expectEqual(errors, "predecessor Human generation", predecessorHuman?.artifact_generation,
    "three_mod_predecessor");
  expectEqual(errors, "predecessor Human audit", predecessorHuman?.audit_status, "pass");
  expectEqual(errors, "predecessor Human records", predecessorHuman?.admitted_records, 10);
  expectEqual(errors, "predecessor Human invalidations", predecessorHuman?.invalidations, 36);
  expectEqual(errors, "predecessor ordinary combat end turns",
    predecessorHuman?.ordinary_combat_end_turn, 9);
  expectEqual(errors, "predecessor generated-card select",
    predecessorHuman?.generated_card_select, 1);
  expectEqual(errors, "predecessor generated-card skip",
    predecessorHuman?.generated_card_skip, "not_exercised");
  expectEqual(errors, "predecessor evidence transfer",
    predecessorHuman?.evidence_transfer_to_unified_artifact, false);

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
    ["candidate game assembly SHA", policyCandidate?.game?.main_assembly_sha256],
    ["candidate Connector source digest", policyCandidate?.connector?.source_digest_sha256],
    ["candidate Connector artifact SHA", policyCandidate?.connector?.artifact_sha256],
    ["candidate Annotator source digest", policyCandidate?.annotator?.source_digest_sha256],
    ["candidate Annotator artifact SHA", policyCandidate?.annotator?.artifact_sha256],
    ["candidate Live UI source digest", policyCandidate?.live_ui?.source_digest_sha256],
    ["candidate Live UI artifact SHA", policyCandidate?.live_ui?.artifact_sha256],
    ["candidate Platform source digest", policyCandidate?.game_mod?.platform_source_digest_sha256],
    ["candidate Game Mod artifact SHA", policyCandidate?.game_mod?.artifact_sha256],
    ["candidate environment fingerprint", policyCandidate?.runtime?.environment_fingerprint],
    ["candidate Modset fingerprint", policyCandidate?.runtime?.modset_fingerprint],
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
  expectPattern(errors, "candidate game MVID", policyCandidate?.game?.main_assembly_mvid, MVID);
  expectPattern(errors, "candidate Connector MVID", policyCandidate?.connector?.artifact_mvid, MVID);
  expectPattern(errors, "candidate Annotator MVID", policyCandidate?.annotator?.artifact_mvid, MVID);
  expectPattern(errors, "candidate Live UI MVID", policyCandidate?.live_ui?.artifact_mvid, MVID);
  expectPattern(errors, "candidate Game Mod MVID", policyCandidate?.game_mod?.artifact_mvid, MVID);
  expectPattern(errors, "candidate runtime instance", policyCandidate?.runtime?.runtime_instance_id, /^[0-9a-f]{32}$/u);
  expectPattern(errors, "predecessor Human runtime", predecessorHuman?.runtime_instance_id, /^[0-9a-f]{32}$/u);
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
    "human_evidence_v2_semantic_timeline_bounded_live_proved");
  if (!bom.non_claims?.includes("human_origin_owner_attested_not_machine_proven"))
    errors.push("human-origin epistemic-boundary non-claim is missing");
  if (!bom.non_claims?.includes("read_rich_v2_candidate_generated_card_choice_not_exercised"))
    errors.push("Read-rich V2 predecessor generated-card-choice non-claim is missing");
  for (const value of [
    "schema2_exact_execution_order_rebind_not_exercised",
    "schema2_catalog_incomplete_handoff_not_exercised",
    "schema2_close_pending_edge_to_proof_not_exercised"
  ]) {
    if (!bom.non_claims?.includes(value))
      errors.push(`Schema-2 semantic timeline non-claim is missing: ${value}`);
  }
  if (!bom.non_claims?.includes("generated_card_skip_not_exercised"))
    errors.push("Generated-card skip non-claim is missing");
  if (!bom.non_claims?.includes("v2_corpus_and_training_not_authorized"))
    errors.push("V2 corpus/training authorization non-claim is missing");
  if (!bom.non_claims?.includes(
    "native_rejected_cancelled_attempt_absence_owner_attested_not_machine_attributable"))
    errors.push("Native-rejected attempt attribution non-claim is missing");
  if (!bom.non_claims?.includes("rapid_ledger_v1_invalidated_targeting_classification_unavailable"))
    errors.push("Rapid ledger v1 decision-payload non-claim is missing");
  if (bom.non_claims?.includes("semantic_execution_order_exact_rebind_not_exercised"))
    errors.push("Live-proved semantic execution-order rebind retains a stale non-claim");
  if (!bom.non_claims?.includes("automated_input_canary_is_not_owner_visibility_evidence"))
    errors.push("Automated-input epistemic-boundary non-claim is missing");
  if (!bom.non_claims?.includes("s1_checkpoint_absent_shadow_one_step_auto_not_exercised"))
    errors.push("S1 checkpoint/model-mode non-claim is missing");
  if (!bom.non_claims?.includes("serialized_canonical_candidate_human_runtime_not_exercised"))
    errors.push("Serialized canonical Human-runtime non-claim is missing");
  if (bom.non_claims?.includes("native_semantic_discriminator_human_runtime_pending"))
    errors.push("Bounded Human-proved native discriminator retains a stale pending non-claim");
  for (const value of [
    "native_semantic_discriminator_overlapping_acceptance_not_exercised",
    "native_semantic_discriminator_cancel_abort_not_exercised",
    "native_semantic_discriminator_handoff_is_candidate_not_final_successor",
    "native_semantic_discriminator_full_run_not_implemented"
  ]) {
    if (!bom.non_claims?.includes(value))
      errors.push(`Native semantic discriminator non-claim is missing: ${value}`);
  }
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
