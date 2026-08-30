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
adds no runtime dependency. Portable source/tests pass; the next gate is final
provenance closeout, build/install/load, then one short exact-artifact Human
canary.

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
- Human/runtime gates remain owner-operated and cannot be promoted by portable
  repository checks.

## Next meaningful gates

- Keep the required `portable` source/test gate green on latest PR heads.
- Extend the execution-bound native semantic lane to the next narrow non-combat
  Full-Run mechanisms. Keep unknown UI playable and fail evidence closed; do
  not restore global UI serialization, add natural-observer polling or transfer
  predecessor Human evidence.

Use `npm run project:context` to start a task and
`npm run project:closeout` to surface likely documentation, evidence, contract,
version, and governance impacts before PR closeout.
