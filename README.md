# STS2 Connector

STS2 Connector exposes the real Slay the Spire 2 UI as one fair-player
Player Environment for external consumers.

```text
STS2 rules, RNG, objects and UI
-> LiveHost visible facts and one current input owner
-> NativeUi exact private binding and native input delivery
-> Snapshot / Read / BoundAction / Receipt
-> localhost REST or optional MCP
-> a strategy-owning consumer
```

The game remains the only rules and effects engine. The Connector does not
expose coordinates, arbitrary reflection, hidden RNG, native operands or a
second legality model. Reads are state-bound and non-authorizing. A complete
finite BoundAction projection is required for input authority; delivery is
revalidated against the current native UI. Unknown delivery is not retryable.

Current release candidate: `1.0.0-rc.2`. Player Environment protocol:
`1.0-rc.2`. RC1 remains published as predecessor evidence but its binary
archive layout is superseded by the
[runtime-sealed RC2 release](https://github.com/rsgcsg/STS2-Connector/releases/tag/v1.0.0-rc.2).

## Repository Map

- `host/`: in-game Mod, exact Live binding, REST and Player Environment core.
- `contracts/`: machine-readable protocol inventory and invariants.
- `sdk/typescript/`: strategy-free strict decoder, REST client and controller session.
- `transports/mcp/`: optional thin MCP-to-REST transport.
- `tools/`: doctor, build, deploy, rollback, identity and conformance checks.
- `docs/`: architecture, protocol, coverage, development and evidence boundaries.

Start with the [new engineer guide](docs/NEW_ENGINEER_GUIDE.md), then read
[Architecture](docs/ARCHITECTURE.md) and the [Protocol](docs/player-environment/PROTOCOL.md).
The [document map](docs/DOCUMENT_MAP.md) separates current design, operation and
dated evidence.

## Developer Quick Start

Requirements: Node.js 20+, npm, .NET 9 SDK, Git and an installed Steam copy of
Slay the Spire 2. Python 3.11+ is needed only for MCP checks.

```bash
git clone https://github.com/rsgcsg/STS2-Connector.git
cd STS2-Connector
npm run bootstrap
npm run doctor
```

`doctor` discovers Steam libraries on supported desktop platforms. Set
`STS2_GAME_DIR` only if discovery cannot locate the exact game installation.

With STS2 fully closed:

```bash
npm run deploy
```

Then cold-start STS2 and verify the process, not just disk bytes:

```bash
npm run verify:loaded
```

See [Installation and rollback](docs/INSTALLATION.md). Never commit game
assemblies, installed artifacts, local runtime data, logs or secrets.

## Consumer Contract

The TypeScript package under `sdk/typescript` strictly validates the wire and
performs transport/control operations only. It also provides a coherence-
checking eager Read aggregator for memoryless consumers; that helper cannot add
facts or authority. Consumers own strategy, normalization and progress policy.
They must not reconstruct native legality or retry an `unknown` receipt.

The REST surface is documented in
[Player Environment Protocol](docs/player-environment/PROTOCOL.md). MCP is an
optional adapter over the same endpoints, not another authority.

## Evidence Boundary

Source, tests, build, install, loaded identity, targeted Live gates, ordinary
journey and release support are different evidence levels. Pre-extraction
SpireAgent evidence is predecessor evidence only. RC2's exact SHA-256, MVID,
runtime, game/Modset and journey seal are attached to its release.
See [Current status](docs/STATUS.md) for the precise non-claims.
