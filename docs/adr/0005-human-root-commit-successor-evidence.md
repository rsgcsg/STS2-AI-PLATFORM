# ADR 0005: Separate Human Root, Native Commit, and Successor Boundary

Status: accepted for PR #6 source migration; Human runtime qualification pending.

## Context

The Human recorder currently exposes several overlapping accounting views:

- `HumanActionScope` and `AcceptedRootActionGate` correlate one synchronous UI root;
- `AcceptedHumanActionLedger` records legacy strict-V2 native actions;
- `SemanticBoundaryTracker` records the modern causal timeline;
- `NativeSemanticDiscriminator` compares execution-time native membership;
- `NativePostCommitCompletionLedger` correlates selected asynchronous tasks.

The latest exact Human session showed that these views do not form one coherent
authority. Modern Map and Treasure `GameAction` roots were rejected by an audit
that expected every discriminator acceptance in the legacy ledger. Reward and
CardReward task completions lost their root when task observation happened
outside the short-lived UI scope. The shared terminal-reward method emitted a
Treasure family even for Reward roots. Finally, `GameAction.Finished` and task
completion captured the current frame and attempted to prove `S'` immediately,
although those events prove native operation completion, not that the next
player decision owner is ready.

These are one definition problem, not four surface defects.

## Candidate designs

1. Patch each existing ledger and domain callback. This is a small diff, but it
   retains duplicate accounting authority and cannot prevent the next shared or
   asynchronous seam from repeating the same failure.
2. Make the semantic timeline the sole modern accounting authority and model
   root, Commit, and successor as distinct facts. Bind asynchronous native
   operations to roots by exact owner/operand/kind identity. Keep old streams as
   compatibility or diagnostics. This removes the contradictory definitions
   while preserving their historical bytes.
3. Introduce a generic event bus and universal gameplay state machine. This
   would hide domain-specific native lifecycle facts and risks becoming a
   second legality/effect model.

Decision: option 2.

## Canonical model

### Authorities

- STS2 owns rules, native legality, effects, Commit, tasks, and lifecycle.
- Connector owns fair-player `Snapshot`, `Read`, `BoundAction`, delivery,
  execute-time revalidation, and `Receipt`.
- Annotator owns Human correlation and immutable evidence only.
- The semantic timeline is the only modern Human-root accounting authority.

### Identity flow

`Human Root` is created once from exact Human input and its frozen decision
frame. A native `GameAction` or asynchronous task is then bound to that root by
session, generation, operation kind, and exact owner/operand/lineage identity.
The durable binding, not an ambient UI scope, carries the root through native
completion. A shared native method obtains family/domain from the matched root;
the callback does not hard-code it.

### Commit rule

A root receives a `Native Commit` only from its exact STS2 operation:

- the domain-proved successful native lifecycle point for a `GameAction`; or
- successful completion of the exact bound native task; or
- creation of an exact nested native decision owner when STS2 deliberately
  suspends the parent task until that child decision completes.

The CardReward claim is the concrete third case: successful
`NCardRewardSelectionScreen.ShowScreen` owner creation commits the parent claim
before the child Human selection, while `SelectLocalReward` remains a later
business completion. Generic UI callbacks, discriminator observations, and
delivery receipts do not prove Commit. Cancelled, faulted, stale, unmatched,
or ambiguous operations fail closed.

### Successor rule

Commit never implies `S'`. A committed root is proved only when an authoritative
next player-decision boundary is captured:

- a typed native owner-ready boundary;
- a legitimate paused `PlayerChoice` boundary; or
- the next Human root's pre-execution boundary, captured before that root's
  effect.

Periodic frames, UI stability, queue-idle, timers, completion order, and later
state backfill cannot create proof. A boundary that crosses another Human effect
is rejected.

An owner-ready event is not self-proving. Durable evidence must name the typed
domain, exact process-local owner witness/type and exact native mechanism, and
the synchronously captured Connector frame must independently be complete and
match that domain. On shipped `v0.111.0`, the first production publisher is the
player combat turn after STS2 has established its semantic play phase and
completed the exact end-turn input-owner callback. Earlier room/combat events
are explicitly insufficient. Domains without a proved publisher continue to
use next-root pre-execution handoff; Recorder Close cannot promote their final
committed root and retains it as explicit unknown rather than backfilling it.

### Legacy role

- `AcceptedHumanActionLedger` and native-action-ledger evidence retain strict-V2
  compatibility meaning only.
- `NativeSemanticDiscriminator` remains an execution-time diagnostic and is not
  an accounting authority.
- Legacy polling successors remain readable historical evidence and cannot
  satisfy modern causal qualification.

### Fail-closed rules

Duplicate roots, stale session/generation, wrong owner/operand/lineage,
ambiguous task binding, cancellation, fault, unmatched completion, intervening
Human effects, and missing successor boundaries produce explicit invalidation or
unknown evidence. They are never retried or backfilled.

## Generalization probes

Shop and Rest were inspected without entering PR #6 production scope.

- Shop fits the same model: exact inventory entry Human root, native purchase
  task Commit, then refreshed inventory, nested removal choice, or Map owner as
  successor. Current UI-derived publication remains migration debt, but no new
  accounting abstraction is required.
- Rest fits the same model: exact `RestSiteOption` Human root, option task/effect
  Commit, then nested selection, remaining rest phase, or Map owner as
  successor. Its native Commit seam still needs exact-version proof before a
  future migration.

Both require domain adapters, not another ledger or universal state machine.

## Consequences

The migration may change runtime bytes, so predecessor Human evidence cannot
qualify the new artifact. PR #6 requires a fresh exact build/load and bounded
Human canary after source, cross-layer, exact-game, and CI checks pass.
