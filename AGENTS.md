# STS2 Connector Engineering Rules

Read `docs/NEW_ENGINEER_GUIDE.md`, `docs/ARCHITECTURE.md`, the current protocol
and coverage before changing game-bound behavior.

## Hard Shell

- STS2 owns rules, RNG, effects and native Commit paths.
- `LiveHost` extracts fair-player facts and exactly one current input owner.
- `NativeUi` owns native objects, exact operands and execute-time revalidation.
- `PlayerEnvironment` owns Snapshot, Read, finite BoundAction, stale rejection,
  Receipt and immediate successor semantics.
- `Authority` owns exact identity, one controller and request idempotency only.
- REST, MCP and client SDK transport or validate; they create no game authority.
- Reads and native-page evidence are state-bound, read-only and non-authorizing.
- Missing identity, owner, referent, binding, controller or delivery truth fails
  closed. `unknown` is never retried automatically.

Never add index/coordinate mutation, arbitrary reflection/method execution,
hidden information, consumer-side legality, silent fallback or a second game
rules engine.

## Evidence

For exact-game changes, record game version/commit, assembly SHA/MVID, source,
tests, build, install, loaded identity and Live exercise separately. Old
artifacts and fixtures never qualify a new source.

Run `npm run check`, exact-game Host tests/build, `git diff --check`, and the
relevant runtime gate before advancing a support claim.

Never commit game binaries, `.local/`, installed Mods, credentials, local
config, logs, run directories or raw consumer/provider output.
