# Current Status

Stable release: `v1.0.0`

Host lifecycle/exact-authority candidate: `1.1.0-rc.1`
(`source/test only`; build/install/load/Live are not yet claimed)

Player Environment protocol: `1.0.0`

Verdict: **Player Environment C1 `v1.0.0` is the runtime-sealed stable
baseline for its exact artifact, game and Modset identity**

The candidate is based on public `v1.0.1` and keeps Player Environment protocol
`1.0.0`. It adds process-local endpoint selection, runtime-bound native
shutdown, exact seed provenance, and a fail-closed exact-game/artifact authority
contract. The audit found that the prior implementation treated complete
identity fields as qualified identity; an unknown but well-formed game build or
an arbitrary clean-source rebuild could therefore reach mutation authority.
`contracts/host-compatibility.json` now distinguishes sealed, candidate and
unknown tuples. Candidate game and artifact identities require two exact,
process-local opt-ins and remain non-support evidence.

Current source evidence: 121 Host tests and 7 SDK tests pass, including empty,
mismatched and explicit canary authority cases. The Windows symlink-dependent
release check now remains runnable without Developer Mode while explicitly
reporting that the symlink entry itself was not exercised. No candidate build,
install, cold-load, journey or seal is claimed in this status yet.

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

## Stable Evidence State

The stable release is exact source/tag
`c38d4ad2e9d6eb029f8853ed852cce1152bc6d50` / `v1.0.0`:

- Player Environment source digest:
  `fdc91b0ee57046b9695d6f5b7c53ac04d43abf363ba2db618251a89796b4c258`;
- built/installed/loaded DLL SHA-256:
  `5014224ce8a1f5a61455f21d6873a87052eac533acffce04ac3fb75195bff185`;
- built/installed/loaded MVID: `68f7a9aa-c293-4897-94cd-1e59ab6dd180`;
- exact game: `v0.111.0/41cef1ea`, assembly hash `1010476334`;
- Modset: `exact_player_environment_only`, with only `STS2_MCP` loaded;
- final loaded runtime: `81aa04efe03a4ea8ad79ee07d781cc52`.

The tag passed 106/106 Host tests, 7/7 SDK tests, strict typecheck/build,
contract/boundary/CLI/docs/Python/package checks, a fresh-clone identity match,
and anonymous release archive/checksum verification. Exact-runtime gates passed
for controller exclusion, duplicate request/Receipt identity, stale action and
Read rejection, wrong-runtime native-page refusal, `run_deck` open/read/return
and owner restoration. An ordinary same-artifact run reached `game_over`; its
final continuous segment delivered 89 inputs, performed 209 Reads, rejected
105 stale Reads and nine stale Snapshot actions, and returned zero `unknown`.

Rollback restored RC2 SHA `cf7ed1454437cb796f5931b361f655222d2f3f2e3da3a21f038a752694645cc6`
and MVID `6824e21d-7486-40fd-a131-43e789fdc8d2`, cold-loaded it as runtime
`f1de33c153084e4e9b8c6f958e2a8f09`, then reinstalled and cold-loaded stable.
The public release contains the matching machine-readable runtime seal.

RC1, RC2 and monorepo runs remain auditable predecessor evidence only. Commits
after the `v1.0.0` tag are not built or loaded merely because they document the
release.

## Explicit Non-Claims

- arbitrary game versions or Modsets;
- hidden-state access;
- coordinate/reflection mutation or visual computer use;
- save isolation, training, search or strategy implementation (process-local
  Host controls are present but remain candidate-only);
- business completion inferred from a delivery Receipt;
- native pages outside the fixed evidence profile;
- transient VFX/SFX/history information closure;
- arbitrary native pages beyond the fixed evidence profile;
- durable support for a future game build without a new exact identity and
  compatibility run.

See [Coverage](player-environment/COVERAGE.md) and [Support](SUPPORT.md).
