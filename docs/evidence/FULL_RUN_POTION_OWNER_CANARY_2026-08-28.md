# Full-Run Potion Owner Canary

## Exact Identity

- Session: `session-20260828T032151Z-43b2f87e65484b8abccccbba71c713c8`
- Timeline: `timeline-31295a4049034cd6bd45d3b7d8fe8304`
- Unified artifact: `be1a96ec762139de7bcda8ec5f4898a482c6dc03cf4fd18e20be41585eb22380`
- Artifact MVID: `79354979-0488-42c3-bd83-8b90d6bbf9e4`
- Annotator source: `e1d88e3582d3d51a383d366d5ede517ca6a98e40`
- Runtime: `1ad1e3f83e9545ab911bd75f85262a96`
- Environment: `94b59c951d4b0004d85bba9de2a35c3fd28b12d35b9ed2e996c8b81fc8c3fafc`
- STS2: `v0.111.0 / 41cef1ea`
- Sole Modset: `35c367613f6caf041842a02850582477edd1dfba018316dbf599f2f79aa81915`

This is owner-attested Human runtime evidence. It does not transfer to a later
artifact and is not exhaustive Combat or Full-Run qualification.

## Semantic Result

Independent audit passes with 219 valid Decision V2 records, zero invalid
records, 287 explicit legacy invalidations, 17,954 materialized Reads and zero
Read failures. Schema 2 accounts for every accepted action:

- 627 accepted;
- 625 proved;
- one cancelled before execution start;
- one `PlayCardAction` cancelled after start and correctly classified
  `transition_unknown`;
- zero unresolved or duplicate dispositions.

The accepted mechanisms comprise 415 `PlayCardAction`, 79
`EndPlayerTurnAction`, 43 `VoteForMapCoordAction`, 53 reward claims, 20 reward
proceeds, 15 card-reward selections, one generated-card selection and one
`UsePotionAction`. Audit found no false successor or proof crossing another
Human effect. The two cancellations do not form successful actions.

## Potion Defect

The single enemy-targeted Vulnerable Potion followed the exact
`NPotionHolder.UsePotion -> PotionModel.EnqueueManualUse -> UsePotionAction`
path and proved a transition. Three accepted self-target uses failed exact
mapping (`COLORLESS_POTION`, `BLOCK_POTION`, and `CURE_ALL`). A later
self-target `DEXTERITY_POTION` was also absent when the arm-time frame was not
eligible, exposing silent accepted-action loss in the source-local witness.

Exact `v0.111.0` source shows that `EnqueueManualUse` receives a pre-normalized
null target for some self-valid potions and then substitutes the owner creature
before constructing `UsePotionAction`. The frozen Player Environment catalog
correctly binds the owner referent. The defect therefore belongs to Annotator
correlation, not Connector legality or STS2 execution.

Source `fba874e8d7a89b7843c82aea3cd5987bb54b41e3` fixes only that boundary:

- original target is matched first, then the STS2 owner operand is tried only
  after a null-target miss and must still resolve exact-unique;
- arm-time and mapping failures become explicit invalidations only if
  `EnqueueManualUse` actually accepts the action;
- closing the target picker before enqueue produces neither an action nor an
  invalidation;
- asynchronous arm cleanup is generation-bound.

It does not call `IsValidTarget`, inspect `TargetType`, authorize an action, or
change Connector/programmatic execution.

## Performance Boundary

Compared with the preceding repair canary, semantic trace batching reduced the
boundary-to-native-start median from about 8 ms to 4 ms and p95 from about 9 ms
to 5 ms. This is evidence for the narrow persistence optimization, not proof
that perceived frame latency is fixed. The latest trace is 102.5 MB across
3,215 events; repeated read-rich capture during settlement remains a plausible
hotspot, but no CPU/GC/frame profiler yet proves it dominant. Polling and
settlement semantics were therefore not changed.

## Repair Artifact Boundary

The clean repair artifact is built, safely installed and cold-loaded as
`b5fbda1277404e277eb8871faa4baa126fb92e324dc0dc09c26f7693e9791f02 /
1cbcff84-1a35-4f4a-a387-dfdce601f8f1` in runtime
`10eb9301c5624a59a5693d2dfb9b480f`, environment
`575f57f4265242e72434b05ec50dc5f89c4bcdf1e45ff02da630cdcc87de2c0e`,
with sole exact Modset
`67f7e0179cba12c2b23342b0144685f7e37ea05d6749b34e1546fa6e1db9162a`.
Rollback is `apps/game-mod/.local/deployments/2026-08-28T08-31-33.162Z`.

Load evidence does not prove self-target mapping, target-picker cancellation,
deferred accepted-action invalidation, reduced perceived lag, or any newly
unexercised Combat selector.
