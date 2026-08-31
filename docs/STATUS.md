# Current Status

## Repository governance

Public `main` commit `5604050ef0e0f55f13bf2fdb720e5c215d774fd5`
is preserved as the non-release
`baseline/pre-governance-platform-20260827`. It is the frozen historical
integration baseline, not yet a stable-release claim. `develop` was created
from that exact commit and is now the only normal integration target. No public
history was rewritten. See [Development Workflow](DEVELOPMENT_WORKFLOW.md).

GitHub enforcement is an operational fact separate from this source document;
the workflow records intended rules, while repository settings must be checked
directly before claiming protection.

## Native Foundation candidate

PR #3 and PR #5 are integrated in `develop@c751952...` through the protected
PR flow. Their gameplay-safe semantic baseline, Native Foundation Combat and
PlayerChoice adapters, exact Windows Human closeout, and final Ritsu route
decision are now durable integration history. Ritsu remains an external design
reference only: there is no runtime or package dependency.

PR #6 is the sole active topic and is directly based on that `develop` commit.
Its continuation source deepens Map, Reward, CardReward and Treasure from
owner discrimination to four typed Native Foundation decision adapters.
The providers read `RunState`/`MapTravel`, the exact `RewardsSet`, and exact
card-reward option arrays. Connector now intersects those native catalogs with
current delivery controls and revalidates native membership at execution;
Annotator consumes the same catalog without gaining legality or mutation
authority. UI-derived reachability/reward/option publication is demoted from
semantic authority. Restacked Treasure component source `9f89d5e...`
additionally binds
the exact `TreasureRoom`/run owner and `TreasureRoomRelicSynchronizer`
collection, with `closed/opening/relic_choice/resolving/completed` stages and
exact `open/select/skip/proceed` membership. The pre-restack
Treasure-continuation artifact `3bc44ddb... / 708ecfab...` built cleanly and
cold-loaded on macOS in
Connector runtime `955e5b02...`, environment `722a4149...`, sole-Platform
Modset `b6b669df...`, with rollback `2026-08-31T03-07-08.478Z`. Startup logs
contain no Platform or Harmony errors. This evidence does not transfer to the
restacked source. A build-provenance omission was also
fixed so every compiled game-Mod composition source is now identity-bound.
This is predecessor bounded main-menu T2 evidence only. The earlier
Map/Reward/CardReward
batch targeted tests and exact clean macOS build pass as
artifact `3e3ebc3c... / 53568805...` against STS2
`v0.111.0 / 41cef1ea / 9cb4f1ad... / 57785517...`. Safe install, rollback
capture and cold-load also pass on macOS in Connector runtime `2c94849e...`,
environment `8db5a2af...`, exact sole-Platform Modset `2f4b276f...`.
Annotator initialized Ready/no-session and the startup log contained no
Platform initialization error. This is predecessor bounded main-menu runtime
evidence: Map/Reward/CardReward/Treasure decisions and Human recording remain
unexercised. The current restacked source has source/test claims only until a
new exact build and runtime qualification are produced. Public
protocol 1.0.0 remains compatible; `Receipt.Successor` is still an immediate
post-delivery observation, not canonical causal `S'`. See the
[continuation source closeout](evidence/NATIVE_FOUNDATION_FULL_RUN_SOURCE_CLOSEOUT_2026-08-31.md)
and [Treasure closeout](evidence/NATIVE_FOUNDATION_TREASURE_SOURCE_CLOSEOUT_2026-08-31.md).

The PR #6 source-hardening pass keeps those authority boundaries unchanged.
Native action keys and exact-once membership now use one mechanical Native
Foundation catalog helper instead of provider-specific copies. Reward capture
returns its exact unselected native rewards with the action catalog, removing
repeated weak-owner reads and per-button reward-array allocation. Connector
builds request-local reference indexes for Map, Reward and CardReward
presentation intersection, while every delivery still re-captures native
membership. A dead reward/map handoff helper and a test-only Witness key wrapper
were removed. Existing profiling identifies full Snapshot capture as the
dominant recorder cost; this pass proves reduced catalog enumeration and
allocation only, not a runtime latency improvement. No public protocol,
semantic authority, loaded artifact, or Human evidence claim changed.

