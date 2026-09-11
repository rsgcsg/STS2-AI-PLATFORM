# PR25 rc.3 two-session audit and repair — 2026-09-11

PR25 remains Draft: the first recording has a structural disposition conflict;
the second has zero true recording failures but begins in progress. These are
not an uninterrupted all-valid native-start-to-terminal Full Run. Old raw bytes
are preserved, including the first session's audit failure.

## Exact Human identity

Repository `rsgcsg/STS2-AI-PLATFORM`, base `develop` at
`cd9a0fbb0c85577a13513abe715f91d794ac86eb`; audited PR head
`7e0980728568bdabe8f8f6db8ee5ea0da833e694`.

Both owner-attested Human sessions used Platform 0.2.0-rc.3 / Annotator 0.3.0-rc.3:

- `session-20260911T123026Z-33157d2d0f1c4676bcb1c487e38d71d8`
- `session-20260911T130445Z-827762453ada4b81a060206b6c24e903`

Native Annotator source `f915fa4865fe244bf1c979ea18519af580b63e90`, build workspace
`a779f0804c279ef299eb45f8cbed903f06a093ed`, DLL SHA256
`571b4e22fd8cd20aca30da8cdc80c813a3aca8577fc1fce243cd20e27333554a`,
MVID `513336da-b4a4-4625-ac71-2e54a5f3bd49`, runtime
`9676ee98e6034844a4bba712700c3f75`. Game v0.111.0 / `41cef1ea`, game DLL
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
Environment fingerprint `f36e1499795d99fa2fcb28f1ae7e4d021a0ba28fede99a3bba2606409785553a`;
Modset `6aaddca350438aebfe0c4ceab3590486c8e7d21dc5a636e4dc5779863a1dd4bc`.
The identity is independently retained in durable decision environments.

## Audit results

| Fact | 12:30 session | 13:04 session |
|---|---:|---:|
| Accepted decisions | 80 | 336 |
| Proved / durable canonical | 72 / 72 | 335 / 335 |
| Unknown events | 5 | 0 |
| Native cancellation events | 5 | 1 |
| Unknown/cancellation overlap | 2 | 0 |
| Failed-closed occurrence outside admitted roots | 1 | 0 |
| Explicit invalidations | 12 | 50 |
| Compatibility valid / invalid | 16 / 0 | 100 / 0 |
| Canonical children / canonical parents | 3 / 3 | 27 / 27 |
| Native diagnostic accepted / successful / cancelled | 62 / 57 / 5 | 233 / 232 / 1 |
| Diagnostic membership unknown | 0 | 0 |
| Native starts / ends | 1 / 0 | 0 / 1 |
| Closed receipt | yes | yes |
| Formal session audit | FAIL | PASS |

The first audit rejects `semantic_action_has_multiple_dispositions` and
`semantic_action_disposition_not_exactly_one`; both error categories are present.
It is not valid to sum its overlapping dispositions or advertise a clean valid
percentage. The second records every successfully executed admitted decision:
335/335 (100%); 335/336 accepted (99.70%) includes one ordinary native cancel.
No pause/resume/reload can fill its missing beginning. Its terminal is native
defeat (`RunManager.OnEnded(isVictory=false)`); the first closes mid-run.

All invalidations are accounted for:

| Reason / native action / disposition | First | Second |
|---|---:|---:|
| human_action_native_type_mismatch / MoveToMapCoordAction / diagnostic | 4 | 28 |
| human_action_native_type_mismatch / ReadyToBeginEnemyTurnAction / diagnostic | 7 | 22 |
| pre_frame_capture_failed / PlayCardAction / failed_closed | 1 | 0 |

The 61 internal diagnostics are not lost Human decisions or real failures. The
capture failure is in scope and cannot be relabelled diagnostic.

All five unknowns in the first session:

- #30 reward Proceed: `successor_not_different` when the next reward input
  returns to the same rewards Snapshot after opening/closing the map.
- #73 lethal PlayCard and queued #74/#75: each receives
  `unrecorded_human_effect_before_successor` from the failed capture.
- #80 reward Proceed: `session_closed_before_successor_boundary`.

#74/#75 subsequently receive `action_cancelled_before_start`, producing the
structural conflict. The unadmitted fourth input has no retained native terminal
subscription: its eventual disposition is **unknown**, not inferred cancellation.

## First incorrect facts and owning repairs

### Exact staged H was discarded during public settling

