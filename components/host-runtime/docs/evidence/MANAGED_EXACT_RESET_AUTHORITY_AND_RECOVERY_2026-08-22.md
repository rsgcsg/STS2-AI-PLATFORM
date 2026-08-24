# Managed Exact Reset Authority And Recovery

Date: 2026-08-22

Verdict: **bounded reset authority, request idempotency, unknown-no-retry and
exact process replacement passed. H1.0 remains incomplete.**

## Exact identity

- Headless source: `9e282de`
- candidate patch: `53cf5872...`
- candidate Host: `b0794fe7...`, MVID `badc4c67-3e6c-4d0e-adb7-fa3e5315cb6c`
- STS2: macOS arm64 `v0.111.0` / `41cef1ea`, assembly `9cb4f1a...`, MVID
  `57785517-0b16-42b9-8b36-bad6fb28384b`

Local reports:

- `.local/evidence/managed-player-environment-2026-08-21T17-56-28-372Z/report.json`
- `.local/evidence/managed-recovery-2026-08-21T17-56-30-926Z/report.json`

## Same-state reset

Ten in-process episodes intentionally reused seed `H1RESETEXACT02`. Every
episode reached `game_over` with the same 167-action trajectory. The probe
delivered 1,670/1,670 actions, completed 3,110 Reads, and observed no partial
canonical decision.

Each reset had to report the requested game-owned seed with no pending native
operation. Snapshot, interaction and BoundAction authority include a monotonic
session sequence, so identical raw game state cannot resurrect an earlier
episode's authority. All 9 old-episode submissions were rejected as
`stale_snapshot` before native execution. The first request in all 10 episodes
was replayed and returned the exact original Receipt without a second native
mutation.

## Process loss and replacement

The recovery probe suspended the exact process, wrote one mutation request,
then killed the process before a response could establish delivery. The
session returned `unknown` with retry disabled, replayed that exact unknown for
the duplicate request, and rejected a different request with
`runtime_tainted_after_unknown`.

A new process using the same exact artifact and environment fingerprint then
mounted the same requested seed and delivered a current BoundAction with a
successor. Its PID and adapter runtime instance differed from the failed
process.

## Evidence boundary

This proves bounded in-process reset isolation and conservative recovery from
ambiguous transport loss. It does not prove that native Commit occurred before
the injected loss, long-soak reliability, one million decisions, cross-machine
recovery, or broad semantic parity.