Connector, Annotator, unified-Mod targeted checks and the full portable suite
pass for component source commit `a3bcd37...`. Clean build, safe install and
cold-load pass as artifact `9a89f1fe... / b1c34f90...` against exact STS2
`v0.111.0 / 41cef1ea`, runtime `b57a37b4...`, environment `f0cbd53a...`, and
sole-Platform Modset `d5054e7b...`. Live controller conflict, stale rejection
and request idempotency pass without gameplay mutation. Shipped headless H0
passes in runtime `efd022e9...`; live/headless main-menu canonical digests are
equal (`71e246ab...`). This parity claim is menu-only, and predecessor PR #3
evidence does not transfer.

Fresh Windows evidence is independently bound to artifact
`a681f8b1... / 7c42c4c3...` and shipped STS2
`v0.111.0 / 41cef1ea / 0861bfa1...`. Artifact-plus-native-settings rollback,
cold-load, sole `STS2_PLATFORM` Modset `e5693d19...`, Connector controller/
stale/idempotency checks, and shipped-headless H0 pass. Visible runtime
`7a1942b6...` and headless runtime `49f34fbf...` agree on the main-menu-only
canonical digest `eaf8516d...`. Recorder initializes Ready/no-session and its
portable lifecycle checks pass. The subsequent exact owner session
`session-20260831T072650Z-b0608291ae7f416d96b058078f441794`, runtime
`d8a10ba2...`, environment `9e0e0cfe...`, and sole-Platform Modset
`1f1bdecc...` passes 35/35 Decision V2 records and accounts for all 37 native
roots as 36 exact-once successes plus one correctly cancelled End Turn. It
exercises ordinary/targeted Play, potion Use, End Turn, three complete
PlayerChoice pause/resume pairs, repeated lethal-to-Reward/CardReward/Map
handoffs, and Recorder New/Pause/Resume/Close. See the
[Windows pre-Human gate](evidence/NATIVE_FOUNDATION_WINDOWS_PRE_HUMAN_GATE_2026-08-31.md).
The exact Human result is recorded in the
[Windows closeout](evidence/NATIVE_FOUNDATION_WINDOWS_HUMAN_CLOSEOUT_2026-08-31.md).
RitsuLib was audited at upstream v0.5.18/main `f224961...` and development
`c466809...`; retrofit and Ritsu-first counterfactual evidence removed zero
whole semantic categories. [ADR 0004](adr/0004-native-foundation-and-ritsu-route.md)
therefore freezes `RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`; see the
[final decision packet](evidence/RITSU_ROUTE_FINAL_DECISION_2026-08-31.md) and
[source closeout](evidence/NATIVE_FOUNDATION_SOURCE_CLOSEOUT_2026-08-30.md).

Phase: **bounded Human-proved native semantic sequential lane**.
Commit `4384a14...` is preserved as the non-release tag
`baseline/pr3-gameplay-safe-4384a14`; it removed the evidence-dependent global
UI gate after that gate blocked ordinary unsupported gameplay. ADR 0003 is now
a historical candidate under re-audit, not active gameplay authority. Current
native source adds an independent read-only stream at the exact execution seam:
it compares the current UI catalog with `S_sem + A_sem(S)` derived from STS2's
logical combat state and native card/potion validators. It tracks exact
accepted action identity, cancellation, pre-Commit abort, player-choice
pause/resume and terminal lifecycle without changing scheduling, legality or UI
delivery. Source, deterministic tests and a clean exact build pass as artifact
`d3b59bed... / 04acd691...`. It is safely installed and cold-loaded in runtime
`f015b026...`, environment `190234e4...`, exact sole-Platform Modset
`968a30c3...`. Closed owner session
`session-20260830T064823Z-ed1d683fe0b44e1db312c7489cda7fba`
records 41 accepted and 41 successful roots with 41 exact-once memberships in
native `A_sem(S)`, zero unknown/cancel/abort, and two complete player-choice
pause/resume pairs. The UI frame was not authoritative for 34 roots and a
complete UI catalog omitted the executing root seven times. This supports
`FEASIBLE_FULL_RUN_NATIVE_SEMANTIC_RECORDER_EXISTS` for the bounded combat
mechanisms; it does not prove overlapping acceptance, cancel/abort, final
successor semantics or Full Run. Audit-only source `193861a...` fixes stream
aggregation for that closed session without changing or inheriting the loaded
native artifact. The game process was exited after recording Close. See the
[source closeout](evidence/NATIVE_SEMANTIC_RUNTIME_DISCRIMINATOR_SOURCE_CLOSEOUT_2026-08-30.md)
and [Human closeout](evidence/NATIVE_SEMANTIC_RUNTIME_DISCRIMINATOR_HUMAN_CLOSEOUT_2026-08-30.md).

