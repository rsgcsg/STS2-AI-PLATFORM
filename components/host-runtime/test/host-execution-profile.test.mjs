import assert from "node:assert/strict";
import test from "node:test";
import {
  DEFAULT_HOST_EXECUTION_PROFILE,
  NONINTERACTIVE_EXPERIMENT_PROFILE,
  evaluateHostExecutionProfile,
  validateRequestedHostExecutionProfile
} from "../src/host-execution-profile.mjs";

test("Host execution profile names are bounded and explicit", () => {
  assert.equal(validateRequestedHostExecutionProfile(null), null);
  assert.equal(
    validateRequestedHostExecutionProfile(NONINTERACTIVE_EXPERIMENT_PROFILE),
    NONINTERACTIVE_EXPERIMENT_PROFILE
  );
  assert.throws(() => validateRequestedHostExecutionProfile("arbitrary"), /must be/u);
});

test("exact NonInteractiveMode evidence is read from runtime-bound Host provenance", () => {
  const result = evaluateHostExecutionProfile({
    requestedProfile: NONINTERACTIVE_EXPERIMENT_PROFILE,
    response: {
      status: "observed",
      response: {
        execution_profile: {
          id: NONINTERACTIVE_EXPERIMENT_PROFILE,
          status: "experimental_exact",
          non_interactive_mode_enabled: true
        }
      }
    }
  });
  assert.equal(result.verdict, "profile_pass");
  assert.deepEqual(result.errors, []);
});

test("Host execution profile evidence fails closed on missing activation", () => {
  const result = evaluateHostExecutionProfile({
    requestedProfile: NONINTERACTIVE_EXPERIMENT_PROFILE,
    response: {
      status: "observed",
      response: {
        execution_profile: {
          id: DEFAULT_HOST_EXECUTION_PROFILE,
          status: "default",
          non_interactive_mode_enabled: false
        }
      }
    }
  });
  assert.equal(result.verdict, "profile_incomplete");
  assert.ok(result.errors.includes("host_execution_profile_mismatch"));
  assert.ok(result.errors.includes("native_noninteractive_mode_not_proven"));
});
