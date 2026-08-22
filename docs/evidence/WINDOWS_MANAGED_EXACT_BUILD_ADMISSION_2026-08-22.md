# Windows Managed Exact Build Admission - 2026-08-22

This record admits a Windows x64 build as a new experimental candidate. It
does not extend or inherit the macOS STPD v0 operational freeze.

## Exact identities

- Headless parent before these portability changes: `29e5778`;
- game: Windows x64 STS2 `v0.111.0`, commit `41cef1ea`;
- executable SHA-256:
  `8602c26bffd2937e3841835fd8360ef8e974624a543e05977229fd3d062be231`;
- runtime assembly hash: `222455745`;
- `sts2.dll` SHA-256:
  `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`;
- `GodotSharp.dll` SHA-256:
  `0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289`;
- upstream: `wuhao21/sts2-cli@d11aa883b582dd68bd39b331f3370746b30d447e`;
- admitted source patch SHA-256:
  `8ced088bffbfeaa378d4580ca2c254c41cd89ae58b5b1e9b6ee1a764f8eef87e`;
- Windows Managed Host SHA-256:
  `0d8c916365f0a64a0ed5cfc706186811e33708c841fef82e1f73c6a33dcfcc4d`;
- Windows Managed Host MVID: `387bc1a3-5a3f-4f76-babb-698a770cdb8b`;
- Host size: `148480` bytes;
- original and runtime `sts2.dll` bytes matched exactly.

Two independent Windows builds produced the same Host SHA/MVID before the
baseline was admitted. A subsequent clean prepare and audit reproduced the
frozen tuple.

## Windows bootstrap corrections

The first fail-closed attempts exposed platform mechanics rather than game or
semantic mismatches:

1. global `core.autocrlf` converted the pinned patch and Bash source;
2. Windows `bash.exe` resolved to WSL and could not consume a Windows process
   path;
3. this Git build's broad `git apply --intent-to-add` rewrote unrelated index
   entries;
4. a no-checkout clone could remain unmaterialized when the pinned revision
   already equaled upstream HEAD.

Preparation now freezes LF patch input, clones with `core.autocrlf=false`,
uses native Windows copies for the already enumerated exact DLL setup, checks
out a materialized tree, applies normally, and marks only patch-declared new
files as intent-to-add. Source diff, unmodified game assembly, artifact SHA,
and MVID remain mandatory.

## Bounded Windows runtime measurement

After commit `ca598364ce9eb638d1fdd75a16e1817621635cfe` made the Windows
bootstrap source-clean, the exact generated candidate was exercised through
the Managed Player Environment probe. The raw report remains local at:

`.local/evidence/managed-player-environment-2026-08-22T08-19-39-382Z/report.json`

For requested seed `STPDWINPE00001`, the game reported the exact same seed.
The bounded run delivered all 64 of 64 attempted canonical actions, completed
122 canonical Reads, recorded 64 of 64 complete action decisions and zero
partial decisions, and ended at the configured action limit with a complete
combat successor. The process exited zero and reported no episode failure.

Measured qualification-profile performance was 145.64 delivered canonical
decisions per second over the bounded decision window, with 118,476,800 bytes
peak RSS. This result is reported only for its exact machine, process, probe,
and candidate tuple. The raw-speed unit remains
`canonical_player_environment_decision_unqualified`.

The hard-shell verdict was `bounded_integrity_pass`; semantic conformance and
H1 admission were both `not_evaluated`. Runtime diagnostics also retained the
CoreCLR 10 patch warnings and other candidate diagnostics rather than treating
them as qualified away.

## Evidence boundary

The generated candidates and raw reports stay under ignored `.local/`. Build
and one bounded runtime admission do not prove H1.0, cross-platform parity,
Player Environment semantic conformance, recovery, soak, policy quality, or
training readiness. Those qualifications remain open under this Windows tuple.
