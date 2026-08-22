# Architecture

## Product Definition

Headless means a qualified Host advances STS2 semantics without a normal
display. The shipped Host uses the official SceneTree, tasks, commands, RNG,
saves and native callbacks. A derived Host may qualify later, but loading some
STS2 classes or approximating rules is not semantic equivalence.

The shipped Reference route is:

```text
ShippedHost
  discover and fingerprint the user's installed game
  require an exact supported build for normal start
  launch the official executable with --headless
  preserve the official SceneTree and Mod loader
  isolate profile roots before startup
  supervise readiness, reset, resources, logs, stop and failure

STS2 Connector
  observe only fair-player information
  publish the complete finite current BoundAction set
  keep native operands private
  revalidate and deliver native UI-equivalent input
  return delivery Receipt and successor Snapshot

Consumer
  choose among published actions and interpret successors
  never create game legality or mutate native objects directly
```

REST is transport, not gameplay authority. The Player Environment contract and
TypeScript SDK are released by STS2 Connector, not copied here.

## Current Route Verdict

Exact runtime experiments establish shipped Godot as the highest-confidence
Reference Host. It can initialize the game, load the Connector, enter runs,
cross events and map transitions, and execute combat decisions under
`--headless` while retaining game-owned semantics.

It has not won the trainer route. The measured Windows Host scales from `0.50`
normalized decision/s at one worker to about `2.30` at eight workers and
consumes about `0.71 GiB` per worker. The current eight-worker windows are not
admitted capacity baselines because intermittent runtime diagnostics failed
shutdown containment. This is far below the current realistic trainer
hypothesis even before that reliability gate.

The first pinned `wuhao21/sts2-cli` spike failed bootstrap, profile,
localization, save and global game-assembly patch boundaries and was rejected.
The frozen rebuild keeps the exact installed `sts2.dll` byte-identical,
follows native single-player bootstrap, and projects the Connector-owned Player
Environment. Its game-owned reward and treasure paths, stable information,
reset/recovery, external-consumer and named Reference-transfer gates admit the
exact STPD v0 operational baseline. TestMode, managed observation extraction
and presentation adapters remain explicit Host-specific risks.

The Managed route has three explicit layers:

```text
exact STS2 managed runtime + narrow absent-UI presentation adapters
-> raw exact-object decision state (experimental, not public contract)
-> strict canonical Player Environment projection and request ledger
```

Raw commands and privileged scenario controls are never a consumer API. Native
object references remain inside the candidate and projection binding map.
Throughput did not grant universal parity. Only the exact named operational
gates authorize this baseline.

Current profiling adds a strict method boundary. Projection, allocation,
transport and supervisor changes remain Managed Exact while the byte-identical
game assembly owns rules, RNG, effects and Commit. Persistent reconstruction or
short-circuiting of task/UI lifecycle is Hybrid even if most gameplay classes
still come from STS2. Implementing card, relic, power, monster, reward or RNG
rules outside the game is Simulator. These names describe semantic ownership,
not speed or amount of code changed.

On the current 10-core M4, the managed training profile plateaued at `2,451
d/s` with 24 environments. One shared Node supervisor was not a bottleneck;
per-worker supervisors increased memory without improving aggregate capacity.
Native lifecycle and allocation dominate the single-environment ceiling.
Managed Exact v2, Hybrid and simulator work are inactive for v1.0 and require a
concrete STPD regression or bottleneck before reopening.

The exact shipped source also contains Mega Crit's `AutoSlayer`: a broad smoke
runner with game-owned seed override, watchdog, native commands and UI
handlers. The Steam assembly makes its command-line entry unreachable through
`NGame.IsReleaseGame()`. An exact patch could measure an official-runtime upper
bound, but AutoSlayer's fixed policy and UI-click orchestration are not a
Host-neutral decision contract and cannot replace Connector conformance.

A rules simulator is also not Headless truth by construction. It may become a
candidate only through the same normalized decision, semantic differential,
reset, recovery and resource gates.

