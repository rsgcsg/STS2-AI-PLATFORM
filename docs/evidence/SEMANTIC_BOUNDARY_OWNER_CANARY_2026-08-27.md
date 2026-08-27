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

## Corrected-artifact owner canary

Owner session
`session-20260826T150700Z-9ffa61b45dc64cb3b5c2ec2373853129`
then ran on that exact artifact and closed normally in the same runtime. The
portable Annotator audit passed with nine valid Decision V2 records, zero
invalid records, 29 explicit invalidations, 88 materialized Reads and zero Read
failures. The immutable local evidence digests are:

```text
Decision V2       a308b8f146523db973e49fd00ff1f264064ae58585ba424e300f95694286a95c
invalidations     edd10f85429213cfcc9183b107794c1b47a4c2a465bfe26dfa5f3c5bcc278818
native ledger v2  6038593f9c73007abde261bc01c80833f7e82ac852eddf158a033ad5ac26062f
semantic trace    1fc6e1a9d7b69aaf55b35eae939536c92d697895b0b807109180f5a53fe09d2e
run journal       9c0ea6e5895b4b74ef524530fad20551b781edfd3a4c2c268b6fdf2b3595e12b
```

The semantic trace accounts for all 35 accepted Human actions: 25 native card
plays, nine native End Turns and one source-local generated-card selection.
Every acceptance has exactly one terminal semantic disposition: 24 proved
transitions, six explicit unknowns, two cancellations before start, two
cancellations after start and one PlayCard abort before native Commit. The
proved set contains 23 ordinary next-decision boundaries and one real
player-choice boundary. No proved transition contains an intervening Human
`action_started`, and every proved pre-state equals the complete authoritative
boundary captured immediately before that action's execution.

Native ledger v2 independently accounts for all 34 GameAction roots with zero
unresolved actions: 32 started, 30 finished, four cancelled, one
pause/ready/resume lifecycle, nine strict Decision V2 admissions and 25 strict
invalidations. The generated-card UI action is represented by the semantic
trace rather than by the GameAction-only ledger.

This is strong corrected-artifact Live evidence for ordinary execution-order
coordination, player-choice lifecycle, cancellation/abort accounting and
fail-closed unknown handling. It did **not** reproduce the predecessor's exact
counterexample: no later accepted Human action started before an earlier queued
action, so
`complete_rebound_after_intervening_human_action = 0`. The exact rebind branch
therefore remains source/test-proved and pending a narrow owner canary; it is not
promoted by inference from the other rapid chains.

## Exact execution-order rebind owner canary

The later closed owner session
`session-20260827T014202Z-a9ef5a3ca4c445549ca1d6237e773661`
ran on the same Native artifact in runtime
`2388aba057ab4ff5a37588d235a28fab`, environment
`da43dbc6084001a92b3e3467adb548e9be9635aa2fafae77964fb188c91a2e63`,
and exact sole-Mod fingerprint
`2aee9e7e2f5d8611cf139319049d2bf9090c6cfff9e5491a00085685e7073be3`.
The game remained STS2 `v0.111.0 / 41cef1ea`, assembly
`9cb4f1ad... / 57785517...`; the loaded unified Mod remained
`04104ca5... / 7408a183...` with Annotator source `cb20bfa...`.

Portable audit passed with three valid Decision V2 records, zero invalid
records, 32 explicit invalidations, 64 materialized Reads and zero Read
failures. The immutable local evidence digests are:

```text
Decision V2       faac884a2de6c5f133ec9960ac99610bdd963841eea8a13c1d620e37c6bfd5ad
invalidations     7675ed060d5cfc8fb615862509f700bac64f1c709d59a3e388fa270d407ad44e
native ledger v2  b85c53e78bba47bd9b82ddf9600f261ca64ed8562d5ee0d21734505a3e4103ba
semantic trace    7e5d743061694a1cc6f437be3973eb9f6ec7dfbb6e1473af296c05de6eedbbf9
run journal       d41a008191cb3f9341a51c2d52e7db8d8c896ab1495092285c0163abe1a1b3c1
```

The trace accounts for all 29 accepted Human actions with 25 starts and one
terminal disposition per acceptance: six proved, eleven unknown, four
cancelled before start and eight cancelled after start. There are no unresolved
actions and no abort classified as a transition unknown. The ledger
independently accounts for 28 GameAction roots; the generated-card selection is
the source-local semantic action.

### Unknown-by-unknown audit

`H` is the Human observation captured at acceptance. `S` is the semantic pre
actually admitted by the coordinator; a missing `S` is never reconstructed
from a later frame.

