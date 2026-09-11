# PR25 potion ingress and causal barrier repair

G5 follow-up from exact head `1d6aaf69b5e705e810c6cbcbe09091409139fdf3`,
base `develop@cd9a0fbb0c85577a13513abe715f91d794ac86eb`. Runtime receipts
and latest-head CI are recorded separately; no Human qualification transfers.

## First incorrect facts

The closed schema-2 Human session created at 2026-09-10T10:49:32Z had 358
admitted decisions (299 roots, 59 children), 356 proved/canonical, one native
cancellation and one terminal unresolved EndTurn. Legacy audit passed 121/0,
with 48 invalidations: 39 internal native mismatches, six failed pre-frames and
three selector inputs missing a recorded Human parent. Native diagnostics had
235 exact memberships and 16 successful unknowns. The journal distinguished one
fresh start from one saved resume and recorded two native ends.

The same runtime log recorded a BLOCK_POTION discard before canonical #111.
Its map-action pre-state held the potion and successor did not, while no
Human discard occurrence or invalidation existed. Exact native
NPotionPopup.OnDiscardButtonPressed enqueues DiscardPotionGameAction in combat
and noncombat; the observer incorrectly required a rewards overlay. Structural
S-prime/S equality did not detect this missing external Human effect.

## Changes and ownership

Connector now observes the exact visible potion popup independently of the
underlying room or overlay. The finite catalog is derived from the actual
native enabled Use/Discard buttons and native Close control. Delivery rechecks
popup, potion, belt slot and button state. Multiple visible owners fail closed;
no reconstructed potion rules, native-slot API or consumer operands are added.
The new `potion_popup` surface exposes visible potion identity, label and slot.
Combat use retains the complete same-state target set from Native Foundation;
it does not reduce an armed potion to an uncorrelatable popup-click-only action.
The existing native use delivery path revalidates each exact target.

Annotator observes discard on every native popup using the same exact enqueue
carrier. It waits for the native ExecuteAction Task and distinguishes native
cancellation instead of claiming Commit at method entry. The new capture
profile `human-full-run-read-rich-v4` declares `potion_belt.discard`; historical
profiles remain immutable. The public popup input is an activation, while the
recorded decision family identifies discard without implying reward acquisition.

An accepted Human input that fails pre-frame or selector admission creates a
durable causal barrier. Pending transitions become explicit unknown before a
later frame can absorb the missing effect. The tracker mutation uses the same
transaction/append path, with rollback and trace shutdown on failed durability.
This fixes false attribution; it does not manufacture missing pre-states.

A selector with an exact PlayCardAction currently GatheringPlayerChoice can be
recorded as native-origin when no admitted Human parent exists, using the actual
action and factory context. Existing exact Human-parent binding wins. Waiting,
finished and unrelated actions remain fail-closed. Native-origin does not assert
that the original native action was automatic; it says no Human parent was
established. No fake parent or new GameAction is created.

UI carries failed occurrence/native type through application events. Internal
ReadyToBeginEnemyTurn/MoveToMapCoord mismatches are labeled Diagnostic, failed
Human capture retains occurrence and mechanism, and typed roots are labeled Root.
Unknown BoundAction/subject IDs remain unknown rather than being fabricated.

## Validation and remaining non-claims

Regression coverage checks the unrecorded-effect barrier and rollback, native
popup catalog enablement and exact surface adapter, native task/cancellation
seam, and diagnostic UI projection. Exact-game compilation and final clean
build/install/load/CI are separate gates in the PR and local receipt.

Six historical pre-frame failures still do not establish which earlier capture
fact was wrong; they remain fail-closed and now cannot contaminate predecessor
transitions. The terminal EndTurn still lacks an independently proved terminal
semantic successor; RunManager.OnEnded is only a native end marker, not S-prime.
Neither issue is claimed solved by this repair. No broad all-valid or exhaustive
Full-Run claim, STPD change, gameplay, historical rewrite, merge or mark-ready.

The next Human canary should test discard in combat/event/map/reward (full and
non-full belts), discard between two ordinary decisions, native-origin hand
selector inputs, rapid card/end-turn boundaries, and a natural run end. All
successful captures need fresh exact-artifact audit before research admission.
