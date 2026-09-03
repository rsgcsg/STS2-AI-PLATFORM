# Semantic Timeline Schema-2 Source Closeout

Date: 2026-08-27

## Evidence boundary

The immutable owner session
`session-20260827T014202Z-a9ef5a3ca4c445549ca1d6237e773661`
remains schema-1 evidence for artifact `04104ca5... / 7408a183...`, runtime
`2388aba057ab4ff5a37588d235a28fab`, and STS2 `v0.111.0 / 41cef1ea`.
Its portable audit still passes with three Decision V2 records, 32
invalidations, 29 semantic acceptances, six proved transitions, eleven unknown,
four cancellations before start and eight cancellations after start.

Schema 1 omitted the frozen state payload whenever the UI/catalog gate was
incomplete. Therefore no schema-2 implementation can retroactively prove that
an omitted historical state was complete. Historical unknowns remain unknown.

## Root cause and source correction

Schema 1 coupled three independent facts:

1. player-visible state plus required Reads are complete;
2. the finite action catalog is complete and interactive;
3. this observation is a causal boundary.

At exact `ActionExecutor.BeforeActionExecuted`, (3) is supplied by the shipped
synchronous lifecycle seam. The next Human effect has not begun. A complete
player-visible state at this seam can close the previous edge and become the
next action's S even when the UI is still `settling` and has not republished its
catalog. Arbitrary settling polls do not gain this authority.

Schema 2 therefore records H separately, binds S only at execution, carries
state/Read/catalog status independently, and uses one continuous timeline:

```text
state-complete S0 -- exact A1 --> state-complete S1 -- exact A2 --> S2
```

Acceptance order creates no semantic pre-state. Cancellation before start is
not A. Abort before native Commit restores the consumed S. Cancellation after
start remains unknown. A missing S does not poison a later complete execution
boundary.

## Eleven historical unknowns

The old rows are not rewritten. The new source addresses their mechanisms as
follows; every recovery claim remains pending exact-runtime schema-2 evidence.

| Schema-1 action(s) | Historical cause | Schema-2 behavior |
| --- | --- | --- |
| A1, A3, A16 | S known; state at the next execution seam was discarded because UI/catalog was incomplete | Proves S' only if the new capture reports complete state and Reads; catalog readiness is irrelevant. |
| A2, A5, A7, A11, A14, A18, A20 | execution S was discarded by the same combined gate | Binds S from the exact pre-execution state when state/Reads are complete; otherwise remains explicit unknown without later backfill. |
| A29 | owner Close finalized before the next semantic observation | Close enters bounded draining, keeps observing, and closes after settlement; a five-second drain expiry remains unknown. |

Thus ten actions exercise one state-vs-catalog coupling and one action exercises
session draining. Deterministic tests prove both mechanisms, but the historical
trace lacks the state payload needed to claim any of the eleven recovered Live.

## Exact source audit

The exact STS2 assembly is SHA-256
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
MVID `57785517-0b16-42b9-8b36-bad6fb28384b`. In this build:

- `ActionExecutor.BeforeActionExecuted` fires synchronously before the selected
  `GameAction` executes;
- native started/paused/resumed/cancelled/finished events remain lifecycle
  facts;
- `EndPlayerTurnAction.ExecuteAction` only commits `PlayerCmd.EndTurn` and does
  not itself prove the full enemy-turn successor;
- `GameAction.Finished`, animation timing and queue-idle are not used as a
  universal semantic boundary.

No new Harmony hook, transpiler, scheduler change, timing proof, card rule or
gameplay reconstruction was added. The existing exact execution seam is the
lowest sufficient instrumentation.

## Automated evidence and remaining Live gate

Focused tests cover rapid A1-A2-A3, targeted/untargeted ordering, generated
choice before queued precommit, state-complete/catalog-unavailable handoff,
missing-state recovery at the next action, End Turn continuity, player-choice
pause/resume, cancel/abort, same-state unknown, Close unresolved accounting,
schema-1 readability and schema-2 audit invariants.

The exact schema-2 source candidate was built and safely installed while the
game was stopped:

- build workspace: `246eb6ab81b2174f7257f7df87668e3de6eda934`;
- Annotator source: `fed721c7b5131cf35d71025a3dd877266b5332fc`;
- unified artifact SHA-256:
  `eb7ed0728f0b908d3f82826fc7f8f63ec7d1d7b9ce9d2968bf423ca925fc872b`;
- unified artifact MVID: `34a36a2b-6ffa-4951-b53c-3fe72d37dd85`;
- built/installed parity: pass;
- rollback:
  `apps/game-mod/.local/deployments/2026-08-27T03-41-37.869Z`.

The game was not running during installation. Therefore `loaded = non-claim`,
schema-2 Human runtime is `pending exact-runtime evidence`, and none of the
predecessor artifact's Live authority transfers to this candidate.

## Schema-2 owner result

The final artifact was subsequently cold-loaded and exercised in owner session
`session-20260827T042832Z-652d5bd7d9ed4943b74ac6252427cbe2`.
Exact identity, immutable hashes, per-action dispositions and remaining
non-claims are recorded in
[Semantic Timeline Schema-2 Owner Closeout](SEMANTIC_TIMELINE_OWNER_CLOSEOUT_2026-08-27.md).

The canary exercised:

- ordinary play followed rapidly by another play;
- play followed by End Turn, including the prior A11 pattern;
- generated choice before an earlier queued action when naturally available;
- one Close immediately after a finished action while the successor is
  becoming interactive.

All 31 accepted roots have one disposition: 19 proved, nine cancelled before
start and three cancelled after start with an explicit unknown transition.
There are no standalone boundary unknowns, no unresolved roots, no proved
pre-state mismatch and no proof crossing another Human action start. A direct
Play -> End Turn execution handoff proves `A.S' == next S`; generated-card
select is proved. Exact acceptance/execution reorder did not recur on this
schema-2 artifact, and pending-edge Close-to-proof, catalog-incomplete Live
handoff, lethal cross-surface and Full-Run settlement remain non-claims.
