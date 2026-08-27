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
| lethal combat -> reward | existing combat lifecycle + reward Player Environment boundary | complete | pending exact-runtime evidence |
| reward claim | `NRewardButton.OnRelease` direct UI delivery | complete | pending exact-runtime evidence |
| reward proceed | `NRewardsScreen.OnProceedButtonPressed` direct UI delivery | complete | pending exact-runtime evidence |
| card reward select | `NCardRewardSelectionScreen.SelectCard` direct UI delivery | complete | pending exact-runtime evidence |
| map travel | `NMapScreen.OnMapPointSelectedLocally` -> `VoteForMapCoordAction` lifecycle | complete | pending exact-runtime evidence |
| event / shop / rest / treasure | Connector observation coverage only | not implemented as Human witnesses | not exercised |
| run entry / game terminal | observation coverage varies | not implemented as Human witnesses | not exercised |

Semantic state Read requirements are interaction-specific: combat requires
`run_deck` and `combat_piles`, shop requires `run_deck` and `shop_catalog`, and
the current ordinary non-combat surfaces require `run_deck`. A failed Read makes
that boundary partial; it never changes action publication or native execution.

## First Canary Gate

The first new-artifact canary must exercise one continuous path:

```text
lethal combat -> reward claim -> card reward select or skip/proceed -> map node
```

Pass requires every encountered accepted Human action to have exactly one
semantic disposition, no proof to cross another Human start, and each proved
`A.S'` to equal the next real execution's `S` when such an execution follows.
Generated-card skip, event, shop, rest, treasure, run entry and terminal remain
non-claims until separately encountered or implemented.

Current canary source is `509e5c6f51a7c68353673a189b7f480d78aa11f7`.
Its clean unified build is `fe3e3a82cdf84cdaa30dea9f5ed0d65fc856099b8391fc165f833b5a57831796 /
b1284288-3a82-4369-b548-a0220793b80e`. It is not installed or loaded while an
older STS2 process is running, and no predecessor Live evidence transfers.
