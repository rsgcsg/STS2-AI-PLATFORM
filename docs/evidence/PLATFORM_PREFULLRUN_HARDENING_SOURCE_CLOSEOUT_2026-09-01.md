# Platform Pre-Full-Run Hardening Source Closeout

Date: 2026-09-01  
Base: `develop@a6389de0d83ce27ed7b27735b43eff57de0b6142`  
Implementation source: `d9c9bd2ece0d0b8862fa6d954a5cd5b91ea2f74d`  
Evidence level: source, tests, exact build, installed and loaded. Human pending.

## Architecture closure

The Platform authority split and shared `Human Root -> Native Commit ->
Successor Boundary` contract remain accepted. The pass removed two structural
drift points without creating a gameplay state machine:

- `SemanticBoundaryObservationCodec` is the single mapping between in-memory
  boundary observations and durable references. Persistence/resolution remains
  caller-owned; the codec cannot establish semantic proof.
- one typed Connector surface registry now composes each supported native UI
  surface's existing projection and execution adapter. Domain-specific STS2
  capture, legality and execution remain in their typed methods.
- combat-hand binding now recomputes exact `CandidateId` after final `hand_id`
  and entity binding are attached.

The durable owner-ready round trip is tested through encode, persistence,
reload/materialization and strict audit. Missing, mismatched or incomplete
typed owner evidence remains fail-closed. Registry coverage fixes the supported
surface set at one adapter each, and candidate identity tests prove different
final hand owners cannot retain the same candidate identity.

## Performance and storage decision

The latest exact PR #6 Human session remains the current factual baseline:
`session-20260901T061040Z-561a204be0bc422da5809e1ec5c148aa`, artifact
`2382b3dd01be009731fdfa02a5f936986487163042a7b4614cc931c3bf6a4f8e /
b1a7d1f1-6f38-4501-a1ef-9a642d40df53`, runtime
`a00b1852fcd44c8b9c489233c78301c0`.

- read-rich Snapshot: 136, mean 21.481 ms, p95 28.440 ms;
- discriminator Snapshot: 104, mean 20.028 ms, p95 24.887 ms;
- semantic Snapshot: 33, mean 22.438 ms, p95 36.440 ms;
- captured-frame projection: 118, mean 1.079 ms;
- semantic-frame serialization: 223, mean 1.054 ms;
- close durable flush: 25.269 ms.

Exact trace classification shows the 104 discriminator captures are distinct
execution/resume boundaries (93 before execution and 11 resume), not duplicate
modern-root captures. Canonical Map boundaries already delegate rather than
recapture at the same boundary. Strict-V2 ordinary-combat compatibility still
uses this evidence. Therefore no capture, storage-format or caching rewrite is
justified here. The session has 334 files and about 10.5 MiB allocated; 409 read
references resolve to 100 session-local blobs. Deferred work, owners, triggers
and deadlines are in the [debt register](../PREFULLRUN_DEFERRED_DEBT.md).

Power conditions were not controlled. These numbers are a baseline, not a
regression or owner-perceived-latency claim, and they do not transfer to the new
artifact below.

## Verification and exact candidate

- root portable `npm run check`: PASS;
- Annotator Core: 122/122;
- Connector Host: 172/172; TypeScript SDK: 7/7;
- Host Runtime: 163 pass, 1 skipped; Game Mod boundary: 45/45;
- exact-game build: PASS with zero compile warnings/errors;
- predecessor session read-only audit under current tooling: strict V2 93 valid,
  0 invalid; modern calibration 25/25 canonical, 0 successor unresolved.

New clean candidate:

- workspace build revision `f1a31b90d14c3b8d753fea2e8ccfdf265af801ec`;
- unified artifact `734098f8458e7369b4e1eb6013b7516fa0c5dc126621aad11109196dd3a8bf2f /
  889b7a2e-2eaf-47e5-9383-7ddc406eb9b7`;
- Connector build `b8754d1e76268543ecd41ff4b8c885407471b125affdbf1a5f70eee8a86444ae /
  df06571d-1a87-48ce-855b-0b4a49350e0c`;
- Annotator build `21d16714fac1b451388ef13799d0d4ba2fd8bb24e706cc789cc85cacead2b04a /
  e41f494b-0d9f-4b9a-9562-e46d42652d05`;
- loaded runtime `97943a5ec5164da389d244a279bb4ab7`;
- environment `c0d853c8d82980dd04b1e5ba6baa97f2deb622a780755cafe28fe6d3245b9b7d`;
- exact sole-Platform Modset
  `3f9a379fc61497d3618350921c5b13c2e6845089a4528e18ada832ea4625cbbc`;
- rollback `apps/game-mod/.local/deployments/2026-09-01T08-16-00.279Z`.

Recorder loaded Ready with no open session. Loaded evidence is not Human action
evidence. PR #6 Human evidence proves its own predecessor artifact and verifies
backward-compatible reading only; it does not qualify these bytes.

## Remaining gate and non-claims

The shortest exact-candidate Human canary is:

1. start one recording;
2. exercise one ordinary supported root and one Map travel into genuinely ready
   Combat without taking a subsequent Human action;
3. close the Recorder;
4. require strict V2 PASS, modern rows canonical, typed owner-ready durable after
   reload, zero unresolved and no close-drain timeout;
5. inspect the published selector/action catalog when naturally encountered and
   require candidate identity to include the exact current owner before any
   Connector delivery claim.

This source does not claim new Full-Run domain coverage, improved runtime
latency, storage-scale qualification, Human origin machine proof, or Human
qualification of the new artifact.
