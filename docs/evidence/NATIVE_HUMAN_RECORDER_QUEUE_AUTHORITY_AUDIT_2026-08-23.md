# Native Human Recorder Queue-Authority Audit

This audit records predecessor runtime evidence and the resulting source
correction. It does not transfer Live authority to the corrected artifact.

## Exact Predecessor Identity

- Connector source: `9a929cc1ce0d4eed7c906087c29c03da0e1f7048`;
- Connector source digest:
  `0458e8edf228458869c81d0bc9b538119e3f5c43063a26ecb982ecd77cdb5685`;
- Connector artifact SHA-256:
  `048b3426bdb26aa5db9d97928a3f9a8700cf2690eac31ae9203d9d9755200349`;
- Connector MVID: `10d4a612-6f83-442e-95f1-094889e4e045`;
- Player Environment protocol: `1.0.0`;
- recorder source: `bc9c568a088897bce0413ac4197fbda988e8d960`;
- recorder artifact SHA-256:
  `009a9e7604d87e91e004afa13459b6f2c5b584a6651801e7c0ad2eadf3943efd`;
- recorder MVID: `9bda84a1-37b7-4fb5-9725-bec2c292d860`;
- runtime instance: `22f56c5893ee4ee899148f7aa6452b34`;
- Modset fingerprint:
  `60a38523af9ee047854fe42387e84ad9fcc477f3045ee7b75bcde45f5ea742bc`;
- exact game: macOS arm64 `v0.111.0/41cef1ea`, assembly SHA-256
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
  MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

## Native-Human Result

The local append-only session
`session-20260822T161651Z-0b19b798e65b4c4a965ac3e78a06ede2`
contains 60 admitted ordinary-combat records:

- 51 `play`, of which 30 bind an exact target;
- 9 `end_turn`;
- 60 `exact_unique` mappings;
- 60 complete interactive successor transitions whose post-Snapshot differs
  from the recorded pre-Snapshot;
- catalogs ranging from 1 to 13 BoundActions;
- strict export of all 60 records and STPD B0 acceptance of all 60.

There were also 22 `pre_frame_capture_failed` invalidations: 17 accepted
`PlayCardAction` roots and 5 accepted `EndPlayerTurnAction` roots. Chronology
shows each occurred after one native action was accepted but before its
successor settled. The native action queue accepted every root; these were not
rejected clicks, stale actions or unknown deliveries.

## Root Cause And Correction

Exact-build inspection confirms that `NPlayerHand.CanPlayCards` delegates to
hand-local actionability guards and does not require an empty game action queue
or the absence of an executing card/potion effect. `NCardPlayQueue` removes an
accepted card holder from `NPlayerHand.ActiveHolders` when it enters the play
queue. `NEndTurnButton` independently tracks whether the real control is
enabled.

The predecessor Host instead required both an empty action queue and no active
card/potion effect before publishing any combat action. It therefore reported
settling while the shipped UI still accepted human input. Current source:

- derives the visible hand and card candidates from active native holders;
- uses native hand-local gates rather than queue emptiness;
- publishes End Turn only while the current native button is enabled;
- rechecks the same holder/control state immediately before delivery.

## Evidence Boundary

The 60 records prove the predecessor witness, root filtering, exact mapping,
successor, export and STPD import paths. They also provide direct evidence for
the queue-authority defect. They do not prove the corrected source is built,
installed, loaded or Live-correct. A new cold-loaded artifact and rapid-input
native-human run are required before that claim can close.
