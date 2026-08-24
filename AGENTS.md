# STS2 AI Platform Engineering Guide

## Mission

Maintain one coherent, fair-player STS2 environment platform without merging
the authorities of its components.

## Required Read Order

1. `README.md`
2. `docs/STATUS.md`
3. `docs/ARCHITECTURE.md`
4. `docs/COMPONENTS.md`
5. the relevant component guide and exact code/tests

## Hard Shell

- STS2 owns rules, RNG, effects, native legality and Commit.
- Connector owns fair-player Snapshot, Read, complete finite BoundAction,
  execute-time revalidation, Receipt and successor semantics.
- Host Runtime owns process lifecycle, profile isolation, exact identity,
  recovery and qualification tooling, not gameplay legality.
- Annotator owns native-human witness correlation and immutable recording
  evidence, not action authority or research admission.
- External consumers own strategy, research projection, training and
  evaluation.

Never add hidden-state leakage, coordinate/index mutation, arbitrary reflection,
a second legality engine, consumer-created native operands, silent fallback or
automatic retry after an `unknown` delivery.

## Component Identity

The workspace commit is provenance, not a component semantic identity. Every
component has its own path-scoped source digest, contract digest, version and
artifact identity. An unrelated component edit must not silently change another
component's source identity.

## Change Loop

Identify the owning component, preserve dependency direction, add fail-closed
tests, run the component check and root check, then report evidence at its exact
level. Never commit game files, decompiled source, raw human data, `.local/`,
credentials, model weights or installed artifacts.
