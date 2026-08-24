# ADR-0001: Consolidate the STS2 Environment Platform

Status: Accepted for implementation

Date: 2026-08-24

## Decision

Consolidate `STS2-Connector`, `STS2-headless` and `STS2-human-Annotator` into
`STS2-AI-PLATFORM`. Keep STPD external as a research consumer.

The consolidation is physical, not semantic. Connector, Host Runtime and
Annotator retain separate responsibilities, public contracts, versions,
artifacts, source identities and runtime evidence.

## Why

The three repositories share one installed game, exact runtime identity,
workstation tooling and release compatibility, but currently recreate a hidden
monorepo through sibling source imports and sibling build-output paths. That
arrangement provides neither independent package consumption nor atomic
cross-component development.

## Dependency Direction

```text
component contracts
  <- Connector
  <- Host Runtime
  <- Annotator
  <- strategy-free SDK and tools
  <- external consumers, including STPD
```

Connector must not depend on Annotator or STPD. Host Runtime must not depend on
STPD. Annotator may consume Connector's process-local witness SPI but cannot
authorize or execute gameplay. STPD may consume public Platform packages and
evidence but not implementation internals.

## Planes

- Environment: Host <-> Connector <-> consumer.
- Host control: supervisor <-> Host lifecycle.
- Evidence/data: Annotator -> immutable bundle -> external evidence consumer.

These transports and authorities remain separate.

## History And Cutover

Import each source repository as a non-squashed Git subtree. Original commits
remain unchanged ancestors of the Platform branch. Record exact source refs and
tree IDs in `migration/source-manifest.json`; do not rewrite old repositories or
historical evidence.

Old repositories remain usable rollback/reference sources until Platform
portable checks, exact builds, package consumption and new-artifact runtime
gates pass. New artifacts do not inherit old loaded or Live evidence.

## Identity And Release

Track both workspace commit and path-scoped component identity. Component
source/contract digests and artifact SHA/MVID are independent. Release tags are
component-scoped; a Platform BOM describes compatible versions without forcing
lockstep releases.

## Non-goals

This consolidation does not add gameplay semantics, a policy/model, training,
Headless simulation, arbitrary Mod support, a GUI Workbench, cloud service or a
new wire protocol.
