# Full-Run Semantic Coverage

This is the current coverage authority for the additive Human Semantic Timeline.
It does not expand `HumanDecisionRecordV2`, corpus admission, or Connector
action authority.

## Causal Model

```text
Human observation H
-> exact current BoundAction correlation
-> STS2-owned GameAction lifecycle or exact source-local UI Commit
-> execution-bound semantic S
-> next complete authoritative Player Environment state S'
```

The ordinary-combat schema-2 owner closeout is the regression oracle. New
surfaces use the same `SemanticBoundaryTracker`; no surface may invent legality,
infer completion from time/animation/queue idle, or fill an earlier successor
from a later Human effect.

## Current Matrix

| Slice | Native witness | Source/test | Current-artifact Live |
|---|---|---:|---:|
| ordinary combat play / End Turn | `GameAction.OnEnqueued` + typed lifecycle + `ActionExecutor.BeforeActionExecuted` | complete | repair canary PASS for bounded accounting: 161 `PlayCard`, 32 `EndTurn` native roots |
| generated-card select | exact selection callback + direct UI delivery | complete | repair canary: 11 selects recorded |
| generated-card skip | exact skip callback + direct UI delivery | complete | not exercised |
| Combat hand selector select / replace / deselect / confirm | exact hand/container callbacks + direct UI delivery | complete | repair canary: 13 confirms; select/replace/deselect not proved |
| potion use / target / cancel | Human `NPotionHolder.UsePotion` arm -> exact `PotionModel.EnqueueManualUse` commit -> `UsePotionAction` lifecycle; target-picker cancel never enqueues | self-target normalization repair source/test/build/load complete | enemy-target use proved once; three self-target mapping failures exposed and fixed; cancel remains unexercised on repair artifact |
| lethal combat -> reward | existing combat lifecycle + reward Player Environment boundary | complete | repair canary PASS |
| reward claim | `NRewardButton.OnRelease` direct UI delivery | complete | repair canary PASS: 11 claims |
| reward proceed | `NRewardsScreen.OnProceedButtonPressed` direct UI delivery | complete | repair canary PASS: five proceeds |
| card reward select | `NCardRewardSelectionScreen.SelectCard` direct UI delivery | complete | repair canary PASS: three selects |
| map travel | `NMapScreen.OnMapPointSelectedLocally` -> `VoteForMapCoordAction` lifecycle | complete | repair canary PASS: 14 actions |
| event / shop / rest / treasure | Connector observation coverage only | not implemented as Human witnesses | map successors only: event (5), shop (1), rest (2), treasure (1); room-internal actions not exercised |
| run entry / game terminal | observation coverage varies | not implemented as Human witnesses | final `EndTurn -> game_over` observed; run entry and exhaustive Full Run not exercised |

Semantic state Read requirements are interaction-specific: combat requires
`run_deck` and `combat_piles`, shop requires `run_deck` and `shop_catalog`, and
the current ordinary non-combat surfaces require `run_deck`. A failed Read makes
that boundary partial; it never changes action publication or native execution.

## Repair Canary Gate

The first batch canary on artifact `fe3e3a82... / b1284288...` is not a PASS.
It exposed one direct-UI boundary defect and one parent-lifecycle pruning defect
that disabled semantic tracing while native accounting continued. Strengthened
audit now rejects that session with 546 missing accepted-root findings. See the
[batch evidence](evidence/FULL_RUN_BATCH1_OWNER_CANARY_2026-08-28.md).

The repair artifact exercised the bounded cross-surface path:

```text
lethal combat -> reward claim -> card reward select or skip/proceed -> map node
```

The later closed owner session
`session-20260828T032151Z-43b2f87e65484b8abccccbba71c713c8`, timeline
`timeline-31295a4049034cd6bd45d3b7d8fe8304`, passes overall schema-2 accounting
with 627 accepted actions, 625 proved, one cancelled before start, one
cancelled after start and correctly unknown, and zero unresolved. Independent
audit passes 219 valid Decision V2 records, 0 invalid records and 287 explicit
legacy invalidations. It exercised a long combat/reward/map path and one
enemy-targeted potion, but exposed three self-target potion mapping failures
and one accepted self-target use that lacked explicit accounting.

The canary proves repaired canonical direct-UI binding and parent-lifecycle
retention. It does not prove hand select/replace/deselect, generated skip,
potion use, room-internal event/shop/rest/treasure actions, run entry,
exhaustive Full Run, semantic-free performance, game outcome success or
qualification.

Source `fba874e8...` repairs the potion witness without changing Connector
authority: it matches STS2's owner normalization only after the original null
operand misses the frozen catalog, still requires exact-unique binding, and
defers arm/mapping failures until native acceptance. Its clean unified artifact
`b5fbda12... / 1cbcff84...` is installed and cold-loaded, but has no Human
potion evidence and inherits none from the failing predecessor. Batching cut
the narrow boundary-to-start median from about 8 ms to 4 ms; repeated read-rich
capture remains only a plausible, unprofiled source of perceived lag.

See the [potion canary](evidence/FULL_RUN_POTION_OWNER_CANARY_2026-08-28.md).

Current repair source is `c8775e1066137c1a7e00993a7ab74493a11717f7`.
Its clean unified build is `8d2f7d2a8e95eac424aa7fed7f22e825821609b83526d38605e813b6a9692c35 /
3043f4f4-63c8-4058-8f4e-44b60801d3d5`. Safe install passed with rollback
`apps/game-mod/.local/deployments/2026-08-27T15-04-13.434Z`. It is cold-loaded
in runtime `fb5a82ea198140aebfcdbe92b654fce1`, environment
`4866d18435e47f10970999ce4111dc51e575b1b9696b5ddf1b4dced04d4ff259`, under
the sole exact Modset
`a66aef087216f2ffdf4e5e87d849f1ffa3df2adc073b1b1651801886dabc3281`.
