# H1 Exact Authority And Combat Mount Canary

Date: 2026-08-16

## Evidence boundary

This record separates two sources. Connector source
`d32c0cbaffaaf491ba9fce57b95968ea612dd8a2` supplied the only Live artifact in
this record: DLL SHA-256 `ea002cfb5ddd1e1f387ee759831e4614530aa56d4f821d0cee068c21f2c56c13`,
MVID `a3ece92a-044d-4000-a879-d0feda7d7c46`, protocol `1.0.0`. Later source in
this repository contains the combat-mount fix and has source/tests only until a
new exact artifact is cold-loaded. Evidence does not transfer between them.

The game was Windows x64 `v0.111.0/41cef1ea`, runtime assembly hash
`222455745`, `sts2.dll` SHA-256 `0861bfa1...`, MVID `73b63ee0...`, with only
`STS2_MCP` loaded. Windows remains a candidate, not supported.

## What the Live canary proved

The H0/menu-control run under runtime `8d12bf3c71324c8ca966742282442770`
reported the exact source, DLL and MVID above, `host_kind: headless`, exact
Connector-only Modset and `compatibility.status: canary_exact`. It passed one
menu delivery, duplicate-request idempotency, stale-snapshot refusal, successor
attribution and native shutdown without forced termination.

A second runtime `388435194f7849728fc4f139ea689c0e` used requested and observed
seed `H1RC1CANARY01`. It delivered three actions across `main_menu`,
`character_select` and `map_navigation`; seed provenance, Reads, receipts,
successors and shutdown containment passed with zero `unknown`.

## Failure and ownership

After map travel entered the first normal combat, the exact Snapshot contained
a CombatRoom model, player/enemy visible facts, `is_play_phase: false`, empty
hand and zero energy. No live combat input Surface matched, and the Host emitted
`visible_unsupported`. This was not a strategy error and not an unknown game
owner: the real runtime was between game-state combat start and live combat UI
mount. The deterministic consumer correctly stopped.

Exact decompilation confirms that `CombatManager.IsInProgress` describes the
turn-state lifecycle, while `NCombatRoom.Instance` and `NPlayerHand.Instance`
come from separately mounted Godot nodes. Static source does not by itself prove
their frame ordering; the Live trace proves the observable gap for this exact
runtime.

## Fix and non-claims

The owning fix classifies only a current CombatRoom with no blocking Surface,
known combat state and a not-yet-live `NCombatRoom` or `NPlayerHand` as setup
settling. A 20-second window bounds it. A mounted combat Surface resets the
window; a blocking owner is never masked; timeout returns to unsupported. Three
new lifecycle tests cover the observed gap and rejection boundaries.

This record does not prove the fix loaded, a combat action, a complete Journey,
Windows support, multi-worker reliability or H1.0. The fixed source requires a
new clean build, install, cold-load and same-seed journey.
