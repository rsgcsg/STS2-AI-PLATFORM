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
- `NativeDomainOwnerProbe`: Reward/CardReward/Map owner discriminator only.

The final item deliberately does not enumerate actions. A domain enters this
component only when its STS2 semantic owner and lifecycle can be expressed
without importing UI timing, transport, evidence, or a second game-rules model.
