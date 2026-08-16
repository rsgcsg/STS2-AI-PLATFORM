function plainObject(value) {
  return value != null && typeof value === "object" && !Array.isArray(value);
}

function cloneJson(value, label) {
  try {
    return JSON.parse(JSON.stringify(value));
  } catch {
    throw new TypeError(`${label} must be JSON-serializable.`);
  }
}

function deepFreeze(value) {
  if (value == null || typeof value !== "object" || Object.isFrozen(value)) return value;
  for (const child of Object.values(value)) deepFreeze(child);
  return Object.freeze(value);
}

function canonicalValue(value) {
  if (Array.isArray(value)) return value.map(canonicalValue);
  if (!plainObject(value)) return value;
  return Object.fromEntries(Object.keys(value).sort()
    .map((key) => [key, canonicalValue(value[key])]));
}

export function canonicalDescriptorJson(value) {
  return JSON.stringify(canonicalValue(value));
}

export function validateSemanticTarget(target) {
  const errors = [];
  if (!plainObject(target)) return ["semantic_target_missing"];
  if (target.schema !== "sts2.headless/semantic-target-1") errors.push("semantic_target_schema_invalid");
  if (typeof target.target_id !== "string" || target.target_id.length === 0) {
    errors.push("semantic_target_id_missing");
  }
  if (typeof target.protocol_version !== "string" || target.protocol_version.length === 0) {
    errors.push("semantic_target_protocol_missing");
  }
  if (!plainObject(target.game_build)
      || typeof target.game_build.version !== "string"
      || typeof target.game_build.commit !== "string"
      || !new Set(["string", "number"]).has(typeof target.game_build.main_assembly_hash)) {
    errors.push("semantic_target_game_build_missing");
  }
  if (typeof target.content_policy_id !== "string" || target.content_policy_id.length === 0) {
    errors.push("semantic_target_content_policy_missing");
  }
  if (typeof target.information_policy_id !== "string" || target.information_policy_id.length === 0) {
    errors.push("semantic_target_information_policy_missing");
  }
  return errors;
}

export function validateScenarioDescriptor(scenario) {
  const errors = [];
  if (!plainObject(scenario)) return ["scenario_missing"];
  if (scenario.schema !== "sts2.headless/scenario-1") errors.push("scenario_schema_invalid");
  for (const field of ["scenario_id", "seed", "policy_id"]) {
    if (typeof scenario[field] !== "string" || scenario[field].length === 0) {
      errors.push(`scenario_${field}_missing`);
    }
  }
  if (!Number.isSafeInteger(scenario.max_actions) || scenario.max_actions < 1) {
    errors.push("scenario_max_actions_invalid");
  }
  return errors;
}

export function createHostDriver({
  driverId,
  hostKind,
  semanticTarget,
  implementation,
  runScenario
}) {
  if (typeof driverId !== "string" || driverId.length === 0) {
    throw new TypeError("HostDriver requires a non-empty driverId.");
  }
  if (typeof hostKind !== "string" || hostKind.length === 0) {
    throw new TypeError("HostDriver requires a non-empty hostKind.");
  }
  const targetErrors = validateSemanticTarget(semanticTarget);
  if (targetErrors.length > 0) throw new TypeError(`Invalid HostDriver semantic target: ${targetErrors.join(", ")}`);
  if (!plainObject(implementation)) throw new TypeError("HostDriver requires implementation identity.");
  if (typeof runScenario !== "function") throw new TypeError("HostDriver requires runScenario.");
  const descriptor = deepFreeze({
    schema: "sts2.headless/host-driver-1",
    driver_id: driverId,
    host_kind: hostKind,
    semantic_target: cloneJson(semanticTarget, "semanticTarget"),
    implementation: cloneJson(implementation, "implementation")
  });
  return Object.freeze({
    descriptor,
    runScenario
  });
}

export async function runHostScenario(driver, scenario) {
  if (driver?.descriptor?.schema !== "sts2.headless/host-driver-1"
      || typeof driver?.runScenario !== "function") {
    throw new TypeError("Expected a HostDriver created by createHostDriver.");
  }
  const scenarioErrors = validateScenarioDescriptor(scenario);
  if (scenarioErrors.length > 0) throw new TypeError(`Invalid Host scenario: ${scenarioErrors.join(", ")}`);
  const result = await driver.runScenario(cloneJson(scenario, "scenario"));
  if (!plainObject(result?.report) || !Array.isArray(result?.events)) {
    throw new TypeError("HostDriver runScenario must return { report, events }.");
  }
  return {
    driver: driver.descriptor,
    scenario: cloneJson(scenario, "scenario"),
    report: result.report,
    events: result.events,
    evidence: result.evidence ?? null
  };
}
