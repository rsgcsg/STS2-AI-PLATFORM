# Status

## Implemented

- process-local Connector witness freezing one stable Snapshot and complete
  finite BoundAction catalog with exact Host-local references;
- native STS2 UI scope around card play and end-turn callbacks;
- observation of accepted `PlayCardAction` and `EndPlayerTurnAction` at the
  game-owned action queue;
- exact card/target reference mapping with zero/ambiguous fail closed;
- different complete interactive successor settlement;
- append-only per-run JSONL, invalidations, coverage, audit, and export;
- exact game/Connector/Annotator SHA, MVID, source revision, source digest,
  runtime, environment, protocol, and Modset provenance;
- STPD strict import through its existing Player Environment projection and B0.

## Automated Evidence

Core validation covers exact records, zero/ambiguous mapping rejection, same
snapshot rejection, nested runtime drift, catalog tampering, multi-run append,
audit, and export. Connector tests cover duplicate-looking native objects,
target disambiguation, incomplete frames, and exact observer fingerprinting.

## Exact Runtime Findings

The first 2026-08-22 observer artifact produced 25 independently auditable
`end_turn` records and exposed late card capture plus nested root
misclassification. Source `bc9c568...`, cold-loaded with queue-aware Connector
source `2a14504...`, then produced 64 admitted records: 51 card plays, including
27 targeted plays, and 13 end turns. All 64 mapped exact-unique and reached a
different complete interactive successor.

That run reduced the prior 22 queue-driven pre-frame misses to four clicks made
without any stable complete S. It also exposed four `mapping_zero` plays:
`TryPlayCard` ran after the selected holder had already left the active hand.
Source `6d474ce...` staged the exact frame at `NPlayerHand.StartCardPlay` and a
second owner-operated run admitted 106 records: 35 targeted plays, 48
untargeted plays and 23 end turns. Audit/export and strict STPD B0 passed with
zero rejected records. Five actions had no complete S and failed closed; one
following action exposed that the old generic latest-frame fallback could cross
a turn boundary. Current source removes that fallback. The 106 records prove
same-card staging and the downstream path, but do not validate the final
fallback-deletion source artifact.

## Pending Exact Runtime Evidence

- current fallback-deletion Annotator source build/install/cold-load identity
  with Connector source `2a14504...`;
- an untargeted card, targeted card and end-turn accepted through the native UI
  without a generic latest-frame authority path;
- exact one-to-one mapping, stable successor, audit/export, and STPD import from
  those real records;
- no observable gameplay interference during a bounded ordinary run.

Until those gates pass, status is **implemented candidate**, not human validated,
qualified, or released.

## Declared Unsupported

Potions, non-Combat selectors, event/reward/shop/rest/map/menu actions,
multiplayer, and gameplay-affecting Modsets are not admitted by version 0.1.0.
