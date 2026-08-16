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

Version `0.1.0` is an **exact-build macOS arm64 preview**. Automated shipped
runtime gates passed on STS2 `v0.111.0` (`41cef1ea`) with Connector Host `1.0.1`
and protocol/SDK `1.0.0`:

- H0: no-display boot, official Mod load, interactive snapshot;
- H1: real menu delivery, duplicate-request idempotency, stale refusal,
  successor observation;
- H2: menu, character select, event, reward, map, combat, `run_deck`, and
  `combat_piles`, with 10 deliveries, 0 unknown deliveries, and 0 Read failures.

This is not yet an unattended server product. The development branch now has a
source-backed experimental Windows profile namespace that disables Steam before
platform initialization. It has native first-run bootstrap evidence, but not
yet reset/soak/Cloud-sentinel qualification. Shared-profile use remains an
explicit opt-in. Full-run completion, deterministic replay, multi-instance
operation, performance, and Windows/Linux release compatibility remain
unproven.

See [Status](docs/STATUS.md), [Compatibility](docs/COMPATIBILITY.md), and
[Evidence](docs/EVIDENCE.md) for exact scope.

## Requirements

- A legally installed Steam copy of Slay the Spire 2
- Node.js 20 or newer
- Git
- macOS arm64 for the currently supported exact runtime

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

The mutation and lifecycle gates are deliberately separate:

```bash
npm run probe:menu-control -- --shared-profile
npm run probe:journey -- --shared-profile
```

`probe:journey` starts and mutates a standard run with a deterministic test
consumer. It is evidence tooling, not a gameplay agent.

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
