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