The official Godot `--single-threaded-scene` switch was tested rather than
promoted by intuition. One eight-worker window passed containment and one
failed; throughput and density were materially unchanged. It is not part of
the production route. This also demonstrates that launch configuration is part
of Host evidence identity even when game and Connector bytes are unchanged.

## Authority And Lifecycle

- STS2 owns rules, RNG, effects, saves, native legality and Commit.
- Headless owns executable discovery, exact-build admission, profile lifecycle,
  no-display boot, health, resources, reset, supervisor and evidence.
- Connector owns fair-player Snapshot/Read/action binding and single-controller
  delivery authority.
- Consumers own strategy, projection, rewards, search and learning.

Every mutation uses one current opaque BoundAction. Connector checks the
snapshot, controller lease, target identity, current actionability and native
legality at execution. Duplicate request IDs return the same Receipt. An
`unknown` delivery is never retried. Receipt proves input delivery, not an
inferred business transaction.

Process lifecycle is a separate Host plane. Starting, stopping, profile
selection, reset, seed, branch, acceleration and scenario controls are not
player actions and never enter the fair-player action set.

Host controls may use a default-disabled process-local transport implemented by
the Connector Host when native game access is required. They remain outside the
Player Environment contract, require exact runtime binding, never enter the SDK
or MCP, and create no gameplay authority. Current native shutdown follows
`NGame.Quit()` and is operationally bounded, but shipped headless teardown is
not diagnostically clean. Headless partitions diagnostics at the native
shutdown request and admits a run only when every error line matches an exact,
phase-scoped and count-bounded signature. This containment policy is Host
lifecycle evidence only; it cannot turn diagnostics into gameplay success or
grant Host qualification.

Seed is also Host lifecycle state, not a player action. The current development
Host accepts it only before run creation, reports requested/canonical/actual
seed through a secret runtime-bound provenance route, and never publishes it as
mutation authority. Differential and supervisor admission require that exact
provenance to match.

## Profile Boundary

A Mod cannot guarantee pre-save isolation because it loads after platform and
save initialization. The experimental Windows route therefore redirects the OS
user-data roots before process creation, passes the native `--force-steam=off`
option and gives each process a local client ID.

The game creates its own profile schema. Headless may atomically enable only
explicit Mod/disclaimer consent for the exact observed schema. Verified
templates bind payload and exact game identity; hard reset creates a fresh
generation; fault/restart evidence verifies a distinct runtime and released
endpoint.

This remains experimental. Promotion still requires Steam Cloud server
evidence, a broader recovery matrix, long soak and a real changed-build drill.
Every runtime command requires exactly one explicit profile mode.

The bounded supervisor now hashes the normal user-data tree before any worker
starts and after every worker exits. A changed tree fails the supervisor even
when gameplay integrity passed. This protects local profile files; it does not
observe or prove Steam Cloud server state.

## Release Boundary

`v1.0.1` is an operational STPD patch baseline, not formal H1.0. It requires exact
identity, complete action authority, stable successor/terminal, reset,
idempotency, unknown-no-retry, recovery, external Python consumption, planned
worker operation and named Reference comparison. Long soak, exhaustive
semantics, changed-build campaigns and cross-platform qualification remain
separate future evidence.

## Project Boundaries

- **STS2-headless**: Host lifecycle, exact compatibility, isolation/reset,
  supervisor, benchmarks, traces, differential orchestration and qualification.
- **STS2-Connector**: Host-neutral gameplay contract, native bindings, SDK,
  transports and artifact identity.
- **SpireAgent**: LLM provider, policy, normalization, supervision and product.
- **Future RL/Search adapters**: tensors, masks, rewards, vectorization and
  learning/search policy. They consume truth and never redefine it.

These projects remain separately versioned. Headless must pin a reproducible
Connector Host and compatible SDK for a release. A local branch or manually
replaced `node_modules` can collect development evidence but cannot support a
release claim.

## References Reviewed

- [Godot command-line tutorial](https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html)
- [wuhao21/sts2-cli](https://github.com/wuhao21/sts2-cli)
- [zhiyue/sts2-rl-agent](https://github.com/zhiyue/sts2-rl-agent)
- [Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP)

These are evidence and comparison inputs, not inherited design authority.
