# PR25 Full-Run coverage inventory (reconciled 2026-09-10)

This is a source/evidence inventory, not an execution result. It does not
create an action registry or admission authority: Connector owns `BoundAction`
legality and delivery, STS2 owns rules/RNG/Commit, and Annotator correlates
accepted native witnesses and persists evidence.

## Evidence identities and source boundary

The current Windows native evidence is STS2 v0.111.0, source `41cef1ea`,
SHA-256
`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`,
MVID `73b63ee0-6c0a-47bb-b0d1-b21f6d94222e`, inspected with ILSpy
10.1.1.8388. The older macOS identity (`9cb4f1ad...`, MVID
`57785517...`) is historical evidence only and must not be presented as the
current Windows artifact.

The current integrated source boundary is
`agent/fullrun-integrated-review@148c20fe22d348d8ce9167a66ba0f8965500fc70`,
with accepted-ingress ancestry at `7513f705cc045e4c676617e21a976b7ea60b4234`
and nested-selector repairs through `f7607b9d7b9bf7f76879a7e205a8db0dd4e6d303`.
The nested chain is integrated and source/test closed; this inventory still
does not claim a loaded runtime or Human result for the current artifact.

## Accepted ingress and durable accounting in the integrated Windows candidate

Accepted game actions enter through the exact Harmony seam
`GameAction.OnEnqueued` (`AcceptedGameActionPatch.Postfix`), with lifecycle
correlation at `ActionExecutor.BeforeActionExecuted` and the exact action
types `PlayCardAction`, `EndPlayerTurnAction`, and `UsePotionAction`.
Direct UI ingress is named in `NativeUiPatches.cs`, including:

- `NRewardButton.OnRelease` -> `RewardsSetSynchronizer.SelectLocalReward` /
  `NCardRewardSelectionScreen.ShowScreen`;
- `NRewardsScreen.OnProceedButtonPressed` ->
  `RunManager.ProceedFromTerminalRewardsScreen` /
  `RewardsSetSynchronizer.SkipLocalRewardsSet`;
- `NPotionPopup.OnDiscardButtonPressed(NButton)` ->
  `DiscardPotionGameAction.ExecuteAction`;
- `ActionQueueSynchronizer.RequestEnqueue(GameAction)` for exact
  `DiscardPotionGameAction` and `VoteToMoveToNextActAction` carriers;
- `VoteToMoveToNextActAction.ExecuteAction` (`OnPlayerReady` commit);
- `RewardsSetSynchronizer.SkipLocalRewardsSet`,
  `NCardRewardSelectionScreen.SelectCard`, and the exact treasure, event,
  shop, and rest-room callbacks.

Durable accounting is source-backed by
`SemanticBoundaryTracker.BeginDurableMutation` (append checkpoint/rollback),
`MarkAuthoritativeAppend`, `PersistTrackerMutationOrUnknown`, and
`CurrentRecordingStore.AppendSemanticEvidenceEvents`. The completion path
uses `NativePostCommitCompletionLedger.PreviewTaskCompletion` followed by
`CommitTaskCompletion`, while `NativeUiCompletionRootBindings` retains the
exact carrier through durable settlement. `RecoverableAppendBatch.Write` is
available for append recovery. This inventory does not claim a serialized
`native-action-ledger.jsonl` is present, and does not introduce a second root,
ledger, or fabricated successor.

## Current source-supported families

These are source/test classifications in the integrated `148c20fe` candidate,
not Human/runtime PASS claims. Exact native owner and disposition remain
required for every accepted action.

| Family | Exact native owner / completion seam | Baseline status |
| --- | --- | --- |
| ordinary combat play/end turn/use potion | `PlayCardAction`, `EndPlayerTurnAction`, `UsePotionAction` lifecycle | InScopeImplemented |
| generated choice | `NChooseACardSelectionScreen.SelectHolder` and skip callback | InScopeImplemented |
| combat hand selector | `NPlayerHand` selection/confirm callbacks | InScopeImplemented |
| boss relic | boss relic selection/skip callbacks and reward completion | InScopeImplemented |
| map travel | `NMapScreen.OnMapPointSelectedLocally`, `VoteForMapCoordAction`/ready | InScopeImplemented |
| reward/card reward | reward synchronizer and typed card-reward owner | InScopeImplemented |
| treasure | `NTreasureRoom`, `PickRelicAction`, terminal proceed | InScopeImplemented |
| event option | `NEventRoom.OptionButtonClicked`, `EventOption.Chosen()` | InScopeImplemented (nested source/test closed; Human pending) |
| shop open/proceed/purchase/close | `NMerchantRoom.OpenInventory`/`HideScreen`, merchant entry task, `NMerchantInventory.Close` | InScopeImplemented (nested source/test closed; Human pending) |
| rest option/proceed | `RestSiteSynchronizer` option task and proceed callback | InScopeImplemented (nested source/test closed; Human pending) |
| act change | `VoteToMoveToNextActAction.ExecuteAction`, `OnPlayerReady` | InScopeImplemented |
| potion discard family | `NPotionPopup.OnDiscardButtonPressed`, `DiscardPotionGameAction.ExecuteAction` | InScopeImplemented for discard only |

