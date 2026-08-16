export const DEFAULT_HOST_EXECUTION_PROFILE = "shipped_default";
export const NONINTERACTIVE_EXPERIMENT_PROFILE =
  "shipped_noninteractive_v01110";

const KNOWN_PROFILES = new Set([
  DEFAULT_HOST_EXECUTION_PROFILE,
  NONINTERACTIVE_EXPERIMENT_PROFILE
]);

export function validateRequestedHostExecutionProfile(profile) {
  if (profile == null) return null;
  if (typeof profile !== "string" || !KNOWN_PROFILES.has(profile)) {
    throw new Error(
      `Host execution profile must be ${[...KNOWN_PROFILES].join(" or ")}.`
    );
  }
  return profile;
}

export function evaluateHostExecutionProfile({ requestedProfile, response }) {
  const requested = validateRequestedHostExecutionProfile(requestedProfile);
  if (requested == null) {
    return {
      verdict: "not_requested",
      errors: [],
      requested_profile: null,
      observed_profile: response?.response?.execution_profile ?? null
    };
  }

  const observed = response?.response?.execution_profile ?? null;
  const errors = [];
  if (response?.status !== "observed") {
    errors.push(`host_provenance:${response?.status ?? "missing"}`);
  }
  if (observed?.id !== requested) errors.push("host_execution_profile_mismatch");
  if (requested === NONINTERACTIVE_EXPERIMENT_PROFILE) {
    if (observed?.status !== "experimental_exact") {
      errors.push("host_execution_profile_not_exact_experiment");
    }
    if (observed?.non_interactive_mode_enabled !== true) {
      errors.push("native_noninteractive_mode_not_proven");
    }
  } else if (observed?.non_interactive_mode_enabled !== false) {
    errors.push("default_profile_not_proven");
  }

  return {
    verdict: errors.length === 0 ? "profile_pass" : "profile_incomplete",
    errors,
    requested_profile: requested,
    observed_profile: observed
  };
}
