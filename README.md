# STS2 AI Platform

STS2 AI Platform is the shared environment foundation for programs that use the
real Slay the Spire 2 runtime.

```text
STS2 game truth
  -> Host Runtime lifecycle and identity
  -> Connector fair-player Player Environment
  -> external consumers

Native human play
  -> Human Annotator
  -> immutable evidence bundles
  -> Platform Evidence verify/store/transfer/receive
  -> external evidence consumers

Operator diagnostics
  -> read-only Platform Workbench services and UI
```

The repository physically unifies Connector, Host Runtime, Human Annotator,
Platform Evidence, and a read-only Workbench while preserving separate
components, identities and authorities. It does not contain a policy, reward
function, model, training system or second game-rules engine. STPD remains an
independent research consumer.

Current status: the V1 public Connector/Host composition retains its exact
runtime seal. Read-rich Human Evidence V2 is implemented and automated-tested:
same-frame `run_deck`/`combat_piles`, typed V2 records and bundles, immutable
local evidence logistics, one generated-card selector witness, verified STPD
consumption, and a minimal Workbench. The exact V2 Connector/Annotator artifacts
are built, installed and cold-loaded, but do not inherit V1 Human action evidence. See
[Status](docs/STATUS.md) and the [V1 candidate report](docs/evidence/RUNTIME_SEAL_CANDIDATE_2026-08-24.md).

The initial consolidation imports the complete histories of:

- `components/connector`: Player Environment contract, native STS2 adapter,
  transports and strategy-free SDK;
- `components/host-runtime`: game discovery, lifecycle, headless/managed Host
  tooling and qualification;
- `components/annotator`: native-human witness recording, audit and immutable
  session bundles.
- `components/evidence`: typed artifact verification, content identity,
  immutable local store, transfer and receiver receipts;
- `apps/workbench`: read-only application services and diagnostics UI.

Read [the consolidation ADR](docs/adr/0001-consolidate-environment-platform.md)
and [migration provenance](migration/source-manifest.json) before changing a
component boundary.

## Quick Start

```bash
npm ci
npm run check
npm run identity
npm run check:bom
npm run doctor
```

External consumers install immutable Connector SDK and Host Runtime release assets;
they do not depend on this checkout or a branch. Native-human operations are available
from the repository root:

```bash
npm run annotator:doctor
npm run annotator:launch
npm run annotator:verify-loaded
npm run annotator:audit -- <session-directory>
npm run annotator:pack-session -- <session-directory> [options]
npm run evidence -- --help
npm run workbench
```

## Evidence Boundary

`source/test -> build -> installed -> loaded -> live_exercised ->
human_validated -> qualified` are separate levels. Importing source history or
reproducing a build does not transfer runtime evidence to a new artifact.
