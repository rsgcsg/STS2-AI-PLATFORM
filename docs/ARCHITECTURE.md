# Architecture

## Product Definition

Headless means a qualified Host advances STS2 semantics without a normal
display. The shipped Host uses the official SceneTree, tasks, commands, RNG,
saves and native callbacks. A derived Host may qualify later, but loading some
STS2 classes or approximating rules is not semantic equivalence.

The current Reference route is:

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
normalized decision/s at one worker to only `2.91` at eight workers and consumes
about `0.71 GiB` per worker. This is far below the current realistic trainer
hypothesis.

The pinned `wuhao21/sts2-cli` source is useful research, but its exact-build
spike required local API/bootstrap adaptation and still failed profile setup,
CoreCLR task patches, localization and saves. Its reflection, patch and manual
simulation surface is not admitted as truth without differential evidence.
This rejects that revision as the primary trainer, not every possible managed
Host.

The exact shipped source also contains Mega Crit's `AutoSlayer`: a broad smoke
runner with game-owned seed override, watchdog, native commands and UI
handlers. The Steam assembly makes its command-line entry unreachable through
`NGame.IsReleaseGame()`. An exact patch could measure an official-runtime upper
bound, but AutoSlayer's fixed policy and UI-click orchestration are not a
Host-neutral decision contract and cannot replace Connector conformance.

A rules simulator is also not Headless truth by construction. It may become a
candidate only through the same normalized decision, semantic differential,
reset, recovery and resource gates.

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

## Release Gates

- **H1.0 Core Release**: reliable exact Host lifecycle, isolation/reset,
  seed/provenance, supervisor/recovery, measurement, differential, update drill,
  clean consumer interface and honest compatibility scope.
- **Training Ready**: at least one semantically qualified backend with realistic
  throughput, 1M+ reset/scale/recovery evidence, Python vector consumption,
  learning smoke and policy evaluation on the Reference Host.
- **H***: the measured Pareto route for fidelity, aggregate throughput,
  CPU/RAM, reset, reliability and update maintenance. It is an experiment
  outcome, not a preselected implementation.

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
