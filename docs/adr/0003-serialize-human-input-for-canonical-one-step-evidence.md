# ADR 0003: Select Serialized Human Input For Canonical One-Step Evidence

- Status: historical candidate under native-semantic re-audit; not active UI authority
- Date: 2026-08-29
- Scope: Human Recorder canonical sequential evidence only
- Runtime effect: serializes mutation-producing Human input while recording

## Context

The sequential research contract is:

```text
S_t + A(S_t) -> A_t -> S_(t+1)
```

`S_t` is the authoritative fair-player state consumed by `A_t`; `A(S_t)` is
the complete same-state Player Environment action space; `A_t` must occur
exactly once in that catalog; and `S_(t+1)` follows the action and its causally
owned automatic continuation. Human observation `H` establishes provenance and
exact native correlation, but does not define `S`.

The latest exact session mechanically disproves the assumption that a complete
state at `ActionExecutor.BeforeActionExecuted` is sufficient for this contract.
STS2 stages accepted card and potion actions before execution, so the selected
Human affordance is no longer in the current UI catalog. A generic later
`interactive` Snapshot also does not by itself prove causal `S'`.

See [the causal closeout](../evidence/RECORDER_CANONICAL_CAUSALITY_DECISION_2026-08-29.md).

## Decision

Serialized Human input is the collection architecture for the current one-step
model. The owner subsequently authorized implementation; the source/test
candidate is described in the
[source closeout](../evidence/SERIALIZED_HUMAN_INPUT_SOURCE_CLOSEOUT_2026-08-30.md).

The recorder:

1. obtain one complete authoritative `S0 + A(S0)`;
2. permit one Human action attempt and correlate its exact native acceptance;
3. close only additional mutation-producing Human inputs while STS2 executes
   the accepted action normally;
4. retain hover, focus, Read and diagnostic access where they cannot mutate;
5. observe game-owned lifecycle without polling Player Environment;
6. perform one authoritative Player Environment freeze only after the owning
   surface can publish complete `S1 + A(S1)` and all tracked Human work is
   terminal;
7. validate identity, same action lineage, complete catalog and causal window,
   then publish `(S0, A(S0), A0, S1)` and reopen input;
8. records explicit unknown and closes or waits for a new owner action; it never
   retries an unknown mutation.

The future gate must never buffer, replay, synthesize or reorder a Human action;
alter STS2 scheduling; create legality; or treat elapsed time as completion.
Source-local adapters may identify the one input already in progress and the
owner-specific readiness fact, while Connector remains the only state/action
authority.

## Bounded Alternatives

### Tier A: repair the current observer

Rejected. Adding same-state action membership makes the current exact session
fail closed for 682 GameActions. Removing duplicated probes improves latency
but cannot restore a catalog that native UI staging has already withdrawn.

### Tier B: use a deeper native lifecycle seam

Rejected. Exact v0.111.0 source confirms `GameAction.OnEnqueued` is strong
identity but too early for execution order, while
`ActionExecutor.BeforeActionExecuted` and `GameAction.BeforeExecuted` occur
after UI staging removed the selected affordance. Native `Finished` does not
prove End Turn, player-choice, or cross-surface settlement. No single deeper
read-only seam supplies both same-state `A(S)` and causal `S'`.

### Tier C: promote an earlier catalog after execution-time parity

Rejected as disproportionate. An offline prototype found that a permissive
persistent-only comparison marks 916/933 pairs equal, while adding visible
interaction context marks 581 different, including every observed PlayCard and
UsePotion action. Normalizing those differences requires action-family staging
rules plus multiple successor-owner adapters. That is a second state-equivalence
model and remains weaker and more expensive than serialized collection.

Future action-chain evidence may retain rapid precommit provenance, but it is a
separate research scheme and cannot weaken this one-step contract.

## Consequences

- Existing V1/V2 records keep their historical meaning and bytes.
- Schema-3 `transition_proved` remains useful state/lifecycle evidence, but is
  not canonical training authority without the offline calibration gate.
- `calibrate-semantic-training` is the mechanical source for current one-step
  eligibility; legacy admission and trace terminology cannot promote rows.
- Supported canonical families no longer publish a parallel schema-3 execution
  path; unported Full-Run families retain that trace temporarily.
- The implementation changes native Mod bytes and requires a new exact-runtime
  artifact seal. Predecessor Human evidence does not transfer.

## Runtime Gate

The source is not Human-proved. Promotion requires clean build provenance,
safe install, cold-load identity and a short owner canary covering ordinary and
rapid input, canonical audit, first-command Close and after-latency profiling.

## 2026-08-30 Re-audit

The global implementation was withdrawn from active gameplay after unsupported
but valid STS2 UI could be blocked by evidence admission. Gameplay-safe commit
`4384a14...` is preserved as `baseline/pr3-gameplay-safe-4384a14`; no current
collector may restore that gate.

Current source instead runs an additive read-only discriminator at native first
execution. It compares the current UI catalog with a semantic catalog derived
from STS2-owned logical state and native validators, while recording exact
accepted identity, cancellation, pre-Commit abort and player-choice lineage.
This experiment neither authorizes nor delays input. This ADR remains the
historical rationale for serialization, but its decision is not promoted unless
the native discriminator is falsified by exact runtime evidence and a narrower
game-owned decision-epoch gate can be proved safe.
