# Roadmap

## Consolidation Baseline

- Complete: import all three histories without squashing.
- Complete: remove sibling source/build-output coupling.
- Complete: establish independent component identity and one portable check entry.
- Complete: publish a source/package-candidate Platform BOM.

## Consumer Cutover

- Complete: package the Connector SDK and Host Runtime from Platform.
- Complete: make STPD consume pinned public packages instead of sibling source.
- Complete: move generic V1/V2 bundle verification after exact V1 parity tests;
- retain ResearchTransition, corpus policy, splits, B0, serialization, models and
  training in STPD.

## Runtime Seal

- Complete: exact-game, deterministic Connector and Annotator builds from clean
  component source.
- Complete: immutable public Connector/SDK/Host releases and anonymous cold
  download verification.
- Complete: install, rollback snapshots, exact Connector H0/H1/H2, and exact
  Annotator loaded-identity/Modset gates.
- Complete: one normal native-human combat recording with the current exact
  Annotator artifact, followed by independent audit and deterministic export.
- Complete: promote the exact composition to Platform BOM `0.1.0-rc.3` after
  the human gate; this remains a runtime-seal candidate, not qualification.

## Human Evidence V2

- Complete at source/test: same-frame process-local `run_deck` and
  `combat_piles` capture, Decision/Read/CaptureProfile/RunJournal/Bundle V2.
- Complete at source/test: generated-card choice select/skip native witness
  without changing Connector action authority.
- Complete: local immutable Evidence Store, directory transfer, staged typed
  receiver verification and receipts.
- Complete: STPD version-pinned V2 consumption with V1 compatibility and
  read-rich projection.
- Complete: read-only Workbench foundation for Environment, Human Recording,
  Evidence, Transfer and Diagnostics.
- Complete: exact build, safe install, cold-load identity, observer Modset and
  read-only Snapshot/Workbench canaries for the V2 artifact.
- Complete: exact-artifact native-human targeted play, untargeted play and
  end-turn with pre/successor `run_deck` and `combat_piles`, followed by V2
  audit/bundle/store/receive/STPD and Workbench closeout.
- Pending exact-runtime evidence: generated-card choice did not naturally occur
  and remains `not exercised`; it is not inferred from ordinary combat.

Cloud storage, broad action-family capture and a full Workbench remain later
work; they are not reasons to weaken the component boundaries.

## Policy Runtime And Platform Live UI

- Complete at source/test: strict versioned Policy Manifest and decision-only
  NDJSON port; model-independent Human/Shadow/One-Step/Auto lifecycle.
- Complete at source/test: exact capabilities/game/protocol admission,
  Manifest-selected Snapshot Reads, complete-catalog digest/index parity,
  controller lifecycle, stale refresh, idempotent request identity,
  unknown-no-retry, stable successor and immutable Agent-run evidence.
- Complete at source/test: Runtime code, canonical Manifest, checkpoint,
  adapter-code and exact environment provenance fail closed before mutation.
- Complete at source/test: STPD S1 policy/model code split from its legacy live
  runner and exposed as one thin adapter; the old `live_s1` remains a golden
  regression until real parity gates pass.
- Complete at source/test: Workbench live Policy Runtime service and one DLL-only
  in-game Overview/Environment/Policy/Human Data/Diagnostics UI.
- Complete at source/test: source-bound Live UI build/deploy/rollback and exact
  loaded SHA/MVID/source verification.
- Pending external artifact: the S1 checkpoint named by the Manifest is absent
  on this Mac, so real model Shadow/One-Step/Auto cannot start.
- Pending exact-runtime evidence: final Annotator/Live UI artifacts must be
  installed, cold-loaded and owner-operated; no old loaded evidence transfers.

Full-run policy support, training UI, cloud policy/evidence services and broad
action-family expansion remain outside this bounded baseline.
