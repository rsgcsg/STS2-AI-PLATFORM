# Current Status

Release candidate: `1.0.0-rc.1`

Player Environment protocol: `1.0-rc.2`

Verdict: **standalone release candidate; targeted runtime gates passed,
ordinary-journey seal pending**

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

The source was history-preservingly extracted from SpireAgent after monorepo
semantic seal commit `4bc448f1fbfa034232b88587faf9a51ea2a15581`.
Monorepo Live runs are predecessor evidence only.

Standalone commit `b050c46ffa8dc3317e66c261175f92ac8e7d3cb4` completed
clean build/install/load identity and targeted runtime gates on one exact
artifact. That evidence is recorded in
[the standalone targeted-runtime closeout](evidence/STANDALONE_C1_TARGETED_RUNTIME_GATES_2026-08-13.md).
It is valid only for that source/artifact/runtime and is predecessor evidence
for any later commit.

Host source `f104e16b6585599e6acf5481c255fa74ea1d221e` completed a
clean build/install, cold-load identity and affected targeted runtime gates.
See the [final-source gate closeout](evidence/STANDALONE_C1_FINAL_SOURCE_RUNTIME_GATES_2026-08-13.md).
It still needs an ordinary same-artifact journey and runtime rollback exercise
before freeze or publication. Until then it is neither generally supported nor
a binary release.

## Explicit Non-Claims

- arbitrary game versions or Modsets;
- hidden-state access;
- coordinate/reflection mutation or visual computer use;
- Headless, training, search or strategy implementation;
- business completion inferred from a delivery Receipt;
- native pages outside the fixed evidence profile;
- transient VFX/SFX/history information closure;
- an ordinary complete standalone same-artifact journey;
- published GitHub, binary or package releases.

See [Coverage](player-environment/COVERAGE.md) and [Support](SUPPORT.md).
