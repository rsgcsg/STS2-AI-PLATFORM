# Native Foundation Source Closeout - 2026-08-30

## Evidence Level

This report covers source, deterministic tests, and exact-game build only until
the final committed candidate is installed and cold-loaded. It does not transfer
the predecessor PR #3 Human evidence to rebuilt bytes.

Exact game baseline:

- STS2 `v0.111.0`, game commit `41cef1ea`;
- `sts2.dll` SHA-256
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`;
- MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

The refactor branch is intentionally stacked from PR #3 head
`72e54f92c8ade16a1fed02f5a0bb9966b49d52e2`; PR #3 remains independent and
unchanged.

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

The architecture evidence set is:

- [Native Seam Matrix](../NATIVE_SEAM_MATRIX.md), including every required
  domain and explicit heuristic/missing-evidence fields;
- [Architecture Example Suite](../NATIVE_FOUNDATION_EXAMPLE_SUITE.md), keeping
  predecessor T3, current T0/T1, and pending T2/T3 distinct;
- [ADR 0004](../adr/0004-native-foundation-and-ritsu-route.md), selecting
  `RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY` without a runtime A/B claim.

## Runtime Gate

The final committed artifact requires install, cold-load, exact identity, and a
short Human canary covering Direct Combat, generated PlayerChoice, and
`lethal -> Reward -> CardReward -> Map`. The owner discriminator must change
domains without authorizing actions, normal gameplay must remain unblocked,
and no predecessor Human claim may be promoted.
