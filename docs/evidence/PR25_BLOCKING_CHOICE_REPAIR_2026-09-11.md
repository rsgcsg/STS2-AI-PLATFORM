# PR25 blocking native choice audit and repair

G5, Annotator-owned native Human correlation. Base develop
`cd9a0fbb0c85577a13513abe715f91d794ac86eb`; audited PR head
`b8d608bf772c4e3eb1942a7846a6afbc973db314`. Draft PR25 remains unmerged.
STPD stays at `67566db562b95f57468290b97659c9f2e3862fe1`, unchanged.

## Exact Human evidence

Session `session-20260911T075257Z-b09e940c8ade447ea2d42b665bf3eb68`
uses Annotator source `322f985ca4b51676e6b16ef213b9eb52b6a29298`, runtime
`bbf9fc93f11042ff85a11c7865570cfa`, DLL SHA256
`987feee88f49377bb7098a173f845bdf1ac185efa57821fa24ad2744badbb209`,
MVID `29e125eb-a687-4747-b665-b9363ad72f03`. Game v0.111.0 / 41cef1ea,
assembly SHA256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
Local audit, calibration, native log/source inspection and immutable session
hashes live under `.local/pr25-canary-075257`; raw evidence is not committed.
The immediately preceding session was empty and closed.

| Population | Count |
| --- | ---: |
| Trace accepted / parentless / children | 511 / 441 / 70 |
| Proved / canonical | 509 / 509 |
| Unresolved / native cancellations | 2 / 0 |
| Additional known accepted but unrecorded selector inputs | 2 |
| Compatible Decision valid / invalid | 147 / 0 |
| Explicit invalidations | 74 |
| Native diagnostic exact / unknown | 330 / 0 |
| Native start / end | 1 / 1 (defeat) |
| Rapid input semantic candidates | 57 |

Canonical is 509/511 (99.61%) of traced inputs, but only 509/513 (99.22%) of
at least 513 known real Human inputs. Compatible-record validity and diagnostic
membership do not prove complete capture. All 70 recorded selector children and
their exact parents are canonical. Five combat-pile decisions and two generated
choices are canonical. No proved-to-canonical gap remains in this sample.

## Complete failure accounting

- 40 `human_action_native_type_mismatch` / ReadyToBeginEnemyTurnAction and
  32 of the same reason / MoveToMapCoordAction: internal native carrier
  diagnostics, not additional Human decisions. Existing Quarantine classification
  excludes them from Human-effect barriers; UI labels them diagnostic/retained.
- Two `selector_decision_pre_or_lineage_unavailable` / NChooseACardSelectionScreen:
  real Human input loss. At 08:12:49 and 08:13:38 UTC the source reports
  `selector_factory_without_exact_parent_scope`. These block canonical for the
  choices and correctly fence EndTurn #475 and #498 with
  `unrecorded_human_effect_before_successor`. Both EndTurns finished and committed
  before the choices; the fence is expected, the missing origin is a defect.

The two unknowns have the same reason, family `ordinary_combat.end_turn`, native
EndPlayerTurnAction mechanism; neither is session-close, persistence, start
lifecycle, or rapid-card failure. No other unresolved bucket is present.

## First incorrect fact and shared repair

Exact shipped KnowledgeDemon.ChooseCurse calls
CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(), cards,
player). The command carries that context through SignalPlayerChoiceBegun to
NChooseACardSelectionScreen.ShowScreen. The observer omitted that command and
ignored BlockingPlayerChoiceContext, incorrectly recording no exact owner.

NativeNestedSelectorPatches now observes that exact command overload and family.
An exact BlockingPlayerChoiceContext can supply a native origin via its opaque
process-local identity, carried through the existing async scope to the exact
screen. Existing enclosing Event/Reward lineage takes precedence. No current
GameAction lookup, timing correlation, invented GameAction, or EndTurn parent is
introduced. Missing, throwing or family-mismatched origins stay fail-closed.

The existing `native_selector` schema records the independent Human decision,
its native context origin, owner, selected action, state/action-space and later
proved successor. `CausalRoot != DecisionOccurrence` remains intact. The existing
UI already renders it as Selector / native origin with actual origin type.
STPD should consume decision kind and versioned evidence, not assume every
native_origin is a GameAction or require a Human parent. No STPD implementation
or research eligibility claim is made here.

## Validation and handoff

Regression coverage checks standalone context propagation across await,
preservation of exact enclosing parents, disposal/isolation, origin round-trip
and corrupted-lineage rejection, and finished EndTurn -> independent selector ->
next boundary. Source seam guards and exact-game compilation cover the observer
route. Final root/exact-game gates, CI and clean package/load identities are
reported in PR25 receipts; fixture success is not new Human evidence.

Rollback uses the owning Game Mod lifecycle and retained pre-deploy snapshot.
Historical session bytes remain unchanged. No gameplay, training, merge, mark
ready, or exhaustive all-valid Full-Run qualification is performed.

Next bounded Human canary: Knowledge Demon curse choice (both occurrences if
encountered); generated-card potion choice with exact parent; event reward and
rest Proceed regression; ordinary rapid cards through a native run end. Missing
origins must remain visibly failed closed. The repaired path needs fresh Human
validation before claiming this run gap closed in actual recording.
