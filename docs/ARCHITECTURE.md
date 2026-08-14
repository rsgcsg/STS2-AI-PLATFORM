# Architecture

## Product Definition

Headless means the official STS2 runtime advances its real SceneTree, tasks,
commands, RNG, saves, and native input callbacks without a display. It does not
mean “a library containing some STS2 classes” and it does not mean “a faster
simulator with similar rules.”

The admitted route is:

```text
ShippedHost
  discover and fingerprint the user's installed game
  require an exact supported build for normal start
  launch the official executable with --headless
  preserve Steam initialization and the official Mod loader
  supervise readiness, logs, process identity, stop, and failure

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

## Why The Shipped Route Won

Exact macOS runtime experiments established that direct launch needs the public
Steam app identity, after which the shipped process can initialize Steam, load
the official Mod, mount the Player Environment, enter a run, cross events and
map transitions, and execute native combat actions under `--headless`.

That evidence removed the immediate need for a managed assembly host. Existing
managed projects demonstrate that selected game classes can be driven, but use
Godot stubs, IL patches, reflection, test constructors, scheduler substitutions,
or manual lifecycle repairs. Those changes may be useful research seams, but
they are not semantically equivalent until differential evidence says so.

A rules simulator was also rejected as Headless truth: it creates another game
implementation and cannot prove behavior of the shipped game.

## Authority And Lifecycle

- STS2 owns rules, RNG, effects, saves, native legality, and Commit.
- Headless owns executable discovery, exact-build admission, process lifecycle,
  no-display boot, health records, logs, and shutdown.
- Connector owns fair-player Snapshot/Read/action binding and single-controller
  delivery authority.
- Consumers own strategy, model projection, rewards, search, and recovery.

Every mutation uses one current opaque BoundAction. Connector checks the
snapshot, controller lease, target identity, current actionability, and native
legality at execution. Duplicate request IDs return the same Receipt. An
`unknown` delivery is never retried. Receipt proves input delivery, not an
inferred business transaction.

Process lifecycle is a separate host plane. Starting, stopping, profile
selection, future reset/seed, branch, acceleration, and scenario controls are
not player actions and must never enter the fair-player action set.

## Profile Boundary

The supported route initializes Steam and uses STS2's normal save startup. The
game can synchronize cloud state before Mods initialize, so a Mod or startup
hook cannot honestly guarantee pre-save isolation. The current implementation
therefore refuses all runtime commands unless the operator passes
`--shared-profile`.

A safe unattended server route needs an earlier, proven isolation boundary:
for example a dedicated OS/Steam account or an exact Host seam that precedes
cloud/local save initialization. It must be tested against real startup order;
changing an environment variable after startup is not sufficient evidence.

## Project Boundaries

- **STS2-headless**: process lifecycle, exact compatibility, setup integration,
  evidence harnesses, future non-player Host controls.
- **STS2-Connector**: fair-player gameplay contract, native bindings, SDK,
  transport, artifact identity.
- **SpireAgent**: LLM provider, policy, normalization, supervision, recording.
- **Future RL/Search adapters**: tensors, masks, rewards, reset orchestration,
  vectorization, branch/search policy. They consume gameplay truth; they do not
  redefine it.

The first two remain separately versioned. Headless pins a released Connector
Host and a compatible released SDK; it never consumes a branch by default.

## References Reviewed

- [Godot command-line tutorial](https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html)
- [wuhao21/sts2-cli](https://github.com/wuhao21/sts2-cli)
- [zhiyue/sts2-rl-agent](https://github.com/zhiyue/sts2-rl-agent)
- [Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP)

These are evidence and comparison inputs, not inherited design authority.
