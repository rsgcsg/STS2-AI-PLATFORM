# Architecture

## Product Boundary

STS2 AI Platform provides a real-game Host, a fair-player Player Environment,
native-human evidence recording and strategy-free integration tools. It does
not provide policy, reward, model, training or research authority.

## Three Planes

```text
Environment:  Host <-> Connector <-> consumer
Host control: Host Runtime <-> STS2 process lifecycle
Evidence:     Annotator -> immutable bundle -> evidence consumer
```

The planes may share exact environment identity but not mutation authority or
transport semantics.

## Hard Shell

STS2 owns rules, RNG, native legality, effects and Commit. Connector publishes
only complete finite Host-bound actions and revalidates exact native operands at
delivery time. Reads are state-bound and non-authorizing. Host control is not a
Player Environment action. Annotator observes accepted native-human actions and
cannot execute them. Unknown delivery is never automatically retried.

## Dependency Direction

```text
contracts
  <- connector
  <- host-runtime
  <- annotator
  <- public SDK/tools
  <- external consumers
```

Connector does not depend on Host Runtime, Annotator or STPD. Annotator may use
Connector's process-local witness SPI and Host Runtime discovery/probe APIs.
STPD consumes public packages/contracts and evidence artifacts, never Platform
implementation internals.

## Identity

`workspace_revision` identifies an atomic Platform checkout.
`source_revision` is the latest Git commit that changed a component path;
`component_tree_revision` identifies that path's exact Git tree, and
`component_source_digest_sha256` covers its tracked and non-ignored source
bytes. These identities remain stable across unrelated component changes.
Public contract and artifact identities are separate. Artifact SHA/MVID and
exact loaded runtime remain the final byte/runtime authorities.
