# Evidence

Evidence is cumulative, exact, and non-transferable.

Every generated report records the Headless version, Git revision when
available, worktree state, deterministic public-source digest, and source file
count in addition to game and Connector identity.

| Level | Proves | Does not prove |
|---|---|---|
| source | reviewable implementation | build or runtime behavior |
| unit | game-independent logic | game boot |
| disk identity | measured installed files | those bytes loaded |
| boot | recorded real process behavior | gameplay mutation |
| loaded | process-reported Host/game/Modset identity | action correctness |
| control | named real input and safety boundary | broad coverage |
| journey | named surfaces in one bounded real run | full game or determinism |
| differential | parity for named cases | universal equivalence |
| performance | measured workload | semantic correctness |

Journey reports keep execution integrity separate from named-surface coverage.
Canonical decision digests support first-divergence analysis but do not replace
the exact raw Snapshot, action, Receipt, or Host identity.

## Current Exact Runtime Evidence

The current source independently reproduced these gates on macOS arm64, STS2
`v0.111.0` / `41cef1ea`, Connector source `154a5cd`, protocol `1.0.0`, DLL SHA
`81f9447a...`, MVID `967fd749...`, and exact Connector-only Modset:

- **H0 pass**, runtime `ff260126721a42529cbb7e742eb438dd`: shipped Godot
  process started with the headless display driver, official Mod loaded, and an
  interactive main-menu snapshot mounted.
- **H1 pass**, same runtime: current opaque menu action delivered, duplicate
  request returned the same action/Receipt, stale snapshot was refused, and a
  different interactive successor mounted. A saved run may natively resume
  directly to its current decision; the harness does not reconstruct that flow.
- **H2 pass**, runtime `1312db720419489ab1ae364456ca9557`: 10 delivered
  actions crossed `main_menu`, `singleplayer_menu`, `character_select`,
  `event_option`, `reward_claim`, `map_navigation`, and `combat_turn`; three
  combat deliveries completed; `run_deck` and `combat_piles` Reads were
  complete; unknown deliveries and Read failures were both zero.

The H2 policy selected only current Connector-published BoundActions. It did
not inspect native operands or reconstruct card, target, event, or map legality.

Raw runtime logs, saves, local paths, and reports remain under `.local/evidence`
and are never published. The machine-readable Connector 1.0.1 runtime seal is a
public GitHub Release asset.

## Windows Experimental Evidence

On the exact Windows x64 STS2 `v0.111.0` / `41cef1ea` tuple, the shipped runtime
created native SettingsSave v8 plus prefs/progress in isolated namespaces with
Steam disabled before platform initialization. The current branch additionally
has exact-template reset, process-local endpoint, game-owned seed provenance,
capacity, fault/restart, semantic repeatability, bounded supervisor and local
profile-write evidence.

The current evidence artifact is Connector source `3e5c5a8...`, protocol
`1.0-rc.2`, DLL SHA `e9673497...`, MVID `c5bcd426...`, and its exact
Connector-only Modset:

- two independent seed `H1D1FF01` runs produced 14 matching canonical semantic
  events each with distinct runtime/profile generations and no first divergence;
- one crash/restart cycle preserved seed `H1REC0VERY01`, replaced runtime and
  profile generation, returned five stable decisions, and released the process
  and endpoint;
- one suspended-process hang cycle preserved seed `H1HANGREC0VERY01`: the
  process remained alive while its endpoint timed out, then a hard reset
  replaced runtime/profile generation and delivered five recovery decisions;
- current-artifact 1/2/4-worker capacity measured `0.4981`, `0.8975`, and
  `1.5246` aggregate normalized decisions/s;
- two default current-route 8-worker windows delivered 64 decisions each at
  `2.3207` and `2.2989` decisions/s, but both failed lifecycle admission: one
  had an unclassified RID/shader diagnostic chain and the other had two
  workers exceed the null-texture count bound;
- `--single-threaded-scene` measured `2.3038` decisions/s with bounded
  containment once, then `2.3660` decisions/s with one worker exceeding the
  `Invalid Task ID` bound. With no material throughput or repeatable
  reliability gain, the candidate was rejected and removed from the CLI;
- a 2-worker x 2-episode smoke delivered 32 decisions through four unique
  runtime/profile generations, with no worker, endpoint or process leak;
- a separate bounded sentinel run left the normal user-data tree unchanged at
  1,051 files and tree digest `f9e58712...`.
- an exact-build official AutoSlayer research Mod completed 50 room entries
  across three acts in `394.5s`, exited zero, restored the Connector-only disk
  Modset byte-for-byte and left the shared-profile sentinel unchanged. It used
  `0.151` average CPU cores and `0.865 GiB` peak RSS.

