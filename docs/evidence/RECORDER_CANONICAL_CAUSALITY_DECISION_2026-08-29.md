# Recorder Canonical Causality Decision - 2026-08-29

## Evidence Boundary

This closeout audits the latest immutable owner recording and exact local game
source. It changes no Native runtime behavior and creates no new loaded or Live
claim.

- Platform analysis baseline: `0e62b8d0d3c7c68e4e7957f88372356cb206edfc` on
  `feature/annotator/full-run-semantic-mainline`, PR #3;
- STPD `develop`: `ae4c8ac43caf224b01951c030842a60814a09bea`;
- session `session-20260829T084437Z-cc4079776c9e417eba53a122e452cab7`;
- timeline `timeline-6385af934f7b4e088a7336a1e11f17eb`;
- Annotator source `6e404204ed5a12ceb62609ee4109d729bd9e933a`;
- unified artifact SHA-256 `bb37d34f6ebe8e3aba55483c7da069aeaa470c4c4f3150083e4f42abe47529b0`;
- unified artifact MVID `3587836e-57e6-4227-8301-5d0f9e25f17f`;
- runtime `9a42d54c537d4672b0d724c8bd439482`;
- exact STS2 `v0.111.0 / 41cef1ea`, assembly SHA-256
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
  MVID `57785517-0b16-42b9-8b36-bad6fb28384b`;
- exact sole-Platform Modset
  `90f3c7f3327fad758988992e77e7a5a818b8825d30f5727ac27e8ccffed6ed95`;
- semantic trace SHA-256
  `6c58b7afdad5c2cba2cffd7fc71abfaabb8e3bab62e42627d0b4f4188c0b0297`;
- native ledger SHA-256
  `7bdbb2e6be1deb14ff0361a7ea534ac6b1ef78b1e39872d5ccb8c72c6d419b32`.

The session spans 2,284.687 seconds. Legacy audit reports 497 valid Decision V2
records, 0 invalid records and 198 explicit invalidations. Semantic audit
accounts for 933 accepted/started/finished/`transition_proved` actions and no
unknown/cancel/abort/unresolved disposition. Those facts prove accounting and
the older state-boundary rules, not the canonical one-step contract below.

## Canonical Contract

```text
H      = Human provenance and exact native correlation; H does not define S
S      = authoritative fair-player state actually consumed by A
A(S)   = complete authoritative action catalog for that same logical S
A      = exact executed action, present exactly once in A(S)
S'     = next authoritative state after A and its causal automatic continuation
```

Therefore:

```text
H != S in general
acceptance/precommit != execution
GameAction.Finished != S'
interactive status alone != causal S'
schema-3 transition_proved != training eligibility
```

## Mechanical Eligibility

`components/annotator/tools/calibrate-semantic-training.mjs` verifies
content-addressed frame digests, joins immutable trace/ledger action facts,
requires complete same-state catalogs and exact-once selected action membership,
and accepts only typed player-choice or exact next-execution causal successors.

For this session all 933 accepted actions receive exactly one primary class:

| Class | Count | Meaning |
|---|---:|---|
| `canonical_s_a_s_prime` | 0 | no row satisfies the full one-step contract |
| `successor_unresolved` | 247 | same-state S/A exists; S' is only generic interactive polling |
| `state_action_space_unresolved` | 682 | executed action is absent from execution-time A(S) |
| `rejected` | 4 | exact action is unavailable in the semantic/ledger evidence |

Additional, deliberately overlapping signals:

- `legacy_usable`: 497 under unchanged Decision V2 semantics;
- `canonical_s_a`: 247, with no canonical S';
- `rapid_rebind_valid`: 0;
- `future_action_chain_candidate`: 682, because H contains the precommitted
  action but execution-time one-step S/A parity does not hold.

The 682 unresolved rows comprise 548 `PlayCardAction`, 127
`EndPlayerTurnAction`, and 7 `UsePotionAction`. The 247 unresolved successors
comprise map, reward, card reward, selector confirm/deselect, and direct UI
commit paths. Four generated-card holder selections lack an exact action in the
schema-3/ledger join and remain rejected.