| Action | Native lifecycle and execution order | H / S and boundaries | Exact terminal proof | Classification |
| --- | --- | --- | --- | --- |
| 1 `game_action_ce596855_5` `PlayCardAction` | accepted 1, started 3, finished 5; execution 1 | H/S `state_e875e1e592_1d`; pre-execution `...1e` incomplete; terminal `...23` incomplete | `boundary_incomplete_before_next_action`, related action 2 | Correct fail-closed: no complete S' before action 2 started. |
| 2 `game_action_ce596855_7` `PlayCardAction` | accepted 4, started 8, finished 11; execution 2 | H `...1f`; S absent; execution boundary `...23` incomplete; later `...28` cannot repair S | `execution_boundary_incomplete` | Correct fail-closed: execution had no complete authoritative semantic pre. |
| 3 `game_action_ce596855_a` `PlayCardAction` | accepted 9, started 14, finished 16; execution 3 | H `...21`; S `...28`; terminal `...2c` incomplete | `boundary_incomplete_before_next_action`, related action 4 | Correct fail-closed: S is known but causal S' is not. |
| 5 `game_action_ce596855_d` `EndPlayerTurnAction` | accepted 15, started 22, finished 23; execution 5 | H `...29`; S absent; execution boundary `...2d` incomplete; later `...43` is too late | `execution_boundary_incomplete` | Correct fail-closed: complete later state cannot be retroactively assigned as S. |
| 7 `game_action_ce596855_15` `EndPlayerTurnAction` | accepted 26, started 31, finished 32; execution 7 | H `...46`; S absent; execution boundary `...4b` incomplete; later `...5a` is too late | `execution_boundary_incomplete` | Correct fail-closed. |
| 11 `game_action_ce596855_22` `EndPlayerTurnAction` | accepted 45, started 51, finished 53; execution 11 | H `...8a`; S absent; execution `...95` and terminal `...b0` both incomplete | `boundary_incomplete_before_next_action`, related `...2b` | Correct fail-closed: neither S nor S' is provable. |
| 14 `game_action_ce596855_2c` `EndPlayerTurnAction` | accepted 55, started 61, finished 62; execution 13 | H `...a8`; S absent; execution boundary `...b1` incomplete; later `...c3` is too late | `execution_boundary_incomplete` | Correct fail-closed. |
| 16 `game_action_ce596855_3a` `PlayCardAction` | accepted 66, started 70, finished 71; execution 14 | H/S `...db`; execution `...e6` incomplete; terminal `...e8` incomplete | `boundary_incomplete_before_next_action`, related `...3c` | Correct fail-closed: S known, S' incomplete. |
| 18 `game_action_ce596855_3e` `EndPlayerTurnAction` | accepted 68, started 77, finished 78; execution 16 | H `...e0`; S absent; execution `...e9` incomplete; later `...fd` is too late | `execution_boundary_incomplete` | Correct fail-closed. |
| 20 `game_action_ce596855_45` `EndPlayerTurnAction` | accepted 81, started 86, finished 87; execution 18 | H `...100`; S absent; execution `...106` incomplete; later `...11c` is too late | `execution_boundary_incomplete` | Correct fail-closed. |
| 29 `game_action_ce596855_6a` `EndPlayerTurnAction` | accepted 121, started 123, finished 124; execution 25 | H `...1b3`; complete execution-bound S `...1b9`; no terminal boundary before Close | `recording_closed_before_semantic_boundary` | Correct fail-closed caused by owner Close before a complete successor; the ledger and RunJournal retain it. |

The eleven unknowns reduce to three evidence boundaries, not an implementation
defect: six missing complete execution-boundary S values, four missing complete
S' boundaries before another Human action starts, and one Close before S'. No
existing typed event can strengthen these claims without replacing an absent
authoritative capture with timing or later-state inference.

### Live rebind proof

The exact reorder occurred. Actions 25 (`PlayCardAction`, old pre
`state_e875e1e592_185`) and 26 (`EndPlayerTurnAction`, pre absent) were accepted
before the later source-local generated-card selection. The selection then
started and finished first and proved `...18c -> ...18d`.

Before action 25 actually started, trace event 113 emitted
`complete_rebound_after_intervening_human_action`, related to the selection,
and replaced the old pre with complete authoritative boundary `...194`.
Action 25 then started and was natively cancelled; no S' was fabricated. Before
action 26 started, event 116 independently rebound it to complete boundary
`...195`. Action 26 finished and proved `...195 -> ...1b0`; no other Human
action started inside that interval. The strengthened audit passed with zero
pre/execution-boundary mismatches and zero proved transitions crossing an
intervening Human start.

This closes the narrow execution-order rebind gate as **Live-proved for this
exact artifact/runtime**. It does not generalize to lethal cross-surface or
Full-Run settlement.

## Non-claims

- The predecessor sidecar is not corpus or training authority.
- Loaded readiness alone is not corrected semantic-transition evidence; the
  corrected-artifact session above is the bounded Human evidence.
- The current canary does not prove lethal cross-surface settlement or Full-Run
  surfaces.
- Automated replay does not replace a corrected-artifact Human canary.
