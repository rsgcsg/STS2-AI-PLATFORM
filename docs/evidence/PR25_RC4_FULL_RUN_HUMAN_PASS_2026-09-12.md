# PR25 rc.4 Full-Run Human PASS — 2026-09-12

The owner-operated rc.4 session passes the bounded Full-Run gate: two uninterrupted
native new-run starts through natural defeat, 524 accepted decisions, 524 proved
and durable canonical transitions, no unresolved decision or real capture failure,
and a valid durable close. This permits PR25 integration into develop. It is not
an exhaustive all-card/all-event/all-potion qualification or a stable main release.

## Exact identity and immutable evidence

Audited PR25 head `415c57c1d2612784c6b94cf59d5201eca2ef6113`, base develop
`cd9a0fbb0c85577a13513abe715f91d794ac86eb`, repository
`rsgcsg/STS2-AI-PLATFORM`. Session:
`session-20260911T142616Z-72910efa8a3b44b69136e08624dbc9ef`.

Platform 0.2.0-rc.4 / Annotator 0.3.0-rc.4, native Annotator source
`e65632c58879e3240cd0febbd247d088b534b0a6`, unified DLL SHA256
`af4dfd93a4236b50aadbff8e9c055a519b20e842c582ab392d7748845df40cc8`,
MVID `1d53eae8-3353-4802-88fa-0eb3538fa217`, runtime
`d29a8620f6f64c398211550b3f2194f6`. Build workspace
`ab4ee5303c8302ae209dd62c4d766a6764ebec51`; source-set comparison confirms the
final documentation/evidence closeout retains the same compiled native source.
Game v0.111.0 / `41cef1ea`, game DLL SHA256
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
Environment `65c4eede4ea5455feef42853bc72c5ef13bfe30e0e85f385c81611bc4bc36f9b`;
Modset `e678bfdb6d545a2fbb9020906cc847e384c5c02040d7adde52aa661acdd3cc96`.

All 1895 original files were hashed before audit and remain byte-for-byte
unchanged after audit/packing/verification. Private receipts are under
`.local/pr25-rc4-final-human/`. No source fix, gameplay input, deployment,
recording edit or historical-evidence promotion was performed in this round.

## Run and disposition audit

| Run | Native start UTC | Natural terminal UTC | Canonical |
|---|---|---|---:|
| run-0001 | 2026-09-11 14:26:23.708891 | 14:30:17.922750, defeat | 161 |
| run-0002 | 2026-09-11 14:30:49.466253 | 14:40:36.317322, defeat | 363 |

Both starts carry exact new-run setup provenance, not resume provenance. Initial
RunState observation followed by native Launch shares the correct run identity;
it does not suppress the native start. The journal contains no Recorder pause,
resume, run reload, abandoned/unproved terminal, close gap or unexplained run
boundary. Defeat is a legitimate natural terminal; completing every act is not
a prerequisite for recording the whole native run.

| Measurement | Result |
|---|---:|
| Accepted / started / proved / canonical | 524 / 524 / 524 / 524 |
| Unresolved / cancelled / aborted | 0 / 0 / 0 |
| Real failed-closed / unexplained lost input within observed scope | 0 / 0 |
| Compatible decision records valid / invalid | 150 / 0 |
| Canonical children / canonical parents | 53 / 53 |
| Native diagnostic accepted / exact / unknown | 390 / 390 / 0 |
| Native PlayerChoice pauses / resumes | 18 / 18 |
| Explicit invalidations | 64, all diagnostic |
| Native starts / native ends | 2 / 2 |
| Close receipt and formal audit | PASS |

All 64 invalidations have reason `human_action_native_type_mismatch` and
`disposition=diagnostic`: 27 MoveToMapCoordAction and 37
ReadyToBeginEnemyTurnAction. They are internal native work, not failed Human
decisions. There are no additional failure buckets.

