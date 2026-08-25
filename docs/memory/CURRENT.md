# Current Context

Phase: **Human Evidence V2 verified; Policy Runtime and unified Live UI source
candidate awaiting exact-artifact runtime evidence.**

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
- STPD `25b53062...` consumes the exact Evidence Git package, preserves V1
  verifier parity, rejects unverified V2 and projects verified Reads.
- Runtime `abb6b2d8...` produced 30 audited native-human V2 decisions with 120
  materialized pre/successor Reads. Bundle `b92778be...` passed immutable
  store/transfer/receiver, STPD imported 30/30, and Workbench HTTP status passed.
- Generated-card choice is source/test-complete but `not exercised`; corpus and
  training authorization remain separate and absent.
- Policy Runtime and the STPD decision-only adapter are source/test complete.
  The Runtime owns generic controller/mode/stale/Receipt/successor lifecycle;
  STPD owns only checkpoint/Qwen/projection/scoring support.
- Workbench and the in-game Live UI consume typed Runtime status and commands.
  Neither can submit a BoundAction directly or write Human records.
- The S1 checkpoint named by the current Policy Manifest is unavailable on this
  Mac. New Annotator/Live UI artifacts are not yet claimed loaded or Live.

## Evidence Boundary

Source, test, build, package, installed, loaded, Live, journey, and qualified
are distinct evidence levels. A current source or package candidate does not
inherit runtime authority from predecessor repositories, old releases, or old
reports. Historical evidence remains useful for comparison and rollback only.
