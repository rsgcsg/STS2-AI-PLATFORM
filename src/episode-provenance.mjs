export function canonicalizeEpisodeSeed(seed) {
  if (seed == null) return null;
  if (typeof seed !== "string") throw new TypeError("Episode seed must be a string.");
  const canonical = seed.trim().toUpperCase().replaceAll("O", "0").replaceAll("I", "1");
  if (!/^[A-Z0-9]{1,64}$/u.test(canonical)) {
    throw new Error("Episode seed must canonicalize to 1-64 ASCII letters or digits.");
  }
  return canonical;
}

export function evaluateEpisodeProvenance({
  requestedSeed,
  expectedRuntimeInstanceId,
  response
}) {
  const canonicalSeed = canonicalizeEpisodeSeed(requestedSeed);
  if (canonicalSeed == null) {
    return {
      verdict: "not_requested",
      errors: [],
      requested_seed: null,
      actual_seed: null,
      runtime_instance_id: expectedRuntimeInstanceId ?? null
    };
  }

  const body = response?.response ?? null;
  const errors = [];
  if (response?.status !== "observed") errors.push(`host_provenance:${response?.status ?? "missing"}`);
  if (body?.status !== "seed_observed") errors.push(`seed_status:${body?.status ?? "missing"}`);
  if (body?.runtime_instance_id !== expectedRuntimeInstanceId) errors.push("runtime_instance_changed");
  if (body?.requested_seed !== canonicalSeed) errors.push("requested_seed_mismatch");
  if (body?.actual_seed !== canonicalSeed) errors.push("actual_seed_mismatch");
  if (body?.seed_matches !== true) errors.push("seed_match_not_proven");

  return {
    verdict: errors.length === 0 ? "provenance_pass" : "provenance_incomplete",
    errors,
    requested_seed: canonicalSeed,
    actual_seed: body?.actual_seed ?? null,
    runtime_instance_id: body?.runtime_instance_id ?? null,
    host_status: body?.status ?? null,
    transport_status: response?.status ?? "missing"
  };
}
