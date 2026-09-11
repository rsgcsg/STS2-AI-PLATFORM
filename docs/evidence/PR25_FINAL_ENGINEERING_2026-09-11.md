# PR25 final engineering closeout — 2026-09-11

## Scope and exact baseline

G5 causal/evidence change with G4 package/load and G2 cross-component contracts.
Base develop `cd9a0fbb0c85577a13513abe715f91d794ac86eb`; starting PR25 head
`65b71b3cda67dc8312b6c459147380ecf41e4674`. Workstream remains existing Draft
PR25, normal-merge only after the final Human gate. STPD stays independently pinned
at `67566db562b95f57468290b97659c9f2e3862fe1`; no STPD modifications.

## Architecture and owning repairs

- Annotator keeps one SemanticBoundaryTracker and canonical path. Default export
  and bundle 3 preserve canonical transitions, complete raw evidence and a hashed
  causal audit. Explicit record/bundle 2 compatibility remains for actual STPD
  consumers. The unused historical writer and inline append API leave production;
  tests encode tracker fixtures through the current writer.
- Native StartCardPlay/factory/Cleanup owns staged H lifetime. Exact holder/owner
  and generation replace the 30-second same-card slot. Native Commit, execution
  S + A(S), successor and Human observation remain separate.
- Status 4/event batch 2 project owner dispositions and unique failure IDs.
  Native cancellation/abort and diagnostics are not unresolved decisions. Accepted
  capture or canonical persistence failures remain visible; unavailable accounting
  never presents zero. Selector capture/persistence uses the same exact occurrence.
- Current close-capability-1 sessions require a flushed matching close receipt.
  Final flush failure leaves Recorder closing/accounting unavailable; journal close
  alone cannot authorize audit/export/bundle. Compatibility append failure cannot
  revoke an already durable canonical decision.
- Evidence 3 checks canonical/proof/action/frame/Read/catalog/lineage links and
  exact inventory. Interaction-scoped required Reads retain their profile scope.
  Failure-only closed sessions are reportable without inventing a success row.
- Live UI removes K, adds a visible launcher and genuine compact Recorder/Policy
  views. Recorder compact is canonical count / real failures / latest three.
  Normal UI has primary status/actions, then selected evidence details. Hidden UI
  does not poll; Recorder does not request Connector snapshots; assembly identity
  is cached, event replay is sequence-based and retention is bounded to 512 rows.

See [current format/consumer inventory](../FULL_RUN_DATA_CHAIN.md),
[ADR-0007](../adr/0007-canonical-collection-and-disposition.md),
[UI specification](../UI_INTERACTION_SPEC.md) and
[distribution / future repair](../ANNOTATOR_COLLECTION.md).

## Measured storage and cost

Measurements are from immutable predecessor session
`session-20260911T090029Z-7be40b615a30417a8865b06d3e5e2fb0`, not a controlled
Recorder OFF/ON experiment or a new-candidate runtime benchmark. All 1,595 source
files were rehashed unchanged. Total storage is 33.03 MB (decimal): frames
13.76 MB, trace 8.04 MB, compatibility decisions 3.74 MB, native diagnostics
2.97 MB, action spaces 1.36 MB, Read blobs 1.25 MB, canonical 1.10 MB.

| Measured phase | Calls | Total | p95 |
| --- | ---: | ---: | ---: |
| semantic native surface state | 599 | 3.916 s | 19.963 ms |
| read-rich native surface state | 370 | 2.393 s | 16.294 ms |
| action-space serialization | 2,380 | 0.519 s | 0.204 ms |
| action-space hash | 2,380 | 0.010 s | 0.008 ms |
| Read blob verify/write | 1,575 | 0.312 s | 0.746 ms |
| canonical buffered append | 467 | 0.021 s | 0.093 ms |
| close durable flush | 1 | 0.029 s | 29.455 ms |

Native state materialization dominates these measured components. No speculative
frame/action-space object caching, serialization rewrite or relaxed flush was
introduced. Content-addressed storage already avoids repeated object writes.
UI fixes remove demonstrable unnecessary hidden/Recorder polling and repeated
immutable DLL hashing. This is a bounded cost reduction, not a claimed FPS,
allocation/heap or controlled OFF/ON improvement. Exact native owner lifetimes,
weak-key bindings, session-bounded tracker/failure IDs and bounded UI retention
were inspected; no forced GC or evidence eviction was introduced.

## Predecessor Human evidence and limits

The same predecessor has 468 accepted, 467 proved/canonical, zero unresolved,
one native cancellation and 49 internal diagnostic invalidations. All 71
selector children and parents are canonical; diagnostic membership is 322 exact,
one cancel and zero unknown. The two run fragments contain native start,
unproved end, explicit resume and native defeat: they are not one uninterrupted
Full Run. Knowledge Demon independent blocking choice was not observed.

C# canonical packing and independent Python verification of those unchanged bytes
produce 467 canonical transitions and one native cancellation. This establishes
cross-language tooling compatibility, not Human qualification of new code.

## Validation and final Human gate

Final exact source, portable/exact-game/CI/build/load and review receipts are
recorded at the PR's final candidate head. Engineering validation is in progress;
no new candidate Human PASS is claimed here. Raw sessions, native decompilation,
logs, packages and local receipts remain outside Git.

The final Human gate covers: (1) fresh uninterrupted native start through natural
terminal; (2) independent blocking and parent/child pile/generated selectors;
(3) AnyTime Fruit Juice / discard and generated potion; (4) event reward / rest
upgrade / Proceed; (5) rapid cards, long aim over 30 seconds, cancel/restage and
compact/restore UI. Naturally absent rare surfaces remain explicit non-claims or
receive a separate bounded canary. Do not use console/reload inside the Full Run.

After Human PASS, re-audit every accepted occurrence/disposition, canonical and
lineage, run boundaries, coverage and bundle. No unexplained lost input, real
failure or evidence/authority regression may be hidden. Only then update the exact
BOM/evidence/PR disposition, mark ready and normal-merge under current governance.
Rollback restores the previous exact deployment; failures after distribution use
versioned incident collection and owning repair, not historical evidence edits.
