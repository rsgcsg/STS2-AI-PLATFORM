# ADR 0003: Select Serialized Human Input For Canonical One-Step Evidence

- Status: selected, implementation not authorized
- Date: 2026-08-29
- Scope: Human Recorder canonical sequential evidence only
- Runtime effect: none

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

Serialized Human input is the preferred next collection architecture for the
current one-step model. This decision does not authorize implementation.

The future recorder design would:

1. obtain one complete authoritative `S0 + A(S0)`;
2. permit one Human action attempt and correlate its exact native acceptance;
3. close only additional mutation-producing Human inputs while STS2 executes
   the accepted action normally;
4. retain hover, focus, Read and diagnostic access where they cannot mutate;
5. observe game-owned lifecycle and a cheap owner/readiness signal;
6. perform one authoritative Player Environment freeze only after the owning
   surface can publish complete `S1 + A(S1)` and all tracked Human work is
   terminal;
7. validate identity, same action lineage, complete catalog and causal window,
   then publish `(S0, A(S0), A0, S1)` and reopen input;
8. enter explicit unknown/recovery state, without automatic unlock or retry, if
   causal settlement cannot be proved.

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
- Full-Run surface expansion is paused for the canonical one-step lane until
  the owner explicitly authorizes serialized-input implementation.
- The loaded artifact and current recorder behavior remain unchanged.

## Owner Gate

No input-gating code, disabled feature flag, native input patch, diagnostic
gating build, install/load, or Human canary may begin without a later explicit
owner approval message.