`audit`, `audit-native-semantic` and semantic calibration all pass. Calibration
independently classifies all 524 decisions as complete S + A(S) -> A -> S',
with zero missing durable canonical rows and 47 exact rapid execution handoffs.
The formal audit JSON SHA256 is
`668d12ab22589995c54139db04dc982c366286f6b9f601d63d7364e54a30d5c9`.
The older compatibility denominator is not the Full-Run denominator.

## Observed coverage and repair verification

Canonical families: 287 PlayCard, 68 EndTurn, 7 UsePotion, 27 map choices,
42 reward claims, 19 reward selections/alternatives, 14 reward proceeds,
16 event choices, 4 event selector decisions, 12 hand selector decisions,
8 generated choices, 3 combat-pile selector decisions, 3 shop purchases,
2 each shop open/close/proceed, 2 each rest choice/proceed, 1 each treasure
open/select/proceed and 1 act-ready decision.

All 53 children have exact durable parent/root lineage and canonical parents;
formal audit and independent bundle verification bind their state, action-space
and Read references. Combat-pile children #422/#456 are discard-pile choices;
#428 is a draw-pile choice. Selected cards and pile provenance are durable.
Children are first-class decisions, never invented GameAction roots.

Fourteen proofs carry the repaired
`RunManager.ProceedFromTerminalRewardsScreen->NMapScreen.Open.return` boundary.
Two carry `NRewardsScreen.ShowScreen->native_input_owner_handoff`. Both natural
terminals have the exact game-over intro owner-ready proof. Rapid execution,
selectors and potion use show no causal loss or authority regression in this
session. No late cancel/abort occurred here: those negative lifecycle cases
remain covered by regression tests, not falsely claimed as freshly Human-tested.

## Independent transfer and qualification limits

Canonical-first bundle3 content ID:
`80a76a5a3eefc41f6bb8845deba8fc970a9933fc6d07281398d5b44b7363ad7e`.
The independent Python Evidence verifier reports PASS, 524 canonical records,
zero findings. Human origin is owner-attested, not machine-proven. Packing does
not change the recorded artifact identity or grant research admission.

Fruit Juice, full-belt discard-before-event-claim, every rare selector/card/event,
long aiming, crashes/disk loss and all external modifications were not exhaustively
exercised here. No hidden-input completeness theorem or blanket non-interference
claim follows from the observed zero-loss result. Prior failed sessions retain
their original failure statuses. STPD is unchanged at
`67566db562b95f57468290b97659c9f2e3862fe1`; its adapters/admission/training remain
external responsibilities.

## Integration and support

This closeout changes evidence/docs/BOM only. The native candidate retains its
existing clean exact-game/build/install/load evidence; no rebuild or cold load
is needed to restate unchanged bytes. Full root/project/BOM checks and exact-head
hosted Linux/Windows/portable CI qualify the final source head separately.
Integrate PR25 by normal merge into develop to preserve path-scoped provenance;
verify the actual merge head and its CI before declaring engineering closeout.
Main remains the governed release line, not automatically promoted by this PR.

After integration, merged topic branches may be deleted. Non-ancestor safety
and old workstream tips must first be retained as explicit archive tags. Existing
worktree files, especially raw evidence and uncommitted work, must not be removed
as part of branch cleanup. Archive tags are recoverability refs, not releases.

Distribution and future incidents follow [collection/support](../ANNOTATOR_COLLECTION.md):
retain exact version/runtime/close/audit identity, immutable raw sessions and
private verified bundles; quarantine failures and reproduce at the first owning
fact; fix plus regression on a new topic PR; never overwrite historical evidence
or silently promote data into training. Rollback remains
`apps/game-mod/.local/deployments/2026-09-11T13-34-00.772Z` through Game Mod lifecycle.

`PR25_FULL_RUN_HUMAN_PASS` is bounded to this exact recorded artifact and observed
native runs. The current repository and exact evidence override historical notes.
