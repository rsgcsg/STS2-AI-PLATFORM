# Native Seam Matrix

This matrix records the exact STS2 `v0.111.0` ownership conclusions used by
Native Foundation. It is a migration artifact, not a coverage or qualification
claim. Native type and method names are conclusions from the exact local game
assembly; proprietary decompiled source is not part of this repository.

Evidence levels are `T0` source/assembly, `T1` deterministic test, `T2` exact
build/load automated runtime, and `T3` Human runtime. Evidence from the PR #3
predecessor artifact does not transfer to a new Native Foundation artifact.

## Combat

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | `CombatManager`, local `PlayerCombatState`, `ActionQueueSynchronizer` |
| State source | logical hand, energy/resources, potion slots, combat phase and current combat participants |
| Semantic action source | `NativeCombatDecisionProvider` enumerates logical hand cards, potions and End Turn |
| Native validator | `CardModel.CanPlayTargeting`; potion ownership, usage, custom usability and `IsValidTarget`; native play phase |
| Decision boundary | local player play phase with game action synchronization in `PlayPhase` |
| Presentation owner | `NCombatRoom`, `NPlayerHand`, potion holders and `NEndTurnButton` |
| Delivery seam | `CardModel.TryManualPlay`, `PotionModel.EnqueueManualUse`, `PlayerCmd.EndTurn` |
| Exact binding | process-local card, potion, target and room objects behind Connector referents |
| Lifecycle | exact `GameAction` accepted/start/pause/resume/cancel/finish observations |
| Next-decision seam | no universal finish rule; a complete later semantic boundary is required, and End Turn additionally requires the next player play phase |
| Root/continuation | card, potion and End Turn are roots; nested choices remain descendants of the executing root |
| Current workaround | presentation readiness still uses hand/control state before intersecting the semantic catalog |
| Heuristic debt | End Turn deliverability depends on the native button; causal `S'` remains Annotator evidence, not Foundation action publication |
| Ritsu support | card/potion/combat lifecycle events exist, but no generic exact action timeline or causal next-decision replacement was found |
| Missing evidence | final artifact T2/T3; normal-display versus shipped-headless T2 differential |
| Migration verdict | Direct Native Foundation implemented; presentation and exact delivery remain Connector-owned |

## PlayerChoice

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | the currently executing parent `GameAction` and its `PlayerChoiceContext`/branching continuation |
| State source | parent action, active choice task and domain-specific native choice collection |
| Semantic action source | domain-specific choice owner; no universal choice catalog is claimed |
| Native validator | the owning choice task and its exact selectable membership/confirmation rules |
| Decision boundary | parent action pauses for a player choice and later becomes ready/resumes |
| Presentation owner | generated-card, hand-selection or other typed choice screen |
| Delivery seam | existing typed native choice callbacks and controls |
| Exact binding | process-local parent action plus exact option/control object |
| Lifecycle | parent pause/ready/resume/cancel/finish; Foundation exposes parent lineage read-only |
| Next-decision seam | choice commit resumes the same root; only a later complete semantic boundary proves the next root decision |
| Root/continuation | continuation of the executing root, never an unrelated new root |
| Current workaround | each supported choice family still owns its presentation and membership adapter |
| Heuristic debt | generic choice action enumeration is not yet centralized; visible screen state cannot define parent lineage |
| Ritsu support | no exact parent/continuation lineage API found in `v0.5.18` or audited dev source |
| Missing evidence | final artifact Survivor/discard and generated-choice T3 |
| Migration verdict | shared lineage implemented; retain typed source-local choice adapters |

## Map

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | `RunState` map graph/current coordinate/visited coordinates and native map point state |
| State source | current act map, `CurrentMapCoord`, reachable `MapPoint` graph and visited history |
| Semantic action source | native travelable destinations; current Platform still derives the list through bound `NMapPoint` nodes plus `RunState` checks |
| Native validator | map-point travelable state, run destination membership and tutorial/travel state |
| Decision boundary | map is open, travel is enabled, no travel is active and no annotation mode owns input |
| Presentation owner | `NMapScreen`, `NMapPoint`, `NMapDrawingInput` and current controller mode |
| Delivery seam | `NMapScreen.OnMapPointSelectedLocally`; annotation stop on the exact drawing input |
| Exact binding | map-screen identity plus exact `NMapPoint`/coordinate object |
| Lifecycle | route vote/travel and room transition; annotation is presentation-only |
| Next-decision seam | entered room's native decision owner or another explicit map decision |
| Root/continuation | route selection is a root; annotation is a non-gameplay UI continuation |
| Current workaround | private `_isInputDisabled`/`_drawingInput`, controller on-screen filtering and bound UI nodes |
| Heuristic debt | semantic reachability and UI deliverability remain mixed in `MapNavigationSurfaceReader` |
| Ritsu support | map-generation and room lifecycle helpers, not exact route-choice authority |
| Missing evidence | native destination provider T1 and final artifact map T2/T3 |
| Migration verdict | owner discriminator implemented; native decision adapter is the next migration step |

