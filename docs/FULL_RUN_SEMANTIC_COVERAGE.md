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
| ordinary combat play / End Turn | `GameAction.OnEnqueued` + typed lifecycle + `ActionExecutor.BeforeActionExecuted` | complete | bounded Human-proved predecessor |
| generated-card select | exact selection callback + direct UI delivery | complete | bounded Human-proved predecessor |
| generated-card skip | exact skip callback + direct UI delivery | complete | not exercised |
| Combat hand selector select / replace / deselect / confirm | exact hand/container callbacks + direct UI delivery | complete | pending repair-artifact Live |
| potion use / target / cancel | Connector BoundActions; `UsePotionAction` lifecycle is distinct | not implemented as Human witness | not exercised |
| lethal combat -> reward | existing combat lifecycle + reward Player Environment boundary | complete | encountered on failed predecessor canary; repair pending |
| reward claim | `NRewardButton.OnRelease` direct UI delivery | complete | predecessor exposed boundary-name defect; repair pending |
| reward proceed | `NRewardsScreen.OnProceedButtonPressed` direct UI delivery | complete | predecessor exposed boundary-name defect; repair pending |
| card reward select | `NCardRewardSelectionScreen.SelectCard` direct UI delivery | complete | predecessor exposed boundary-name defect; repair pending |
| map travel | `NMapScreen.OnMapPointSelectedLocally` -> `VoteForMapCoordAction` lifecycle | complete | encountered before predecessor trace failure; repair pending |
| event / shop / rest / treasure | Connector observation coverage only | not implemented as Human witnesses | not exercised |
| run entry / game terminal | observation coverage varies | not implemented as Human witnesses | not exercised |

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

The repair artifact must exercise one continuous path:

```text
lethal combat -> reward claim -> card reward select or skip/proceed -> map node
```

Pass requires every encountered accepted Human action to have exactly one
semantic disposition, no proof to cross another Human start, and each proved
`A.S'` to equal the next real execution's `S` when such an execution follows.
It should additionally exercise one Combat hand selection with select,
replacement or deselect, and confirm when naturally available. Generated-card
skip, potion, event, shop, rest, treasure, run entry and terminal remain
non-claims until separately encountered or implemented.

Current repair source is `c8775e1066137c1a7e00993a7ab74493a11717f7`.
Its clean unified build is `8d2f7d2a8e95eac424aa7fed7f22e825821609b83526d38605e813b6a9692c35 /
3043f4f4-63c8-4058-8f4e-44b60801d3d5`. Safe install passed with rollback
`apps/game-mod/.local/deployments/2026-08-27T15-04-13.434Z`; loaded and Human
runtime remain pending, and no predecessor Live evidence transfers.
