# Rapid Input Ledger Source Closeout

Evidence level: **source + automated test + exact-game build**. Loaded and Live
claims are pending for the final clean artifact.

## Root Cause

The recorder kept one strict `_pending` transition. When STS2 accepted A2 before
A1 reached a provable stable successor, A2 was invalidated but there was no
durable per-action native lifecycle accounting, and A1 could not safely receive
an S' without risking inclusion of A2 effects. A queue of decision frames would
not solve causal attribution: A2's decision pre-frame is not A1's successor.

## Exact Seam

Local STS2 `v0.111.0` (`41cef1ea`) assembly
`9cb4f1ad... / 57785517...` shows that
`ActionQueueSet.EnqueueWithoutSynchronizing` calls `GameAction.OnEnqueued`
after assigning native ID/state and before notification, cancellation checks,
queue insertion or executor start. `GameAction` itself publishes started,
player-choice pause/ready/resume, cancelled and finished events. The repair uses
only a Postfix and event subscriptions; it does not patch `ActionExecutor`,
transpile IL, mutate arguments/results, delay input or infer completion from
animation/timing/queue emptiness.

## Semantics

- Each exact-correlated accepted root gets an additive ledger identity and one
  recorder disposition.
- One non-overlapping action may form a V2 transition only after native
  `finished` and the existing stable interactive successor gates.
- Any accepted overlap invalidates strict transition eligibility for the whole
  causal window while preserving decision-pre and native lifecycle evidence.
- Cancelled or persistence-unknown actions never form strict transitions.
- Recovery requires every tracked action terminal plus a new complete
  interactive boundary.
- `HumanDecisionRecordV2` is unchanged; predecessor sessions without the
  additive sidecar remain readable.

## Non-Claims

Native `finished` is not business completion. The ledger does not prove an
action-local S' for overlapping actions, reconstruct legality/effects, or make
the Annotator an executor. Automated fixtures and a dirty-worktree build do not
prove the final artifact loaded or Live. The predecessor
`887630f4... / 14761ed4... / bcf2b3f1...` runtime supplies the motivating
overlap evidence only.
