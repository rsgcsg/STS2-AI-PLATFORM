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

Version `1.0.0` freezes one **STPD v0 operational baseline**. It does not claim
formal H1.0 qualification or universal STS2 compatibility.

The baseline is exact and fail-closed:

- Headless source/tag: `v1.0.0`;
- game: macOS arm64 STS2 `v0.111.0` / `41cef1ea`, `sts2.dll`
  `9cb4f1ad...`, MVID `57785517...`;
- Managed Exact upstream `d11aa883...`, patch `ed9248b...`, Host artifact
  `a884b104...`, MVID `5b6adbd6...`;
- Connector `v1.1.0-rc.1`, source `e065102...`, artifact `c1877f1a...`, MVID
  `64765ea1...`, protocol/SDK `1.0.0`.

The real game still owns rules, RNG, effects and Commit. The managed route is
the primary STPD environment; shipped Godot remains the Reference Host. Exact
evidence covers complete finite actions, state-bound Reads, stable successors,
terminal episodes, reset authority rotation, duplicate request replay,
unknown-no-retry recovery, an independent Python consumer, two-worker learner
contention and two Candidate-to-Reference terminal runs. One of those two runs
matched the exact terminal outcome; this is not broad semantic equivalence.

Long soak, exhaustive/randomized CrossHost coverage, arbitrary cards/relics/
events, a real changed-build campaign, cluster/high-core qualification and
cross-platform support are deliberately deferred. See [Status](docs/STATUS.md)
and the release runtime seal for the exact evidence boundary.

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

`setup` downloads the pinned Connector `1.1.0-rc.1` GitHub Release, verifies its
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

## STPD Baseline Smoke

The cheap pre-training regression uses the independent Python consumer and the
exact prepared Managed Host:

```bash
npm run experiment:managed -- audit --candidate .local/candidates/<exact-candidate>
PYTHONPATH=consumers/python python3 -m sts2_headless.smoke \
  --candidate .local/candidates/<exact-candidate> \
  --max-actions 64 \
  --evidence-file .local/evidence/stpd-environment-smoke/report.json
```

Any incomplete action catalog, unknown/non-delivery, missing successor,
request/action identity mismatch, seed mismatch, or mid-episode environment
identity change fails the command. It is a cheap regression gate, not full
qualification.

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
