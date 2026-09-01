# Native Foundation Causal Evidence Source Closeout - 2026-09-01

## Evidence boundary

This report closes source, deterministic test, exact build, install, and load
work for PR #6's Human-root accounting repair. Human qualification remains
pending. The failed predecessor session is evidence about the old architecture,
not evidence for the new artifact.

- Source branch: `refactor/platform/native-foundation-full-run-mainline`.
- Canonical source commit: `f320ef65c8f34db02d321e06448391e459131331`.
- Build workspace commit: `cdcc8e17de0ca07757d7087e336b28030722d8c0`.
- Predecessor failure session:
  `session-20260831T164142Z-ee4ed79cff7a4f2296806ce7d224d93f`.
- Exact game: STS2 `v0.111.0 / 41cef1ea`, assembly
  `9cb4f1ad... / 57785517...`.

## Root cause

The old implementation exposed five overlapping views of one action:
`HumanActionScope`, the accepted-root gate, strict-V2 native ledger, modern
semantic timeline, and native semantic discriminator. The modern timeline was
already the causal evidence stream, but audit still required every diagnostic
discriminator acceptance to appear in the strict-V2 ledger. Async Task
completion attempted to recover a root from a short-lived ambient UI scope;
the shared terminal-reward callback hard-coded a domain family; and both Task
completion and `GameAction.Finished` were treated as immediate successor
boundaries.

That explains the predecessor failures: Map/PickRelic modern roots were rejected
for lacking a legacy projection, Reward/CardReward completions lost roots or
families, and Map `Finished` snapshots could remain the same decision state.
Green unit tests exercised each helper separately and did not traverse root,
native binding, Commit, successor, persistence, and final audit together.

With the corrected accounting definition, the predecessor bytes now audit as
118 valid Decision V2 records, 73 explicit invalidations, and no malformed
stream errors: discriminator roots are allowed to resolve through the modern
timeline instead of requiring a legacy projection. This is an audit-definition
correction, not retroactive semantic qualification. Offline calibration still
finds only 12/45 modern canonical transitions, 32 unresolved successors, and
one unresolved state/action-space row on those old bytes.

## Canonical model

ADR 0005 defines the single modern model:

```text
Human Root
  -> exact native operation binding
  -> Native Commit
  -> authoritative Successor Boundary
```

- The semantic timeline is the sole modern accounting authority.
- The strict-V2 native ledger remains a historical compatibility projection.
- The discriminator remains a non-authorizing execution diagnostic.
- A Task is bound while native owner/operand/lineage are available. Its durable
  binding carries session, generation, root, family, kind and Task identity;
  completion never consults `HumanActionScope.Current`.
- A shared callback gets family from the matched root, never from callback
  naming or completion order.
- `GameAction.Finished` and successful Task completion prove Commit only. They
  never capture or prove `S'`.
- `S'` requires a typed native owner-ready boundary, a legitimate paused
  PlayerChoice, or the next Human root's complete pre-execution state before
  its effect. A committed unresolved root is therefore allowed to hand off to
  the next exact root.
- Duplicate, stale, ambiguous, cancelled, faulted, unmatched, missing-boundary,
  and cross-Human cases fail closed. There is no FIFO, count, timer, polling,
  UI-stability, queue-idle, retry, or backfill proof path.

## Native seams

| Family | Human root and pre-state | Native Commit | Successor boundary |
|---|---|---|---|
| Map | exact `MapPoint` in the frozen Map decision frame | exact `VoteForMapCoordAction.Finished` | proved typed owner-ready publisher or next exact root execution pre; room entry alone is insufficient |
| Reward claim | exact `RewardsSet` and `Reward` | successful bound `SelectLocalReward` Task | future proved typed owner-ready publisher or next exact root execution pre |
| Reward proceed | exact Reward terminal state | successful bound `ProceedFromTerminalRewardsScreen` Task | future proved typed owner-ready publisher or next exact root execution pre |
| CardReward | exact active native card option | synchronous `NCardRewardSelectionScreen.SelectCard` completion-source Commit | future proved typed owner-ready publisher or next exact root execution pre |
| Treasure open | exact `TreasureRoom` closed decision | successful bound normal-reward Task | future proved typed owner-ready publisher or next exact root execution pre |
| Treasure select/skip | exact relic-choice decision | exact `PickRelicAction.Finished` | future proved typed owner-ready publisher or next exact root execution pre |
| Treasure proceed | exact completed Treasure decision | successful bound terminal-proceed Task | future proved typed owner-ready publisher or next exact root execution pre |

Correction after exact runtime review: the original table described possible
owner-ready outcomes as if they were already production observations. At that
source there was no production `NativeDecisionOwnerReady` publisher. Continuous
gameplay was settled by next-root pre-execution handoff; explicit Close after a
committed final root therefore remained unknown. The successor repair records
the first proved publisher for a player-ready Combat owner only.

Shop and Rest were source-audited only as generalization probes. Both fit the
same root/Commit/successor contract with domain adapters; neither requires a
new ledger. Their exact production seams and Human coverage remain out of PR #6.

## Automated evidence

The repair adds cross-layer counterexamples for exact GameAction accounting,
async Task binding after UI scope exit, shared-method family recovery,
Commit-before-successor, ambiguous/stale/cancel/fault/unmatched failure, the
committed-root execution handoff, and a complete persisted synthetic audit.
Annotator Core passes 116 tests; game-Mod boundary tests pass 42 tests.

Clean exact artifacts:

- Annotator `05e03fa2925866ba169e018c8ff4e7afdc0cb2eb2d602025f588c98c81f25028`
  / `e651b3e4-2577-4b69-81a9-a78cd4a03e2c`.
- Unified `STS2_PLATFORM`
  `b637d380990c5f5c28d5c390c4b6d215083e84ed7fc8c9e276beae254e212b16`
  / `9286ca51-4617-4ec1-8015-3de9d35cbda3`.
- Connector remains `6215718... / cb205b8e...`.

Safe install and cold-load pass in Connector runtime `583ab4e5...`, environment
`ea08bd5d...`, exact sole-Platform Modset `30b507c5...`. The Recorder starts
Ready with no session. Rollback is
`apps/game-mod/.local/deployments/2026-08-31T18-18-39.375Z`.

## Non-claims and next gate

Loaded is not Human-qualified. No old Human PASS transfers, no runtime
performance improvement is claimed, lifecycle-only full capture must remain
zero, and Shop/Event/Rest/Full Run are not added.

The next gate is a bounded Human canary covering Map, Reward claim/proceed,
CardReward, Treasure open/select-or-skip/proceed, and one natural rapid case.
Every accepted root must receive one explicit disposition; successful modern
proof must contain exact Commit identity and a later causal boundary.

Current status: `source_test_build_load_pass_human_pending`.
