# Recorder Causal Performance Baseline - 2026-08-29

## Evidence boundary

This is exact Human runtime evidence for the predecessor profiler artifact. It
establishes the lag baseline and the semantic regression oracle. The repair
described below has source/test evidence only until a new artifact is loaded and
owner-operated.

Latest closed owner session:

- session `session-20260829T072035Z-807f6a97b0e8498a828bb25c84e04ae4`;
- timeline `timeline-ebf451f9152d47c0a9636e74a032a385`;
- STS2 `v0.111.0 / 41cef1ea`, assembly
  `9cb4f1ad... / 57785517-0b16-42b9-8b36-bad6fb28384b`;
- unified artifact `f1afebd2... / a618ef18-b0a1-415a-b460-bf54d9c47048`;
- Connector source `54efe38d...`, Annotator source `42edd9d0...`;
- runtime `74c63f9afe454b63b34d9ca2f9f3f505`;
- environment `98390e13...`;
- sole exact Platform Modset `319c4fd2...`;
- Player Environment protocol `1.0.0`.

Independent Decision V2 audit passes 102/102 records with 119 explicit
invalidations. The semantic trace accounts for 267 accepted roots with 267
proved dispositions and zero unknown, cancel, abort or unresolved action. The
performance diagnosis therefore does not trade against a known semantic failure.

## Measured causal cost

The session spans 498.578 seconds. The Close-time profiler reports:

| Main-thread phase | calls | mean | p95 | max | total |
|---|---:|---:|---:|---:|---:|
| legacy combined `snapshot_probe` | 10,676 | 20.128 ms | 25.710 ms | 67.821 ms | 214.890 s |
| Read-rich Snapshot capture | 1,294 | 21.569 ms | 28.187 ms | 66.490 ms | 27.911 s |
| semantic Snapshot capture | 424 | 20.830 ms | 26.729 ms | 38.710 ms | 8.832 s |
| all Player Environment captures | 12,394 | - | - | 67.821 ms | 251.633 s |

All captures execute synchronously on the game process. They average 24.859
calls/second and consume 50.47% of recording wall time. The combined probe alone
consumes 43.11%. Source inspection identifies an unconditional 50 ms outer-loop
Snapshot build used to look for a legacy ledger recovery boundary even when
`RecoveryBoundaryRequired` is false. `PlayerEnvironmentNativeWitness.Capture()`
materializes a complete public Snapshot and BoundAction catalog; it is not a
cheap readiness probe.

Evidence appends contribute another 6.562 seconds (1.316% of wall time). The
important interaction cost is concentration: native-ledger, RunJournal,
Decision and invalidation streams each used `WriteThrough` plus `Flush(true)`
on the game thread, with typical calls around 3.8-4.5 ms and multiple calls per
Human action.

## Evidence footprint

The closed session contains 968 files and 21,991,002 bytes:

- normalized semantic event trace: 2,105,234 bytes;
- 737 immutable semantic frames: 11,395,248 bytes;
- Decision V2 compatibility projection: 3,391,183 bytes;
- native lifecycle compatibility ledger: 3,880,646 bytes;
- 222 unique Read blobs: 849,890 bytes.

The event/frame graph remains the canonical causal representation. Decision V2
and the native ledger remain compatibility/audit projections. Cold compression
is appropriate after Close; it cannot repair main-thread capture or fsync stalls.

## Current source repair

The repair keeps all native and semantic authority unchanged:

- frame work is now explicitly scheduled: a 50 ms recovery probe exists only
  while the ledger has real recovery debt;
- operational status refresh is requested by session/run lifecycle changes and
  reuses one Read-rich frame; idle recording no longer rebuilds Player
  Environment truth for status polling;
- semantic-boundary and legacy-successor probes remain bounded and now have
  separate profiler phase names;
- append-only streams write and flush to the OS on the hot path, then all streams
  receive one `Flush(true)` durable seal during Close;
- derived `coverage.json` is written at creation and Close rather than after
  every Read/action;
- the analyzer reports capture call rate, cumulative main-thread stall and
  evidence-append cost deterministically.

No H/S/A/S' role, native lifecycle callback, exact mapping, Read policy,
BoundAction authority, stale/identity check, disposition rule or audit invariant
changes. An interrupted/unclosed session is not a durable Human evidence seal.

## Non-claims and next gate

- No after-repair Human latency or frame-stall result exists yet.
- The source change predicts zero idle Snapshot polling, but no predicted count
  is promoted to runtime evidence.
- No exhaustive Full Run, target-picker cancel, generated skip or room-family
  qualification follows from this baseline.

The next gate is one short exact-artifact owner session. It must repeat ordinary
Play/End Turn plus at least one rapid chain, then Close. The same analyzer must
show the new phase split, zero unexplained idle polling, materially lower capture
calls/second and append latency, while semantic audit remains fully accounted.
