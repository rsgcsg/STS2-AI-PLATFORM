# PR25 long Human audit: reward owners and native no-op repair

G5, Annotator-owned Human correlation, nested decision lineage and offline
calibration. STS2 still owns effects/legality; Connector owns state/actions.
Base develop `cd9a0fbb0c85577a13513abe715f91d794ac86eb`; audited head
`1e977ac9ed5e58a5750357b51dd3d6fed3c163ba`. Continue Draft PR25, no merge.
STPD remains `67566db562b95f57468290b97659c9f2e3862fe1`, unchanged.

## Exact observed candidate

Session `session-20260911T065058Z-6b8b0f47737546849e1c57d529eb51de`:
06:50:58 through 07:33:05 UTC. Runtime `aabad7f5cac74a2c8ada2e6ff9661d69`;
artifact SHA256 `56cb1cc7cd586fac7f5d76492fd0517ccf085d21ae02ed9c068216979c7c777d`,
MVID `e603efc5-f5eb-4d2c-8cb4-4e011b56fbb3`. Annotator source
`90ea1b2d5e01752e3e64d65fbe3c4029b7aeaf73`; Connector source
`08cc7f0449a17562ba85eb841ab6d28732519775`. Game v0.111.0 / 41cef1ea,
assembly SHA256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
Raw evidence and native log remain local and immutable; receipts under
`.local/pr25-canary-065058`.

| Population | Count |
| --- | ---: |
| Accepted / roots / nested decisions | 1533 / 1206 / 327 |
| Proved / durable canonical | 1527 / 1527 |
| Unknown / native cancelled after start | 4 / 2 |
| Compatible Decision valid / invalid | 446 / 0 |
| Explicit invalidations | 100 |
| Native diagnostic accepted / exact successful / cancelled / unknown | 1028 / 1026 / 2 / 0 |
| Native start / end | 2 / 2, both ends defeat |
| Rapid native-input semantic candidates | 157 |

Run 1 has 205 accepted / 205 canonical. Run 2 has 1328 accepted / 1322 canonical,
four unknowns and two cancellations. Both runs have exact new-run Launch and
native defeat endpoints; initial RunState observation is followed by the same
run's native start, not an unexplained missing start. These are bounded complete
run samples, not exhaustive all-surface qualification.

All 327 children and exact parents are canonical: 267 hand inputs, 25 card rewards,
16 upgrades, eight native deck inputs, five transforms, three simple card inputs
and three generated choices. Colorless Potion #960 -> generated choice #961
(Discovery) and Attack Potion #1167 -> #1168 (Predator) have canonical parents,
exact parent/root identities, independent choice state and four-action catalogs.
Clarity #717 is canonical. All three observed uses match native use logs. This
session does not exercise Fruit Juice or the potion native-input/no-public-action
branch; it validates these generated-potion samples, not every deferral variant.

Canonical ratio is 1527/1533 = 99.61% of accepted inputs, or 1527/1531 = 99.74%
excluding the two native cancellations. No missing proved-to-canonical rows.

## Complete failure accounting and owning fixes

Of 100 invalidations, 60 MoveToMapCoordAction and 37 ReadyToBeginEnemyTurnAction
are internal diagnostics, not separate Human decisions. Three rest-site Proceed
callbacks have semantic_pre_frame_capture_failed while H already shows map
navigation; each wrongly adds an unrecorded-Human-effect barrier and makes the
preceding real Proceed (#320/#667/#710) unknown.

Exact native source: NRestSiteRoom.OnProceedButtonReleased only calls
NMapScreen.Open; Open immediately returns when IsOpen is true. The first
incorrect fact is treating that no-op callback as an accepted Human mutation.
The observer now returns without staging a Human scope when the exact native
map IsOpen guard already applies. No gameplay call is suppressed. Genuine first
Proceed remains tracked and waits for its existing authoritative successor.

The fourth unknown is event option #206 (Neow Kaleidoscope). Its exact outer
EventOption.Chosen Task awaits a reward screen, but reward claim #207 was recorded
as a new external root before that outer Task's Commit. The existing event async
parent scope already supports generic selectors; NRewardsScreen.ShowScreen was
missing from exact input-owner registration, and claim/proceed did not consume
that binding.

The factory now binds the actual screen under the same exact async owner scope.
Claim/proceed retain the parent decision before native dispatch, then the existing
atomic tracker mutation closes the parent at the child's frozen input boundary
and admits the child's own decision, native completion and canonical stream.
The existing nested_selector wire kind is retained, with the actual reward family,
independent action identity and shared causal root; no fake GameAction, new ledger,
ambient-current-root lookup or late-frame backfill. Card selection remains a child
of the reward claim. Queued carriers retain this lineage on the exact lifecycle
subscription and hand off only at actual pre-execution S, never admission H. Ordinary combat rewards without an enclosing exact scope
remain roots. Session mismatch/missing parent fails closed. Late outer Task
completion stays lifecycle evidence and cannot erase the child lineage.

## Calibration disagreement

The old calibrator classified 1525 strict candidates and six unresolved despite
1527 canonical. #286/#288 were complete before #287/#289's exact execution edge;
the latter two were then explicitly cancelled by STS2. Requiring those cancelled
attempts to appear in the action catalog wrongly rejected their predecessors.
A complete captured state and a legal next successful action are different facts.

Calibration now recognizes this bounded case only with exact shared content refs,
a native pre-execution witness, complete state/Reads, ordered started/cancelled
facts and no successful transition for the cancelled input. The cancelled actions
stay rejected. Missing cancel, wrong frame, missing Reads and polling remain
rejected. Recalibration of unchanged raw bytes gives 1527 candidates, four unknown,
two cancelled and zero proved/canonical gap. This is offline classification only,
not historical evidence rewriting or STPD research admission.

## Verification and remaining boundaries

233 Core tests pass, including event -> reward -> card choice and late outer
Commit through the final causal/lineage validators, exact reward-owner source
routing and the native rest no-op guard. Fifteen calibration tests pass, including
both handoff proof families and cancellation-negative cases. Exact native source
and compilation confirm patched signatures. Final root/exact-game/CI/BOM and
clean install/cold-load receipts belong to the final candidate and are recorded
in Draft PR25/local receipts; new runtime bytes require their own Human canary.

Next canary: Neow Kaleidoscope reward claim/card selection/proceed; rest upgrade
then rapid double Proceed; rapid card cancellation; generated-potion selection;
Fruit Juice at the turn boundary. Require exact nested reward lineage, no false
rest invalidations, successful canonical persistence, and explicit cancellation.
No Human gameplay, STPD edits, merge/mark-ready, historical backfill or universal
Full-Run qualification is performed or claimed.