Native shutdown returned code zero without forced fallback. The current
phase-aware policy rejects unknown, misplaced and over-limit diagnostics. A
two-worker run first rejected one pre-shutdown `Invalid Task ID`; after an exact
three-occurrence bound was justified from the local corpus, clean Headless
source `63d03ee...` passed a new two-worker run with only known post-shutdown
diagnostics. This is a bounded containment candidate, not clean-shutdown or
long-soak qualification. A predecessor artifact's 8-worker `2.9085`
decisions/s result remains historical and is not merged into current evidence.

See the [capacity/recovery closeout](evidence/WINDOWS_REFERENCE_CAPACITY_RECOVERY_AND_MANAGED_ADMISSION_2026-08-16.md),
[8-worker/scene-thread experiment closeout](evidence/WINDOWS_REFERENCE_EIGHT_WORKER_AND_SCENE_THREAD_EXPERIMENT_2026-08-16.md),
and [seed/differential/supervisor closeout](evidence/WINDOWS_REFERENCE_SEED_DIFFERENTIAL_SUPERVISOR_AND_REQUALIFICATION_2026-08-16.md).

## Managed Exact Candidate Evidence

The rebuilt macOS arm64 candidate uses upstream `d11aa883...`, patch
`708c51c...`, Host artifact `34aa29f...` / MVID `ff6c7349...`, and the
byte-identical exact `v0.111.0` game assembly `9cb4f1a...` / MVID
`57785517...`.

- 24/24 targeted native binding gates passed, including exact negative
  identities, canonical treasure projection, native treasure vote/effect, and
  successor;
- three clean-source fair-player episodes delivered 381/381 actions plus 384 Reads with
  three matched game-owned seeds, zero unknown, and three `game_over`
  boundaries;
- clean-source serial profiling measured `D_engine`, `D_train`, and `D_qual` at
  `297.66`, `234.25`, and `213.25 d/s` mean respectively;
- the shared-supervisor training profile measured `256.32/506.93/992.41/
  1,390.72/1,676.64/1,865.33 d/s` at 1/2/4/6/8/10 workers and plateaued at
  `2,451.00 d/s` with 24 environments on the 10-core M4;
- per-worker Node supervisors did not improve throughput and materially
  increased memory; raw JSON serialization added only `0.069 ms/decision`.

The projection remains explicitly partial, treasure was targeted rather than
organic, and no run passed Act 1 floor 7. These results establish a fast
candidate and working bounded lifecycle, not semantic equivalence, complete
gameplay, H1.0, or Training Ready. See the [managed exact closeout](evidence/MANAGED_EXACT_NATIVE_REWARDS_TREASURE_AND_CAPACITY_2026-08-17.md).
The performance and Host-route interpretation is in the
[performance route closeout](evidence/MANAGED_HOST_PERFORMANCE_ROUTE_SELECTION_2026-08-21.md).

## Non-Claims

- H2 is a bounded test consumer, not a full-run Agent or gameplay policy.
- Same-artifact, same-seed repeatability is not deterministic replay or
  cross-Host semantic equivalence.
- No long soak, broad cross-platform support or Training-Ready claim is made.
- The game wrote the active Steam profile and cloud store during H2.
- Delivery Receipt does not assert business completion.
- Old Connector, Live-UI, fixture, or external-project evidence is not evidence
  for the current exact Headless tuple.
- Operational recovery does not imply clean shutdown. The shipped Windows
  headless runtime emitted about 950-1090 diagnostics around native quit across
  recorded artifact windows; the current policy exposes and bounds them rather
  than declaring them harmless.
- A manually replaced local SDK is not reproducible release evidence.
- The local shared-profile sentinel does not inspect Steam Cloud server state.
- The update planner and drift fixtures are not a real changed-build drill.
- AutoSlayer room/native-action log rates are not normalized semantic decision
  throughput, cross-Host parity, Connector conformance or policy evidence.
- Managed capacity is partial-canonical performance evidence. It is not
  cross-Host parity, full-run coverage, 1M reliability, or learning transfer.
- `D_engine` is an internal semantic-floor measurement, not a canonical Player
  Environment decision. High-core, cluster, Hybrid, snapshotable and simulator
  ceilings remain estimates until measured.

## Reproduce

With no STS2 process running:

```bash
npm run doctor
npm run probe:shipped -- --shared-profile
npm run probe:menu-control -- --shared-profile
npm run probe:journey -- --shared-profile
npm run bench:capacity -- --template vanilla-clean --workers 1,2,4,8
npm run probe:recovery -- --template vanilla-clean --experimental-build
npm run probe:differential -- --template vanilla-clean --seed H1D1FF01 --experimental-build
npm run soak:reference -- --template vanilla-clean --workers 2 --episodes 2 --actions 8 --experimental-build
npm run drill:update

# Experimental and unqualified; requires the exact admitted game build.
npm run experiment:managed -- prepare
```

Each probe creates a timestamped local directory with process logs, identity,
structured events, verdict, and non-claims. Never attach that directory to a
public issue without reviewing it for Steam identifiers and save information.
