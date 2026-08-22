import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import {
  inspectManagedCandidateBuild,
  assertManagedCandidateGame,
  chooseManagedCandidateAction,
  loadManagedCandidateManifest,
  runManagedCandidateCapacity,
  runManagedCandidateProbe
} from "../src/managed-candidate.mjs";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

test("managed candidate manifest freezes one exact operational baseline", () => {
  const { manifest } = loadManagedCandidateManifest(ROOT);
  assert.equal(manifest.status, "stpd_v0_operational_baseline");
  assert.equal(manifest.expected_build.artifact_sha256.length, 64);
  assert.match(manifest.expected_build.artifact_mvid, /^[0-9a-f-]{36}$/u);
  assert.equal(manifest.admission.forbidden_claims.includes("formal H1.0 qualification"), true);
  assert.ok(manifest.semantic_shims.some((entry) => entry.risk === "critical"));
  assert.throws(() => assertManagedCandidateGame(manifest, {
    platform: "darwin",
    architecture: "arm64",
    release: { version: "v0.112.0", commit: "changed" },
    runtime_main_assembly_hash: 1,
    sts2_assembly: { sha256: "changed" },
    godotsharp_assembly: { sha256: "changed" }
  }), /refuses this game identity/u);
});

test("managed candidate patch keeps normal actions on native identity and commit paths", () => {
  const { manifest } = loadManagedCandidateManifest(ROOT);
  const patch = readFileSync(path.join(ROOT, "experiments", "managed-exact", manifest.source_patch), "utf8");
  const additions = patch.split(/\r?\n/u)
    .filter((line) => line.startsWith("+") && !line.startsWith("+++"))
    .map((line) => line.slice(1))
    .join("\n");
  for (const forbidden of [
    "EnqueueWithoutSynchronizing",
    "RunManager.Instance.EnterMapCoord(",
    "EnterRoom(new MapRoom",
    "ForceToMap",
    "HealBetweenActs",
    "RunManager.Instance.EnterNextAct(",
    "NeutralizePrefix",
    "PotionCmd.Discard"
  ]) {
    assert.equal(additions.includes(forbidden), false, `normal action patch must not add ${forbidden}`);
  }
  assert.match(additions, /TryManualPlay\(target\)/u);
  assert.match(additions, /EnqueueManualUse\(target\)/u);
  assert.match(additions, /ActionQueueSynchronizer\.RequestEnqueue/u);
  assert.match(additions, /NativeObjectIdentity\.Get/u);
  assert.match(additions, /GameOverState\(_runState\.CurrentRoom\?\.IsVictoryRoom == true\)/u);
  assert.doesNotMatch(additions, /if \(RunManager\.Instance\.IsGameOver\)\s+return GameOverState\(true\)/u);
});

test("fresh candidate preparation admits added source files into the audited diff", () => {
  const source = readFileSync(path.join(ROOT, "src", "managed-candidate.mjs"), "utf8");
  assert.equal(source.includes('["apply", "--intent-to-add"'), true);
});

test("managed probe policy uses advertised semantic operands and fails closed on unknown decisions", () => {
  assert.deepEqual(chooseManagedCandidateAction({
    decision: "map_select",
    choices: [
      { row: 2, col: 1, native_ref: "map-b" },
      { row: 1, col: 2, native_ref: "map-a" }
    ]
  }), {
    cmd: "action",
    action: "select_map_node",
    args: { col: 2, row: 1, map_point_ref: "map-a" }
  });
  assert.deepEqual(chooseManagedCandidateAction({
    decision: "combat_rewards_complete",
    room_ref: "boss-room-a"
  }), {
    cmd: "action",
    action: "proceed",
    args: { room_ref: "boss-room-a" }
  });
  assert.deepEqual(chooseManagedCandidateAction({
    decision: "reward_set",
    rewards: [{ native_ref: "reward-a", kind: "gold" }],
    is_terminal: true,
    can_proceed: true,
    room_ref: "room-a"
  }), {
    cmd: "action",
    action: "select_reward",
    args: { reward_ref: "reward-a" }
  });
  assert.deepEqual(chooseManagedCandidateAction({
    decision: "treasure_relic",
    room_ref: "treasure-room-a",
    relics: [{ native_ref: "relic-a", name: "Bag" }],
    can_skip: true
  }), {
    cmd: "action",
    action: "select_treasure_relic",
    args: { relic_ref: "relic-a" }
  });
  assert.deepEqual(chooseManagedCandidateAction({
    decision: "combat_play",
    hand: [{
      id: "STRIKE",
      native_ref: "card-a",
      can_play: true,
      target_type: "AnyEnemy",
      valid_target_refs: ["enemy-a", "enemy-b"]
    }],
    enemies: [
      { id: "B", native_ref: "enemy-b", index: 1, hp: 5 },
      { id: "A", native_ref: "enemy-a", index: 0, hp: 5 }
    ]
  }), {
    cmd: "action",
    action: "play_card",
    args: { card_ref: "card-a", target_ref: "enemy-a" }
  });
  assert.equal(chooseManagedCandidateAction({ decision: "unrecognized" }), null);
});

test("managed build inspection resolves a relative candidate path before returning it", async (context) => {
  const candidate = process.env.STS2_MANAGED_TEST_CANDIDATE;
  if (!candidate) {
    context.skip("set STS2_MANAGED_TEST_CANDIDATE for the proprietary exact-build integration gate");
    return;
  }
  const { manifest } = loadManagedCandidateManifest(ROOT);
  const result = await inspectManagedCandidateBuild({ root: ROOT, candidateDirectory: candidate, manifest });
  assert.equal(path.isAbsolute(result.candidate_directory), true);
  assert.equal(path.isAbsolute(result.artifact), true);
  assert.equal(result.artifact_mvid, manifest.expected_build.artifact_mvid);
});

test("managed probes reject invalid workload dimensions before touching a runtime", async () => {
  await assert.rejects(runManagedCandidateProbe({
    root: ROOT,
    candidateDirectory: "unused",
    diskIdentity: {},
    seed: "TEST",
    maxActions: 0
  }), /maxActions must be a positive integer/u);
  await assert.rejects(runManagedCandidateProbe({
    root: ROOT,
    candidateDirectory: "unused",
    diskIdentity: {},
    seed: "TEST",
    episodeCount: 1.5
  }), /episodeCount must be a positive integer/u);
  await assert.rejects(runManagedCandidateProbe({
    root: ROOT,
    candidateDirectory: "unused",
    diskIdentity: {},
    seed: "TEST",
    resetAtDecisions: [""]
  }), /resetAtDecisions must contain non-empty decision names/u);
  await assert.rejects(runManagedCandidateCapacity({
    root: ROOT,
    candidateDirectory: "unused",
    diskIdentity: {},
    workerCounts: [0],
    evidenceRoot: "unused"
  }), /workerCount must be a positive integer/u);
});
