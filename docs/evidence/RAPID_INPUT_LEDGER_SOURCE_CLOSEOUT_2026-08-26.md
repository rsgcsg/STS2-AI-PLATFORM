# Rapid Input Ledger Source Closeout

Evidence level: **ledger v1 source/test/build/install/load/Live behavior;
ledger v2 source/test/build/install/load**. The v2 decision payload needs an
owner rapid-input canary.

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
  recorder disposition. Ledger v2 also retains its frozen pre-frame, exact
  witness/mapping and BoundAction on the accepted event.
- One non-overlapping action may form a V2 transition only after native
  `finished` and the existing stable interactive successor gates.
- Any accepted overlap invalidates strict transition eligibility for the whole
  causal window while preserving decision-pre and native lifecycle evidence.
- Cancelled or persistence-unknown actions never form strict transitions.
- Recovery requires every tracked action terminal plus a new complete
  interactive boundary.
- `HumanDecisionRecordV2` is unchanged; predecessor sessions without the
  additive sidecar and ledger v1 sessions remain readable.

## Exact Runtime Evidence

Artifact `080701b3bf787c6055504678f5acf426141d4c65f33f4c94098db4d8e7f1d50e`
with MVID `142054a5-267c-40ad-b254-6989aa40ef8d` loaded in runtime
`39fa2d2e388c4ce798bdd291148fcce9` against STS2 `v0.111.0/41cef1ea`, exact
assembly `9cb4f1ad... / 57785517...`, and sole Mod `STS2_PLATFORM`.

Closed session
`session-20260826T062916Z-957f201043a4456a89d13407682f0541` passed independent
audit with 12 valid and zero invalid strict records plus 94 materialized Reads.
Its 140 ledger v1 events contain 35 accepted, 35 started and 35 finished roots,
12 strict admissions, 23 strict invalidations and zero unresolved actions. No
invalidated action was admitted, and no admitted action had an overlapping
predecessor. The action set was 23 `PlayCardAction` and 12
`EndPlayerTurnAction`; observed bursts reached five accepted actions.

This proves accepted-action accounting and no fabricated strict successor for
the observed windows. Cancellation and player-choice pause/resume were not
naturally exercised. Ledger v1 omitted the frozen decision payload for
invalidated roots, so targeted/untargeted classification of those rows is a
non-claim. Ledger v2 closes that source-level evidence gap and needs a new Live
canary.

Ledger v2 artifact
`df5d2c61304be5dfbbfe8f608a5832539a723f0330c93e7330f48fc97d0a3d0e /
9072e515-69f2-4131-957b-417d80008b04` is clean-built from Annotator source
`de5e55fc...`, safely installed and cold-loaded in runtime `ebe7a9fc...` with
exact Modset `20b2de1a...`. It reached Ready/no-session. This proves exact
identity and initialization, not that an owner action wrote a v2 ledger event.

## Non-Claims

Native `finished` is not business completion. The ledger does not prove an
action-local S' for overlapping actions, reconstruct legality/effects, or make
the Annotator an executor. Load does not prove ledger v2 mutation behavior. The
v1 Live artifact cannot transfer evidence to bytes it did not contain.
