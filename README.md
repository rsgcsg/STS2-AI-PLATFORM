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
  -> Workbench and one in-game Platform Live UI

Trained policy
  -> thin external Policy Adapter + versioned Policy Manifest
  -> model-neutral Platform Policy Runtime
  -> Connector-authorized BoundAction delivery
```

The repository physically unifies Connector, Host Runtime, Human Annotator,
Platform Evidence, a model-neutral Policy Runtime, Workbench and one in-game
Live UI while preserving separate identities and authorities. It does not own
a trained policy, reward function, model, training system or second game-rules
engine. STPD remains an independent research consumer and supplies only a thin
policy adapter plus model artifact.

Current status: the V1 public Connector/Host composition retains its exact
runtime seal. The exact V2 Connector/Annotator artifact has also produced 30
audited native-human ordinary-combat decisions with materialized `run_deck` and
`combat_piles` Reads before and after every admitted action. Its immutable V2
bundle passed Platform store/transfer/receiver and independent STPD import.
Generated-card choice remains source/test-verified but `not exercised` at
runtime. See [Status](docs/STATUS.md), the [V2 closeout](docs/evidence/HUMAN_EVIDENCE_V2_READ_RICH_COMBAT_CLOSEOUT_2026-08-25.md), and the
[V1 candidate report](docs/evidence/RUNTIME_SEAL_CANDIDATE_2026-08-24.md).

The initial consolidation imports the complete histories of:

- `components/connector`: Player Environment contract, native STS2 adapter,
  transports and strategy-free SDK;
- `components/host-runtime`: game discovery, lifecycle, headless/managed Host
  tooling and qualification;
- `components/annotator`: native-human witness recording, audit and immutable
  session bundles.
- `components/evidence`: typed artifact verification, content identity,
  immutable local store, transfer and receiver receipts;
- `components/policy-runtime`: controller/mode/stale/Receipt/successor lifecycle
  for any strict decision-only policy adapter;
- `apps/workbench`: typed live/fallback application services and browser UI;
- `apps/ingame-ui`: the unified DLL-only in-game Platform Live UI.

The three predecessor GitHub repositories are archived and remain read-only
history/rollback references. All forward Platform development happens here;
STPD remains the independent research consumer.

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
npm run policy:check
npm run workbench
npm run live-ui:doctor
```

See [Policy Runtime](docs/POLICY_RUNTIME.md) and [Live UI](docs/LIVE_UI.md).
The current STPD S1 Policy Manifest is source/test verified, but its checkpoint
is not present on this Mac; this repository therefore makes no current live
Shadow, One-Step, or Auto claim.

## Evidence Boundary

`source/test -> build -> installed -> loaded -> live_exercised ->
human_validated -> qualified` are separate levels. Importing source history or
reproducing a build does not transfer runtime evidence to a new artifact.
