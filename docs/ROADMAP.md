# Roadmap

## Native Foundation Migration

- Complete at source/test: introduce a semantic-only game-side Native
  Foundation and route Connector/Annotator Direct Combat through one STS2-owned
  logical/native catalog.
- Complete at source/test: route Annotator action lifecycle and PlayerChoice
  parent lineage through one read-only Foundation adapter.
- Complete at source/test/build on stacked continuation `47dacf9...`: route
  Map destinations, Reward choices/proceed and CardReward options through
  typed shared Native Foundation decision providers. Connector intersects
  presentation/delivery and Annotator consumes the same catalog.
- Complete at source/test on continuation component source `85cb0a5...`: route
  Treasure chest, exact relic choice, skip and proceed through
  `NativeTreasureDecisionProvider`; visible controls are delivery bindings only.
- Complete: audit Ritsu v0.5.18/main and development and choose the bounded
  reference-only route in ADR 0004; no runtime dependency is added.
- Complete for exact artifact `9a89f1fe... / b1c34f90...`: clean build, safe
  install, cold-load, non-mutating live controller/stale/idempotency checks,
  shipped-headless H0, and main-menu-only canonical live/headless parity.
- Complete independently on Windows artifact `a681f8b1... / 7c42c4c3...`:
  source checks, exact shipped-game discovery, clean build, artifact plus native
  Mod-settings rollback, cold-load with sole `STS2_PLATFORM`, bounded visible
  Connector checks, shipped-headless H0, and main-menu-only canonical parity.
- Pending Human evidence: a short Combat/PlayerChoice and
  `lethal -> Reward -> CardReward -> Map` canary on the exact Windows artifact;
  Recorder owner New/Pause/Resume/Close is included in that gate.
- Independently pending for the continuation artifact: final exact build,
  install/load and representative runtime exercise of the new
  Map/Reward/CardReward/Treasure adapters; no PR #5 Human evidence transfers.
- Next non-Human batch after Treasure closeout: migrate Shop, then Event, Rest,
  run entry, and terminal by native mechanism. Remove each old
  publication/witness path only after both consumers use the shared adapter.

## Canonical Human One-Step Evidence

- Complete: mechanically separate Human provenance H, authoritative S,
  complete same-state A(S), exact A membership and causal S' from legacy V2 and
  schema-3 trace terminology.
- Complete: calibrate the latest 933-action exact Human session; 0 rows satisfy
  the full canonical one-step contract, while 497 retain only legacy V2 use.
- Complete: bound the earlier alternatives; ADR 0003 records the historical
  serialized-input choice, while gameplay evidence later withdrew its global
  UI gate.
- Historical candidate only: serialized mutation admission and artifact
  `b805474d... / 3ab1e10e...`. They are not active gameplay authority.
- Complete at source/test: preserve gameplay-safe input and add a read-only
  native semantic discriminator at first execution, with STS2-owned logical
  state/native validators, exact action identity and cancellation/abort/player-
  choice accounting.
- Complete: current Managed Exact rebuild plus 80-action/158-Read reset,
  stale, idempotency and bounded performance measurement.
- Complete bounded exact-runtime canary: 41/41 successful roots are exact-once
  in STS2-owned native `A_sem(S)`, across 30 PlayCard, ten EndTurn, one potion
  use and two player-choice pause/resume pairs. The same run disproves current
  `A(UI)` as sequential semantic authority. See the
  [Human closeout](evidence/NATIVE_SEMANTIC_RUNTIME_DISCRIMINATOR_HUMAN_CLOSEOUT_2026-08-30.md).
- Next: extend the same execution-bound native semantic adapters to non-combat
  Full-Run mechanisms. Keep separate targeted gates for true overlapping
  acceptance/execution reorder and native cancel/abort; do not restore global
  UI serialization, natural-observer polling or a second legality model.

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
- Complete bounded exact-runtime Human evidence: the latest repair canary passes
  semantic accounting on the unified artifact. Generated-card skip remains
  `not exercised`, and the bounded canary does not qualify exhaustive Full Run.

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
- Complete corrected-artifact initial bounded Human runtime: session
  `session-20260826T150700Z-...` accounted for all 35 accepted Human actions
  with 24 proved, six unknown, four cancelled and one pre-Commit abort. It
  exercised native ledger v2, rapid chains, End Turn, player-choice
  pause/resume and generated-card select; the audit passed with no false
  transition finding.
