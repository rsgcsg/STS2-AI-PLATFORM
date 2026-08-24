# Current Status

Phase: **Human Evidence V2 loaded candidate; native-human validation pending**.

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
- Connector `1.2.0-rc.6` exposes process-local same-frame Snapshot plus required
  Read materialization for Annotator without creating a wire authority.
- Annotator `0.3.0-rc.1` implements Decision V2, state-bound ReadEvidence,
  CaptureProfile, minimal RunJournal, portable Bundle V2 and generated-card
  choice witness while retaining the exact ordinary-combat correlation kernel.
- Platform Evidence `0.1.0-rc.1` verifies V1/V2 typed artifacts and provides an
  immutable local store, transfer and staged receiver with receipts.
- Workbench `0.1.0-rc.1` provides read-only Environment, Human Recording,
  Evidence, Transfer and Diagnostics status through application services.
- STPD `d222e340...` installs Evidence from the exact public Git revision,
  preserves V1 verifier parity, rejects unverified V2 JSONL and projects
  verified `run_deck`/`combat_piles` into state and successor.
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

The public package and native-human bullets above are the retained V1 runtime
seal. They are predecessor evidence for regression only and do not claim that
the V2 Connector/Annotator source has loaded or recorded Human V2 evidence.

## Current Evidence

The current candidate's exact-game, package and runtime identities are recorded
in `platform-bom.json`; `npm run check:bom` verifies that composition against
component and package authorities. The dated
[runtime-seal report](evidence/RUNTIME_SEAL_CANDIDATE_2026-08-24.md) records the
evidence boundaries and local report hashes. Predecessor loaded/Live evidence
does not transfer.

The V2 candidate is independently built, installed and cold-loaded against
macOS arm64 STS2 `v0.111.0/41cef1ea`. Connector
`6c66cbf.../11f1da35...` and Annotator `4e911dd6.../692e9dd9...` loaded in
runtime `abb6b2d8...` under exact observer Modset `be4b23c7...`. A read-only
menu Snapshot canary passed. Mutation remained disabled by the observer Modset,
as required. Evidence wheel build/fresh install, remote STPD consumption and a
real Workbench status read passed. These facts prove load and bounded
non-human operation, not native-human V2 capture.

## Non-claims

- Human origin is owner-attested and cannot be independently machine-proven.
  Eight additional actions with unstable pre-frames failed closed and were not
  admitted; the gate does not claim lossless capture under rapid input.
- H0/H1/H2 are automated real-runtime evidence, not human validation, a full
  game journey, durable qualification, semantic parity or long-soak proof.
- The optional noninteractive Host execution profile was not implemented by the
  current Connector and remains a non-claim; shipped-default semantics passed.
- V2 generated-card choice has exact-source and deterministic test coverage but
  no current native-human runtime exercise. No V2 session, store/receiver
  receipt, STPD V2 corpus admission or training authorization is yet Live.
- Platform Evidence is a focused evidence-integrity package, not a
  Platform-wide gameplay SDK. Workbench remains read-only and is not an
  authority or full product UI.
