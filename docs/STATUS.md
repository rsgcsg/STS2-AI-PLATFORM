# Current Status

Release candidate: `1.0.0-rc.2`

Player Environment protocol: `1.0-rc.2`

Verdict: **RC2 distribution repair candidate; deterministic and exact-runtime
gates must be repeated for its own final artifact before publication**

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

RC1 source/tag `a5db1aea0aabfde457383012b4cae9aa41c92a74` /
`v1.0.0-rc.1` has exact source/build/install/load identity, targeted Live
gates, a fresh ordinary Journey to `game_over`, native-page open/read/return
and an actual rollback roundtrip on game `v0.111.0/41cef1ea`. Its runtime seal
is attached to the public GitHub prerelease.

Release inspection then found that the RC1 Host archive exposed a development
stage layout and stale installation wording. RC2 repairs only Distribution:
the archive has an explicit `payload/`, self-contained install/rollback and
loaded-identity tools, and deterministic release-tool tests. RC1 remains
auditable predecessor evidence; it is not the recommended binary install.
Because RC2 embeds a new source revision, it must complete its own clean build,
cold-load, targeted gates, ordinary Journey and rollback before publication.

## Explicit Non-Claims

- arbitrary game versions or Modsets;
- hidden-state access;
- coordinate/reflection mutation or visual computer use;
- Headless, training, search or strategy implementation;
- business completion inferred from a delivery Receipt;
- native pages outside the fixed evidence profile;
- transient VFX/SFX/history information closure;
- exact loaded identity, Live mutation, ordinary Journey or rollback for the
  not-yet-sealed RC2 artifact;
- a published RC2 binary or SDK asset until its external runtime seal exists.

See [Coverage](player-environment/COVERAGE.md) and [Support](SUPPORT.md).
