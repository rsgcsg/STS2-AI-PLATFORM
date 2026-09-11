# PR25 non-combat AnyTime potion audit and repair

G5: Native Foundation owns typed native potion semantics; Connector owns the
popup BoundAction and independent execution witness. Annotator's causal tracker,
parent/child identities and fail-closed evidence rules are unchanged. Base develop
is `cd9a0fbb0c85577a13513abe715f91d794ac86eb`; audited PR head is
`32de960cbd198fda09777d46a06d8789c8452954`. PR25 stays Draft. STPD is unchanged.

## Exact predecessor Human evidence

Closed session `session-20260911T050534Z-27d456d1bff04f1395b910d3ebaff05a`
belongs to runtime `084723a0dafc4e04993eb951aae93e4a`, DLL SHA256
`0e7e2d4544b66581afd45459350f324a86cca044c75cfd955d0b631d8d2c4aa0`,
MVID `2ade3dcc-1749-4e3d-ab08-697fd373c5af`. Game v0.111.0 / 41cef1ea,
assembly SHA256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
The Human explicitly attests console insertion followed by drinking Fruit Juice.
Console insertion is not ordinary gameplay admission; drinking is a distinct
native Human input. Console command errors are not this repair's target.

| Population | Count |
| --- | ---: |
| Trace accepted / proved / durable canonical / strict candidates | 287 / 284 / 284 / 284 |
| Additional accepted native use dropped before trace | 1 |
| Known accepted Human total / recorded proportion | 288 / 98.61% |
| Trace unknown | 3 |
| Explicit invalidations | 32 |
| Legacy audit valid / invalid | 78 / 0 |
| Native diagnostic exact / unknown | 180 / 0 |
| Native starts / native ends / unproved run ends | 2 / 1 / 1 |

Invalidations are 20 internal MoveToMapCoordAction, 11 internal
ReadyToBeginEnemyTurnAction, and one real `potion_exact_mapping_failed`.
The three unknowns are event roots #246 and #269
(`intervening_human_action_before_native_commit`) and shop-open #274
(`unrecorded_human_effect_before_successor`, collateral to the dropped use).
The first run has 245 accepted and 245 canonical, including final EndTurn #245
with a game_over successor. This validates the previous terminal repair for
that bounded Human sample; it does not qualify every Full-Run decision family.

At 05:14:49.997406Z the recorder rejects Fruit Juice mapping. The game then logs
`Player 1 using potion FRUIT_JUICE`; next Human discard #275 records HP 62/80,
versus shop-open #274's 57/75. The real use succeeded. Its pre-snapshot was not
persisted and no canonical Juice row exists. A green native diagnostic audit
therefore does not account for all real Human inputs.

## First incorrect facts and repair

The popup asked only NativeCombatDecisionProvider for use operands. In a shop
that provider returns not_combat, so the popup advertised choose_potion_use
(activate) instead of use with the native player target. RecorderRuntime's exact
UsePotionAction mapping correctly rejected it. Separately, the non-combat
execution witness selected the room/reward catalog, which cannot prove potion
use even if the ingress mapping alone is repaired.

NativePotionUseDecisionProvider now supplies the current non-combat AnyTime
belt decisions, using native Usage, CanUseOrRemovePotions, life/custom usability,
IsValidTarget, and NPotionHolder's exact self-target behavior. Combat retains its
existing complete combat catalog. Popup projection and execute-time delivery
share this provider; delivery also checks the exact current popup control,
potion slot, target and queued-input guard before native EnqueueManualUse.
The execution witness selects the typed potion provider by exact native action
type independently of the underlying room/overlay. Target reconstruction reads
native NetUsePotionAction.targetPlayerId instead of substituting the local
player when a target fails to resolve.

IsQueued is a delivery guard, not exclusion from execution semantics: STS2 sets
it at enqueue while the same potion remains in the belt until native execution.
The existing DirectCombatUse field and ordinary_combat.use_potion family are
retained as legacy names for wire/recording compatibility; the coverage entry
now documents non-combat use and native owner-ready successor requirements.
No old records are changed, no unknown is reclassified, no policy/legality
reconstruction is added to STPD, and no new causal root type is manufactured.

## Verification and explicit limits

Exact-game regression uses native FruitJuice and IsValidTarget with a minimal
player/creature fixture: exact self operand, queued membership, native disabled
state, dead owner, empty belt, usage and custom guards. Popup regression requires
use with a player operand and rejects disabled delivery projection. Existing
combat, selector and recorder suites remain required. Source review checks both
admission and before-execution routes against the exact native PotionModel,
NPotionHolder, NPotionPopup and UsePotionAction. Local receipts are under
`.local/pr25-juice-repair`; all 962 original session files are hash-preserved.
Final root checks, exact-game build, CI and install/load identities are recorded
on the final Draft PR head; none imply fresh Human success.

Non-combat TargetedNoCreature potions require an exact target-node witness and
remain outside this direct-use addition; a null Creature is not enough.
AnyTime input during a non-play combat phase is not newly qualified. Event
reward-tree parent ownership for #246/#269 remains a separate open source gap;
it is not repaired by the potion catalog and must not be hidden by relaxed
successor checks. Close before a proved successor remains unknown. All-valid
Full-Run recording and STPD training admission remain unclaimed.

Next bounded Human canary: (1) drink Fruit Juice in a shop, then make one normal
shop decision; (2) drink it on a map/rest/event surface, then make a normal next
decision; (3) use a combat potion and perform a short rapid-card/selector sequence.
Check exact use/player operands, independent execution membership, HP/max-HP
change, canonical successor and absence of potion_exact_mapping_failed. If a
console is used to stage a potion, distinguish that intervention from native
use in the audit. Do not require arbitrary Close to manufacture a successor.
