# PR25 accepted-effect and queued-carrier repair

G5 Annotator/Live UI follow-up from PR25 head
`a26d893f70e490c4b3e27969f3c82129b93e1005`, base
`develop@cd9a0fbb0c85577a13513abe715f91d794ac86eb`.
No Connector mutation authority or STPD contract/policy is added.

## Trigger and first incorrect facts

The closed Human session created at 2026-09-10T12:35:34Z used source
`2272b499b94372e8d40a3450c78a37a77d629e96`, artifact SHA256
`9d607f5936951f0ff0ca5f06d205a8ad8e9809eca8af58754db2e9fe39aab1e6`,
MVID `7db201ac-b46b-497f-9aa8-a2faf7614982`. It contains 122 accepted
(107 roots, 15 children), 113 canonical, 9 unresolved and 18 invalidations.
14 canonical children have canonical parents and exact root lineage; the last
selector Close lacked successor at session close. There was one native start,
no native end, and no rest-site discard sample.

Official audit FAIL: action #72 has duplicate Started. The 36 valid / 0 invalid
legacy records do not override the failed trace audit. Native diagnostics had
72 exact / 7 unknown map votes. Calibration counts structural canonical rows;
it does not certify all 113 as causally correct.

1. An accepted EndTurn mapping failure did not match Quarantine's short reason
   whitelist, so it did not fence pending causality. Canonical Strike #70 then
   included the end-turn PLATING effect (block 5 -> 9, Play -> non-Play).
2. Direct UI admission unconditionally bound execution S and emitted Started,
   then subscribed the same root to actual GameAction lifecycle. Discard #72
   started again about 1.607 seconds later; its callback-time enemy-turn frame
   had already been labeled execution S. A duplicate-event-only fix would not
   repair that wrong boundary.
3. Ten PlayCard pre-frame failures split into five phase-Start and five
   phase-Play/InCardPlay captures. Native permits early/shortcut selection;
   the public immediate-action projection does not establish a complete
   executable catalog at those seams. Their missing S is not repaired by
   pretending the input or a nearby frame is execution evidence.

## Repair and authority

Accepted native/UI observer failures and deferred accepted failures now enter
an explicit accepted-Human-effect quarantine path. Mapping, missing-scope and
observer failures share the existing durable barrier, independent of their
reason strings. Known internal MoveToMapCoord/ReadyToBeginEnemyTurn diagnostics
remain distinct. Failed persistence keeps the existing rollback/trace shutdown.

An exact UI-owned GameAction can be admitted/subscribed at OnEnqueued before
ActionExecutor notification. Potion discard additionally subscribes on the exact
RequestEnqueue carrier before the native call, while its UI witness is still
available. Exact game source confirms RequestEnqueue can defer a combat-play-only
action during NotPlayPhase, then later re-request that same object, or cancel it.
This early subscription preserves delayed start and synchronous cancellation.
The UI Postfix encounters an already-claimed root and cannot duplicate admission.
No synthetic GameAction or inferred current-root correlation is introduced.

A carrier-backed UI occurrence retains H at input but does not bind S or emit
Started there. The shared exact ActionExecutor boundary resolves the retained
subscription's root identity and captures execution S; native BeforeExecuted
owns Started. An admission-time semantic catalog is not reused as the carrier's
execution catalog. If a complete chosen-action proof is unavailable at actual
execution, it stays unproved rather than receiving the old H-as-S claim.

Core lifecycle emissions are idempotent for repeated Started/Finished callbacks;
this supplements, rather than replaces, the actual boundary repair. Historical
trace validation still rejects duplicate Started records.

Failed PlayCard occurrences now retain the exact card and target witness IDs
from TryPlayCard. Live UI displays those explicitly as native witnesses, without
fabricating public BoundAction or subject IDs. These failed inputs remain
failed-closed; the early/shortcut capture completeness gap is still open.

## Validation and handoff

Faithful Core regressions cover queued admission versus delayed execution,
exact execution pre-state, lifecycle idempotence and the durable failed-effect
barrier. Source-seam regressions cover accepted mapping-failure routing,
pre-request carrier subscription and exact-root execution lookup. Existing
act-ready duplicate-ingress checks are updated for the new carrier admission.
Root/exact-game/latest-head CI and installed/loaded SHA/MVID are separate
receipts in the PR and local release record; compilation alone is not Human
qualification. Lead review is not an independent reviewer claim.

Keep raw Human evidence immutable. No STPD edit, gameplay, merge, mark-ready or
force push. STPD pin remains `67566db562b95f57468290b97659c9f2e3862fe1`.
Rollback uses the owning Game Mod deployment snapshot with the game closed.

Remaining non-claims: early/shortcut PlayCard S capture, exact terminal successor,
all-valid Full Run, exhaustive surface coverage, research admission, performance
and non-interference. The repaired queued carrier needs fresh Human validation;
canonical counts may decrease when previously false execution claims fail closed.

Bounded next canary: EndTurn with a popup open; discard during enemy turn and
native cancellation; rapid first/shortcut cards; rest-site discard; finish a
selector and naturally end a run. Do not slow Human input to conceal capture gaps.
