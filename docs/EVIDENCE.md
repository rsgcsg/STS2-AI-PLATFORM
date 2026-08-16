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
has exact-template reset, process-local endpoint, capacity and fault/restart
evidence.

The latest recovery evidence belongs to Connector source `08a5990...`, protocol
`1.0-rc.2`, DLL SHA `97727e...`, MVID `7a6992a7...`, and its exact Modset. One
cycle changed profile generation and runtime instance, retained exact identity,
delivered three recovery decisions, and released both process and endpoint.
Its status is `recovery_operational_pass_shutdown_diagnostics_observed`, not
clean shutdown qualification.

Reference capacity belongs to the earlier Connector source `b9df6c1...` and
cannot be transferred to `08a5990...`. Measured aggregate normalized decisions/s
were `0.4966`, `0.9483`, `1.7388`, and `2.9085` for 1/2/4/8 workers. Eight
workers averaged `2.710` CPU cores and `5.696 GiB` summed peak RSS.

See the [dated Windows closeout](evidence/WINDOWS_REFERENCE_CAPACITY_RECOVERY_AND_MANAGED_ADMISSION_2026-08-16.md).

## Non-Claims

- H2 is a bounded test consumer, not a full-run Agent or gameplay policy.
- No deterministic replay, semantic differential, long soak, broad
  cross-platform support or Training-Ready claim is made.
- The game wrote the active Steam profile and cloud store during H2.
- Delivery Receipt does not assert business completion.
- Old Connector, Live-UI, fixture, or external-project evidence is not evidence
  for the current exact Headless tuple.
- Operational recovery does not imply clean shutdown; the shipped Windows
  headless runtime emitted about 1090 Godot teardown errors after native quit.
- A manually replaced local SDK is not reproducible release evidence.

## Reproduce

With no STS2 process running:

```bash
npm run doctor
npm run probe:shipped -- --shared-profile
npm run probe:menu-control -- --shared-profile
npm run probe:journey -- --shared-profile
npm run bench:capacity -- --template vanilla-clean --workers 1,2,4,8
npm run probe:recovery -- --template vanilla-clean --experimental-build
```

Each probe creates a timestamped local directory with process logs, identity,
structured events, verdict, and non-claims. Never attach that directory to a
public issue without reviewing it for Steam identifiers and save information.
