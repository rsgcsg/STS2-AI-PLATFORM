# Status

Platform migration note: historical Annotator evidence below remains bound to
its original source/artifact. Annotator `0.2.0-rc.1` in the consolidated source
has no inherited build, loaded or native-human evidence claim; root
`../../../docs/STATUS.md` is authoritative for migration status.

## Implemented

- process-local Connector witness freezing one stable Snapshot and complete
  finite BoundAction catalog with exact Host-local references;
- native STS2 UI scope around card play and end-turn callbacks;
- observation of accepted `PlayCardAction` and `EndPlayerTurnAction` at the
  game-owned action queue;
- exact card/target reference mapping with zero/ambiguous fail closed;
- different complete interactive successor settlement;
- append-only per-run JSONL, invalidations, coverage, audit, and export;
- deterministic immutable session bundles with exact collection profile,
  pseudonymous worker/campaign fields, explicit human attestation,
  raw/audit/export provenance and complete checksums;
- exact game/Connector/Annotator SHA, MVID, source revision, source digest,
  runtime, environment, protocol, and Modset provenance;
- shared Windows Steam discovery, strict process detection, safe deployment,
  native cold launch, exact Connector game/source canaries, Modset admission,
  loaded-process verification, and rollback while retaining the macOS path;
- deterministic UTF-8/LF exports on Windows and macOS;
- STPD strict import through its existing Player Environment projection and B0.

## Automated Evidence

Core validation covers exact records, zero/ambiguous mapping rejection, same
snapshot rejection, nested runtime drift, catalog tampering, multi-run append,
audit, export, deterministic bundle packing, retry reuse, immutable-destination
rejection, collection-profile drift and missing-attestation rejection. Connector
tests cover duplicate-looking native objects,
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
a turn boundary. Source `6254967...` removed that fallback and admitted another
64 exact records, but five nonmatching game-owned `PlayCardAction` instances
claimed the human root before exact mapping. Current source exact-matches the
frozen native operands before claiming the root. The 170 predecessor records
prove same-card staging, no-fallback operation and the downstream path, but do
not validate this final ordering source artifact.

## Current Exact Runtime Seal

Source `9459d22...` was built, installed, cold-loaded and owner-operated with
Connector source `2a14504...`. Session
`session-20260822T172319Z-35064ba4aeb34a029828e5953b00903b` admitted 20
records: 8 targeted plays, 8 untargeted plays and 4 end turns. Every record had
exact one-to-one mapping and a different complete interactive successor;
audit/export and strict STPD B0 accepted all 20 with zero rejection and no
`mapping_zero`.

The latest owner-completed same-artifact session,
`session-20260822T175331Z-509754657baa4d3c8536a9215b6d7b97`, admitted 28
records: 14 targeted plays, 9 untargeted plays and 5 end turns. All 28 mapped
exact-unique and reached a different stable interactive successor. Independent
audit passed 28/0; one overlapping action was invalidated before admission. The
preceding empty bootstrap session admitted nothing. An earlier 72-action session
was launched without the exact observer Modset canary, so every action failed
closed as `exact_observer_modset_canary_missing`; that is environment evidence,
not a mapping regression.

The manifest's `not human validated` non-claim applies until owner review; the
owner has now confirmed manual native UI operation for this exact session.
Machine audit still cannot independently prove operator identity or controlled
non-interference. These runs validate the declared ordinary-combat recorder
slice. They do not validate the new offline bundle/corpus tooling against
multiple real workers, qualify a broader action family, or authorize training.

Windows x64 automated checks cover path discovery, exact candidate Connector
canaries, executable matching, deterministic export, and exact-game Mod builds.
A Windows loaded seal and human canary remain separate evidence and must be
recorded only after the corresponding cold loads and owner-operated actions.

## Declared Unsupported

Potions, non-Combat selectors, event/reward/shop/rest/map/menu actions,
multiplayer, and gameplay-affecting Modsets are not admitted by version 0.1.0.
