# PR #6 Successor Owner-Ready Source Closeout

Date: 2026-09-01  
Evidence level: exact-native inspection, source and deterministic tests. Exact
clean build/install/load and Human qualification are pending.

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
warnings. Final exact candidate identities and load evidence will be appended
after the clean committed build.

Because production bytes changed, prior Human sessions remain historical
evidence only. The shortest new gate is exact Map travel into Combat, wait until
cards/end-turn are genuinely usable, perform no next Human gameplay action,
then Close. The Map root must contain typed owner-ready evidence, become one
canonical transition, and Close with no unresolved lifecycle. One ordinary
continuous action afterward in a separate short recording is the bounded
double-settlement regression.
