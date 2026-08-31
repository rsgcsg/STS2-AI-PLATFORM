# Native Foundation

Native Foundation is the game-side semantic and lifecycle seam shared by the
Player Environment and Human Annotator. It reads STS2-owned state and native
validators; it does not expose transport, execute input, persist evidence, or
define strategy.

The Player Environment projects fair-player visible and deliverable actions
from these facts. The Annotator observes execution against the same facts.
Exact native operands remain process-local.

Current bounded ownership:

- `NativeCombatDecisionProvider`: logical combat decision and native legality;
- `NativeActionLifecycleObserver`: exact read-only `GameAction` lifecycle;
- `NativePlayerChoiceLineage`: current parent/continuation identity;
- `NativeMapDecisionProvider`: game-owned map destinations from `RunState` and
  `MapTravel`;
- `NativeRewardDecisionProvider`: exact `RewardsSet` membership, potion-belt
  alternatives and native proceed policy;
- `NativeCardRewardDecisionProvider`: exact card and alternative option lists;
- `NativeTreasureDecisionProvider`: exact treasure room lifecycle, relic
  collection membership and local vote;
- `NativeDomainOwnerProbe`: cross-domain owner discriminator only.

The owner probe deliberately does not enumerate actions. Each typed provider
reads a real STS2 owner; visible screens and controls only bind current input
delivery. A domain enters this component only when its semantic owner can be
expressed without importing UI timing, transport, evidence, or a second
game-rules model.
