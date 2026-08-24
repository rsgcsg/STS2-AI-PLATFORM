import assert from "node:assert/strict";
import test from "node:test";
import { managedPerformanceProfile } from "../src/managed-performance-lab.mjs";

test("managed profiles separate qualification overhead from the training path", () => {
  const qualification = managedPerformanceProfile("qualification");
  const training = managedPerformanceProfile("training");
  assert.deepEqual(qualification, {
    profileName: "qualification",
    identityMode: "crypto",
    validateSdk: true,
    eagerReads: true,
    canonicalEvidence: true,
    resourceSamplingIntervalMs: 250,
    quietDiagnostics: false
  });
  assert.equal(training.identityMode, "sequence");
  assert.equal(training.eagerReads, false);
  assert.equal(training.canonicalEvidence, false);
  assert.equal(training.resourceSamplingIntervalMs, null);
  assert.throws(() => managedPerformanceProfile("fast-but-undefined"), /Unknown managed performance profile/u);
});