`RecorderRuntime.StageCardPlay` captured H inside the exact native
`NPlayerHand.StartCardPlay` invocation and bound it to the returned Mouse/Controller
CardPlay. At release, `TryEnterScope` required that historical H to have had a
public interactive catalog. Evidence reports combat=true / phase=Play at stage,
but combat=false / phase=None at release. STS2 enqueued another native input
while combat ended; the recorder discarded its exact H and erected an
unrecorded-effect barrier. This affected one failed capture and three unknowns.

The fix retains a state/Read/identity-complete staged combat H even when the public
catalog is settling, with exact owner/generation/card and scoped operand matching.
This is native-input evidence only. STS2 still owns acceptance; execution supplies
S + A(S); a cancellation never gets a successful transition. No later capture,
old execution state or legality reconstruction supplies missing evidence.

### A late native terminal wrote a second disposition

`SemanticBoundaryTracker.Cancelled` and `AbortedBeforeCommit` unconditionally
replaced tracker state after an earlier durable disposition. In the session,
unknown events 433/434 are followed by cancellation events 435/436. The repair
retains native terminal bookkeeping for cleanup but emits no second disposition
and does not restore an old current state. Durable unknown stays unknown.
A regression exercises both late cancel and late pre-Commit abort through the
production final semantic auditor, plus ordinary queued cancellation preserving
the lethal predecessor.

### Synchronous Proceed lost the map-owner boundary

Exact game source shows `ProceedFromTerminalRewardsScreen` synchronously calls
`NMapScreen.Open` for ordinary terminal rewards. Open sets IsOpen/Visible,
recalculates travelability and updates ActiveScreenContext before returning.
The recorder previously transported only Task completion through the frame loop;
it did not observe that native input-owner boundary. Returning to rewards or
closing Recorder then left #30/#80 unresolved despite successful native Proceed.

The fix uses the existing exact reward/treasure owner and matching Human scope to
record synchronous native Commit, then the shared Native Foundation owner-ready
provider publishes the exact newly opened map owner. Connector must independently
capture a complete map frame at that same seam. Already-open map, wrong owner,
asynchronous parent-event resume or incomplete state cannot use this path.
Commit and successor remain separate. Regression covers returning to the same
rewards state after the native opening and proves the old omission fails closed.

Source locations: `RecorderRuntime.cs` StageCardPlay/TryEnterScope;
`SemanticBoundaryTrace.cs` Cancelled/AbortedBeforeCommit;
`NativeUiPatches.cs` NativeTreasureProceedCompletionPatch;
`NativeDecisionOwnerReadyProvider.cs` ObserveMapProceedReady.

## Coverage and remaining non-claims

The second session records 158 successful PlayCard, 40 EndTurn, 3 potion uses,
28 map choices, reward/alternate reward, event, treasure, shop/removal, rest
upgrade/cancel/proceed and act-ready decisions. All 30 canonical children across
the pair have canonical parents. No parent is reconstructed from proximity.
The second has two genuine native PlayerChoice pauses and resumes.

This pair does not independently establish the previous event-reward/full-belt
potion discard repair, Fruit Juice at any surface, every selector/pile family,
long aiming duration, non-interference, research eligibility or STPD training.
STPD remains unchanged at `67566db562b95f57468290b97659c9f2e3862fe1`.

## Validation and next Human gate

Change class G5. Private receipts and raw hash inventories are under
`.local/pr25-rc3-two-run-audit/`; raw sessions remain outside Git. Commands:
`annotator.mjs audit`, `audit-native-semantic`,
`calibrate-semantic-training.mjs`, component tests, root check, exact-game,
project closeout and diff review. The first session must continue to fail its
original structural audit and must not be exported as a clean bundle.

The rc.4 candidate changes no public schema or policy/training contract. Build,
load and latest-head CI identities are recorded in the final candidate BOM and
closeout below. No rc.3 evidence qualifies the repaired artifact.

The next bounded Human gate is:

1. Rapidly queue cards while the last enemy dies; remaining queued cancellations
   must have one disposition, and the successful lethal play must retain S'.
2. Proceed from rewards, close/reopen the map, and close Recorder at the map;
   also exercise treasure Proceed. Native opening must be a durable successor.
3. Event reward with full belt: discard before claim; selector/rest paths and
   out-of-combat Fruit Juice use remain exact independent/nested decisions.
4. Start recording before a new native launch, then finish one uninterrupted
   natural run to native victory or defeat before closing recording.

A missing or partial native boundary stays unknown. Any true failure or failed
structural audit blocks ready/merge; owner Human gameplay is never automated.