Normalized semantic evidence schema 3 is now bounded Human-proved for its
trace-level accounting and storage contract on exact
artifact `4fa67570... / 51c7c37b...`, runtime `7bcc19e7...`, exact Modset
`2263e395...`. Closed session
`session-20260829T052157Z-e549d3601e7640f997b6f475180b2dfe` has 333
accepted/started/finished/proved actions with zero unknown, cancellation, abort
or unresolved disposition. It exercises Play, End Turn, three potion uses,
reward claim/proceed, card reward and generated-card selection, Combat hand
confirm and map travel. Exact role references resolve to 947 immutable frames;
the event log contains no inline H/S/S' frames and persisted Reads fall to 5.354
per accepted action from predecessor rates of 23.97-28.63. Independent Decision
V2 audit passes 188/188. This proves bounded trace semantics and storage for
that exact artifact, not canonical one-step training, exhaustive Full Run, or
elimination of owner-perceived lag.

Current source adds a Close-time bounded stage profiler for snapshot/Read capture,
serialization, hashing, object writes and durable appends. The profiler changes
no action or semantic authority and has source/test evidence only; the Human run
above cannot supply its timings. See the
[schema-3 closeout](evidence/SCHEMA3_HUMAN_DATA_LIFECYCLE_CLOSEOUT_2026-08-29.md).

The subsequent exact profiler session
`session-20260829T072035Z-807f6a97b0e8498a828bb25c84e04ae4`, artifact
`f1afebd2... / a618ef18...`, runtime `74c63f9a...`, independently passes
102/102 Decision V2 records and accounts for 267/267 proved semantic actions.
It also causally explains owner-perceived lag: 12,394 synchronous Player
Environment captures consume 251.633 seconds, 50.47% of its 498.578-second
recording window. The old combined Snapshot probe alone runs 10,676 times and
consumes 214.890 seconds. Current source removes idle polling, gates legacy
recovery capture on actual recovery debt, reuses lifecycle-requested status
frames, buffers hot evidence appends and durably seals every stream at Close.
Those repairs are source/test/build/install/load complete as exact artifact
`bb37d34f... / 3587836e...`, cold-loaded in process `22308` and Connector runtime
`9a42d54c...` with exact sole-Platform Modset `90f3c7f3...`. Loaded verification
passes in the legal Ready/no-session state; after-repair Human latency and
semantic evidence remain pending and do not transfer from the predecessor. See the
[causal performance baseline](evidence/RECORDER_CAUSAL_PERFORMANCE_BASELINE_2026-08-29.md).

The after-repair owner session
`session-20260829T084437Z-cc4079776c9e417eba53a122e452cab7` is exact evidence
for the loaded artifact above. It accounts for 933/933 trace-level proved
actions and 497 valid Decision V2 records, but also records 31,613 synchronous
full Player Environment captures consuming 628.720 seconds (27.519% of the
2,284.687-second window), with one call reaching 273.851 ms. Mechanical
canonical calibration classifies 247 actions as `S + A(S)` with unresolved S',
682 as execution-time state/action-space unresolved, and 4 rejected; canonical
`S + A(S) -> A -> S'` is 0. See the
[canonical causality decision](evidence/RECORDER_CANONICAL_CAUSALITY_DECISION_2026-08-29.md).

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
- Recording startup now stops at `Ready`. The typed RecordingService owns
  status, idempotent lifecycle commands and a bounded ordered event stream;
  explicit New Session supports multiple isolated session/timeline/store
  lifecycles in one STS2 process. Pause preserves admitted pending settlement,
  and Close waits for pending settlement/invalidation before flush/dispose.
  Application events and closeout state are operational projections, not Human
  Evidence or action authority.
- The rapid-input repair observes exact accepted roots at
  `GameAction.OnEnqueued` and records an additive bounded native lifecycle
  ledger. A single finished action may retain strict V2 successor eligibility;
  any accepted overlap explicitly invalidates every action in that causal
  window. The next decision pre-frame is never reused as the prior action's S'.
  Decision V2 is unchanged, and persistence uncertainty remains no-retry.
