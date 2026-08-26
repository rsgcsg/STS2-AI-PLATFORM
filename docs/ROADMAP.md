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
- Complete on predecessor exact artifact: one naturally occurring generated-
  card select mapped exact-unique and reached an interactive successor.
- Pending exact-runtime evidence: generated-card skip and the unified artifact's
  Human action path remain `not exercised`; evidence is not transferred.

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
- Complete: one `STS2_PLATFORM` artifact containing Connector, Annotator and
  Live UI was safely installed, cold-loaded and verified as the only loaded
  non-gameplay Mod with Connector input delivery available.
- Complete automated runtime canary: built-in-node UI mounted and K opened and
  closed the panel through the SceneTree frame signal.
- Pending owner interaction: confirm the five K pages are visible, exercise
  recording pause/resume and ordinary native Human play; no predecessor Human
  evidence transfers.

Full-run policy support, training UI, cloud policy/evidence services and broad
action-family expansion remain outside this bounded baseline.

## Recording Application Plane

- Complete at source/test: runtime startup stops at `Ready`; explicit
  `StartNewSession` supports multiple isolated sessions in one STS2 process.
- Complete at source/test: typed Query/Command/Event status, command
  idempotency, bounded ordered reconnect, counters, pending/last decision,
  Read/append/disk health and closeout status.
- Complete at source/test: Pause blocks new witnesses while preserving pending
  settlement; Close waits for pending settlement/invalidation, flushes and
  disposes the session store before allowing a new session.
- Complete at source/test: Live UI consumes only `RecordingService`; application
  commands have no native action or HumanDecision path.
- Complete exact runtime: final artifact `a7b11d93... / c3e7127a...` is installed
  and cold-loaded in runtime `bd6b73e7...`; it starts Ready with no session and
  has an available rollback.
- Complete on artifact `a7b11d93...`: owner checked K, New Session,
  Pause/Resume, pending-safe Close and a second isolated session in one process.
- Defect found and repaired at source/test: unified `exact_platform_modset` was
  accepted by runtime but rejected by final record validation, and persistence
  exceptions retried successor Reads. Runtime and audit now share exact Modset
  admission; unknown evidence commit invalidates once without retry.
- Complete exact runtime: repaired artifact `d3b25e62... / ee78d9a1...` is
  installed and cold-loaded in runtime `88db3f9c...` with Ready/no session.
- Complete on `d3b25e62...`: session `session-20260825T115335Z-...` audited six
  end-turn records and 24 bounded Reads. First Close completed in 5.036 ms.
- Defect found and repaired at source/test: 21 supported play-card attempts were
  rejected because staged and transient snapshots were required to be equal;
  the UI also retained intermediate Closing text. Exact native continuity now
  preserves the complete staged frame, and status explicitly partitions
  recorded, failed-closed, not-observed and out-of-scope families.
- Complete exact runtime: repair artifact `06f62285... / 17981f40...` is safely
  installed and cold-loaded in runtime `e3a89aae...`; rollback is available.
- Complete owner runtime on `06f62285...`: two sessions audited 39/39 records,
  including 25 card plays and 14 end turns with 158 bounded Reads and no Read
  failures. Immediate and pending-safe Close behavior both matched lifecycle.
- Defect found and repaired at source/test: pre-frame invalidations were emitted
  before STS2 native acceptance, so cancelled/rejected UI attempts inflated the
  failed-closed category. RecordingStatus revision 3 now counts only accepted
  native actions in that category.
- Complete exact runtime: accepted-only accounting is clean-built, installed and
  cold-loaded as `887630f4... / 14761ed4...` in runtime `bcf2b3f1...` with
  Ready/no-session and rollback available.
- Complete owner runtime: exact session `session-20260826T025703Z-...` audited
  19/19 records (10 play, nine end-turn), 78 Reads, zero Read failures, and 16
  explicitly separated accepted failures. First-click Close completed in
  5.153 ms. The owner-observed cancelled/rejected attempt emitted no evidence;
  absence of an intentionally unrecorded attempt is not machine-attributable.
- Complete at source/test/Live behavior: replace the single-pending overlap drop with an
  additive exact `GameAction` lifecycle ledger. Every accepted root receives a
  durable disposition; overlapping causal windows cannot emit strict V2 S'.
  V2 remains byte/meaning compatible and old sessions remain readable.
- Complete at source/test: ledger v2 retains each invalidated root's frozen
  decision pre-frame, exact witness/mapping and BoundAction without changing
  Decision V2; admitted payloads are cross-checked and ledger v1 remains
  readable.
- Complete owner runtime: ledger v2 retained exact decision payloads for 33
  accepted roots, including two cancel-before-start and three cancel-after-start
  facts; player-choice pause/resume remains `not exercised`.
- Complete at source/test: additive semantic-boundary trace separates Human H
  from transition S, captures a complete authoritative boundary before the next
  tracked Human action effect, and classifies proved/cancelled/non-Commit abort/
  unknown without changing Decision V2.
- Complete predecessor build/install/cold-load: unified artifact
  `2cb46ead... / 66ed1396...` initialized Ready in runtime `af2e7370...` with
  exact STS2 and sole `STS2_PLATFORM` Mod.
- Complete Live defect discovery: owner session `session-20260826T141755Z-...`
  retained 22/22 accepted actions and exercised a generated-card select, but
  proved that acceptance and execution order can differ across player-choice
  pause. One predecessor End Turn proof is rejected rather than transferred.
- Complete at corrected source/test: causal settlement follows exact execution
  order; a precommit is rebound only from a complete boundary immediately before
  native execution. Audit rejects mismatched pre-state and interleaved effects.
- Complete corrected build/install/cold-load: artifact
  `04104ca5... / 7408a183...` is Ready in runtime `97829317...` with exact STS2
  and the sole `STS2_PLATFORM` Mod.
- Pending corrected-artifact Human runtime: repeat generated choice before a
  queued action plus ordinary rapid chains and End Turn. Lethal cancellation
  and combat-to-next-surface remain opportunistic. No semantic-transition
  admission or training authority exists before that gate.

Next phase, not part of this slice: **Live Workspace Window Kernel -> Human
Recorder Window**.