## Exact Native Audit

ILSpy 9.1 was run locally against the exact assembly hash above; no decompiled
source is committed.

- `NPlayerHand.StartCardPlay` reparents the holder and marks card-play staging
  before native acceptance.
- `NCardPlay.TryPlayCard` applies native target/play validation and requests a
  `PlayCardAction`.
- `NCardPlayQueue.OnActionEnqueued/OnLocalCardPlayed` removes the accepted
  holder from the current Human hand affordances.
- `GameAction.OnEnqueued` assigns exact queue identity before queue insertion,
  but acceptance order can differ from execution order.
- `ActionExecutor.BeforeActionExecuted` runs immediately before `Execute`, after
  the UI action catalog has already changed.
- `PlayCardAction.ExecuteAction` later checks that the model is still in Hand,
  validates target/play natively, spends resources and executes `OnPlay`.
- `EndPlayerTurnAction.ExecuteAction` only invokes `PlayerCmd.EndTurn`; its
  finish cannot prove enemy phase and next player decision settlement.

These facts explain the measured zero-membership rows. Moving from
`BeforeActionExecuted` to `GameAction.BeforeExecuted` is later, not earlier;
moving to enqueue recovers the old Human catalog but loses execution-state and
execution-order truth.

## Bounded Architecture Search

Tier A cannot repair missing same-state action membership. Tier B found no
deeper read-only native seam that owns identity, same-state action publication,
execution order and S' together.

The one bounded Tier C parity prototype compared H and execution-pre:

| Comparison | Same | Different |
|---|---:|---:|
| persistent state only | 916 | 17 |
| persistent + visible interaction context | 352 | 581 |
| plus Read payload digests | 352 | 581 |

Persistent-only equality is unsafely permissive because it omits combat state.
The stronger projection differs for all 548 PlayCard and all 7 UsePotion rows,
plus 26 EndTurn rows. Repairing that requires per-mechanism UI-staging
normalization, catalog reconstruction, and several S' owner adapters. It would
be a fragile second equivalence/legality model rather than a small observer.

The preferred next architecture is therefore serialized Human input for this
sequential data lane. The design and owner gate are in ADR 0003. No runtime
implementation has started.

## Latency And Footprint

The repaired artifact removed idle capture and per-event fsync, but the latest
Human session still performs 31,613 synchronous full Player Environment
captures: 13.837 calls/s, 628.720 seconds cumulative, 27.519% of recording wall
time, and a 273.851 ms maximum call. Major contributors are:

| Phase | Calls | Total |
|---|---:|---:|
| legacy successor Snapshot probe | 10,783 | 216.371 s |
| semantic boundary Snapshot probe | 10,975 | 213.156 s |
| native recovery Snapshot probe | 3,430 | 66.343 s |
| Read-rich Snapshot capture | 4,736 | 96.911 s |
| semantic Snapshot capture | 1,689 | 35.939 s |

Buffered evidence appends consume only 0.615 seconds (0.027% wall time), and
the durable Close flush takes 31.569 ms. The remaining lag is therefore repeated
full observation on the game thread, not append durability.

The session contains 3,535 files and 97,484,285 bytes. Semantic trace plus
unique content-addressed frames is 56,665,908 bytes and gzip control is
4,280,850 bytes. Decision V2 (20,181,231 bytes) and native ledger
(15,196,016 bytes) remain parallel compatibility/audit representations. A
future canonical lane should freeze once at each closed decision boundary and
derive compatibility views offline; compression alone cannot fix frame stalls.

## Non-Claims

- No current schema-3 row is authorized for canonical one-step training.
- The existing 497 Decision V2 rows retain only their old eligibility.
- The parity prototype is a counterexample, not a production comparator.
- No input gate, runtime feature, diagnostic build, install/load, or Human
  canary was created.
- The currently installed artifact and its rollback remain unchanged.
