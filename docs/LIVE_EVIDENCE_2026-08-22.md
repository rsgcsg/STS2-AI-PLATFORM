# Predecessor Live Evidence: 2026-08-22

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

The two systematic Recorder defects are fixed in the successor source by using
the latest same-interaction authoritative frame and admitting one expected root
action per native UI scope. This evidence belongs only to the exact predecessor
artifact above.

## Non-Claims

- no `play` record from this artifact is admitted;
- owner operation is reported context, not independently machine-proven origin;
- the run does not qualify duplicate-card, targeted-card, potion, selector, or
  non-Combat families;
- normal owner use found no reported gameplay interruption, but this is not a
  controlled non-interference proof;
- successor source, build, install, loaded identity, and Live behavior require
  their own evidence.
