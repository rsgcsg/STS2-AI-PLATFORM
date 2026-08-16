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

## Non-Claims

- H2 is a bounded test consumer, not a full-run Agent or gameplay policy.
- No full-run, deterministic replay, save isolation, crash recovery,
  multi-instance, throughput, or broad cross-platform claim is made.
- The game wrote the active Steam profile and cloud store during H2.
- Delivery Receipt does not assert business completion.
- Old Connector, Live-UI, fixture, or external-project evidence is not evidence
  for the current exact Headless tuple.

## Reproduce

With no STS2 process running:

```bash
npm run doctor
npm run probe:shipped -- --shared-profile
npm run probe:menu-control -- --shared-profile
npm run probe:journey -- --shared-profile
```

Each probe creates a timestamped local directory with process logs, identity,
structured events, verdict, and non-claims. Never attach that directory to a
public issue without reviewing it for Steam identifiers and save information.
