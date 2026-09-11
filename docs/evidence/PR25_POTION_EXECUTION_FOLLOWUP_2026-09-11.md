# PR25 long-canary audit and potion execution follow-up

G5: shared native execution evidence, exact reward Commit ownership and calibration.
Audited head `6db6ebe893c19ef1d2bf8b1fc6a8345e31a761ef`, base develop
`cd9a0fbb0c85577a13513abe715f91d794ac86eb`. STPD remains unchanged at
`67566db562b95f57468290b97659c9f2e3862fe1`. PR25 remains Draft.

## Exact observed evidence

Closed Human session `session-20260911T015908Z-e4c29f840fab4ae48c84754e92a5849b`
uses Annotator/Connector/UI source `8f66bc933597214e7c8d766ce7f590cdb550ed0f`,
DLL SHA256 `090020d091a40984aa777dc64b28c291c982d511e1178279ec1827ac900822de`,
MVID `d00f922b-072c-44cb-856b-3a45f9b364ed`. Exact game remains v0.111.0,
41cef1ea, assembly SHA256
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
Read-only hashes, audits, calibration and gates are under `.local/pr25-followup`.

| Boundary | Count |
| --- | ---: |
| Accepted semantic decisions | 1,438 |
| Proved | 1,433 |
| Canonical | 1,432 |
| Unknown | 5 |
| Explicit invalidations | 160 |
| Native execution membership exact / unknown | 987 / 0 |
| Native starts / resumes / ends | 2 / 1 / 3 |
| Legacy audit valid / invalid | 455 / 0 |
| Strict semantic candidates, corrected calibration | 1,431 |

All invalidations are counted: 81 internal ReadyToBeginEnemyTurnAction,
78 internal MoveToMapCoordAction, and one real potion-discard projection failure.
The 159 internal callbacks are diagnostics, not extra Human decisions. No
pre-frame ingress failures recur. All 12 native-input PlayCards introduced by
the previous repair are canonical. All 188 selector decisions (184 nested,
four native-origin) are canonical. There are 1,250 root decisions, of which
1,244 are canonical. Canonical counts per run are 499, 226 and 707.

This is strong bounded evidence for the rapid-input repair and observed
selectors, not exhaustive Full-Run or non-interference qualification.

## Repairs and first incorrect facts

### Potion discard catalog: confirmed defect

Decision #1163 at 02:43:54Z is Discard shackling potion while a card reward
selector is underneath the potion popup. H has exact potion-popup binding;
S at DiscardPotionGameAction execution has card_reward_selection as the public
owner. The old native provider selected that card-reward catalog, so the
already accepted discard was absent. Native Commit and successor were proved,
but canonical projection failed. The error label
semantic_projection_persistence_unknown here describes a semantic projection
failure, not evidence of disk loss. Decision #1128 is the other discard and
was canonical; reward-only support concealed the shared gap.

Native source independently shows NPotionPopup enqueues the exact belt slot;
DiscardPotionGameAction.ExecuteAction resolves its current slot and cancels if
empty. The popup or reward screen does not own execution legality. Native
Foundation now projects current occupied slots for this exact operation;
Connector routes the observed discard before combat/room overlay selection.
The same slot projection is reused by reward potion alternatives, with their
existing UI availability gates. Membership still requires the exact retained
potion object: a removed/replaced/foreign potion fails closed. No native
operand, mutation, hidden state or public BoundAction is created.

### Reward skip synchronous Commit: confirmed defect

Decision #144 (02:03:10Z) had no finished or native Commit event until session
close. It was Proceed on the nonterminal rewards opened by shop purchase #123.
Native SkipLocalRewardsSet completes before the outer UI accepted Postfix.
The previous shared Commit path consumed a matched completion even when no
tracker root existed yet. A second unsafe lookup used both screen and
RewardsSet in Postfix, after this operation, when callbacks may already have
changed the overlay. The session does not distinguish these two loss paths.
Prefix now captures the exact
screen, RewardsSet and bound action witness; Postfix consumes only those
identities, with identity-checked cleanup. The shared synchronous Commit seam
also admits an unclaimed UI root only after matching its native completion
registration to the exact active scope. Otherwise a Commit arriving before the
outer UI Postfix would be consumed without a tracker root. Later duplicate UI
callbacks remain idempotent. This repairs both orderings without
using the newly exposed owner, a timer or last-root fallback. It does not by
itself repair the enclosing purchase's reward-tree lineage.

### ActEntered calibration: confirmed diagnostic defect

Three act-change roots (#311, #975, #1278) have exact native_act_entered map
boundaries. Calibration recognized only generic native_decision_owner_ready
for their proof branch and incorrectly rejected them. It now accepts the
specific act-change action type and complete map/Read boundary, rejecting a
foreign root. Strict candidates increase from 1,428 to 1,431 on unchanged
historical bytes. No historical record is rewritten or backfilled.

## All five unknowns and remaining Full-Run blockers

| Decision | Native family | Reason and remaining scope |
| --- | --- | --- |
| #123 | Merchant purchase, Orrery | intervening_human_action_before_boundary; nested reward claims are recorded as roots while purchase awaits the reward tree |
| #502 | Event option | intervening_human_action_before_native_commit; reward/selector decisions occur before enclosing event completes |
| #1210 | Event option | intervening_human_action_before_native_commit; reward claim Commit arrives before enclosing event successor |
| #144 | Nonterminal reward Proceed | session_closed_before_successor_boundary; exact Commit owner race repaired above, needs fresh Human confirmation |
| #1438 | EndTurn at run end | session_closed_before_successor_boundary; native run end is journaled but no action-bound terminal successor is proved |

The first three are reward-tree ownership coverage gaps, not player mistakes or
Recorder joining late. This patch preserves fail-closed behavior; it does not
invent parent lineage for NRewardsScreen or grant parent success from child
completion. Broad Full-Run remains blocked on exact reward-tree continuation
ownership and a faithful action-bound terminal successor. These need source
work, not repeated broad Human runs to rediscover the same issue.

One further canonical predecessor is not a strict candidate because its next
execution boundary is the failed discard #1163 with no matching execution
catalog. The 1,432 canonical and 1,431 candidate counts therefore intentionally
differ. Structural legacy valid count is not a substitute for either.

## Validation and handoff

Tests cover occupied/empty/replaced native belt operands, same-scope reuse,
discard routing before unrelated overlays, Prefix ownership across synchronous
reward screen removal, and positive/foreign-root ActEntered calibration.
Root and exact-game gates, clean identity/BOM, hosted CI and installed/loaded
artifact receipts are recorded on the final Draft PR head. Old Human evidence
does not qualify the repaired bytes; no STPD or gameplay changes are made.

Next bounded Human tests: discard while card rewards, combat, and event/shop
owners are visible; skip/Proceed from a nonterminal reward collection; retain a
short rapid-card plus selector regression. Broad all-valid Full-Run and STPD
training admission remain unclaimed. Rollback uses the owning Game Mod snapshot.