The label `reward_potion_belt.discard_replace` is a compound family label for
the observed discard/reward path. It is not evidence of an atomic potion
replacement transaction: no atomic potion replacement implementation is
present in this source candidate.

## Nested-selector exact-context census

The integrated nested selector path adds explicit context resolution. The exact
context-bearing factory seams are:

- `CardSelectCmd.FromSimpleGridForRewards(PlayerChoiceContext, List<CardCreationResult>, Player, CardSelectorPrefs)`;
- `CardSelectCmd.FromSimpleGrid(PlayerChoiceContext, IReadOnlyList<CardModel>, Player, CardSelectorPrefs)`;
- `CardSelectCmd.FromCombatPile(PlayerChoiceContext, CardPile, Player, CardSelectorPrefs, Func<CardModel,bool>)`.

The context-free factories requiring an exact outer owner are
`FromDeckForUpgrade`, `FromDeckForTransformation`, `FromDeckForEnchantment`,
`FromDeckForRemoval`, `FromDeckGeneric`, and
`FromChooseABundleScreen`. The corresponding native screens are
`NSimpleCardSelectScreen.Create`, `NCombatPileCardSelectScreen.Create`,
`NDeckCardSelectScreen.Create`, `NDeckUpgradeSelectScreen.ShowScreen`,
`NDeckTransformSelectScreen.ShowScreen`, `NDeckEnchantSelectScreen.ShowScreen`,
and `NChooseABundleSelectionScreen.ShowScreen`.

The proposed exact outer registrations are `EventOption.Chosen`,
`Reward.SelectUnsynchronized`, `CardRemovalReward.OnSelect`, and the exact
`CardReward.OnSelect`/reward acquisition root. `GameAction` ambient fallback
is removed: an absent exact parent must fail closed. The census therefore
distinguishes an exact-parent fail-closed gate from actual Full-Run coverage;
coverage is not closed merely because an unregistered generic selector is
rejected.

Vanilla card-removal paths have two distinct parents:

- `CardRemovalReward.OnSelect` ->
  `RewardSynchronizer.DoUnsyncedCardRemoval` ->
  `CardSelectCmd.FromDeckForRemoval`;
- `MerchantCardRemovalEntry.OnTryPurchaseWrapper` ->
  `OneOffSynchronizer.DoMerchantCardRemoval` ->
  `CardSelectCmd.FromDeckForRemoval`.

The first is the nested `reward_card_removal` candidate surface. The second is
the shop inventory nested surface. Both require explicit parent registration;
neither may be inferred from a generic `GameAction` carrier.

Card-reward alternatives are also distinct from ordinary card selection:
`CardReward.OnSelect` presents `NCardRewardSelectionScreen` and awaits
`OptionSelected`; `CardRewardAlternative.Generate` can add `Skip` and
`REROLL`, while `PaelsWing.TryModifyCardRewardAlternatives` adds `SACRIFICE`.
`REROLL` refreshes the same screen and leaves the outer reward task open until
the eventual terminal selection. `REROLL` is itself an accepted nonterminal
operation and must be atomically accounted for separately from the eventual
terminal reward settlement (`Skip`, `SACRIFICE`, or card selection). The
candidate must preserve this one outer scope and dispose it only at settlement.

The exact native facts matter for admission. `NDeckTransformSelectScreen` has
a main `ConfirmSelection` which, when `RequireManualConfirmation` is true,
opens the preview; the terminal callback is the preview's
`CompleteSelection`. `Claws.cs` uses this manual-confirmation path. `SeaGlass.cs`
uses `CardSelectCmd.FromSimpleGridForRewards` with `CardSelectorPrefs` min `0`,
so an empty selection is valid.

## Native non-decisions and intentionally out-of-envelope paths

Target-picker cancel before enqueue is a native nondecision: no accepted
action, exact parent, or terminal completion exists to bind or queue. It is
therefore excluded from the canonical action envelope by semantics, not because
the nested candidate is incomplete. Dialogue/presentation-only callbacks and
run-setup provenance are likewise not gameplay decisions in this inventory.

## Integrated nested selector source/test status