- Exact artifact `080701b3... / 142054a5...` proved that repair in runtime
  `39fa2d2e...`: 35 accepted roots all started and finished, with 12 strict
  admissions, 23 explicit invalidations and zero unresolved lifecycle. Current
  ledger v2 source additionally stores each accepted root's frozen pre-frame,
  exact mapping and BoundAction so invalidated decisions are independently
  classifiable. Ledger v2 is now exercised by the later owner session described
  below; its evidence does not transfer to the new semantic-boundary source.
- The semantic-boundary observer keeps Decision V2 unchanged and adds an
  independently audited observation sidecar. Exact accepted/started/choice/
  cancelled/finished facts are coordinated with a complete authoritative
  Player Environment capture before the next tracked Human action executes or
  at the next complete decision surface. Queued cancellation before start is
  not a successful A; cancellation after start and incomplete capture remain
  unknown. One exact-build read-only PlayCard abort witness prevents native
  `Finished` without resource/OnPlay Commit from becoming a false transition.
  Predecessor artifact `2cb46ead... / 66ed1396...` was installed and cold-loaded
  in runtime `af2e7370...`. Its owner canary retained all 22 accepted actions,
  including one real generated-card choice, but exposed acceptance/execution
  reordering: the choice executed before an earlier queued End Turn. Current
  source orders causal settlement by execution and rebinds a precommit only to
  a complete authoritative boundary immediately before its execution. The old
  sidecar now fails strengthened audit with
  `semantic_transition_pre_not_execution_boundary`; its two Decision V2 records
  remain readable. Corrected source `cb20bfa...` is now built, installed and
  cold-loaded as `04104ca5... / 7408a183...` in runtime `97829317...` with the
  sole exact `STS2_PLATFORM` Mod. Initial owner session
  `session-20260826T150700Z-...` accounted for all 35 accepted Human
  actions: 24 proved transitions, six explicit unknowns, four native
  cancellations and one PlayCard abort before Commit. It exercised rapid
  chains, End Turn, generated-card select and player-choice pause/resume; every
  acceptance has one disposition, all proved pre-states match their exact
  execution boundary, and no proof crosses another Human action start. The
  predecessor's exact acceptance/execution reorder did not recur in that run.
  Latest closed owner session `session-20260827T014202Z-...`, runtime
  `2388aba0...`, then exercised two complete execution-order rebinds after a
  later accepted generated-card selection started first. One rebound action was
  natively cancelled without a false S', and the other proved a transition
  whose semantic pre exactly equals its complete execution boundary. All 29
  acceptances have one disposition: six proved, eleven explicit unknown, four
  cancelled before start and eight cancelled after start. The eleven unknowns
  are individually audited as six incomplete execution boundaries, four
  incomplete successors before the next Human start and one owner Close before
  successor; none is an unaccounted cancellation or implementation defect.
- Semantic trace schema 2 is loaded from exact Annotator source `fed721c...` as
  unified artifact `eb7ed072... / 34a36a2b...` in runtime `b24a9d44...`, STS2
  `v0.111.0 / 41cef1ea`, with the sole exact `STS2_PLATFORM` Mod. Latest owner
  session `session-20260827T042832Z-...` has 31 accepted roots and exactly one
  disposition each: 19 proved, nine cancelled before start and three cancelled
  after start with unknown transition; there are no standalone boundary
  unknowns or unresolved roots. All proved pre-states match their exact
  execution boundary, no proof crosses another Human start, generated-card
  select is proved, and a direct Play -> End Turn handoff has `A.S' == next S`.
  Acceptance/execution reorder did not recur on this artifact. See the
  [schema-2 owner closeout](evidence/SEMANTIC_TIMELINE_OWNER_CLOSEOUT_2026-08-27.md).
