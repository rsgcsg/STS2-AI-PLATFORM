# PR #6 Human Qualification Repair Source Closeout

Date: 2026-09-01  
Evidence level: source, exact-native inspection, recorded-Human audit, and tests
only. The repaired bytes are not yet Human-qualified.

## Authority and inputs

The audit used PR #6 head `8697f71ae01b821f3de4e5f1dc0e450cc4f8efc1`,
shipped STS2 `v0.111.0 / 41cef1ea`, assembly
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4 /
57785517-0b16-42b9-8b36-bad6fb28384b`, exact decompiled native call paths,
and these same-bytes closed Human sessions:

- low-power `session-20260901T024729Z-bd58dd9af62c4105838967e276c02916`;
- normal-power `session-20260901T012556Z-b91447ffe6f1419db385253178dd6871`.

The low-power session has 173 valid strict records, zero invalid records, 125
explicit invalidations, and 188/188 successful native discriminator roots. Its
semantic calibration has 46 accepted roots: 43 canonical, one unresolved
state/action match, and two unresolved successors. The normal-power session has
35 accepted roots: 32 canonical, one unresolved state/action match, and two
unresolved successors.

## Root cause

CardReward used the whole `RewardsSetSynchronizer.SelectLocalReward` Task as
the parent Reward claim Commit. STS2 awaits `CardReward.OnSelect`, which opens
`NCardRewardSelectionScreen.ShowScreen` and then waits for the child Human
selection. The child therefore arrived before the parent Task could finish and
correctly failed closed as serialized overlap. The exact nested owner creation,
not that later business Task outcome, is the parent Commit seam.

Treasure had two independent native-state mistakes. `CurrentRelics` is created
when the room is entered, before the chest is opened, so its presence cannot
classify the room as `opening`. Later, `PickRelicLocally` publishes a predicted
vote before `PickRelicAction.ExecuteAction` calls
`TreasureRoomRelicSynchronizer.OnPicked`; `GetPlayerVote` therefore cannot
prove Commit. The exact `OnPicked(Player, int?)` lifecycle callback is the
committed vote observation.

The `Human Root -> Native Commit -> Successor Boundary` model remains
sufficient. The defects were domain seams inside that model. Commit still does
not equal `S'`; no polling, timer, FIFO, count, current-root lookup, or backfill
was added.

## Hypotheses resolved

- Reward globally unstable: rejected. Reward roots and most native accounting
  repeat successfully in both same-bytes sessions.
- CardReward option/provider ownership is the first failure: rejected. The
  child is blocked before its Commit because its parent is still open.
- Treasure task binding is the primary failure: rejected. The task mismatch is
  downstream of an unopened chest being misclassified with no actionable
  pre-frame.
- Treasure local vote is committed when first visible through
  `GetPlayerVote`: rejected by native action ordering.
- Map catalog collapse: rejected. The latest trace has 15/15
  `VoteForMapCoordAction` accepts, native commits, and proved transitions; its
  two invalidations are overlap cascades.

## The 125 invalidations

The 125 rows are explicit fail-closed accounting, not 125 independent native
failures: 121 are `serialized_evidence_overlap`, three are
`native_task_binding_no_match`, and one is
`semantic_pre_frame_capture_failed`. The overlaps comprise PlayCard 72,
EndTurn 16, hand confirm 15, CardReward select 9, deselect 4, Map 2, Reward
claim 2, and Reward proceed 1. One stale pre-snapshot identifier accounts for
38 overlap rows, showing causal amplification from an unresolved scope. The
remaining task/pre-frame rows localize the CardReward/Treasure seam defects;
they do not establish persistence failure or broad gameplay illegality.

## Bounded Recorder hot-path audit

There is no idle per-frame Snapshot polling: each audited session contains one
`idle_status_refresh`. Appends are buffered and Close performs the durable
flush. Normal-power measured read-rich capture P50/P95 was 12.777/20.315 ms;
low-power was 20.682/28.595 ms with a 52.460 ms maximum. Because power state
differs, this is not version-regression evidence. It does show synchronous
Snapshot capture can cross a frame budget and can explain light Recording-on
stutter.

The additive discriminator also performs fallback full Snapshots for legacy
execution samples; those were absent from the existing profiler totals. The
repair adds separate diagnostic timings for fallback Snapshot capture and
projection from an already captured frame. It does not remove evidence,
change authority, move work asynchronously, or claim a latency improvement.
The staged-card path already avoids a second capture when there is no causal
debt and reuses the settlement frame when debt exists, so no speculative rewrite
was made. Repeated identity hashing remains a measured optimization candidate,
not a proven safe change in this repair.

## Source and test result

The repair:

- commits CardReward reward-claim lineage at exact `ShowScreen` owner creation
  and does not bind its later `SelectLocalReward` Task as the root Commit;
- treats unopened Treasure as `closed` even when relics are pre-generated;
- records the exact single-player `OnPicked` Commit and never consumes
  predicted `GetPlayerVote` as committed state;
- adds nested-parent, Treasure-stage, exact-patch, no-predicted-vote, and
  profiler-routing regression guards.

Targeted Annotator tests pass 117/117 plus all semantic-analysis/calibration
tests. Game-Mod boundary tests pass 44/44. Root `npm run check`, exact-game
checks, and clean compilation pass with zero warnings and zero errors.

Clean build workspace `4fc9702aa8097a8c9859e79eb3eb7197fc34f1f6`
contains semantic component source `b0ebd966c7b1aa8395a635965599ec3fbb4db763`.
The unified artifact is
`641f543d93bfa2090f6257245d453c8def6734881dbb61f2be990cd77a78e0f8 /
7ac2fa24-83e5-40cc-af5d-f872946bf802`. Safe install and cold-load pass in
runtime `c3e4d2b79cef469c91a6065f66bec743`, environment
`6655e3a862b0e38f4ea01f6e9ed8b209f23b005e98d6badf387fcf3e07ed211e`,
and sole-Platform Modset
`91d2f6e71d75f2d4625514db9d3eeab29a6572be1dd616ea0f13f6c50dd1768c`.
Recorder is Ready with no open session. Rollback is
`apps/game-mod/.local/deployments/2026-09-01T03-47-46.154Z`.

## Non-claims and next gate

This audit and loaded qualification do not transfer Human evidence to repaired bytes, prove a
performance improvement, qualify exhaustive Full Run, or admit the two prior
sessions as repaired evidence. After a clean exact build/install/load, the
shortest Human canary is: one Reward claim that opens CardReward and one card
selection through its next authoritative boundary; one Treasure open and relic
select (or skip if naturally offered) through proceed/Map; then one Map travel.
Recording Close must seal with no unresolved lifecycle. A natural rapid case is
useful for performance observation but cannot manufacture semantic proof.
