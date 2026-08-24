# Managed Exact First Cross-Host Match

Date: 2026-08-22

Verdict: one fixed same-seed 12-action Player Environment prefix matches the
shipped Reference Host. This closes the known first-divergence defects; it is
not broad semantic qualification or H1.0 admission.

## Exact identities

- game: macOS arm64 STS2 `v0.111.0` / `41cef1ea`, main assembly hash
  `1010476334`, `sts2.dll` SHA `9cb4f1a...`, MVID `57785517...`;
- Reference: Connector source `99f09a7...`, artifact `99d5df9...`, MVID
  `e7b2be84...`, protocol `1.0.0`;
- Candidate: upstream `d11aa883...`, patch `5d1cfab...`, artifact
  `37fe6b9...`, MVID `e7cd6aa9...`;
- semantic target: `sts2-v0.111.0-player-visible-zhs-v1`;
- scenario: seed `H1CR0SSCURR01`, deterministic policy, start
  `map_navigation`, 12 actions.

## Closed defects

1. Managed playable-card referents leaked Host-local `hand_index` and repeated
   complete-card fields already available in the visible hand.
2. Managed localization replaced native lookup with localization keys and did
   not load the requested exact language tables or SmartFormat pipeline.
3. Visible but currently unplayable hand cards retained the actionable
   `playable_card` role after their action bindings disappeared.
4. Visible Powers omitted native definition/type and used a static description
   instead of the amount-aware native hover description.
5. CrossHost Reference launch accidentally depended on an ambient source
   canary. The launcher now removes ambient canaries and admits only an
   explicitly requested revision read from the verified installed sidecar.

The final comparator reported `cross_host_semantic_match`, 12 Reference and 12
Candidate semantic events, no first divergence, and integrity pass on both
Hosts. The same Candidate also passed all 26 targeted native binding gates.
Repository checks passed 137 tests with one proprietary integration test
skipped by default; docs and repository boundaries passed.

## Evidence boundary

Local reports:

- `.local/evidence/managed-cross-host-2026-08-21T17-02-47-934Z/report.json`;
- `.local/evidence/managed-native-binding-gates-2026-08-21T17-03-15-635Z/report.json`.

The reports prove only the named exact artifacts, fixed prefix and targeted
native gates. Randomized/high-risk interactions, complete runs, long soak,
1M+ reliability, external Python consumption, learner contention, learning,
and Candidate-to-Reference policy transfer remain open.
