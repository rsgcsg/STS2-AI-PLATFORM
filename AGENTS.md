# STS2 AI Platform Engineering Guide

## Mission

Maintain one coherent, fair-player STS2 environment platform without merging
the authorities of its components.

## Required Read Order

1. `README.md`
2. `docs/memory/CURRENT.md`
3. `docs/ARCHITECTURE.md` and `docs/COMPONENTS.md`
4. the relevant component `AGENTS.md` or guide and exact code/tests

Before ordinary development, also read `docs/DEVELOPMENT_WORKFLOW.md` and
`docs/TESTING.md`. Normal work starts from current `origin/develop`, uses one
short-lived topic branch and targets `develop` by pull request. Do not
direct-push `main` or `develop`, share a writable branch between agents, or
create permanent component develop lines. Use
`npm run project:context -- --component <name>` for a bounded routing map; load
`docs/STATUS.md` and dated evidence only when current claims matter.

## Hard Shell

- STS2 owns rules, RNG, effects, native legality and Commit.
- Connector owns fair-player Snapshot, Read, complete finite BoundAction,
  execute-time revalidation, Receipt and successor semantics.
- Host Runtime owns process lifecycle, profile isolation, exact identity,
  recovery and qualification tooling, not gameplay legality.
- Annotator owns native-human witness correlation and immutable recording
  evidence, not action authority or research admission.
- Evidence owns typed verification and immutable transfer, not Human origin or
  research admission.
- Policy Runtime consumes Connector-owned finite actions and stays
  model-neutral; it owns no inference, legality, or native operands.
- Game Mod owns one production package, exact install/load identity, and
  rollback; packaging never merges component authorities.
- External consumers own strategy, research projection, training and
  evaluation.

Platform is the upper-level, model-neutral foundation. STPD is an independent
research project that consumes versioned Platform contracts; repository
independence does not make it a peer platform. Platform must not import STPD
model, reward, training or research semantics.

Never add hidden-state leakage, coordinate/index mutation, arbitrary reflection,
a second legality engine, consumer-created native operands, silent fallback or
automatic retry after an `unknown` delivery.

## Component Identity

The workspace commit is provenance, not a component semantic identity. Every
component has its own path-scoped source revision, source digest, contract
digest, version and artifact identity. An unrelated component edit must not
silently change another component's source identity.

The current path-scoped `source_revision` is commit provenance. A normal merge
commit preserves the topic component revision; squash/rebase integration does
not. Until that provenance contract is deliberately changed, any PR that
changes component source uses a normal merge commit. Docs/governance-only PRs
that do not change component source may be squashed. Never weaken BOM/identity
checks merely to accommodate a provenance-rewriting merge method.

## Change Loop

Identify the owning component, preserve dependency direction, add fail-closed
tests, run the component check and root check, then report evidence at its exact
level. Hosted CI must remain source/test-only; game-bound changes additionally
use the local exact-game/build/runtime gates defined in `docs/TESTING.md`.
Never commit game files, decompiled source, raw human data, `.local/`,
credentials, model weights or installed artifacts.

Record the base branch/SHA, workstream, cross-repository pin, evidence level,
rollback and non-claims in every PR. A merge never promotes source/test evidence
to build, loaded, runtime, Human or qualification evidence.
Run `npm run project:closeout` before the PR so documentation, evidence,
contract, version, and governance impacts stay visible.
