# Current Status

Release candidate: `1.0.0-rc.1`

Player Environment protocol: `1.0-rc.2`

Verdict: **standalone source repair candidate; deterministic gates pass, final
exact-runtime seal and release are pending**

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

The responsibility source point is SpireAgent semantic-seal commit
`4bc448f1fbfa034232b88587faf9a51ea2a15581`. Monorepo Live runs are predecessor
evidence only.

The first public standalone snapshot, `a91e3e72e37a896945a9f0c4f0b667ce28423e6e`,
contained truncated text files and was not releasable. The repaired source has
93/93 exact-game Host tests, 7/7 SDK tests, strict typecheck/build, package,
contract, boundary, CLI, Python and documentation evidence. It still requires
a clean commit, build, install, cold-load and exact-runtime exercise.

The evidence notes for local-only source names `b050c46...` and `f104e16...`
remain operator-recorded predecessor diagnostics. Those Git objects are not
fetchable and their evidence cannot qualify this source or release.

## Explicit Non-Claims

- arbitrary game versions or Modsets;
- hidden-state access;
- coordinate/reflection mutation or visual computer use;
- Headless, training, search or strategy implementation;
- business completion inferred from a delivery Receipt;
- native pages outside the fixed evidence profile;
- transient VFX/SFX/history information closure;
- exact loaded identity or Live mutation for the repaired public source;
- an ordinary complete same-artifact journey;
- a tested runtime rollback;
- published binary or SDK package releases.

See [Coverage](player-environment/COVERAGE.md) and [Support](SUPPORT.md).