The generic simple/deck/combat-pile/bundle selectors requiring an exact parent,
shop card-removal nested selector, event nested selector, rest nested selector,
reward nested replacement, and `CardRemovalReward` nested removal are now
integrated and covered by source/conformance tests. Exact parent registration
remains mandatory; an absent parent fails closed and is never inferred from an
ambient `GameAction`. The terminal reservation is terminal-only for each exact
screen: `NDeckTransformSelectScreen.ConfirmSelection` opens the manual preview,
while `CompleteSelection` settles the `Claws` path. Valid min-0 `SeaGlass`
empty completion is admitted as a successful native continuation.

These are source/test results for the `148c20fe` candidate only. No exact
current-artifact install/load, runtime, or Human PASS is claimed. Historical
macOS artifacts, older Windows runs, and the failed six-root session do not
transfer to the current Windows identity.

## Evidence status

The inventory is reconciled against integrated source and exact native seam
evidence only. It is not a replacement for the Full-Run cold-load/Human
evidence packet.
For current-artifact runtime/Human qualification, the evidence packet must separately show
accepted ingress, exact parent binding, terminal reservation -> durable append
-> consume, carrier retention on failure, native fault versus cancel, both
Reroll signals with scope disposal, `accepted=false` no bind/queue, callback
barrier completion, and one root/ledger. Until then these are acceptance
criteria, not results.

## Historical failed-session forensic disposition (not qualification)

The prior Windows session
`session-20260903T102650Z-50552cf165a8439397b71d7a1967f957` is rejected for
Human retest and does not qualify any candidate. Its manifest SHA is
`70897d4ed041c92744be5ff8180f10ef94f3d84b7c3306a18e75aecf1ea99879`.
The raw coverage is 76 admitted records, 124 invalidations, 1,058 materialized
reads and zero failed reads. The invalidation reasons are 64
`semantic_pre_frame_capture_failed`, 44 `native_task_binding_no_match`, 15
`native_task_binding_ambiguous` and one `pre_frame_capture_failed`.

The six accepted roots that started but never received a final disposition are
listed below by their stable action witness. They are intentionally retained
as unresolved evidence rather than backfilled or transferred:

| sequence | root | native seam | bound label | final raw state |
| ---: | --- | --- | --- | --- |
| 202 | `ui-root-7c2927d5392f43b7b2428bc731de6b03` | `NRewardsScreen.OnProceedButtonPressed` | Continue from rewards | `action_started` only |
| 220 | `ui-root-4030144f9d984f30809715b6d1b9c495` | `NRewardsScreen.OnProceedButtonPressed` | Skip remaining rewards and continue | `action_started` only |
| 242 | `ui-root-b163490db86349d49ce7f48f5675bc56` | `NRewardsScreen.OnProceedButtonPressed` | Skip remaining rewards and continue | `action_started` only |
| 264 | `ui-root-2dde6c35482c467b98407435d415a68c` | `NRewardsScreen.OnProceedButtonPressed` | Continue from rewards | `action_started` only |
| 270 | `ui-root-074bf651491447fbafcc65efc318bf8a` | `NTreasureRoom.OnProceedButtonPressed` | Continue from the treasure room | `action_started` only |
| 298 | `ui-root-12283f649bd44f128e2361b8323d2ecb` | `NRewardsScreen.OnProceedButtonPressed` | Continue from rewards | `action_started` only |

The same trace contains one separately classified `transition_unknown` potion
root (sequence 86) and a final PlayerChoice parent (sequence 304) that ends at
`action_resumed`; neither is silently promoted to a terminal disposition.
Map travel in this exact raw session is represented by native
`VoteForMapCoordAction` roots and has no `membership_unknown` invalidation;
that task note is stale relative to this raw evidence. The terminal tail was
only the polling journal entry `run_ended` followed by close; no native
`RunManager.OnEnded(bool)` marker was present.

Exact shipped decomp confirms the owning seams: event option completion is
`EventOption.Chosen()`, rest options return `RestSiteSynchronizer.ChooseLocalOption`
after `NRestSiteButton.OnRelease` disables options, reward/treasure continuation
uses `RunManager.ProceedFromTerminalRewardsScreen`, and victory/defeat converge
at `RunManager.OnEnded(bool)`. The current repair therefore carries exact root
identity through the shared async callbacks, captures rest-site pre-frame at
the button boundary, aligns observations to the public `activate/open/cancel`
projection, and records terminal evidence only from `OnEnded`. Polling remains
an explicitly unproved lifecycle note; it cannot publish `RunEnded` or settle a
root. These changes are source/test evidence only until a fresh exact build,
cold load and new runtime session prove them.