## Merchant / Shop

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | current `MerchantInventory` and its card/relic/potion/removal entries |
| State source | stocked entries, prices, local player gold/capacity and removal state |
| Semantic action source | inventory entries; current publication additionally requires visible enabled slots |
| Native validator | entry stock/affordability and `OnTryPurchaseWrapper` domain validation |
| Decision boundary | current merchant room/inventory and no purchase/selection transition in progress |
| Presentation owner | merchant room button, inventory screen slots/back button and room Proceed |
| Delivery seam | entry `OnTryPurchaseWrapper`; exact room/open/back/proceed controls |
| Exact binding | inventory plus exact entry/slot/removal object |
| Lifecycle | purchase task and any nested removal choice; no universal causal completion rule |
| Next-decision seam | refreshed inventory, nested removal decision, or room/map transition |
| Root/continuation | purchase is a root; card removal selection is a continuation |
| Current workaround | `CanPurchase` mixes stock/gold with hitbox visibility/enabled state |
| Heuristic debt | UI slot actionability is still used as semantic publication input |
| Ritsu support | high-level merchant purchase lifecycle, but not exact entry binding or nested removal lineage |
| Missing evidence | shared semantic inventory provider T0/T1 and representative final artifact T3 |
| Migration verdict | direct presentation adapter retained; migrate after Map/Reward route |

## Reward

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | current room reward set and each native `Reward` object |
| State source | unclaimed rewards, potion capacity, linked reward sets and reward-screen progression |
| Semantic action source | native reward collection; current Platform discovers ordinary choices through `NRewardButton` |
| Native validator | reward-specific selection/procure logic and player capacity; linked sets require their own contract |
| Decision boundary | rewards overlay owns input and has at least one exact claim/discard/proceed operation |
| Presentation owner | `NRewardsScreen`, `NRewardButton`, potion popup and `NProceedButton` |
| Delivery seam | exact reward button callback, `DiscardPotionGameAction`, or Proceed control |
| Exact binding | reward-screen plus exact `Reward`/button or potion slot |
| Lifecycle | claim may open CardReward or another nested selection; proceed starts room/map transition |
| Next-decision seam | CardReward owner, remaining reward set, or Map owner after room transition |
| Root/continuation | claim/proceed are roots; nested reward choices are continuations |
| Current workaround | visible buttons are the publication source; linked sets fail closed |
| Heuristic debt | reward model membership is not yet isolated from screen controls |
| Ritsu support | reward-taken and rewards-screen continuation events, not complete action enumeration/binding |
| Missing evidence | shared reward provider T0/T1 and final `lethal -> Reward` T3 |
| Migration verdict | cross-domain owner discriminator implemented; decision adapter pending |

## CardReward

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | native card-reward option collection and alternative reward choices |
| State source | offered cards, alternatives and skip/proceed policy of the active reward |
| Semantic action source | current selectable native options; Platform presently reads exact holders/buttons |
| Native validator | active option membership and reward-specific select/alternative callback |
| Decision boundary | `NCardRewardSelectionScreen` is current and one exact option is actionable |
| Presentation owner | card-reward screen, `NCardHolder` and alternative buttons |
| Delivery seam | holder `Pressed` signal or exact alternative button callback |
| Exact binding | screen plus exact card model/holder or alternative control |
| Lifecycle | selection resolves the reward continuation and returns to rewards/map flow |
| Next-decision seam | remaining Reward owner or Map after native transition |
| Root/continuation | continuation of the reward claim that opened it |
| Current workaround | private `_isClickable` and UI controls define current publication |
| Heuristic debt | native option owner/validator is not yet a Foundation adapter |
| Ritsu support | no exact card-reward option catalog/parent lineage replacement found |
| Missing evidence | shared option provider T0/T1 and final artifact T3 |
| Migration verdict | owner discriminator implemented; typed continuation adapter pending |

## Event

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | current event model/state and its current option/proceed phase |
| State source | event state, current option set and visible event text |
| Semantic action source | native event options; current Platform enumerates visible `NEventOptionButton` controls |
| Native validator | option state/availability and event-owned transition callback |
| Decision boundary | event room owns input and an option, proceed or dialogue-advance control is current |
| Presentation owner | event option buttons, dialogue hitbox and room Proceed |
| Delivery seam | exact option/proceed button or dialogue hitbox callback |
| Exact binding | room/event owner plus exact option/control object |
| Lifecycle | event task/state transition, possible nested selector, room completion |
| Next-decision seam | next event phase, nested choice, Map, or terminal owner |
| Root/continuation | event option is a root; dialogue and nested choices are continuations |
| Current workaround | visible/enabled option controls define action publication |
| Heuristic debt | no shared native event option provider or commit witness yet |
| Ritsu support | room/event lifecycle coverage is useful, but no exact generic option authority was found |
| Missing evidence | source-local native option seam T0/T1 and final artifact T3 |
| Migration verdict | direct presentation adapter retained; migrate by event mechanism, not event ID |