- Full-Run batch 1 keeps the same `SemanticBoundaryTracker` and adds only two
  native witness mechanisms: existing typed `GameAction` lifecycle for map
  travel, and exact source-local UI delivery callbacks for reward claim,
  reward proceed and card-reward select. Interaction-specific semantic Read
  policy removes the accidental combat-only `combat_piles` requirement from
  reward/map boundaries without weakening state completeness. Deterministic
  tests prove a continuous lethal -> reward -> card choice -> map timeline.
  This is source/test/build evidence only; see
  [Full-Run semantic coverage](FULL_RUN_SEMANTIC_COVERAGE.md).
  Exact source `509e5c6...` built unified artifact
  `fe3e3a82... / b1284288...` against STS2 `v0.111.0 / 41cef1ea`.
  Its owner canary exercised combat, generated choice, reward/card reward and
  map, but failed semantic accounting: direct UI commits used a non-canonical
  execution witness, and a paused parent was pruned before native finish,
  disabling the trace. Strengthened audit now reports 546 missing accepted
  roots instead of the former false PASS. Repair source `c8775e1...` retains
  parents through native terminal lifecycle, canonicalizes direct UI execution,
  and adds exact Combat hand select/deselect/confirm witnesses. Clean artifact
  `8d2f7d2a... / 3043f4f4...` is safely installed and cold-loaded in runtime
  `fb5a82ea...` with the sole exact `STS2_PLATFORM` Mod. The latest repair
  canary passes bounded semantic accounting: 250 schema-2 accepted actions,
  248 proved, two cancelled before start and 0 semantic unknown/unresolved; its
  193 native accepted roots are all accounted for. See the
  [batch canary](evidence/FULL_RUN_BATCH1_OWNER_CANARY_2026-08-28.md).
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
  `a7b11d93... / c3e7127a...` contains Connector, Annotator and Live UI and is
  cold-loaded against STS2 `v0.111.0`, assembly
  `9cb4f1ad... / 57785517...`, in runtime `bd6b73e7...`. The exact Modset contains
  only `STS2_PLATFORM`; Connector reports `execution_available=true`, and the
  Recording Application reports `Ready` with no implicit session.
- Live UI source `94ecc515...` uses only built-in Godot nodes and a
  `SceneTree.ProcessFrame` signal because standard single-DLL Mods do not run
  Godot's C# node source generator. The final artifact reports panel readiness;
  its K toggle was not observed by the non-human verifier and remains an owner
  visibility gate rather than an automated claim.
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

The Recording Application artifact was built, safely installed, cold-loaded and
identity-verified. Its exact SHA/MVID is
`a7b11d930c0d5b2dee22ac7ce5faea7bc5db84802b5b36bfb27e8258320c9c0f /
c3e7127a-93bf-4e29-9c05-257b5089edc6`; runtime
`bd6b73e7c2744680848539f96b6cae6d` reports environment
`f8de356a0991d09c687538bccb831ebde72a53a6e11d75176f333a6c40fdcc9f`,
exact unified Modset `89f0a3ed...`, and Ready/no-session state. Rollback is at
`apps/game-mod/.local/deployments/2026-08-25T10-36-08.359Z`.

The owner then exercised the K workspace, New Session, Pause/Resume,
pending-safe Close and a second isolated session in the same process. Both
sessions closed safely, proving the Recording Application lifecycle. They
admitted zero decisions: independent audit found that the unified
`exact_platform_modset` passed runtime admission but the record validator still
required the predecessor observer-Modset spelling. The resulting append error
also retried successor Read persistence until timeout. The dated
[owner validation](evidence/RECORDING_APPLICATION_OWNER_VALIDATION_2026-08-25.md)
records exact sessions, counts, attribution and non-claims.

Current source uses one exact-Modset predicate in runtime and independent audit,
and turns any evidence-commit exception into one unknown/no-retry invalidation.
The repaired artifact is now built, installed and cold-loaded as
`d3b25e628068f4a6946be0c1182f00745fd6195f9c0b02920bc9bc699b2d0b2d /
ee78d9a1-791e-4582-b8fa-97cc1949cd2a` in runtime
`88db3f9cf1e940ba906cab09e87714df`. It reports Ready/no session under exact
Modset `bfc65fbb...`; rollback is
`apps/game-mod/.local/deployments/2026-08-25T11-41-44.984Z`. Owner session
`session-20260825T115335Z-08907007f20a49318573f638ff627696` then independently
audited `6/6` admitted end turns with 24 materialized Reads and zero Read
failures. Twenty-one attempted `PlayCardAction`s failed closed; because
play-card is in the active profile, this is a recorder defect rather than an
out-of-scope boundary. The first Close actually completed in 5.036 ms, but the
UI retained the command's intermediate `Closing` text until another click. The
dated [decision gate](evidence/RECORDING_APPLICATION_DECISION_GATE_2026-08-25.md)
records exact attribution.

