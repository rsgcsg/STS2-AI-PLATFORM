# Current Status

Phase: **runtime-seal candidate; current native-human gate passed**.

## Implemented

- Connector, Host Runtime, and Human Annotator histories are preserved under
  `components/` without squashing the imported ancestry.
- Root dependency direction, evidence terminology, and component identity are
  explicit and checked by the portable suite.
- Annotator compiles against the exact Connector output produced by the Platform
  build; it does not create a second Connector build.
- The Host Runtime package and Connector SDK are represented by the current
  public-package entries in `platform-bom.json`; source may be ahead of the
  last BOM only during an explicit release closeout.
- Host Runtime setup is pinned to the immutable Platform Connector release,
  including archive checksum and native source/SHA/MVID/protocol identity.
- STPD consumes public packages and does not require a predecessor sibling
  checkout for its package path.
- Root and component portable checks pass at the current source revision.
- Connector `1.2.0-rc.5` and Host Runtime `1.1.0-rc.7` are immutable public
  releases whose assets were cold-downloaded and checksum-verified.
- The public Host package passed exact-game H0, H1 and bounded H2 against the
  same Connector artifact. H2 delivered 52 actions, including 47 combat
  deliveries, reached reward flow, exercised two Reads and eight stale
  refusals, and had zero unknown, Read, successor or provenance failures.
- Annotator `0.2.0-rc.2` is component-reproducible, installed and cold-loaded
  with the exact two-observer Modset. Session
  `session-20260824T125449Z-1104aece077d4b0eb1e4cfb9709a7d16` recorded 30
  owner-operated ordinary-combat decisions: 10 targeted plays, 12 untargeted
  plays and 8 end turns. All mapped exact-unique and reached a different stable
  interactive successor; independent audit accepted 30/30.

## Current Evidence

The current candidate's exact-game, package and runtime identities are recorded
in `platform-bom.json`; `npm run check:bom` verifies that composition against
component and package authorities. The dated
[runtime-seal report](evidence/RUNTIME_SEAL_CANDIDATE_2026-08-24.md) records the
evidence boundaries and local report hashes. Predecessor loaded/Live evidence
does not transfer.

## Non-claims

- Human origin is owner-attested and cannot be independently machine-proven.
  Eight additional actions with unstable pre-frames failed closed and were not
  admitted; the gate does not claim lossless capture under rapid input.
- H0/H1/H2 are automated real-runtime evidence, not human validation, a full
  game journey, durable qualification, semantic parity or long-soak proof.
- The optional noninteractive Host execution profile was not implemented by the
  current Connector and remains a non-claim; shipped-default semantics passed.
- Workbench, generic evidence receiver, and a Platform-wide Python SDK are not
  current Platform claims; the Host component's Python consumer is component
  scope.
