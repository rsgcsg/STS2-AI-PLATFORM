# Current Context

This is a bounded handoff, not repository authority. Resolve live GitHub refs,
open PRs, rulesets, code, tests, `platform-bom.json`, and exact runtime/evidence
before making a current claim. Those sources override this file.

## PR25 Windows Full-Run source continuation (2026-09-09)

The docs-only reconciliation is based on Windows source
`agent/fullrun-docs@7513f705cc045e4c676617e21a976b7ea60b4234`. That continuation
has source support for accepted `GameAction.OnEnqueued` ingress, exact direct UI
seams in `NativeUiPatches.cs`, durable append checkpoint/rollback and
post-commit completion accounting, room/boss/act owners, and potion discard.
The exact native identity is STS2 v0.111.0 source `41cef1ea`, SHA-256
`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`, MVID
`73b63ee0-6c0a-47bb-b0d1-b21f6d94222e`. The prior macOS identity is historical
only; it is not current Windows evidence.

The nested-selector chain `dc919e9` -> `474b471` -> `de8de0e` ->
`1a15d4482b4523a769280c2891c670030ecb3c4f` is pending integration and tested
qualification. Its census covers explicit `PlayerChoiceContext` resolution,
generic simple/deck/combat-pile/bundle factories, `CardRemovalReward` and
merchant card-removal parents, and `CardReward` alternatives `Skip`, `REROLL`,
and `SACRIFICE`. Exact-parent absence must fail closed; the old `GameAction`
ambient fallback must not return. Rejection alone does not make the generic
selector families Full-Run covered.

Two P1 integration repairs remain recorded as pending, not fixed: terminal
reservation must not treat `NDeckTransformSelectScreen.ConfirmSelection` as the
terminal callback (the `Claws` path completes at preview
`CompleteSelection`), and a valid min-0 `SeaGlass` simple-grid completion must
not be rejected merely because the selection is empty. The compound
`reward_potion_belt.discard_replace` label does not mean atomic potion
replacement; the source has discard accounting only. No new exact build,
install/load, runtime, or Human PASS exists for the nested bytes.

## Last recorded integration boundary

The last verified integration boundary before this governance synthesis was the
post-CI-hardening `develop` state in which:

- the recorder had one active Human causal/successor authority and one canonical
  durable transition path;
- predecessor recording formats and ledgers were archival readers only;
- Linux and Windows both ran the complete portable root gate behind the required
  `portable` aggregate;
- current path-scoped component revision remained Git commit provenance, so
  component-source PRs required normal merge.

Use GitHub to resolve the exact current `develop` SHA and active pull request.
Do not copy this handoff's integration boundary to a newer head.

## Active Live UI integration work

The current bounded topic is the selective reconciliation of historical
`ui-testing` presentation work with current `develop`. It changes Live UI and a
narrow Annotator application-event projection. It is classified `G4`: the
game-bound component and runtime lifecycle require exact build/install/load,
while Human origin, causal admission, and durable evidence semantics are
unchanged. Portable, exact-game, install, cold-load, and a bounded
owner-operated Human UI canary are recorded in the dated Live UI integration
closeout. PR #15 now has a follow-up convergence candidate that reduces the
presentation to exactly Agent Run and Human Recorder, removes the old collapse
and dashboard scaffold, and requires a fresh exact build/load plus a new Human
UI canary. Prior sessions do not qualify those bytes. Resolve the exact topic
branch, pull request, and latest head from GitHub rather than this handoff.

## Remaining Platform non-claims

Bounded prior Human qualification was not exhaustive Full-Run qualification.
Shop/Event/Rest internal Human decisions, generated skip, hand-selector
variants, potion target-picker cancel, run entry, and exhaustive terminal paths
still require their own coverage where applicable. Business outcome correctness,
STPD model/training quality, and controlled Recorder OFF/ON performance
improvement remain unclaimed unless newer exact evidence says otherwise. Nested
generic selectors, `CardRemovalReward` removal, event/rest nested choices,
reward replacement alternatives, atomic potion replacement, and any new
runtime/Human qualification remain unclaimed.

Use `npm run project:context` to start work and `npm run project:closeout` before
PR closeout. See [Engineering Governance](../ENGINEERING_GOVERNANCE.md),
[Testing and Evidence](../TESTING.md), and
[Development Workflow](../DEVELOPMENT_WORKFLOW.md).
