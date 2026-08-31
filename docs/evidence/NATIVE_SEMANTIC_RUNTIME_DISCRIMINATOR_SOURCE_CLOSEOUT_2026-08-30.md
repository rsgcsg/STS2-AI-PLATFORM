# Native Semantic Runtime Discriminator Source Closeout - 2026-08-30

## Evidence boundary

This report binds source, exact-source audit, deterministic tests, clean exact
build, install and load. It does not claim Human gameplay, canonical rows or
Full-Run qualification. Gameplay-safe commit `4384a14...` is preserved by the
non-release tag `baseline/pr3-gameplay-safe-4384a14` and remains the rollback
baseline.

## Exact native facts

The installed STS2 identity is `v0.111.0 / 41cef1ea`, assembly
`9cb4f1ad... / 57785517-0b16-42b9-8b36-bad6fb28384b`. Local exact decompilation
confirms:

- `GameAction` is STS2's wrapper for player input; `BeforeExecuted` occurs once
  at first real execution, while player choice pauses and resumes the same
  action;
- card/potion UI acceptance stages native objects but does not itself spend
  resources or apply gameplay effects;
- `PlayCardAction` resolves the logical hand card and calls native
  `CanPlay`/`IsValidTarget` before resource spend and `OnPlayWrapper`;
- `UsePotionAction` resolves the current slot and native target before
  `OnUseWrapper`;
- queued cancellation is explicit, while a card removed from hand can finish
  without Commit and therefore needs the existing narrow read-only abort
  witness.

## Candidate architecture

The new process-local Connector witness has no mutation or transport surface.
At native execution it captures:

1. current public `A(UI)` from the existing Player Environment frame;
2. compact `S_sem` from STS2 logical combat state;
3. `A_sem(S)` from logical hand, current potion slots, combat phase and native
   card/potion validators;
4. exact observed native action membership.

Annotator writes this to an independent append-only stream and tracks accepted,
first execution, pause/resume, cancellation, pre-Commit abort and finish by
exact action identity. Generated/player choice is parent lineage, not an
independent replacement root. Any capture failure stays non-authorizing and
fails audit rather than affecting gameplay.

## Automated evidence

- seven deterministic analyzer/store tests pass, including rapid A1/A2,
  effect-cancelled A2, pre-Commit abort, exact-membership rejection,
  player-choice pause/resume, sequence/duplicate rejection and persistence;
- all Annotator Core tests pass (`98/98`), plus CLI and semantic-analysis tests;
- Connector SDK/package/docs/contract/boundary/compatibility/CLI/release/Python
  checks pass;
- exact Connector and Annotator builds succeed with zero warnings/errors;
- clean workspace `05d9e8e...` builds unified artifact
  `d3b59bed... / 04acd691...`.

Safe install records rollback
`apps/game-mod/.local/deployments/2026-08-30T06-43-57.781Z`. Cold-load verifies
artifact `d3b59bed... / 04acd691...`, Connector runtime `f015b026...`,
environment `190234e4...`, sole-Platform Modset `968a30c3...`, protocol `1.0.0`
and Recorder Ready/no-session. Loaded is not Human action evidence.

## Runtime discriminator gate

One exact-artifact owner canary must exercise rapid card roots, an earlier
effect cancelling a later queued card, potion use, End Turn and a real
player-choice pause/resume. The audit must show each successful root exact-once
in `A_sem(S)`, cancellation/abort not successful, and ordinary next-root
handoff candidates without crossing another Human effect.

The final feasibility verdict is pending this runtime discriminator. No source
or predecessor evidence can decide it.