- Complete narrow exact-reorder runtime: latest closed owner session
  `session-20260827T014202Z-...` exercised two complete execution-boundary
  rebinds after a later accepted generated-card choice executed first. One
  rebound action was natively cancelled without a false successor; the other
  proved from its rebound boundary with no intervening Human start. All eleven
  transition unknowns were individually audited and remain correct fail-closed
  outcomes. Lethal cross-surface settlement and Full-Run surfaces remain
  opportunistic. No corpus or training authority is implied.
- Complete at schema-2 source/test: acceptance no longer assigns semantic S;
  exact execution consumes a state-complete boundary, action-catalog readiness
  is independent, unknown does not cascade into the next proved execution
  boundary, and Close drains semantic work before bounded unknown disposition.
  Exact artifact `eb7ed072... / 34a36a2b...` is cold-loaded in runtime
  `b24a9d44...`; latest owner session `session-20260827T042832Z-...` proves 19
  transitions, including rapid Play, generated-card select and a direct Play ->
  End Turn execution handoff, with zero false/interleaved proof and complete
  accounting for all 31 accepted roots. Exact schema-2 execution reorder,
  catalog-incomplete Live handoff and successful pending-edge Close drain remain
  targeted evidence gaps; predecessor schema-1 evidence is not transferred.

## Full-Run Human Semantic Timeline

- Superseded as canonical one-step authority: existing `transition_proved`
  counts are trace-level lifecycle/state evidence. They require the mechanical
  calibration gate and currently yield zero canonical one-step rows in the
  latest exact session.

- Active on a short-lived topic branch from `develop`; ordinary combat schema 2
  is frozen as the regression oracle rather than tuned to reduce unknowns.
- Complete at source/test: semantic state Reads are interaction-specific instead
  of inheriting the combat-only profile.
- Complete at source/test: lethal combat may settle at the first complete reward
  boundary; reward claim/proceed and card reward select use exact direct UI
  delivery witnesses; map travel reuses the game-owned
  `VoteForMapCoordAction` lifecycle.
- Failed predecessor runtime gate: the first owner canary exposed direct-UI
  execution binding and paused-parent lifecycle defects; strengthened audit
  rejects its truncated trace rather than transferring its evidence.
- Complete at source/test/build: the repair preserves parent lifecycle through
  native finish, uses one canonical direct-UI boundary, and adds Combat hand
  select/replace/deselect/confirm through the same bounded UI-commit mechanism.
- Complete bounded exact-runtime gate: owner session
  `session-20260827T151912Z-4c7f26e56b954b498cfa0c3213e4b488` passes the repair
  canary for semantic accounting, including repaired canonical direct-UI
  binding and parent-lifecycle retention.
- The predecessor long owner run proved one enemy-targeted potion and exposed
  self-target operand normalization and accepted-action accounting defects in
  the Annotator witness; the subsequent bounded repair-artifact gate below
  closes those defects.
- Complete bounded repair-artifact gate: the latest closed owner session proves
  233 accepted/233 semantic transitions, including enemy-target, no-target and
  self-target potion use, with zero unresolved. Target-picker cancel remains
  unexercised.
- Bounded Human-proved on artifact `4fa67570... / 51c7c37b...`: semantic evidence schema 3 stores exact
  content-addressed H/S/S' frames behind role references; legacy schema-1/2
  remains auditable. The latest closed session accounts for 333/333 proved
  actions with zero unknown and reduces persisted Reads to 5.354 per accepted
  action without creating a new boundary proof.
- Remaining Live gaps: potion target-picker cancel, hand
  select/replace/deselect, generated skip, room-internal
  event/shop/rest/treasure actions and run entry.
- Complete at subsequent source/test: Human potion use arms from the native
  holder, binds only at `EnqueueManualUse`, and then reuses the typed
  `UsePotionAction` lifecycle. Target-picker cancel produces no accepted action;
  programmatic Connector use has no Human arm and is not recorded.
- The profiler artifact is Human-exercised and attributes 50.47% of recording
  wall time to synchronous Player Environment capture, including an
  unconditional 50 ms legacy recovery/status probe. Current source removes idle
  capture, gates recovery on real debt, buffers hot append streams and durably
  seals them at Close. Complete build/install/cold-load: exact artifact
  `bb37d34f... / 3587836e...` is Ready in Connector runtime `9a42d54c...` with
  exact sole-Platform Modset; loaded verification passes without requiring a
  session environment heartbeat. Next gate: run one short Play/End Turn/rapid-
  chain canary for quantitative before/after plus semantic audit. Then close
  target-picker cancel and representative generated-skip/
  hand-selector gaps before Event options.
- Non-combat room expansion follows a clean Combat mechanism canary.
- Later: run entry/terminal, representative long-act run, then continuous Full
  Run. Rare content remains targeted evidence and does not block the mainline.
