# Current Context

This file is the bounded handoff for active work. It does not replace
[`STATUS.md`](../STATUS.md), the Platform BOM, component manifests, pull
requests, or dated evidence.

## Current phase

`develop@791e271...` contains the completed PR #3, PR #5, PR #6, PR #9 and PR
#10 production lines. PR #6 is merged and Human-qualified for its explicitly
bounded scope; PR #11 is the active authority/evidence convergence topic. The integrated
PR #3 history preserves gameplay-safe commit
`4384a14...` as a non-release tag and contains a bounded Human-proved read-only
native semantic sequential lane. Exact session
`session-20260830T064823Z-...` records 41/41 successful roots as exact-once in
STS2-owned logical `A_sem(S)` at first native execution; it includes PlayCard,
EndTurn, potion and player-choice pause/resume. The same run proves that
execution-time `A(UI)` is not the semantic action authority. The discriminator
cannot authorize, block or execute UI. Loaded native artifact
`d3b59bed... / 04acd691...`, runtime `f015b026...`, and Modset `968a30c3...`
remain exact; audit-only source `193861a...` repaired cross-stream accounting
without changing that artifact. ADR 0003 remains a historical candidate, not
active gameplay authority.

The integrated Native Foundation workstream carries component source commit
`a3bcd37...`: one bounded combat semantic catalog, one exact lifecycle
adapter, one PlayerChoice lineage adapter, and a non-authorizing
Reward/CardReward/Map owner discriminator. It changes native bytes, so no PR #3
Human evidence transfers. ADR 0004 now freezes RitsuLib as an external
reference with no runtime dependency after both retrofit and Ritsu-first
counterfactual evaluation. Portable source/tests, clean build, safe install,
cold-load, non-mutating controller/stale/idempotency gates and shipped-headless
H0 pass for `9a89f1fe... / b1c34f90...`. Live/headless canonical parity is
proved only at the main menu. No predecessor Human evidence transfers.

The independent Windows candidate is built and Human-qualified as
`a681f8b1... / 7c42c4c3...` against shipped STS2
`v0.111.0 / 41cef1ea / 0861bfa1...`. Safe artifact/native-settings rollback,
sole `STS2_PLATFORM` Modset `e5693d19...`, bounded visible Connector gates, and
root-invoked shipped-headless H0 pass. Visible runtime `7a1942b6...` and
headless runtime `49f34fbf...` have equal main-menu-only canonical digest
`eaf8516d...`. Exact runtime `d8a10ba2...`, environment `9e0e0cfe...`, and
sole-Platform Modset `1f1bdecc...` bind closed session
`session-20260831T072650Z-b0608291ae7f416d96b058078f441794`. It passes 35/35
Decision V2 records, 37/37 native-root dispositions, potion, three PlayerChoice
pause/resume pairs, repeated lethal-to-Reward/CardReward/Map handoffs, and
Recorder New/Pause/Resume/Close. The macOS and PR #3 Human evidence do not
transfer. See the
[Windows pre-Human gate](../evidence/NATIVE_FOUNDATION_WINDOWS_PRE_HUMAN_GATE_2026-08-31.md).
The durable result is in the
[Windows Human closeout](../evidence/NATIVE_FOUNDATION_WINDOWS_HUMAN_CLOSEOUT_2026-08-31.md).

## Active workstreams

- PR #11 `cleanup/platform/authority-evidence-single-source` is the active
  Platform topic, based on `develop@791e271...`. Its current recording-format
  hard cut preserves
  the exact Native Foundation execution `A_sem(S)` as typed durable evidence,
  with a schema-2 action-space sidecar carrying the exact Human
  `BoundActionId`, while leaving `SemanticBoundaryTracker` as the sole
  causal/successor authority and Connector as the sole public action/delivery
  authority. The earlier pre-hard-cut source/test/build/install/load evidence
  was sealed for artifact `3d54c8b8... / 601bfbf9...` in runtime `cac58962...`
  under the sole exact Platform Modset; it is historical and does not qualify
  the hard-cut bytes. A fresh bounded owner canary remains required.
- PR #3, PR #5 and PR #6 are merged integration history. Ritsu research PRs #7 and #8
  are closed without their experimental runtime code; durable findings live in
  ADR 0004 and the final route-decision packet.
- Repository System v1 remains the integrated governance baseline. It changes
  no game behavior or component semantic version.

## Current blockers and open questions

- True overlapping accepted roots/execution reorder and native cancel/abort did
  not occur in the bounded discriminator canary and remain targeted Live gates.
- Adjacent first-execution digests are causal handoff candidates, not proof of
  final successor/business outcome semantics; non-combat Full-Run native
  adapters remain to be implemented.
- The S1 checkpoint named by the current Policy Manifest is unavailable on this
  Mac, so real-model Shadow, One-Step, Auto, and Agent-run evidence remain
  unexercised.
- The Native Foundation Human gate is complete for PR #6's bounded scope. It is
  not exhaustive Full-Run qualification. The exact merged candidate and
  owner-attested Human qualification are recorded below; predecessor evidence
  does not transfer to it or from it to new runtime bytes.
- Historical PR #6 predecessor source `f320ef6...` separated Human Root, exact Native
  Commit and later Successor Boundary. Its same-bytes Human sessions prove
  Reward and Map stable but expose CardReward nested-owner and Treasure
  pre-generated/predicted-state seam defects. Current repair source uses exact
  CardReward `ShowScreen` owner creation and Treasure `OnPicked` Commit. Clean
  artifact `641f543d... / 7ac2fa24...` is cold-loaded in runtime `c3e4d2b...`,
  environment `6655e3a...`, sole-Platform Modset `91d2f6e7...`; Human
  qualification was superseded and no predecessor evidence transfers.
- A subsequent historical repaired-bytes Human run proved CardReward/Treasure and all
  but its final Map successor. A focused replay proved the final Map Commit and
  playable Combat before Close, exposing a production observation gap rather
  than early Close: `NativeDecisionOwnerReady` had no publisher. Source
  `c1b3144...` adds one exact typed Combat publisher after native play phase and
  input-owner readiness, plus complete domain-matching Connector capture and
  durable owner/mechanism evidence. Root/exact checks, clean build, safe install
  and cold-load pass as artifact `627b5b69... / f19b8863...`, runtime
  `c296ec99...`, environment `0c47c311...`, sole-Platform Modset
  `c8dd91e3...`; its canary was superseded by the current qualified session.

## Next meaningful gates

- PR #6's merged candidate remains Human-qualified: artifact `2382b3dd... /
  b1a7d1f1...`, runtime `a00b1852...`, environment `c9cc1a5d...`, and Modset
  `79bdf7ca...`. Session `session-20260901T061040Z-561a204be0bc422da5809e1ec5c148aa`
  passes strict V2, modern 25/25, Map 7/7 and owner-ready 5/5 with no
  unresolved transition or close-drain timeout.
- The performance topic passes portable and exact-game checks, clean build, safe
  install and exact cold-load for unified artifact `111bcba1... / 7187768f...`,
  runtime `dd4e2229...`, environment `6b7ccbf2...` and sole-Platform Modset
  `05df53c2...`; Recorder is Ready with no session and rollback is available.
  The exact-candidate Human OFF/ON canary and subphase profile are still
  required; no predecessor Human evidence transfers.
- Shop, Event, Rest, run entry and terminal remain the next Full-Run domain
  expansion after this bounded hardening closeout.

Use `npm run project:context` to start a task and
`npm run project:closeout` to surface likely documentation, evidence, contract,
version, and governance impacts before PR closeout.
