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

## Predecessor Live Finding

The 2026-08-22 exact observer artifact produced 25 independently auditable
`end_turn` records that exported and passed STPD import/B0. It also exposed two
Recorder defects: card release captured a settling frame too late, and the
end-turn scope treated `ReadyToBeginEnemyTurnAction` as another human root.
Current source keeps the latest same-interaction authoritative frame and admits
only one expected root action per UI scope. The predecessor records prove the
native end-turn seam and downstream data path only; they do not validate this
new source artifact.

## Pending Exact Runtime Evidence

- current Connector and Annotator source build/install/cold-load identity;
- an untargeted card, targeted card, duplicate-looking-card distinction, and
  end-turn accepted through the native UI;
- exact one-to-one mapping, stable successor, audit/export, and STPD import from
  those real records;
- no observable gameplay interference during a bounded ordinary run.

Until those gates pass, status is **implemented candidate**, not human validated,
qualified, or released.

## Declared Unsupported

Potions, non-Combat selectors, event/reward/shop/rest/map/menu actions,
multiplayer, and gameplay-affecting Modsets are not admitted by version 0.1.0.
