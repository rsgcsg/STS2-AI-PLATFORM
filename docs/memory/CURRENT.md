# Current Context

This file is the bounded handoff for active work. It does not replace
[`STATUS.md`](../STATUS.md), the Platform BOM, component manifests, pull
requests, or dated evidence.

## Current phase

`develop` retains the bounded Human-proved schema-2 trace baseline. PR #3
preserves gameplay-safe commit `4384a14...` as a non-release tag and now has a
bounded Human-proved read-only native semantic sequential lane. Exact session
`session-20260830T064823Z-...` records 41/41 successful roots as exact-once in
STS2-owned logical `A_sem(S)` at first native execution; it includes PlayCard,
EndTurn, potion and player-choice pause/resume. The same run proves that
execution-time `A(UI)` is not the semantic action authority. The discriminator
cannot authorize, block or execute UI. Loaded native artifact
`d3b59bed... / 04acd691...`, runtime `f015b026...`, and Modset `968a30c3...`
remain exact; audit-only source `193861a...` repaired cross-stream accounting
without changing that artifact. ADR 0003 remains a historical candidate, not
active gameplay authority.

An independent stacked worktree now carries Native Foundation component source
commit `a3bcd37...`: one bounded combat semantic catalog, one exact lifecycle
adapter, one PlayerChoice lineage adapter, and a non-authorizing
Reward/CardReward/Map owner discriminator. It changes native bytes, so no PR #3
Human evidence transfers. ADR 0004 keeps RitsuLib as an external reference and
adds no runtime dependency. Portable source/tests, clean build, safe install,
cold-load, non-mutating controller/stale/idempotency gates and shipped-headless
H0 pass for `9a89f1fe... / b1c34f90...`. Live/headless canonical parity is
proved only at the main menu. The next gate is one short exact-artifact Human
canary; no predecessor Human evidence transfers.

The independent Windows candidate is now built and loaded as
`a681f8b1... / 7c42c4c3...` against shipped STS2
`v0.111.0 / 41cef1ea / 0861bfa1...`. Safe artifact/native-settings rollback,
sole `STS2_PLATFORM` Modset `e5693d19...`, bounded visible Connector gates, and
root-invoked shipped-headless H0 pass. Visible runtime `7a1942b6...` and
headless runtime `49f34fbf...` have equal main-menu-only canonical digest
`eaf8516d...`. Human gameplay and Recorder owner lifecycle remain unexercised;
the macOS and PR #3 Human evidence do not transfer. See the
[Windows pre-Human gate](../evidence/NATIVE_FOUNDATION_WINDOWS_PRE_HUMAN_GATE_2026-08-31.md).

## Active workstreams

- Full-Run Human Semantic Timeline and evidence representation: PR #3. Keep its
  runtime semantics and exact Human evidence on this feature branch. Existing
  schema-3 proof is trace-level only; canonical eligibility comes from
  `calibrate-semantic-training`.
- Native Foundation refactor: isolated stacked branch
  `refactor/platform/native-foundation`; do not fold it into or rewrite PR #3.
- Repository System v1: the integrated governance baseline for documentation
  routing, bounded context, sparse Skills, deterministic checks, and
  supply-chain configuration. It changes no game behavior or component
  semantic version.

## Current blockers and open questions

- True overlapping accepted roots/execution reorder and native cancel/abort did
  not occur in the bounded discriminator canary and remain targeted Live gates.
- Adjacent first-execution digests are causal handoff candidates, not proof of
  final successor/business outcome semantics; non-combat Full-Run native
  adapters remain to be implemented.
- The S1 checkpoint named by the current Policy Manifest is unavailable on this
  Mac, so real-model Shadow, One-Step, Auto, and Agent-run evidence remain
  unexercised.
- The Native Foundation artifact has automated runtime evidence but no Human
  Combat/PlayerChoice/cross-domain evidence; owner operation is the remaining
  gate and cannot be promoted by portable checks.

## Next meaningful gates

- Run one bounded Windows Human canary on exact artifact
  `a681f8b1... / 7c42c4c3...`:
  Direct Combat/PlayerChoice, then `lethal -> Reward -> CardReward -> Map`.
- After that evidence is audited, extend the execution-bound native semantic
  lane to the next narrow Full-Run mechanisms. Keep unknown UI playable and
  fail evidence closed; do not restore global UI serialization, add natural
  observer polling, or transfer predecessor Human evidence.

Use `npm run project:context` to start a task and
`npm run project:closeout` to surface likely documentation, evidence, contract,
version, and governance impacts before PR closeout.
