# Architecture

## Product Boundary

STS2 AI Platform provides a real-game Host, a fair-player Player Environment,
native-human evidence recording and strategy-free integration tools. It does
not provide policy, reward, model, training or research authority.

## Planes

```text
Environment:  Host <-> Connector <-> consumer
Host control: Host Runtime <-> STS2 process lifecycle
Evidence:     Annotator -> immutable bundle -> verify/store/transfer/receive -> consumer
Operations:   read-only Workbench services -> diagnostics UI
```

The planes may share exact environment identity, but not mutation authority or
transport semantics. The Platform is not a strategy, reward, training, or
research service.

## Hard Shell

STS2 owns rules, RNG, native legality, effects and Commit. Connector publishes
only complete finite Host-bound actions and revalidates exact native operands at
delivery time. Reads are state-bound and non-authorizing. Host control is not a
Player Environment action. Annotator observes accepted native-human actions and
cannot execute them. Unknown delivery is never automatically retried.

## Component DAG

```text
STS2 installation and native runtime
  -> Connector component
     -> Player Environment contract, native Mod, SDK, and release identity
  -> Host Runtime component
     -> lifecycle, exact-build admission, probes
     -> public Connector SDK + pinned Connector Host release
  -> Annotator component
     -> Host workstation seam + exact Connector witness artifact
     -> native-human witness recording and session evidence
  -> Evidence component
     -> typed V1/V2 verification, content identity, immutable local logistics
  -> Workbench application
     -> read-only Environment/Annotator/Evidence/Transfer diagnostics

External consumers -> public Connector SDK / Host Runtime package
STPD              -> public packages + version-pinned Evidence package
SpireAgent        -> consumer integration and policy
```

The Connector gameplay authority is one component-local path. Host Runtime
owns process lifecycle and may consume the public SDK; it must install the
Connector artifact named by the current Platform BOM/release authority, not an
unrelated branch or predecessor release. Annotator may use the explicitly
declared Platform composition seams for exact native witnessing, but it does
not create a second Connector build or action authority. STPD consumes public
packages and verified evidence artifacts, never Platform implementation
internals. Evidence validates artifact integrity and transport; STPD alone owns
research admission, splits, labels, B0 and training authorization. Workbench
orchestrates read-only application services and owns no domain decision.

Portable boundary tests require the Host installer to consume a versioned
Platform Connector release and require Annotator to use only the declared Host
workstation seam plus the exact component-local Connector witness artifact.

## Identity

`workspace_revision` identifies an atomic Platform checkout.
`source_revision` is the latest Git commit that changed a component path;
`component_tree_revision` identifies that path's exact Git tree, and
`component_source_digest_sha256` covers its tracked and non-ignored source
bytes. These identities remain stable across unrelated component changes.
Public contract and artifact identities are separate. Artifact SHA/MVID and
exact loaded runtime remain the final byte/runtime authorities.
