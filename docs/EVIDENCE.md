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
- current-artifact 1/2/4-worker capacity measured `0.4981`, `0.8975`, and
  `1.5246` aggregate normalized decisions/s;
- a 2-worker x 2-episode smoke delivered 32 decisions through four unique
  runtime/profile generations, with no worker, endpoint or process leak;
- a separate bounded sentinel run left the normal user-data tree unchanged at
  1,051 files and tree digest `f9e58712...`.

Native shutdown returned code zero without forced fallback, but bounded exits
still emitted roughly 950-1000 Godot diagnostics. Recovery is operational, not
clean-shutdown qualification. A predecessor artifact's 8-worker `2.9085`
decisions/s result remains historical and is not merged into current evidence.

See the [capacity/recovery closeout](evidence/WINDOWS_REFERENCE_CAPACITY_RECOVERY_AND_MANAGED_ADMISSION_2026-08-16.md)
and [seed/differential/supervisor closeout](evidence/WINDOWS_REFERENCE_SEED_DIFFERENTIAL_SUPERVISOR_AND_REQUALIFICATION_2026-08-16.md).

## Non-Claims

- H2 is a bounded test consumer, not a full-run Agent or gameplay policy.
- Same-artifact, same-seed repeatability is not deterministic replay or
  cross-Host semantic equivalence.
- No long soak, broad cross-platform support or Training-Ready claim is made.
- The game wrote the active Steam profile and cloud store during H2.
- Delivery Receipt does not assert business completion.
- Old Connector, Live-UI, fixture, or external-project evidence is not evidence
  for the current exact Headless tuple.
- Operational recovery does not imply clean shutdown; the shipped Windows
  headless runtime emitted about 950-1090 Godot diagnostics after native quit
  across recorded artifact windows.
- A manually replaced local SDK is not reproducible release evidence.
- The local shared-profile sentinel does not inspect Steam Cloud server state.
- The update planner and drift fixtures are not a real changed-build drill.

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
```

Each probe creates a timestamped local directory with process logs, identity,
structured events, verdict, and non-claims. Never attach that directory to a
public issue without reviewing it for Steam identifiers and save information.
