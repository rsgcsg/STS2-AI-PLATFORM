# Current Context

Phase: **Human Evidence V2 loaded candidate; native-human validation pending.**

`STS2-AI-PLATFORM` is the only forward-development source authority for the
Platform components. The root BOM and component release manifests are the
machine-readable authorities for component revisions, package identities,
protocol, compatibility, and non-claims. This file is a handoff, not a second
identity registry.

## Current Work

- Keep portable source, BOM, boundary, history, package and component checks
  green.
- Preserve the V1 public package/runtime seal as predecessor evidence while
  tracking current V2 source/build/install/load independently.
- Connector `1.2.0-rc.6`, Annotator `0.3.0-rc.1`, Evidence `0.1.0-rc.1` and
  Workbench `0.1.0-rc.1` implement the read-rich V2 source path.
- STPD `d222e340...` consumes the exact Evidence Git package, preserves V1
  verifier parity, rejects unverified V2 and projects verified Reads.
- Exact build/deploy/load and non-human Snapshot/Workbench canaries pass for
  runtime `abb6b2d8...`; request one owner native-UI session for
  play/end-turn/generated choice, then close the evidence pipeline automatically.

## Evidence Boundary

Source, test, build, package, installed, loaded, Live, journey, and qualified
are distinct evidence levels. A current source or package candidate does not
inherit runtime authority from predecessor repositories, old releases, or old
reports. Historical evidence remains useful for comparison and rollback only.
