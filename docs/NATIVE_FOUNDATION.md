# Native Foundation

Native Foundation is the neutral, game-side seam between STS2 and Platform
products. It expresses STS2-owned semantic decisions, process-local operands,
action lifecycle, and owner lineage without transport, persistence, strategy,
or input execution.

```text
STS2 semantic owners and native validators
  -> Native Foundation facts and lifecycle
     -> Connector fair-player projection + exact binding/delivery
     -> Annotator read-only Human/native correlation

Host Runtime -> process lifecycle only
Receipt.Successor -> immediate post-delivery observation only
```

## Ownership

| Concern | Owner |
|---|---|
| rules, RNG, effects, native legality, Commit | STS2 |
| semantic decision and native action lifecycle adapter | Native Foundation |
| player-visible facts, Reads, finite BoundActions, controller, delivery | Connector |
| Human witness, disposition, semantic trace, durable evidence | Annotator |
| boot, profile, process, recovery, differential orchestration | Host Runtime |
| model projection, strategy, reward, training | external consumers |

Native Foundation does not publish public IDs. It accepts a process-local
identity interface so Connector and Annotator can refer to the same objects
without leaking exact native operands over the wire.

## Architecture Examples

### Direct combat

`NativeCombatDecisionProvider` reads the logical hand and current potion slots,
then calls STS2 validators for target and potion legality. Connector intersects
that canonical decision with currently visible/deliverable native controls.
Annotator compares the exact executing `GameAction` to the same decision.

Presentation may omit an action temporarily, but it cannot create semantic
legality. A visible and a shipped-headless presentation can therefore project
different deliverability while retaining the same native semantic decision.

### Player choice

`NativePlayerChoiceLineage` identifies the current `GameAction` parent from the
game's `ActionExecutor`. Generated-card choice remains a visible Connector
surface, while Annotator records pause/resume against that exact parent. The
lineage neither enumerates choices nor authorizes input.

### Reward to card reward to map and treasure

`NativeDomainOwnerProbe` distinguishes semantic owner from presentation/input
owner using `RunState.CurrentRoom`, `NOverlayStack.Peek`, and
`NMapScreen.IsOpen`. Three typed decision providers now supply the action
catalogs behind that route:

- `NativeMapDecisionProvider` reads destinations from `RunState.Map`, the
  current point and `MapTravel.GetTravelablePointsFrom`;
- `NativeRewardDecisionProvider` observes the exact `RewardsSet` supplied to
  `NRewardsScreen.ShowScreen` and projects unclaimed rewards, full-belt potion
  discard choices and native proceed policy;
- `NativeCardRewardDecisionProvider` observes the exact option arrays supplied
  to `NCardRewardSelectionScreen.ShowScreen` and `RefreshOptions`.
- `NativeTreasureDecisionProvider` binds the exact `TreasureRoom`/run pair from
  `NTreasureRoom.Create`, then reads the room-owned lifecycle and
  `TreasureRoomRelicSynchronizer` collection for exact open/select/skip/proceed
  membership.

The unified Mod registers those exact owner arguments through three bounded,
read-only Postfix seams. Connector intersects each catalog with current visible
controls, retains Host-local delivery operands and re-captures native membership
at execution. Annotator consumes the same catalog without gaining authority.

`NativeSemanticActionCatalog` contains only deterministic key construction,
exact-once membership and typed subject projection. It does not discover
legality or bind controls. Exact native owner data is captured once per
decision; request-local reference sets accelerate presentation intersection
without surviving a Snapshot or becoming a second state cache.

## Seam Matrix And Example Suite

The detailed [Native Seam Matrix](NATIVE_SEAM_MATRIX.md) records semantic owner,
state/action source, native validator, decision boundary, presentation owner,
delivery, binding, lifecycle, next-decision seam, heuristic debt, Ritsu support,
evidence, and migration verdict for every required domain.

The [Architecture Example Suite](NATIVE_FOUNDATION_EXAMPLE_SUITE.md) maps real
mechanisms to source, deterministic, exact-runtime, and Human evidence. Pending
T2/T3 rows are non-claims, not implied support.

## Heuristic Retirement Ledger

| Previous mechanism | Disposition | Reason |
|---|---|---|
| Connector combat action reconstruction | removed | Native Foundation now owns one combat semantic catalog |
| Annotator combat action reconstruction | removed | Annotator consumes the same catalog as Connector |
| UI catalog as execution-time semantic authority | forbidden | exact Human evidence disproved it for bounded combat |
| animation, elapsed time, queue-idle completion | forbidden | cannot prove causal settlement |
| generic `interactive` as canonical next decision | forbidden | interactivity is presentation readiness, not causal S' |
| Map reachability from `NMapPoint.State` | removed as semantic authority | `RunState` and `MapTravel` own destinations; map nodes only bind delivery |
| Reward publication from `NRewardButton` | removed as semantic authority | exact `RewardsSet` membership and proceed policy now own the catalog |
| CardReward options from holders/buttons | removed as semantic authority | exact native option arrays own membership; controls only bind delivery |
| Treasure stage from chest/holder/proceed visibility | removed as semantic authority | exact room lifecycle and synchronizer vote own the catalog; controls only bind delivery |
| per-surface owner detection | retained for presentation routing only | semantic owner and action source now come from typed Foundation providers |
| duplicated action-key and exact-membership helpers | removed | one stateless Native Foundation catalog helper serves all typed providers without owning legality |
| duplicated Describe/Start dispatch in source adapters | migration debt | consolidate only when a native domain adapter is proved |
| SnapshotBuilder source-specific special paths | migration debt | move facts before actions when owning domains migrate |

## Migration Map

1. Keep Direct Combat and PlayerChoice as regression oracles.
2. Keep Map/Reward/CardReward/Treasure typed decision adapters source-local and validate
   their exact owner registration, presentation intersection and execution-time
   revalidation on a new artifact.
3. Use Shop as the next domain migration, then Event, Rest, run entry, and
   terminal by native mechanism rather than screen.
4. Remove old owner/publication branches only after their Connector consumer
   and Annotator witness both use the shared adapter.

No migration may introduce a second legality model, UI allowlist, arbitrary
reflection, coordinate authority, timing completion, or silent fallback.

## Receipt And Next Decision

Public protocol `1.0.0` remains wire-compatible. `Receipt.Successor` is the
Snapshot obtained immediately after known input delivery when observation
succeeds. It is useful for continuity, but it is not business completion,
causal settlement, or canonical `S'`. Consumers that need the next stable
decision observe separately; an unknown delivery is never retried.

## Ritsu Boundary

RitsuLib remains an external implementation reference. Native Foundation uses
direct exact-game public lifecycle events and validators. The dependency route
and evidence are recorded in [ADR 0004](adr/0004-native-foundation-and-ritsu-route.md).