Current source accepts the complete frame staged at native card selection across
the expected transient card-play snapshot while retaining exact card, runtime,
environment, interaction, monotonic-sequence, bounded-age and controller checks.
Recording status separately reports recorded, native-accepted-but-failed,
supported-not-observed and declared-out-of-scope outcomes. The Live UI reads
authoritative post-command status, so a completed first Close displays Closed.
The repair is built, installed and cold-loaded as
`06f62285b11df705bcaf269d0da39f0ad291973f5bd16e189045833271e8aa67 /
17981f40-4d76-4d06-9e15-b4184cb9707c` in runtime
`e3a89aaef04042f988697374960801af`, under exact unified Modset
`1d8b001f...`. Rollback is
`apps/game-mod/.local/deployments/2026-08-25T12-12-57.940Z`. Owner sessions
`session-20260825T121841Z-...` and `session-20260825T122157Z-...`
independently audit 26/26 and 13/13 records: 25 card plays and 14 end turns with
158 materialized Reads and zero Read failures. A no-pending Close reached
`session_closed` in 4.139 ms after one request. Another Close correctly remained
`Closing` while an admitted end turn was pending, then closed after its bounded
successor timeout. This proves card correlation, authoritative Close display and
owner-visible scope UI on this exact artifact.

Those sessions also exposed a scope-accounting defect: 42 pre-frame
invalidations were emitted at native UI method entry before STS2 decided whether
an action was accepted. Exact `NCardPlay.TryPlayCard` includes cancel,
missing-target, invalid-target and `TryManualPlay == false` paths, so those
counts cannot be claimed as accepted actions that should have become records.
Current source defers capture-failure invalidation until the expected game-owned
root action is actually enqueued. RecordingStatus revision 3 labels that
category `native-accepted but failed closed`.

That source is now built, safely installed and cold-loaded as
`887630f4f4505f7ce7889e855c64dd4593aa061d22ffb00a80dfaed0bbf3c342 /
14761ed4-fed3-4a50-8dd7-d731b2a8b94b` in runtime
`bcf2b3f1dc0545b8ba1867c4a6357fec`, under exact unified Modset
`977c56a6...`. Loaded Annotator and Live UI both bind source `305a2cac...`;
rollback is `apps/game-mod/.local/deployments/2026-08-25T12-37-06.961Z`.
The runtime is healthy at Ready/no-session, but no recording session was
created during the first owner interaction. The 39 predecessor Live records do
not transfer.

Owner-operated session
`session-20260826T025703Z-d499a75e9e484cbda2fa64f7bb1f552f`
then exercised the exact `887630f4... / 14761ed4... / bcf2b3f1...` runtime.
Independent V2 audit passed 19/19 records with zero invalid records: 10 card
plays and nine end turns, with 78 materialized Reads and zero Read failures.
Sixteen accepted card actions failed closed instead of becoming records: 15
lacked a complete same-context pre-frame and one overlapped a pending
successor. They were kept out of admitted evidence and are now visible in the
accepted-failure category. Close reached `session_closed` 5.153 ms after its
first request. The owner also attests that one cancelled/rejected card attempt
did not change record or invalidation counts; because an intentionally
unrecorded attempt has no machine event, that absence is not independently
machine-attributable.

The subsequent exact artifact `080701b3bf... / 142054a5...` loaded in runtime
`39fa2d2e...`. Owner session
`session-20260826T062916Z-957f201043a4456a89d13407682f0541` independently
audits 12/12 strict records and 94 materialized Reads. Its additive ledger has
140 events: 35 accepted roots, all 35 started and finished, 12 strict-admitted,
23 strict-invalidated and zero unresolved. The 23 card plays and 12 end turns
include rapid windows up to five actions; no invalidated row was admitted and no
admitted action had a prior overlap. This is Live evidence for accounting and
no-false-S' behavior, not for cancellation or player-choice lifecycle, which did
not naturally occur.

