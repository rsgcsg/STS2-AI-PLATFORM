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
  -> external evidence consumers
```

The repository physically unifies the Connector, Host Runtime and Human
Annotator while preserving them as separate components, artifacts, identities
and authorities. It does not contain a policy, reward function, model, training
system or second game-rules engine. STPD remains an independent research
consumer.

The initial consolidation imports the complete histories of:

- `components/connector`: Player Environment contract, native STS2 adapter,
  transports and strategy-free SDK;
- `components/host-runtime`: game discovery, lifecycle, headless/managed Host
  tooling and qualification;
- `components/annotator`: native-human witness recording, audit and immutable
  session bundles.

Read [the consolidation ADR](docs/adr/0001-consolidate-environment-platform.md)
and [migration provenance](migration/source-manifest.json) before changing a
component boundary.

## Evidence Boundary

`source/test -> build -> installed -> loaded -> live_exercised ->
human_validated -> qualified` are separate levels. Importing source history or
reproducing a build does not transfer runtime evidence to a new artifact.
