# PR25 terminal owner-ready audit and repair

G5: Native Foundation owns the exact game-over input-ready fact; Game Mod
composes its read-only native patches; Annotator retains its existing sole
causal tracker, projection and immutable evidence contracts. Base develop is
`cd9a0fbb0c85577a13513abe715f91d794ac86eb`; audited PR head is
`5371c671c7ffc029cc2af2bf2c83000f2d7bd573`. PR25 remains Draft, no merge.
STPD remains an independent consumer and is unchanged.

## Exact Human evidence

Closed session `session-20260911T042149Z-1c150ff64bb34482bc79bccb27aba4ca`
was recorded by Annotator `5fad893846d3f87980f45b3f1f59961719bf37ac`,
Connector `64c13f5ca4bd59478305754d460dea1737e99ffb`, common DLL SHA256
`cb7515cca0a0e014567f68053ac1ca9c56ad987f0fbb5dc21d8134111c7e6396`,
MVID `2a0275b3-d1f7-4846-b178-ca963e034570`, runtime
`db56bf55712c483f9fc03f963ca38807`. Game v0.111.0 / 41cef1ea, assembly
SHA256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
All 2,357 session files retain their original hashes. Local receipts and exact
native source inspection are under `.local/pr25-newcanary`; raw data is not Git.

| Population | Count |
| --- | ---: |
| Accepted decision occurrences | 647 |
| Proved / durable canonical / strict semantic candidates | 645 / 645 / 645 |
| Cancelled before native execution | 1 |
| Successor unresolved | 1 |
| Explicit invalidations | 73 |
| Native execution diagnostic exact / unknown | 504 / 0 |
| Native starts / ends | 1 / 1 |
| Legacy audit valid / invalid | 198 / 0 |

All 73 invalidations are `human_action_native_type_mismatch`: 46 internal
ReadyToBeginEnemyTurnAction and 27 internal MoveToMapCoordAction callbacks.
There are zero real Human capture/projection invalidations and no pre-frame
failures. All four native-input rapid PlayCards and all 53 selector decisions
are canonical. No semantic state/action-space gap remains in this session.

Discard #579 succeeds with reward_claim as execution input domain, proving
that the native potion-belt catalog survives removal of its popup. This sample
does not exercise the prior card-reward-underlay failure. All 15 reward Proceed
and the act-change decision are canonical; nonterminal reward-tree variants
not identified by exact evidence remain unqualified.

The cancelled PlayCard #524 is retained with exact card/target and cancellation;
STS2 cancelled before execution. It is not a lost successful action, and must
not become a successful canonical transition just to equal the accepted count.

## Confirmed first missing fact

The sole unknown is #647 EndPlayerTurnAction. Its exact execution frame,
native execution catalog and GameAction.Finished Commit exist. Native OnEnded
records defeat at 04:40:46.237835Z; Close at 04:40:53.469798Z leaves
`session_closed_before_successor_boundary`. No disk failure or rapid-input
correlation failure occurred. The record is correctly fail-closed for the old
producer's missing successor witness.

Exact native v0.111.0 source shows:

1. EndPlayerTurnAction marks readiness; its Finished does not encompass enemy
   turns or death processing.
2. RunManager.OnEnded records terminal run data before the game-over input
   screen is ready. Its journal marker is not an action successor.
3. NGameOverScreen.Create receives the exact RunState. AnimateIn later enables
   its intro Continue control; NGameOverContinueButton.OnEnable sets native
   enabled/visible state. This is an actual owner-ready callback, not elapsed
   animation time or a periodic interactive observation.
4. Connector already projects the game_over surface, complete public catalog,
   persistent visible state and run_deck Read. The missing fact was the native
   readiness notification to the existing recorder boundary observer.

## Repair and failure model

NativeDecisionOwnerReadyProvider weakly binds the factory-created terminal
screen to its exact RunState and observes the exact intro button's OnEnable
Postfix. It rejects foreign buttons (including leaderboard), unregistered or
stale screen/run identity, non-current screen context, incomplete native input
readiness, multiplayer/nonstandard games, cleanup and abandoned runs.
Game Mod installs only read-only factory and OnEnable Postfixes.

The observer then uses the existing complete Connector capture and typed
`native_decision_owner_ready` evidence. SemanticBoundaryTracker remains the
only causal authority: native Commit, execution S, no intervening Human effect,
complete successor/Reads and a distinct Snapshot are still required. The native
provider emits no Human action identity and does not look up a latest root;
causal settlement follows the same existing owner-ready rules as combat turns.
There is no synthetic terminal state, game mutation, new GameAction, timer,
Task wrapper, automatic retry or historical backfill. Run-end journaling is
unchanged and cannot settle anything on its own.

## Verification and remaining limits

Regression coverage includes committed final action to game_over, missing
Commit, status polling, missing Reads, an unrecorded Human-effect barrier,
duplicate native-ready delivery, tracker-to-canonical persistence and independent
final audit. Exact native source verifies Create/Enable ordering and input state;
source guards require the factory/run/control checks and composition seams.
Root gates, exact-game build, final CI, clean component/BOM identities and
installed/loaded receipts are recorded on the final Draft PR head.

This fixes a source-supported terminal boundary gap, not a fresh Human terminal
qualification. An early Close before native intro readiness, unsupported terminal
mode or failed frame capture still remains unknown. The previous reward-tree
parent ownership gap for enclosing shop/event rewards is not exercised away by
this session and remains open. Arbitrary navigation after game over is outside
this bounded Human collection claim. Accepted-and-cancelled actions retain their
own disposition; an all-valid Full Run is not claimed.

Next bounded Human canary: record through a natural defeat, leave recording
open until native Continue is visible, then Close without another gameplay
input; expect the final committed action to have a game_over successor and no
terminal unknown. Separately exercise a merchant/event reward tree and a short
rapid-card/selector regression. Rollback uses the exact previous Game Mod
snapshot; old Human bytes never qualify the new artifact.
