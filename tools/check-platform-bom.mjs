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
  const policyRuntimePackage = readJson(path.join(platformRoot, "components", "policy-runtime", "package.json"));
  const workbenchPackage = readJson(path.join(platformRoot, "apps", "workbench", "package.json"));
  const liveUiPackage = readJson(path.join(platformRoot, "apps", "ingame-ui", "package.json"));
  const gameModPackage = readJson(path.join(platformRoot, "apps", "game-mod", "package.json"));
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
    "rapid_input_ledger_v1_live_v2_source_test_pending_runtime");
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
    "loaded_native_source_scope_matches_current_component");
  expectEqual(errors, "candidate Annotator source relation", policyCandidate?.annotator?.source_relation,
    "loaded_ledger_v1_precedes_decision_bound_ledger_v2_source");
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
    "observed");
  expectEqual(errors, "candidate owner UI visibility", policyCandidate?.owner_ui_visibility,
    "pass_owner_attested_on_current_artifact");
  expectEqual(errors, "candidate recording controls",
    policyCandidate?.human_recording_controls_exercised,
    "card_play_end_turn_rapid_close_scope_ui_live_pass");
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
  expectEqual(errors, "rapid ledger artifact", rapidGate?.artifact_sha256,
    policyCandidate?.game_mod?.artifact_sha256);
  expectEqual(errors, "rapid ledger MVID", rapidGate?.artifact_mvid,
    policyCandidate?.game_mod?.artifact_mvid);
  expectEqual(errors, "rapid ledger loaded Annotator source",
    rapidGate?.annotator_source_revision, policyCandidate?.annotator?.source_revision);
  expectEqual(errors, "rapid ledger runtime", rapidGate?.runtime_instance_id,
    policyCandidate?.runtime?.runtime_instance_id);
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
    "human_evidence_v2_rapid_accounting_live_ledger_v2_source_test");
  if (!bom.non_claims?.includes("human_origin_owner_attested_not_machine_proven"))
    errors.push("human-origin epistemic-boundary non-claim is missing");
  if (!bom.non_claims?.includes("current_v2_candidate_generated_card_choice_not_exercised"))
    errors.push("Current V2 candidate generated-card-choice non-claim is missing");
  if (!bom.non_claims?.includes("generated_card_skip_not_exercised"))
    errors.push("Generated-card skip non-claim is missing");
  if (!bom.non_claims?.includes("v2_corpus_and_training_not_authorized"))
    errors.push("V2 corpus/training authorization non-claim is missing");
  if (!bom.non_claims?.includes(
    "native_rejected_cancelled_attempt_absence_owner_attested_not_machine_attributable"))
    errors.push("Native-rejected attempt attribution non-claim is missing");
  if (!bom.non_claims?.includes("rapid_ledger_v1_invalidated_targeting_classification_unavailable"))
    errors.push("Rapid ledger v1 decision-payload non-claim is missing");
  if (!bom.non_claims?.includes("rapid_ledger_v2_decision_payload_pending_exact_runtime_evidence"))
    errors.push("Rapid ledger v2 runtime non-claim is missing");
  if (!bom.non_claims?.includes("rapid_cancel_and_player_choice_lifecycle_not_exercised"))
    errors.push("Rapid cancellation/player-choice non-claim is missing");
  if (!bom.non_claims?.includes("automated_input_canary_is_not_owner_visibility_evidence"))
    errors.push("Automated-input epistemic-boundary non-claim is missing");
  if (!bom.non_claims?.includes("s1_checkpoint_absent_shadow_one_step_auto_not_exercised"))
    errors.push("S1 checkpoint/model-mode non-claim is missing");
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
