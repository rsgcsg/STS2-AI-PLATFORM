# PR25 native map diagnostic presentation repair

G5 evidence presentation follow-up to PR25 head
`470ff9a28f0890c4da572a39bea1630878235c80`, base develop
`cd9a0fbb0c85577a13513abe715f91d794ac86eb`.

The screenshot's feed row #14 is the sole invalidation in closed session
`session-20260910T211207Z-245793054228456d96739fa5c542a3b9`:
`human_action_native_type_mismatch / MoveToMapCoordAction`. Its native Human
map choice is canonical decision #3, action witness `game_action_c0c1488f_5`.
The feed row number is not the decision sequence. The last Strike is separately
unresolved because the recorder was closed before its successor boundary.

Exact STS2 v0.111.0 / 41cef1ea source confirms:
VoteForMapCoordAction.ExecuteAction calls MapSelectionSynchronizer.PlayerVotedForMapCoord;
when all votes are present and the process is not a Client, the synchronizer
creates and enqueues MoveToMapCoordAction to enter the room. That internal action
is not a second Human decision. Recorder already explicitly flags its mismatch
as IsDiagnostic and excludes it from the unrecorded-Human-effect barrier.

The first incorrect presentation fact was FormatEntry/FormatLifecycleDetail and
row styling ignoring IsDiagnostic, and feed failure counts counting diagnostic
invalidations as Human capture failures. UI now gives explicit native diagnostics
a neutral retained status and border, counts them separately by event identity,
and avoids displaying their archived occurrence payload as a Human input.
Real non-diagnostic invalidations remain failures. Session counters continue to
show the immutable raw invalidation total, explicitly labelled as including
native diagnostics; a retained feed never subtracts from authoritative totals.

This does not erase raw invalidations, synthesize a successful root, change map
legality/Commit/correlation, change STPD, or qualify a Full Run. The same display
fix covers the already-tagged ReadyToBeginEnemyTurnAction diagnostic. Unknown
and actual failed Human captures remain visible. Historical audit still preserves
all original evidence. Focused tests cover both diagnostic types, mixed real
failures, event counts, detail/row labels and neutral styling. Root/exact-game
and final-head CI results and loaded identity are retained in local receipts.

Rollback is the owning Game Mod deployment snapshot. No active Human run should
be terminated merely to replace this presentation artifact. Next bounded canary:
select a map node, inspect the diagnostic and successful Human map root; retain
one real failed capture to confirm its red failure status is unchanged.
