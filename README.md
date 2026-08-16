# STS2 Headless

Run the **real installed Slay the Spire 2 process** without a display and
control its normal single-player decisions through a verified fair-player
interface. STS2 remains the rules, RNG, legality, task, and effects engine.

The current route launches the shipped Godot executable with `--headless`,
retains the official SceneTree and Mod loader, and uses the separately released
[STS2 Connector](https://github.com/rsgcsg/STS2-Connector) for observations,
Reads, current BoundActions, delivery Receipts, and successors. It is not a
simulator or a wrapper around a reimplemented game.

## Current Status

Version `0.1.0` is an evidence-first preview, not H1.0 and not Training Ready.
The shipped Godot route is currently the highest-confidence **Reference Host**:
it preserves the official runtime, SceneTree, Mod loader, legality, RNG and
effects. It is not a practical primary trainer on the measured Windows tuple.

Current exact Windows experimental evidence on STS2 `v0.111.0` (`41cef1ea`)
includes native profile isolation, verified template reset, game-owned seed
provenance, process-local Connector endpoints, same-artifact semantic
repeatability, capacity, crash/restart recovery, bounded multi-worker
supervision, crash and suspended-process hang recovery, a shared-profile write
sentinel, and runtime-bound native shutdown.

The current measured development artifact uses protocol `1.0-rc.2`, Connector
source `3e5c5a8...`, DLL SHA `e9673497...`, and MVID `c5bcd426...`. It measured
only `1.52` aggregate normalized semantic decisions/s at four workers. Current
eight-worker windows delivered all 64 decisions at about `2.30` decisions/s
and `5.6-5.7 GiB` summed peak RSS, but failed lifecycle admission on intermittent
pre-shutdown Godot diagnostics. A predecessor artifact reached `2.91` at eight
workers. All are far below the current `>=1000` trainer hypothesis.
Native exit returns code zero and releases processes/endpoints. A phase-aware
classifier now rejects unknown, misplaced, or over-limit diagnostics and admits
only a small exact signature set. The observed roughly 950-1000 Godot messages
are therefore bounded as a containment candidate, not hidden; clean shutdown
and containment qualification remain unresolved.

The released macOS arm64 tuple remains the only declared supported tuple.
Windows x64 is `known_experimental`; a matching hash does not grant support.
Long soak, broad fault/hang recovery, a real changed-build update drill, a
reproducibly published RC Connector SDK/Host, and a high-throughput qualified
backend remain open gates. The current same-artifact differential is a
repeatability baseline, not cross-Host equivalence.

An exact-build research Mod also completed one full official AutoSlayer run in
`394.5s`, covering 50 room entries across three acts. It averaged only `0.151`
CPU cores and peaked at `0.865 GiB` RSS. This proves that route can drive the
native game end to end; its 616 log actions are not normalized semantic
decisions and do not qualify it as a Connector Host or trainer.

Godot's official `--single-threaded-scene` option was tested as a bounded Host
configuration candidate. It produced one admitted and one rejected
eight-worker window, no material throughput gain, and no reliable diagnostic
improvement. The option was therefore removed from the production CLI.

See [Status](docs/STATUS.md), [Compatibility](docs/COMPATIBILITY.md), and
[Evidence](docs/EVIDENCE.md) for exact scope.

## Requirements

- A legally installed Steam copy of Slay the Spire 2
- Node.js 20 or newer
- Git
- macOS arm64 for the currently supported exact runtime, or an explicitly
  acknowledged experimental tuple for maintainer evidence collection

No game binary, asset, save, or decompiled source is distributed by this
repository.

## Quick Start

```bash
git clone https://github.com/rsgcsg/STS2-headless.git
cd STS2-headless
npm ci
npm run check
npm run doctor
npm run setup
```

`setup` downloads the pinned Connector `1.0.1` GitHub Release, verifies its
SHA-256, delegates to the release installer, and records a rollback snapshot.
It does not build or install a branch.

Fully exit all STS2 processes. The smallest real boot gate is:

```bash
npm run probe:shipped -- --shared-profile
```

Maintainers testing the experimental isolated Windows route first let the game
create its own native profile files, then grant only the explicit local Mod and
disclaimer consent required by that exact settings schema:

```bash
node tools/headless.mjs reset-profile --isolated-profile h1-train
npm run bootstrap:profile -- --isolated-profile h1-train
node tools/headless.mjs enable-profile-mods --isolated-profile h1-train --settings-schema 8 --accept-ea-disclaimer
npm run probe:shipped -- --isolated-profile h1-train --experimental-build
```

The bootstrap command does not fabricate a save or claim that Connector was
loaded. `--experimental-build` collects evidence; it does not grant support.

The mutation, lifecycle and measurement gates are deliberately separate:

```bash
npm run probe:menu-control -- --shared-profile
npm run probe:journey -- --shared-profile
npm run bench:capacity -- --workers 1,2,4,8
npm run probe:recovery -- --template vanilla-clean --experimental-build
npm run probe:differential -- --template vanilla-clean --seed H1D1FF01 --experimental-build
npm run soak:reference -- --template vanilla-clean --workers 2 --episodes 2 --actions 8 --experimental-build
npm run drill:update
```

`probe:journey` starts and mutates a standard run with a deterministic test
consumer. It is evidence tooling, not a gameplay agent.

`drill:update` exits nonzero for experimental or changed identities by design.
It generates required gates; it never promotes compatibility.

See [Roadmap](docs/ROADMAP.md) for the separate H1.0 Core Release,
Training-Ready, and H* route gates.

## Run As A Service

Keep the first terminal open:

```bash
npm start -- --shared-profile
```

After it prints `"status": "ready"`, another local program can consume the
versioned Player Environment REST/SDK contract. Inspect or stop the exact
recorded process from another terminal:

```bash
npm run status
npm run stop
```

The normal path is:

```text
installed STS2 + official resources + official Mod loader
-> STS2 Headless process lifecycle and exact-build gate
-> STS2 Connector fair-player Snapshot / Read / BoundAction / Receipt
-> program, agent, test harness, or future training/search adapter
```

Headless owns process lifecycle and identity. Connector owns the fair-player
gameplay contract. A consumer owns policy. Future training adapters may encode
observations, masks, and rewards, but those do not become STS2 truth.

## Documentation

- [Document map](docs/DOCUMENT_MAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Operations](docs/OPERATIONS.md)
- [Compatibility](docs/COMPATIBILITY.md)
- [Evidence](docs/EVIDENCE.md)
- [Development](docs/DEVELOPMENT.md)
- [Release policy](docs/RELEASING.md)
- [Measurement contract](docs/MEASUREMENT.md)
- [Security](SECURITY.md)

For an unknown game build, normal `start` fails closed. Maintainers may run an
explicit non-support probe with `--experimental-build`; passing that probe is
new evidence, not automatic compatibility.
