# Current Status

Phase: **Human Evidence V2 verified; one-Mod Policy Runtime/Live UI runtime
candidate loaded with automated K-toggle canary, awaiting owner visibility and
control validation**.

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
- GitHub predecessors `STS2-Connector`, `STS2-headless`, and
  `STS2-human-Annotator` are archived; Platform is the only forward-development
  authority for those responsibilities.
- Connector `1.2.0-rc.6` exposes process-local same-frame Snapshot plus required
  Read materialization for Annotator without creating a wire authority.
- Annotator `0.3.0-rc.1` implements Decision V2, state-bound ReadEvidence,
  CaptureProfile, minimal RunJournal, portable Bundle V2 and generated-card
  choice witness while retaining the exact ordinary-combat correlation kernel.
- Platform Evidence `0.1.0-rc.1` verifies V1/V2 typed artifacts and provides an
  immutable local store, transfer and staged receiver with receipts.
- Workbench `0.1.0-rc.1` provides Environment, Policy, Human Data, Evidence,
  Transfer and Diagnostics status through application services; its only
  command surface is the typed Policy Runtime mode/one-step boundary.
- Policy Runtime `0.1.0-rc.1` is a model-neutral Connector consumer with strict
  Policy Manifest/NDJSON contracts, exact environment admission,
  Manifest-selected Reads, complete-catalog parity, Human/Shadow/One-Step/Auto,
  stale refresh, controller lifecycle, Receipt/successor handling and immutable
  Agent-run evidence. Unknown delivery is never retried.
- Runtime and Agent evidence bind a compiled Runtime code digest, canonical
  Policy Manifest digest, policy artifact SHA-256 and pre-decision exact
  Connector/game/Modset admission; STPD independently checks the Manifest-bound
  adapter code digest before serving decisions.
- STPD exposes the current S1 model as one thin decision-only adapter while its
  original `live_s1` remains a golden regression. Generic controller, stale,
  delivery and successor lifecycle is no longer required in new model lanes.
- Workbench now consumes strict live Policy Runtime status and bounded mode/tick
  commands with an explicitly partial filesystem fallback.
- Platform Live UI `0.1.0-rc.1` provides one hidden-by-default DLL-only in-game
  Overview/Environment/Policy/Human Data/Diagnostics shell over typed services.
  It has no direct BoundAction submission path; Connector Reads and
  Connector/Annotator/UI identities remain inspectable without a policy process.
- The current exact-game candidate is one `STS2_PLATFORM` Mod. Common artifact
  `65498489... / c280d226...` contains Connector, Annotator and Live UI and is
  cold-loaded against STS2 `v0.111.0`, assembly
  `9cb4f1ad... / 57785517...`, in runtime `f3195698...`. The exact Modset contains
  only `STS2_PLATFORM`; Connector reports `execution_available=true`.
- Live UI source `5bb1236f...` uses only built-in Godot nodes and a
  `SceneTree.ProcessFrame` signal because standard single-DLL Mods do not run
  Godot's C# node source generator. Panel readiness and automated K open/close
  input canaries passed. Automation is not owner-visible UI evidence.
- The portable boundary check validates that every declared workspace CLI
  entrypoint exists in Git source authority; ignored local files cannot satisfy
  a clean-check claim.
- STPD `e23215ee...` installs Evidence from the exact public Git revision,
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

The V2 native artifact was independently built, installed and cold-loaded against
macOS arm64 STS2 `v0.111.0/41cef1ea`. Connector
`6c66cbf.../11f1da35...` and Annotator `4e911dd6.../692e9dd9...` loaded in
runtime `abb6b2d8...` under exact observer Modset `be4b23c7...`.
Owner-operated session `session-20260824T153454Z-3005d3b9cea9425ab0c615f0bd961a39`
then admitted 30 audited Decision V2 records: 7 targeted plays, 16 untargeted
plays, and 7 end turns. Every admitted record contains materialized
`run_deck` and `combat_piles` for both S and S', for 120 verified Read results;
all 30 successors were interactive. Five actions without a complete stable
pre-frame failed closed and were not admitted.

Bundle `b92778be...` was typed-verified, promoted into the immutable local
Evidence Store, and reused idempotently on retry with zero findings. STPD
`25b53062...` imported all 30 records with zero rejection while preserving V1
compatibility; the records were not added to the frozen corpus or authorized
for training. A real Workbench HTTP smoke reported Environment, Annotator,
  Evidence, Transfer, and Diagnostics available. Workbench source
  `0ae81907...` additionally closes the Git source-completeness defect without
  changing its read-only behavior. The dated
[V2 closeout](evidence/HUMAN_EVIDENCE_V2_READ_RICH_COMBAT_CLOSEOUT_2026-08-25.md)
holds exact hashes and evidence boundaries.

The original 30-record Human V2 journey remains bound to predecessor Annotator
source `09e5c236...`; it does not transfer to later artifacts. A later owner-
operated three-Mod session
`session-20260825T022415Z-18df9e07f32d43f2a1beb8ad0db91c14`
ran under runtime `7c5e7f16...` and independently audited 10/10 records. It
contains nine ordinary-combat end turns and one naturally occurring
generated-card select (`record-00000009-cd9814ce3eab458f8fe268c04e1acb09`),
with exact-unique mapping and an interactive successor. Generated-card skip,
targeted play and untargeted play were not exercised in that session. This is
predecessor evidence and does not qualify the unified artifact.

The current unified artifact was built, safely installed, cold-loaded and
identity-verified with rollback at
`apps/game-mod/.local/deployments/2026-08-25T06-42-44.907Z`. Runtime
`f3195698...` reports environment `02ec1f39...`, exact unified Modset
`447fc8f9...`, ready UI, and automated K open/close events. Owner page
visibility, recording controls, ordinary Human play and policy modes remain
separate pending gates.

## Non-claims

- Human origin is owner-attested and cannot be independently machine-proven.
  Eight additional actions with unstable pre-frames failed closed and were not
  admitted; the gate does not claim lossless capture under rapid input.
- H0/H1/H2 are automated real-runtime evidence, not human validation, a full
  game journey, durable qualification, semantic parity or long-soak proof.
- The optional noninteractive Host execution profile was not implemented by the
  current Connector and remains a non-claim; shipped-default semantics passed.
- Generated-card select has one audited native-human predecessor record;
  generated-card skip remains `not exercised`. Neither claim transfers to the
  current unified artifact.
- The V2 bundle/store/receiver/STPD path is verified, but this does not authorize
  corpus inclusion or training and does not qualify unexercised action families.
- Platform Evidence is a focused evidence-integrity package, not a
  Platform-wide gameplay SDK. Workbench and Live UI are application shells, not
  action/evidence authorities.
- The unified Connector, Annotator and Platform Live UI have source/test/build/
  install/load evidence plus a non-human K input canary. Actual five-page
  visibility, recording controls and normal Human play on this artifact remain
  `pending owner interaction`; predecessor Human V2 evidence does not transfer.
- The current S1 Policy Manifest is validated, but its exact checkpoint is not
  present on this Mac. Real-model Shadow, One-Step, Auto, policy Agent evidence
  and legacy/new path parity are therefore `not exercised`.
