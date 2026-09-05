# Exact Native Seam Notes

These findings are bound to the exact Windows STS2 `v0.111.0`, commit
`41cef1ea`, `sts2.dll` SHA-256
`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`,
MVID `73b63ee0-6c0a-47bb-b0d1-b21f6d94222e`.  The earlier macOS assembly
identity is historical and is not evidence for this Windows candidate.

- `NMouseCardPlay.StartAsync` reaches `NCardPlay.TryPlayCard(target)`.
- `NPlayerHand.StartCardPlay` receives the exact `NHandCardHolder` before the
  holder is moved out of the active hand; this is the last stable pre-action
  freeze point for both mouse and controller card play.
- `NCardPlay.TryPlayCard` uses native checks and `CardModel.TryManualPlay`.
- accepted manual play requests `ActionQueueSynchronizer.RequestEnqueue` with a
  `PlayCardAction`; `NetCombatCard.ToCardModelOrNull()` and `Target` expose the
  exact native operands already accepted by the game.
- `NEndTurnButton.CallReleaseLogic` and `SecretEndTurnLogicViaFtue` request an
  `EndPlayerTurnAction` through the same queue.
- `NCardPlayQueue.OnLocalCardPlayed` removes the accepted holder from
  `NPlayerHand.ActiveHolders`; a Snapshot captured only at `TryPlayCard` can
  therefore be complete yet correctly omit the card being committed.
- `ActionQueueSynchronizer.RequestEnqueue` is process-global. Runtime evidence
  shows that a same-type game-owned `PlayCardAction` can be observed during the
  short native UI scope, so native type cannot claim the human root; exact
  frozen card/target matching must happen first.
- `ActionQueueSet.EnqueueWithoutSynchronizing` calls
  `GameAction.OnEnqueued(PopAction, GetAndIncrementActionId())` before
  `ActionEnqueued`, native queue cancellation checks, queue insertion and
  `ActionQueueChanged`. A Postfix on `OnEnqueued` therefore observes the exact
  assigned ID/state early enough to subscribe before started/cancelled/finished
  events can occur.
- `GameAction` exposes game-owned `BeforeExecuted`, player-choice
  pause/ready/resume, `BeforeCancelled` and `AfterFinished` events. These facts
  prove native lifecycle, not business completion.
- `ActionExecutor` executes ready actions serially and emits
  `BeforeActionExecuted` immediately before the selected action's `Execute`. It
  performs game-owned win-condition handling after each action before selecting
  another. Capturing immediately before the next tracked Human action therefore
  excludes that next action's effect while retaining prior continuation.
- Exact owner evidence shows that acceptance and execution order can differ
  around player-choice pause: a later accepted source-local choice can execute
  before an earlier queued `GameAction`. Semantic coordination must therefore
  use observed execution order. A precommit can be rebound only from the
  complete authoritative capture immediately before its own execution; its
  earlier Human observation cannot remain semantic S across an intervening
  Human effect.
- `EndPlayerTurnAction.ExecuteAction` only invokes `PlayerCmd.EndTurn`; its
  `Finished` event does not prove enemy-turn and next-turn settlement. A valid
  End Turn successor must be a later complete player decision boundary.
- `GenericHookGameAction` can carry player-choice work inside a parent action.
  A complete choice surface may therefore be a semantic decision boundary even
  while the parent native action is paused rather than finished.
- `PlayCardAction.ExecuteAction` has one exact no-Commit branch: if its card is
  no longer in a pile/hand, it calls
  `NCardPlayQueue.RemoveCardFromQueueForCancellation(this)` and returns without
  `GameAction.Cancel()`, resource spending or `OnPlay`. The action later reports
  `Finished`. A narrow read-only Prefix on that exact overload distinguishes
  this abort from successful execution; explicit native Cancel is ignored
  because its state is already `Canceled`.
- The semantic observer subscribes to `ActionExecutor.BeforeActionExecuted` and
  uses the single narrow Prefix above. No transpiler, scheduler change, sleep,
  timing heuristic, argument/result mutation or reconstructed gameplay rule is
  used.
- `NetFullCombatState` is a divergence/checksum representation and explicitly
  omits reward/shop RNG facts. `RunManager.StateDiverged` does not restore it.
  Combat replay starts from room-initial `SerializableRun`; ordinary save/load
  rebuilds scenes and re-enters the latest map room. Exact v0.111.0 therefore
  exposes no complete arbitrary decision-boundary clone/restore primitive for a
  low-cost twin collector.

## Full-Run room and terminal seams

The same Windows assembly was decompiled with ILSpy 10.1.1.8388.  The exact
room callback shapes are:

- `NEventRoom.OptionButtonClicked(EventOption,int)` invokes
  `EventOption.Chosen()` (or the event synchronizer) from the native button;
  the `EventOption.Chosen` task is the completion seam.
- `NRestSiteButton.OnRelease` sets its executing flag and disables visible
  options before awaiting `RestSiteSynchronizer.ChooseLocalOption(int)`.  The
  button callback is therefore the last interactive pre-frame seam; the task
  is the later native completion.
- `NRestSiteRoom.OnProceedButtonReleased(NButton)` opens the native map screen
  and is a direct control observation, not a successor proof.
- `NRewardsScreen.OnProceedButtonPressed` routes either through
  `RewardsSetSynchronizer.SkipLocalRewardsSet()` or
  `RunManager.ProceedFromTerminalRewardsScreen()` depending on the native
  reward state.  The shared terminal-proceed callback is bound by exact root
  identity and native operand.
- `RunManager.WinRun()` and the native defeat command converge at
  `RunManager.OnEnded(bool)`.  The Annotator observes that method as a terminal
  marker only; it never infers a successor, reward outcome or action legality
  from `IsInProgress` polling.

These are implementation evidence, not current Live evidence. Decompiled source
and proprietary assemblies are not committed.
