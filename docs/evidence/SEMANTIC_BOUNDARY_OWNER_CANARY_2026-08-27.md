# Semantic Boundary Owner Canary

Date: 2026-08-27

## Exact evidence

Owner session
`session-20260826T141755Z-0f4b31b20ac14b75a1ea3deaeed65caa` ran in
runtime `af2e7370136549ddbc547a2c9cd3cb13` with the sole
`STS2_PLATFORM` Mod, artifact SHA-256
`2cb46ead44ea8d906e7abf834da917f9504bfdc0c6e1a577d152ec3049a5118e`,
MVID `66ed1396-2186-46b0-9fbf-7260c2a2a177`, and exact STS2 `v0.111.0 /
41cef1ea`.

The closed session contains 22 accepted and started Human actions: 13
`PlayCardAction`, eight `EndPlayerTurnAction`, and one real
`NChooseACardSelectionScreen.SelectHolder`. Twenty actions finished and two
plays were cancelled after start. Every accepted action has exactly one
semantic disposition. Decision V2 independently retains two valid records and
21 explicit invalidations.

## Counterexample

The canary disproved the source-closeout assumption that acceptance order is
always semantic execution order. End Turn action sequence 9 was accepted first;
generated-card choice sequence 10 then started and finished before the queued
End Turn started. The predecessor coordinator retained the End Turn's earlier
Human observation as semantic S and later emitted a proved successor after the
choice effect. That is not a valid sequential S -> A -> S' sample.

The strengthened audit rejects this exact immutable trace with:

```text
semantic_transition_pre_not_execution_boundary = 1
```

The two Decision V2 records remain individually valid and readable. The
additive semantic sidecar is rejected as a seal; raw evidence is not modified.

## Owning-layer correction

The coordinator now orders causal predecessors by exact observed execution,
not acceptance sequence. A precommitted action may consume a later semantic S,
but only when a complete authoritative boundary is captured immediately before
its native execution. That boundary replaces the earlier precommit frame. An
incomplete execution boundary yields unknown. Audit also rejects:

- a proved transition whose semantic pre differs from its complete execution
  boundary; and
- any proved transition containing another Human action start after its own
  start and before its successor boundary.

Source tests replay the observed choice-before-queued-action ordering. The
correction changes Native source and therefore does not inherit the predecessor
session's semantic evidence.

## Corrected artifact load boundary

Corrected Annotator source
`cb20bfa6e4e0e64b3ee8fdf0e1d472e8a668450b` was built from clean workspace
`fa44bb1c4890a4ef6fbb77f1a93caeb469b6ba29`, installed, and cold-loaded as the
unified artifact SHA-256
`04104ca5cd47c82329be185a8aa7017f7982b2062048fbf825488bead231d1c1`, MVID
`7408a183-f5a8-4f95-a997-dbf588b1536b`. Runtime
`978293175c054fc89171f40da5365fd9` reports exact STS2 `v0.111.0 / 41cef1ea`,
only `STS2_PLATFORM`, Modset fingerprint
`995308265700bd1eb95fd4ee0b74d17d0919193427c311837ae9c41e1ee80c6e`, and
Connector execution available. Rollback is
`apps/game-mod/.local/deployments/2026-08-26T14-37-45.145Z`.

This proves build, install, cold-load, identity publication and Ready state.
No Human action has exercised the corrected semantic coordinator on this
artifact yet, so corrected S -> A -> S' remains pending owner runtime evidence.

## Non-claims

- The predecessor sidecar is not corpus or training authority.
- Loaded readiness is not corrected semantic-transition evidence.
- The canary does not prove cancel-before-start, non-Commit PlayCard abort,
  lethal cross-surface settlement, or Full-Run surfaces.
- Automated replay does not replace a corrected-artifact Human canary.
