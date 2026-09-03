# Native Foundation Source Closeout - 2026-08-30

## Evidence Level

This report covers committed source, deterministic tests, exact build,
install, cold-load, bounded non-Human live checks, shipped-headless H0, and a
main-menu-only live/headless canonical comparison. It does not transfer
predecessor PR #3 Human evidence to rebuilt bytes; Human gameplay remains
pending.

Exact game baseline:

- STS2 `v0.111.0`, game commit `41cef1ea`;
- `sts2.dll` SHA-256
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`;
- MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

The refactor branch is intentionally stacked from PR #3 head
`72e54f92c8ade16a1fed02f5a0bb9966b49d52e2`; PR #3 remains independent and
unchanged.

Native Foundation, Connector, Annotator and unified-Mod component source is
anchored by implementation commit
`a3bcd373e156fb354a6b4947b72c15236457c4b0`. Their component tree and digest
identities are recorded in `platform-bom.json`; the later BOM/docs closeout
commit does not change those component sources.

## Vertical Slices

- Direct Combat: one logical/native card, potion, target, and End Turn catalog
  consumed by Connector and Annotator.
- PlayerChoice: one exact parent-action lineage and one read-only lifecycle
  observer shared with Annotator.
- Reward/CardReward/Map: semantic owner and input owner are separately reported
  without creating actions or completion claims.
- Receipt: wire behavior is unchanged; successor is explicitly an immediate
  post-delivery observation.

## Direct Versus Ritsu Comparison

| Required seam | Direct exact STS2 | Ritsu v0.5.18/dev | Selected route |
|---|---|---|---|
| logical combat state and native validators | public exact types/methods | no higher-level canonical decision API | direct |
| exact execution-order start | `GameAction.BeforeExecuted` / `ActionExecutor` | no generic gameplay timeline API | direct |
| pause/resume/cancel/finish | typed `GameAction` events | no unified replacement contract | direct |
| PlayerChoice parent lineage | `CurrentlyRunningAction` | no parent/continuation API | direct |
| causal next decision | must be domain/lifecycle proved | not supplied | no universal claim |
| patch diagnostics and Mod utilities | local bounded code | broad reusable framework | reference only for this scope |

Ritsu source/API absence closes the dependency choice for this slice. No Ritsu
runtime A/B was performed, and no claim is made about unrelated Ritsu features.

## Automated Evidence

The owning checks must pass on the final commit:

```text
npm --prefix components/connector run test
npm --prefix components/annotator run test
npm --prefix apps/game-mod run check
npm --prefix apps/game-mod run build
npm run check
npm run project:closeout
git diff --check
```

Boundary tests require one combat semantic provider, one lifecycle adapter,
semantic-only Native Foundation source, no Ritsu dependency, and non-causal
receipt wording.

## Exact Artifact And Runtime

- implementation source: `a3bcd373e156fb354a6b4947b72c15236457c4b0`;
- clean build workspace: `da9f60535ade0fb9bc792c18be1b8976b3bedcd4`;
- artifact SHA-256:
  `9a89f1fe728bdce442c70de0daaec0299230e80c6442c97f4bd0752620ce959b`;
- artifact MVID: `b1c34f90-f143-4f7f-97da-eea90c23dbde`;
- visible runtime: `b57a37b4767a42aab5cffa4bba8870f4`;
- environment: `f0cbd53a1be10fad5630252aa4f4ee484b426d733ac3f4d52a04d069584b1c37`;
- exact Modset: only `STS2_PLATFORM`, fingerprint
  `d5054e7bbfc30d8787c3573f57ada09bb808874c0c4f82485433c0e725a96e8d`;
- rollback archive:
  `apps/game-mod/.local/deployments/2026-08-30T13-23-42.253Z`.

The visible runtime passed loaded identity and a complete interactive main-menu
Snapshot. A second controller was rejected with HTTP 409. A deliberately stale
request returned `not_delivered`; resubmitting the same request ID returned the
same receipt and executed no action.

The same artifact passed shipped Godot headless H0 in runtime
`efd022e91c7e4f0287494707b423d700`. Its final main-menu Snapshot and a fresh
visible runtime Snapshot canonicalize to the same digest
`71e246abad05c8a9a805bb6e041cef526ea54645ebc71bb68d103a4c490ab3d7`.
This proves only main-menu semantic invariance. No combat, non-menu mutation,
performance, or full cross-Host parity claim follows.

The architecture evidence set is:

- [Native Seam Matrix](../NATIVE_SEAM_MATRIX.md), including every required
  domain and explicit heuristic/missing-evidence fields;
- [Architecture Example Suite](../NATIVE_FOUNDATION_EXAMPLE_SUITE.md), keeping
  predecessor T3, current T0/T1, and pending T2/T3 distinct;
- [ADR 0004](../adr/0004-native-foundation-and-ritsu-route.md), selecting
  `RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY` without a runtime A/B claim.

## Runtime Gate

Build/install/load and bounded automated gates are complete. The artifact now
requires a short Human canary covering Direct Combat, generated PlayerChoice, and
`lethal -> Reward -> CardReward -> Map`. The owner discriminator must change
domains without authorizing actions, normal gameplay must remain unblocked,
and no predecessor Human claim may be promoted.
