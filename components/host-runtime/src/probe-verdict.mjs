export function evaluateShippedProbe({
  endpointWasClear,
  processStarted,
  processExit,
  capabilities,
  snapshots,
  stdout = "",
  stderr = ""
}) {
  const latestSnapshot = snapshots.at(-1) ?? null;
  const connectorLoaded = capabilities?.host?.runtime_instance_id != null;
  const headlessHostIdentified = capabilities?.host?.host_kind === "headless";
  const exactModset = capabilities?.game?.modset?.status === "exact_player_environment_only";
  const snapshotObserved = latestSnapshot?.ok === true;
  const decisionMounted = snapshotObserved
    && latestSnapshot.value?.status === "interactive"
    && latestSnapshot.value?.bound_actions?.status === "complete"
    && latestSnapshot.value?.bound_actions?.actions?.length > 0;
  const headlessDriverEvidence = /headless/i.test(`${stdout}\n${stderr}`);
  const errors = [];
  if (!endpointWasClear) errors.push("endpoint_was_already_owned");
  if (!processStarted) errors.push("process_not_started");
  if (!connectorLoaded) errors.push("connector_not_observed");
  if (connectorLoaded && !headlessHostIdentified) errors.push("connector_host_kind_not_headless");
  if (connectorLoaded && !exactModset) errors.push("unsupported_modset");
  if (!snapshotObserved) errors.push("player_environment_snapshot_not_observed");
  if (snapshotObserved && !decisionMounted) errors.push("interactive_decision_not_mounted");
  if (!headlessDriverEvidence) errors.push("headless_display_driver_not_observed");

  return {
    route: "shipped_godot_headless",
    verdict: errors.length === 0 ? "h0_pass" : "h0_incomplete",
    errors,
    engine_process_started: processStarted,
    process_exit: processExit,
    connector_loaded: connectorLoaded,
    connector_host_kind: capabilities?.host?.host_kind ?? null,
    connector_runtime_instance_id: capabilities?.host?.runtime_instance_id ?? null,
    connector_artifact_sha256: capabilities?.host?.implementation?.artifact_sha256 ?? null,
    connector_artifact_mvid: capabilities?.host?.implementation?.module_version_id ?? null,
    connector_protocol: capabilities?.protocol_version ?? null,
    loaded_game: capabilities?.game ?? null,
    snapshot_observed: snapshotObserved,
    interactive_decision_mounted: decisionMounted,
    snapshot_status: latestSnapshot?.value?.status ?? null,
    snapshot_id: latestSnapshot?.value?.snapshot_id ?? null,
    headless_driver_log_evidence: headlessDriverEvidence,
    non_claims: [
      "Only no-display boot and menu decision discovery were exercised.",
      "No mutation, settling, determinism, differential parity, or performance claim is made.",
      "A no-display boot does not by itself qualify the Player Environment contract."
    ]
  };
}

export function evaluateMenuControlGate({
  initialSnapshot,
  receipt,
  duplicateReceipt,
  staleReceipt,
  successorSnapshot
}) {
  const errors = [];
  if (initialSnapshot?.interaction?.kind !== "main_menu") {
    errors.push("initial_main_menu_not_observed");
  }
  if (receipt?.delivery !== "delivered") errors.push("menu_action_not_delivered");
  if (duplicateReceipt?.request_id !== receipt?.request_id
      || duplicateReceipt?.delivery !== receipt?.delivery
      || duplicateReceipt?.action?.bound_action_id !== receipt?.action?.bound_action_id) {
    errors.push("duplicate_request_not_idempotent");
  }
  if (staleReceipt?.delivery !== "not_delivered") errors.push("stale_action_not_rejected");
  if (staleReceipt?.reason_code !== "stale_snapshot"
      || staleReceipt?.retry?.allowed !== true
      || staleReceipt?.retry?.reason !== "fresh_snapshot_required") {
    errors.push("stale_receipt_recovery_policy_invalid");
  }
  if (successorSnapshot?.status !== "interactive"
      || successorSnapshot?.snapshot_id === initialSnapshot?.snapshot_id
      || successorSnapshot?.interaction?.interaction_id === initialSnapshot?.interaction?.interaction_id) {
    errors.push("interactive_successor_not_observed");
  }
  return {
    verdict: errors.length === 0 ? "h1_pass" : "h1_incomplete",
    errors,
    initial_snapshot_id: initialSnapshot?.snapshot_id ?? null,
    delivered_request_id: receipt?.request_id ?? null,
    delivery: receipt?.delivery ?? null,
    duplicate_request_id: duplicateReceipt?.request_id ?? null,
    stale_delivery: staleReceipt?.delivery ?? null,
    stale_reason_code: staleReceipt?.reason_code ?? null,
    successor_snapshot_id: successorSnapshot?.snapshot_id ?? null,
    successor_interaction: successorSnapshot?.interaction?.kind ?? null,
    non_claims: [
      "The gate exercises one current main-menu action; a saved run may resume to its native current decision.",
      "It does not assert business completion or qualify combat, selectors, settling, or journey parity."
    ]
  };
}
