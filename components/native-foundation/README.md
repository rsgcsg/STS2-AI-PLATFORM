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
- `NativeBossRelicDecisionProvider`: exact `NChooseARelicSelection` options,
  skip path, and `RelicSelectCmd.FromChooseARelicScreen` PlayerChoice lineage;
  the command's exact option list is registered before `ShowScreen`, and
  execution revalidates that list and parent action before delivery;
- `NativeActChangeDecisionProvider`: a typed process-local contract for the
  exact act-ready enqueue, `VoteToMoveToNextActAction` Commit, and
  `ActChangeSynchronizer.OnPlayerReady` owner-ready seams, with a conditional
  next-boundary description that never claims `EnterNextAct` has completed;
- `NativeDecisionOwnerReadyProvider`: typed process-local notification from an
  exact owner-ready seam; consumers must still capture and validate a complete
  Connector frame at that seam;
- `NativeDomainOwnerProbe`: cross-domain owner discriminator only.

The owner probe deliberately does not enumerate actions. Each typed provider
reads a real STS2 owner; visible screens and controls only bind current input
delivery. A domain enters this component only when its semantic owner can be
expressed without importing UI timing, transport, evidence, or a second
game-rules model.

Nested selector proof boundary (v0.111.0): Native Foundation continues to own
only typed selector facts. Annotator binds Human continuation evidence from an
exact parent/root logical invocation scope to the exact typed selector factory,
then keys the resulting native screen until its terminal completion callback.
Shop removal uses the shipped three-argument
`MerchantCardRemovalEntry.OnTryPurchaseWrapper`; Event uses the exact
`EventOption.Chosen` option; Rest uses the exact option selected by
`RestSiteSynchronizer.ChooseLocalOption`. A GameAction-owned selector uses
`NativePlayerChoiceLineage` and the exact bound parent action. The child remains
a durable continuation on the parent root, never a second root, Commit, ledger,
or successor. No async `MoveNext`, FIFO, latest-frame, overlay, or timing
association is used.
