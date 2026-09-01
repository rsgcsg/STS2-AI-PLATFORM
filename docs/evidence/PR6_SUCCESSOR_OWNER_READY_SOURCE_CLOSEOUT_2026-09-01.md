# PR #6 Successor Owner-Ready Source Closeout

Date: 2026-09-01  
Evidence level: exact-native inspection, source, deterministic tests, exact
clean build, safe install and cold-load. Human qualification is pending.

## Counterexample and root cause

On repaired artifact `641f543d... / 7ac2fa24...`, exact Map Commit was
observed and STS2 reached a playable Combat, but closing Recorder before the
next Human action left the Map root unresolved. `CanOpenNextRoot` correctly
admitted a future execution handoff; it did not prove a successor. Core/tests
defined `NativeDecisionOwnerReady`, but production had no publisher, so Close
could only retain `recording_close_drain_timeout` unknown evidence.

Exact shipped `v0.111.0` call order rejects `RunManager.RoomEntered`,
`CombatManager.CombatBegan`, and `NCombatRoom.TransitionToActiveCombat`: they
precede the complete player play phase. In the player branch of
`CombatManager.StartTurn`, STS2 first reaches `PlayerTurnPhase.Play`, checks
combat outcome, unpauses `ActionExecutor`, sets
`ActionQueueSynchronizer.PlayPhase`, and fires `TurnStarted`.
`NEndTurnButton.OnTurnStarted` then refreshes the exact input owner. Its Postfix
is the first low-complexity seam where both native decision state and the
presentation needed by Connector have been established.

## Repair

Native Foundation now emits one typed process-local Combat owner-ready fact
from that exact Postfix, after rejecting stale, enemy-side, non-current and
non-semantic-play-phase callbacks. Annotator synchronously captures the
Connector frame, requires complete state, Reads and catalog, requires the
frame domain to match the typed owner, persists exact owner witness/type and
native mechanism, and only then offers the boundary to the existing tracker.

The event does not authorize actions or claim `S'`. Incomplete or mismatched
capture produces no proof. A later next-root execution cannot double-settle an
already disposed predecessor. No polling, timer, frame delay, UI-stability,
queue-idle, FIFO/count/current-root guess, retry or backfill path exists.

## Architecture judgment

`Human Root -> exact Native Commit -> causal Successor Boundary` remains the
shared core. The missing piece was production wiring plus durable evidence for
one already-designed boundary class, not a new ledger or Map state machine.
Different domains may add exact typed publishers only after their own call
order proves both native owner readiness and Connector capture readiness.
Until then, continuous gameplay uses next-root pre-execution handoff and an
explicit Recorder Close retains a final committed root as unknown; it does not
promote terminal truncation to success.

Shop, Event, Rest, Run Entry and non-Combat terminal transitions still require
typed domain adapters/seams. This repair does not require them to modify root
identity, global admission, Commit semantics, completion ledgers or Close.

## Automated evidence and next gate

Annotator Core passes 121 tests, including missing/mismatched/incomplete owner
evidence and owner-ready-versus-next-root double settlement. The persisted
root/Commit/owner-ready path audits end to end. Game-Mod boundary tests pass 45
tests and exact compilation resolves the private shipped method with zero
warnings. Root portable and exact-game checks pass.

Clean build workspace `7dd4e5a34c0e426ba54d2baf02420d8e0db08691`
contains semantic source `c1b3144f07ba210c7f7064087d0d37bb3c9a2e66`.
The unified artifact is
`627b5b6980c8477bbfd42631140e18dd75177299c2c84ff650854024fbae4858 /
f19b8863-a15f-4574-89ac-68954fee5944`. Safe install and cold-load pass in
Connector runtime `c296ec997abd4d7dbbfbfa1ec74596f1`, environment
`0c47c311da4d775232e9e920a70044a5f5ca4aad38555fefe82b5313f332816e`,
and exact sole-Platform Modset
`c8dd91e3283d347f17f886b9ad81063ccb9660dc3d4637cffb8b02b5763d67ce`.
Recorder is Ready with no open session. Rollback is
`apps/game-mod/.local/deployments/2026-09-01T05-17-12.536Z`. Startup contains
no Platform, Harmony, Native Foundation or Annotator error.

Because production bytes changed, prior Human sessions remain historical
evidence only. The shortest new gate is exact Map travel into Combat, wait until
cards/end-turn are genuinely usable, perform no next Human gameplay action,
then Close. The Map root must contain typed owner-ready evidence, become one
canonical transition, and Close with no unresolved lifecycle. One ordinary
continuous action afterward in a separate short recording is the bounded
double-settlement regression.
