# Live Evidence: 2026-08-22

## Staged-Card Artifact

- session: `session-20260822T165516Z-acdf97684d244f1e97b6088c342b64ce`;
- Connector source: `2a14504fffe2cf6fc21298dcb5e4084b9ac27ef1`;
- Connector artifact SHA-256:
  `f991eab77f846416468fcaaa014565912d7ee547ee7848cc46d24735c4ac2040`;
- Connector MVID: `5b35e8ad-4f52-4dae-a551-17efc06547fb`;
- Annotator source: `6d474ce93116bc0e2c3c6236c170da574b8f5d51`;
- Annotator artifact SHA-256:
  `5a61ebb2baab34f0b2eaa41a9aa4106fd070a541fba1aa0e7abdd20c007d8b9b`;
- Annotator MVID: `2234467d-fee7-4d83-9c24-db8f7e5ca658`;
- runtime instance: `3b6b160356d84310b860a2073dd4d1f3`;
- Modset fingerprint:
  `043689451b721fdda3791d8b6f519f03bcbe444a27119b4e81af73ef831852e8`.

The owner-operated run admitted 106 ordinary-combat transitions: 83 plays,
split into 35 targeted and 48 untargeted plays, plus 23 end turns. Every record
had a complete interactive pre-frame, an exact-unique process-local mapping, a
catalog of 1 through 18 BoundActions and a different complete interactive
successor. Successors covered combat turns, combat-hand card selection and
reward claim. Independent audit accepted 106 and rejected zero. Deterministic
export SHA-256 is
`05c52ad223b6e8e19f1fe300de63edb9c9090503ff255747a8537fefa64c5f10`;
strict STPD import accepted all 106, rejected zero and passed B0 as manifest
`dataset-8af38f14ec1a7611` with one exact environment.

Five native card starts had no complete interactive S and failed closed. A
sixth action followed one of those unstable starts and reached `mapping_zero`:
same-card staging was unavailable, but the intermediate generic latest-frame
fallback still admitted an older same-interaction catalog. The current
successor source deletes that fallback. This run proves staged-card timing and
the downstream path, but is predecessor evidence for the fallback deletion.

## Queue-Aware Combat Artifact

- session: `session-20260822T164124Z-abefd960a8144166b5d890cb4dfd7c61`;
- Connector source: `2a14504fffe2cf6fc21298dcb5e4084b9ac27ef1`;
- Connector source digest:
  `d6faf7f01844786896a9db75d80b97caf35b0cf99049d4c200a3ab59e85ac6df`;
- Connector artifact SHA-256:
  `f991eab77f846416468fcaaa014565912d7ee547ee7848cc46d24735c4ac2040`;
- Connector MVID: `5b35e8ad-4f52-4dae-a551-17efc06547fb`;
- Annotator source: `bc9c568a088897bce0413ac4197fbda988e8d960`;
- Annotator artifact SHA-256:
  `4ad0c4323258535b27218ffe1d0d61fb9ae553be936f5363d734451fbd765b90`;
- Annotator MVID: `58bbbbea-9b21-4392-9920-2bc99a62f9cb`;
- runtime instance: `52c21f811ebe43eeb990332c6eea8604`;
- Modset fingerprint:
  `dbd916a2445996bee127418d44d98b6cad60b4b9ea0e095b504825ca4c67fa1d`.

The owner-operated run produced 64 admitted ordinary-combat transitions: 51
plays, including 27 targeted plays, and 13 end turns. Every record had a
complete interactive pre-frame, exact-unique mapping and a different complete
interactive successor. Catalogs contained 2 through 16 BoundActions.
Independent audit accepted 64 and rejected zero. The deterministic export
SHA-256 is
`b9b1fda00a71d7839c67eae5a045ae9fe278de917950a0a5e6c6b507b07ce787`;
strict STPD import accepted all 64, rejected zero and passed B0 with one exact
environment.

Eight observations failed closed. Four occurred while no stable complete S was
available during a turn/selection handoff. Four were accepted card plays whose
holder had left the active hand before the old `TryPlayCard` freeze point,
causing `mapping_zero`. The successor source stages the exact frame at
`StartCardPlay`; the 64 records and eight invalidations remain predecessor
evidence for that Recorder change. They do prove the queue-aware Connector
correction reduced the earlier systematic queue misses.

## Earlier Predecessor Envelope

## Exact Envelope

- session: `session-20260822T155425Z-64bb5d5f271246c78b7d882403ee1b31`
- game: `v0.111.0`, commit `41cef1ea`
- game assembly SHA-256: `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`
- game MVID: `57785517-0b16-42b9-8b36-bad6fb28384b`
- Connector source: `9a929cc1ce0d4eed7c906087c29c03da0e1f7048`
- Connector artifact SHA-256: `048b3426bdb26aa5db9d97928a3f9a8700cf2690eac31ae9203d9d9755200349`
- Connector MVID: `10d4a612-6f83-442e-95f1-094889e4e045`
- Annotator source: `26725c17f96fbdc84d7be8d2133a03fbb37aa869`
- Annotator artifact SHA-256: `48f19497565969bc91b7a88c0c6363375baa1562264f458ebb630302ee2a34b6`
- Annotator MVID: `b9c99bb9-a4e2-4bb3-8dc8-f3acfc4fb059`
- Player Environment protocol: `1.0.0`
- runtime instance: `f86ab110b81e4f3bac4bbc30f3a42a53`
- Modset status: `canary_exact_observer_modset`
- Modset fingerprint: `601351403ef8dc43faca28aeb0450aa3536593b6cc8808060844a7c3e92158a7`

## Results

- owner-operated native run produced 25 admitted `end_turn` records;
- independent audit: 25 valid, 0 invalid;
- deterministic export: 25 records, SHA-256
  `b19387b5faf24df17be15d1abb42124a1078bac6ff1a4373592ed77fdd5d2ca0`;
- STPD strict import: 25 accepted, 0 rejected; B0 passed;
- 109 accepted `PlayCardAction` observations failed closed because the prefix
  captured a settling, non-authoritative frame;
- 24 `ReadyToBeginEnemyTurnAction` instances were incorrectly classified as
  unsupported while nested under the end-turn scope;
- four end-turn attempts also failed closed before an authoritative frame was
  available.

The intermediate successor used a latest same-interaction frame and admitted
one expected root action per native UI scope. Later exact-runtime evidence
showed that a generic latest-frame fallback could cross a turn boundary, so the
current source replaces it with same-card staging and otherwise fails closed.
This evidence belongs only to the exact predecessor artifact above.

## Non-Claims

- no `play` record from this artifact is admitted;
- owner operation is reported context, not independently machine-proven origin;
- the run does not qualify duplicate-card, targeted-card, potion, selector, or
  non-Combat families;
- normal owner use found no reported gameplay interruption, but this is not a
  controlled non-interference proof;
- successor source, build, install, loaded identity, and Live behavior require
  their own evidence.
