# Schema-3 Human And Data-Lifecycle Closeout - 2026-08-29

## Exact Human Runtime

Latest closed owner session:

- session `session-20260829T052157Z-e549d3601e7640f997b6f475180b2dfe`;
- timeline `timeline-53a417ad759941c99a6ba9e138115453`;
- STS2 `v0.111.0 / 41cef1ea`;
- game assembly `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4 /
  57785517-0b16-42b9-8b36-bad6fb28384b`;
- Connector/Annotator source `54efe38d6d2f49051e04248072acb548feddfe9a`;
- Connector source digest `6ee81df970fb4e1a06a26e4895fcb95d5bb4886eea9a01043859d0d5f4b8f73e`;
- Annotator source digest `a601e6d9ee85c54fbf1841535dc11c59e7d224a20f0d6b003493a7e1b53aa622`;
- unified artifact `4fa6757045b6d5c2b137e78b1e96e7163c2a5c64372a41955682257d6a6a1056 /
  51c7c37b-3305-4286-b2bc-52cd5725ac76`;
- runtime `7bcc19e7fb614eedad563db93310adc7`;
- environment `15177b88c13f87fac1c4b676aee2529a643411952eeda50b82ca67837be1f15f`;
- exact Platform Modset
  `2263e3958c03544a5a43ed462be1f85406a9a1c0fba8bf981a0c4c69fe54b544`;
- Player Environment protocol `1.0.0`.

## Semantic Result

Independent audit passes 188 Decision V2 records, zero invalid records and 87 explicit
legacy invalidations. The normalized semantic trace contains 333 accepted, 333 started,
333 finished and 333 proved actions, with zero unknown, cancellation, abort or unresolved
action. Each accepted action has one disposition.

| Native action | Count |
|---|---:|
| `PlayCardAction` | 214 |
| `EndPlayerTurnAction` | 48 |
| `VoteForMapCoordAction` | 24 |
| `NRewardButton.OnRelease` | 20 |
| `NRewardsScreen.OnProceedButtonPressed` | 10 |
| `NPlayerHand.OnSelectModeConfirmButtonPressed` | 8 |
| `UsePotionAction` | 3 |
| `NChooseACardSelectionScreen.SelectHolder` | 3 |
| `NCardRewardSelectionScreen.SelectCard` | 3 |

Proof boundaries are 301 interactive decision, 20 execution handoff and 12 player-choice
boundaries. Audit retains `H != S`, execution-bound S, causal S' and no-cross-Human-effect
checks. This is bounded Human proof for encountered combat/reward/map/selector mechanisms,
not exhaustive Full Run qualification.

## Storage And Interaction Result

The 864.324-second session contains 1,247 files and 32,463,533 measured bytes:

- semantic event trace: 2,590,104 bytes;
- 947 unique content-addressed semantic frames: 16,029,783 bytes;
- 2,724 role references;
- 1,783 materialized Reads / 292 unique Read blobs / 1,224,674 blob bytes;
- 5.354 persisted Reads per accepted action;
- Decision V2: 6,892,784 bytes;
- native ledger: 5,329,806 bytes;
- RunJournal: 344,482 bytes.

The event+frame graph is 18,619,887 bytes and compresses to 1,280,283 bytes in the analyzer's
gzip control. Schema 3 has no repeated inline frames in the event log. This validates the
normalized representation and the reduction in Read capture from predecessor rates of
23.97-28.63 Reads/action. It does not prove perceived lag is eliminated, because the loaded
artifact did not contain stage timing instrumentation.

## Follow-Up Source Boundary

Current topic source adds bounded `performance-profile.json` output only during Close. It
measures snapshot probes, Read-rich/semantic capture, serialization/hash/object writes,
durable appends and close flush. The profile is operational diagnostic evidence only and
cannot change action publication, native execution, semantic proof or Human records.

That profiler has source/test evidence only. A new artifact must be cold-loaded before its
timings can be attributed to runtime. The Human result remains bound only to artifact
`4fa67570... / 51c7c37b...`.

## Non-Claims

- no target-picker cancel, generated skip, hand select/replace/deselect, room-internal
  event/shop/rest/treasure action or run-entry proof;
- no exhaustive content or Full Run qualification;
- no stage latency, GC/allocation or rendering attribution from the new profiler;
- no claim that storage bytes alone explain all perceived lag;
- no STPD corpus admission, model improvement or training authorization follows from this run.