## Rest

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | current rest-site option collection and local player/run state |
| State source | available `RestSiteOption` objects, used/disabled state and room progression |
| Semantic action source | native rest-site options; current Platform discovers bound option buttons |
| Native validator | option availability and native option execution callback |
| Decision boundary | current rest-site room has an enabled option or exact Proceed control |
| Presentation owner | rest-site option buttons and room Proceed |
| Delivery seam | exact option button or Proceed callback |
| Exact binding | room plus exact option model/control identity |
| Lifecycle | option task/effect and room progression |
| Next-decision seam | nested selection, remaining rest phase, or Map |
| Root/continuation | rest option is a root; upgrade/other selectors are continuations |
| Current workaround | button enablement and visibility publish options |
| Heuristic debt | model availability is not independently projected from presentation |
| Ritsu support | rest heal/smith hooks, not complete option authority or nested lineage |
| Missing evidence | native option provider T0/T1 and final artifact T3 |
| Migration verdict | direct typed adapter retained; migration pending |

## Treasure

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | current treasure room/chest state and generated relic choice collection |
| State source | chest-open stage, relic options, skip/completed state and room progression |
| Semantic action source | native chest/relic collection; current Platform reads exact chest/holders/proceed control |
| Native validator | current stage, holder membership and room-owned claim/proceed callbacks |
| Decision boundary | current treasure stage has one exact open/choose/skip/proceed operation |
| Presentation owner | chest control, relic holders and `NProceedButton` |
| Delivery seam | exact chest/holder/proceed callback |
| Exact binding | treasure room plus chest, relic collection/holder or proceed control |
| Lifecycle | chest generation, relic claim/skip and room completion |
| Next-decision seam | relic choice continuation, completed treasure phase, or Map |
| Root/continuation | open is a root; relic choice is its continuation; proceed starts room transition |
| Current workaround | stage is reconstructed from visible nodes and Proceed `IsSkip` |
| Heuristic debt | no native treasure lifecycle adapter; visibility currently participates in stage truth |
| Ritsu support | treasure generation hooks are not an exact claim/continuation contract |
| Missing evidence | preferred next lifecycle discriminator T0/T1/T3 |
| Migration verdict | direct presentation adapter retained; next lifecycle discriminator |

## Run Entry / Room And Act Transition

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | `StartRunLobby`, `RunManager`, `RunState` and native room/act lifecycle |
| State source | selected character/run configuration, active run identity, room and act transition state |
| Semantic action source | lobby choices before start; current room's domain adapter after start |
| Native validator | lobby/run setup constraints and game-owned room/act transition rules |
| Decision boundary | exact menu/lobby owner before run, then the current domain owner inside the run |
| Presentation owner | main/single-player/character-select/tutorial screens, then room/overlay controls |
| Delivery seam | exact menu/select/embark callbacks; game-owned room transition thereafter |
| Exact binding | menu/lobby/character object or current run/room identity |
| Lifecycle | run create/resume, room enter/exit and act transition |
| Next-decision seam | first room decision or the next domain owner after transition |
| Root/continuation | run entry is lifecycle control expressed through player UI; room decisions remain domain roots |
| Current workaround | several menu controls and private lobby binding define publication |
| Heuristic debt | run lifecycle and gameplay decision ownership are not yet represented by one neutral adapter |
| Ritsu support | broad room/act lifecycle and identity helpers, but no replacement for exact Platform action authority |
| Missing evidence | final artifact run-entry T2 and room/act transition T3 |
| Migration verdict | retain typed menu adapters; add lifecycle observations without moving process ownership from Host Runtime |

## Terminal / GameOver

| Field | Exact-version conclusion |
|---|---|
| Semantic owner | `RunManager.History`, terminal run state and `NGameOverScreen` stage |
| State source | win/loss, score/summary and terminal navigation state |
| Semantic action source | terminal summary advance/return controls; there is no gameplay successor after terminal |
| Native validator | current game-over stage and exact enabled control |
| Decision boundary | exact game-over overlay owns input and is at intro or summary stage |
| Presentation owner | `NGameOverScreen`, Continue and return controls |
| Delivery seam | exact Continue or return button callback |
| Exact binding | game-over screen plus stage-specific control |
| Lifecycle | summary animation/advance and return-to-menu/timeline |
| Next-decision seam | none for gameplay terminal; later menu state is a new lifecycle context |
| Root/continuation | terminal navigation, not a gameplay continuation |
| Current workaround | private score/animation fields and enabled controls classify stage |
| Heuristic debt | terminal semantic state and presentation stage are still co-located in one reader |
| Ritsu support | no exact terminal Player Environment replacement identified |
| Missing evidence | final artifact T2/T3 terminal journey |
| Migration verdict | typed terminal adapter retained; neutral terminal observation pending |

## Cross-Cutting Verdict

`Surface` is presentation classification. `NativeDecision` is semantic truth.
`current_ui_owned` means that the current UI owns input, never that it owns all
game legality. A decision-time capture may publish actions; execution-time
binding must resolve the same process-local operands and re-run native checks.
Only Connector BoundActions authorize mutation. Annotator consumes lifecycle
and decision facts read-only. Host Runtime owns process lifecycle only.