The same audit found that ledger v1 could not independently classify invalidated
roots as targeted or untargeted because it persisted identity/lifecycle but not
the frozen decision payload. Current source revises only the additive ledger to
v2: accepted events carry frozen Decision V2 pre, native witness, exact mapping
and BoundAction; lifecycle rows cannot repeat them; audit cross-checks admitted
payloads against Decision V2. Historical v1 ledgers remain readable. The
revision is clean-built, installed and cold-loaded as
`df5d2c61... / 9072e515...` in runtime `ebe7a9fc...`, exact Modset
`20b2de1a...`, with rollback at
`apps/game-mod/.local/deployments/2026-08-26T06-50-54.021Z`. Ready/no-session
proves identity and lifecycle initialization only. A later same-artifact owner
session `session-20260826T075502Z-9fe1ac91c78a48f9a8f4eeef204a3665`
independently audits 8/8 strict records, 26 invalidations and a ledger v2 with
33 accepted, 31 started, 28 finished, five cancelled, eight strict admissions
and 25 strict invalidations. Two cancellations occurred before start and three
after start. This is exact-runtime evidence for ledger v2 payload/lifecycle
accounting and cancellation observation; player-choice pause/resume did not
  naturally occur. The new semantic sidecar did not exist in that artifact.

The first semantic-sidecar owner session
`session-20260826T141755Z-0f4b31b20ac14b75a1ea3deaeed65caa` ran on exact
artifact `2cb46ead... / 66ed1396...` in runtime `af2e7370...`. It retained 22/22
accepted and started actions: 13 plays, eight End Turns and one generated-card
select. Twenty finished and two plays cancelled after start; every action has
one disposition. The run initially reported seven proved, 13 unknown and two
cancelled dispositions, but stronger causal audit rejects one proof because the
generated choice executed before an earlier accepted queued End Turn and the
End Turn was not rebound to its execution boundary. This is Live defect evidence,
not a semantic seal. See the dated owner-canary report.

## Non-claims

- Human origin is owner-attested and cannot be independently machine-proven.
  Unstable pre-frames still fail closed. Ledger v1 and v2 have exact-runtime
  evidence for bounded accounting in their observed rapid windows; neither
  retroactively proves a strict successor for overlap. The semantic-boundary
  sidecar predecessor run proves action accounting but contains one rejected
  semantic proof. Schema 2 is now owner-operated and proves bounded ordinary
  combat, generated-card select and direct execution handoff. Exact reorder on
  the schema-2 artifact, catalog-incomplete Live handoff, successful pending-
  edge Close drain, hand select/replace/deselect, generated skip, repaired
  self-target potion and target-picker cancel, room-internal
  event/shop/rest/treasure actions, run entry, exhaustive Full
  Run, semantic-free performance, game outcome success, corpus admission and
  training authority remain non-claims.
- H0/H1/H2 are automated real-runtime evidence, not human validation, a full
  game journey, durable qualification, semantic parity or long-soak proof.
- The optional noninteractive Host execution profile was not implemented by the
  current Connector and remains a non-claim; shipped-default semantics passed.
- Generated-card select is audited on the current schema-2 unified artifact;
  generated-card skip remains `not exercised`.
- The first potion owner canary proved one enemy-targeted use but exposed three
  self-target exact-mapping failures and one accepted self-target use without
  explicit accounting. Repair source `fba874e8...` follows STS2's owner
  normalization only through exact frozen-catalog matching and defers failures
  until native acceptance. Artifact `b5fbda12... / 1cbcff84...` is cold-loaded
  under the sole exact Platform Modset; Human proof is pending. Trace batching
  improved the narrow boundary-to-start median from about 8 ms to 4 ms, but
  perceived-lag elimination remains a non-claim.
- The V2 bundle/store/receiver/STPD path is verified, but this does not authorize
  corpus inclusion or training and does not qualify unexercised action families.
- Platform Evidence is a focused evidence-integrity package, not a
  Platform-wide gameplay SDK. Workbench and Live UI are application shells, not
  action/evidence authorities.
- The predecessor `d3b25e62...` artifact has six audited owner-attested end-turn
  records with bounded Reads. Its 21 play-card invalidations prove a current
  supported-family defect, not play-card coverage. The staged-card/UI clarity
  repair was loaded as `06f62285... / 17981f40...`; its two owner sessions prove
  card-play and Close. They do not prove accepted-only invalidation accounting
  introduced by the later source. The later `887630f4... / 14761ed4...`
  artifact now has 19 audited owner-operated records and explicit accepted-
  failure accounting. This does not prove lossless rapid-input capture or
  independently identify the cancelled/rejected attempt.
- The current S1 Policy Manifest is validated, but its exact checkpoint is not
  present on this Mac. Real-model Shadow, One-Step, Auto, policy Agent evidence
  and legacy/new path parity are therefore `not exercised`.
