# PR25 two-session Human audit and reward-owner repair — 2026-09-11

Base develop `cd9a0fbb0c85577a13513abe715f91d794ac86eb`; audited PR head
`f3a64526048a0052a4937e2c06d59198fce5b2e1`. This is a G5 owning Annotator
boundary repair plus a portable calibration correction. PR25 remains Draft;
STPD is unchanged. The owner reports real Human play, not a machine-origin claim.

## Audited identity and outcome

Both sessions used Platform 0.2.0-rc.2, Annotator 0.3.0-rc.2, native Annotator
source `5752e27c5c71b0345708a02c80fe3d82667520bf`, DLL SHA-256
`94ff3849e87954efa86f824876e334cb43c130c62227c63e2c7682dad7f928f8`, MVID
`49525b2e-bf11-4117-bae6-fb8fc23861b1`, runtime
`d7cfd290292343ffa6069214789ab24f`, game `v0.111.0 / 41cef1ea` and game DLL
SHA-256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.

| Session | Accepted | Proved/canonical | Cancel | Pre-Commit abort | Unresolved | Diagnostics |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `session-20260911T111847Z-091bcc6d49174db0ab3c06ecfc1a1a25` | 456 | 456 | 0 | 0 | 0 | 67 |
| `session-20260911T113641Z-557442be069043b9b2159731d3d280b4` | 759 | 755 | 2 | 1 | 1 | 128 |
| Total | 1215 | 1211 | 2 | 1 | 1 | 195 |

Excluding three native non-successes, 1211/1212 (99.9175%) observed decisions
are canonical. Both formal structural audits pass, with 133/207 compatible
records and zero invalid compatible records. Those legacy record counts are not
the Full-Run denominator. Both close-capability-1 receipts are present and valid.
Native diagnostics have 847 accepted, 844 successful exact-once memberships,
two cancellations, one abort, zero unknown and 27 paired choice pause/resumes.
195 diagnostic invalidations are exactly: 75 MoveToMapCoordAction and 119
ReadyToBeginEnemyTurnAction `human_action_native_type_mismatch`, plus one
`native_completion_no_match` following the event's unresolved disposition.
None is itself an extra failed Human decision.

The first session has a fresh run ending in native defeat (169 canonical), then
a fresh run interrupted without native terminal (124), explicitly resumed as
another observed run ID and ending in native defeat (163). Do not concatenate
those latter fragments into an uninterrupted run. The second session has one
fresh native start and native victory, with no Recorder pause or run resume;
it meets the uninterrupted lifecycle shape but contains one real recording
failure, so it is not an all-valid Full-Run PASS.

There are 76/59 canonical children. All first-session parents are canonical;
second-session child #576 has the exact accepted parent #574, which is unresolved.
The child keeps its independently proved evidence without inventing a parent
canonical row. Knowledge Demon choices #461/#479 have exact independent
BlockingPlayerChoiceContext origins, complete selectable cards and canonical
transitions. The prior unexercised independent-choice gap is covered on these
bytes. Four combat-pile selectors and generated-potion choices are also recorded.
607 successful PlayCard decisions are canonical; the other three PlayCard
outcomes are native cancel/abort, not evidence loss. Fruit Juice and a timed
long-aim duration are not established by this pair.

## Confirmed owning defects and repair

1. **Event reward handoff observed too late.** Second session #574, event option
   洗劫, awaited its exact reward screen. The opening patch registered lineage
   but did not observe the input-owner boundary. Independent potion discard #575
   then changed state. Only reward claim #576 triggered the parent handoff,
   correctly producing `intervening_human_action_before_boundary` for #574.
   #575 itself is canonical. The first missing fact belongs to
   `NativeRewardDecisionLineage.cs`'s ShowScreen observer, not potion ownership
   or the tracker fail-closed rule.

   Exact native ShowScreen calls NOverlayStack.Push; Push establishes the screen,
   AfterOverlayOpened/Shown and ActiveScreenContext.Update before returning.
   The patch now observes that exact ready reward owner after shared native
   owner registration. A synchronous factory whose Human parent is admitted
   only on outer callback return is handled on that same return seam, requiring
   the registered parent to equal the accepted callback. Top owner, native
   active context, session, full frame/Reads and tracker lineage are checked.
   Both paths use the existing tracker and exact input-owner continuation.
   No timer, later-frame repair, invented potion child or second tracker is added.

2. **Calibration rejected a valid predecessor of an aborted card.** #314 was
   durably proved/canonical at #315's complete execution boundary; #315 then
   aborted before native Commit. C# audit preserved that boundary correctly,
   but JS calibration special-cased cancellation only and reported two
   unresolved instead of one. Its shared non-committing-execution check now
   admits native cancellation or pre-Commit abort while requiring exact state
   refs, ordering, completeness and no success row for the rejected action.
   Recalibration gives 755 candidates/canonical, three rejected, one unresolved.
   This fixes a report false negative, not the original recording.

## Validation and next candidate

Focused regressions cover early reward opening versus a too-late handoff after
independent discard, immutable parent/root lineage and final causal validation;
source wiring guards cover exact owner/session/native context and synchronous
callback routing. Calibration tests include both proof kinds and both native
non-success dispositions, with missing/wrong/incomplete boundary counterexamples.
Full gates, exact package identity and load receipts are recorded at final closeout.
New candidate versions are Annotator 0.3.0-rc.3 / Platform 0.2.0-rc.3.

Raw sessions and logs are retained outside Git. Re-audit and bundles preserve
original bytes, including #574 unresolved. New source/DLL receives no transferred
Human qualification. Next bounded Human canary: event-generated rewards with
full potion belt, discard before claim, then claim/Proceed; also a generated
selector and rapid play/cancel. A fresh uninterrupted native-start-to-terminal
run on the new candidate is still required before all-valid Full-Run/merge.

## Canonical family inventory

| Family | 111847 | 113641 |
| --- | ---: | ---: |
| `act_change.ready` | 1 | 3 |
| `combat_hand_selector` | 42 | 6 |
| `event_option.choose` | 15 | 20 |
| `event_option.nested_selector` | 6 | 5 |
| `generic_combat_pile_selector` | 4 | 0 |
| `map_navigation.travel` | 28 | 47 |
| `native_generated_card_choice` | 2 | 2 |
| `ordinary_combat.end_turn` | 57 | 89 |
| `ordinary_combat.play_card` | 201 | 406 |
| `ordinary_combat.use_potion` | 4 | 7 |
| `potion_belt.discard` | 0 | 1 |
| `rest_site.choose` | 3 | 12 |
| `rest_site.nested_selector` | 6 | 22 |
| `rest_site.proceed` | 3 | 10 |
| `reward_claim.claim` | 41 | 54 |
| `reward_claim.proceed` | 14 | 19 |
| `reward_nested.replacement_selection` | 16 | 21 |
| `shop_inventory.card_removal` | 0 | 2 |
| `shop_inventory.card_removal_nested_selector` | 0 | 4 |
| `shop_inventory.close` | 3 | 3 |
| `shop_inventory.purchase` | 2 | 4 |
| `shop_room.open` | 3 | 3 |
| `shop_room.proceed` | 2 | 3 |
| `treasure_room.open` | 1 | 4 |
| `treasure_room.proceed` | 1 | 4 |
| `treasure_room.select` | 1 | 4 |
