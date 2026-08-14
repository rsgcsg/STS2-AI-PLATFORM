# Current Status

Release candidate: `1.0.0-rc.2`

Player Environment protocol: `1.0-rc.2`

Verdict: **C1 RC2 is a published, runtime-sealed prerelease and the temporary
Player Environment freeze baseline**

## Implemented

- standalone Host, machine contract, TypeScript SDK and optional MCP adapter;
- single fair-player Observe/Read/Interact production path;
- complete finite BoundAction authority with Host-local native operands;
- execute-time rediscovery/revalidation, one controller and request
  idempotency;
- `delivered / not_delivered / unknown` receipts and immediate successor;
- state-bound non-authorizing Reads and default-off native-page evidence;
- exact-game tooltip subtype drift check and explicit information limits;
- standalone doctor/build/deploy/verify/rollback tooling;
- a strategy-free SDK helper for coherence-checked eager Read aggregation.

## Evidence State

The recommended release is source/tag
`547c9addac624f7df363a93a3873ee1c2062ecc3` / `v1.0.0-rc.2`:

- source digest: `a04ae9cf77ea22532e27a120b6df4ed5975e40f9ce6f4699bf6503ca17de4484`;
- DLL SHA-256: `cf7ed1454437cb796f5931b361f655222d2f3f2e3da3a21f038a752694645cc6`;
- MVID: `6824e21d-7486-40fd-a131-43e789fdc8d2`;
- exact game: `v0.111.0/41cef1ea`, assembly hash `1010476334`;
- Modset: the exact Connector artifact only.

RC2 passed deterministic checks, a byte-identical fresh-clone DLL/PDB build,
source/build/install/load identity, controller/idempotency/stale gates, 261
state-bound Reads, native-page open/read/return, a fresh ordinary Journey to
`game_over` with 117 delivered actions and zero unknown outcomes, archive-
extracted identity verification, and an actual rollback/cold-load/restore
roundtrip. The public runtime-seal asset is the machine evidence summary.

RC1 remains auditable predecessor evidence, but its development-stage binary
layout is superseded. This current-docs commit is newer than the RC2 tag and is
not itself a built or loaded Host identity.

## Explicit Non-Claims

- arbitrary game versions or Modsets;
- hidden-state access;
- coordinate/reflection mutation or visual computer use;
- Headless, training, search or strategy implementation;
- business completion inferred from a delivery Receipt;
- native pages outside the fixed evidence profile;
- transient VFX/SFX/history information closure;
- arbitrary native pages beyond the fixed evidence profile;
- durable support for a future game build without a new exact identity and
  compatibility run.

See [Coverage](player-environment/COVERAGE.md) and [Support](SUPPORT.md).
