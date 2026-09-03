# Native Restore And Twin Runtime Audit - 2026-08-30

## Evidence boundary

This is an exact-source architecture audit, not Live gameplay evidence. ILSpy
9.1 inspected the installed STS2 `v0.111.0 / 41cef1ea` assembly
`9cb4f1ad... / 57785517-0b16-42b9-8b36-bad6fb28384b`. Decompiled output
remained local and is not part of the repository.

## Findings

- `NetFullCombatState` is a multiplayer checksum/divergence payload. Its own
  source notes that rewards, shop RNG and rarity odds are omitted. It is not a
  complete decision-boundary snapshot and exposes no authoritative restore.
- `RunManager.StateDiverged` reports or aborts on divergence. It does not load
  the received state into the current run.
- combat replay rebuilds from a room-initial `SerializableRun` and replays
  events. It does not clone or restore an arbitrary current Human boundary.
- normal save/load rebuilds scenes and re-enters the latest map room. It is an
  episode lifecycle path, not an in-place action-boundary rollback primitive.
- Platform Host reset/recovery is process or episode scoped and already states
  that it does not prove arbitrary in-place save recovery.

Therefore a shadow/twin runtime cannot cheaply receive an exact current Live
boundary and execute one action without either replaying substantial history or
building a new state reconstruction layer. Such a layer would add semantic
drift, maintenance and identity cost before it could reduce Recorder latency.

## Decision

- `TWIN_RUNTIME_PRIMARY_COLLECTOR_REJECTED_ON_COST`.
- `TWIN_RUNTIME_RETAINED_AS_DIFFERENTIAL_TOOL` for bounded, exact-pinned
  comparisons where its start state is independently established.
- `SERIALIZED_INPUT_IMPLEMENTATION_AUTHORIZED` because it keeps STS2 as the
  sole rules/RNG/effects owner and needs no state restore.

The built-in console may remain an investigation aid. It is not a restore
authority, Player Environment surface, or proof of semantic parity.

## Non-claims

This audit does not prove that no future STS2 build can expose a suitable
snapshot/restore seam. It does not qualify a twin runtime, replay all room
families, or transfer Reference evidence to Managed Exact.
