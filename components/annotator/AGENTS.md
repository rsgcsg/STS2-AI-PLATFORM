# Agent Guide

This file adds Human Annotator-specific rules to the Platform root `AGENTS.md`.

## Mission

Record exact human decisions made through the shipped STS2 UI without changing
gameplay or creating a second action authority.

## Required Read Order

1. `README.md`
2. `docs/DOCUMENT_MAP.md`
3. `docs/STATUS.md`
4. `docs/ARCHITECTURE.md`
5. `docs/DATA_CONTRACT.md`
6. the exact source and tests being changed

## Authority Boundaries

- STS2 owns rules, RNG, native legality, effects, and accepted game actions.
- The Platform Connector component owns stable Player Environment truth and
  finite BoundActions.
- This repository owns native-human witness correlation and recording evidence.
- STPD owns research projection, datasets, eligibility, and training.
- Native references are process-local and never serialized or executable.

Never add coordinate action identity, synthetic input, arbitrary reflection
mutation, a second legality engine, source-specific business authority, or an
automatic retry after uncertain mutation.

## Evidence Discipline

Keep source, test, build, install, loaded, native-human mutation, journey, and
qualification evidence distinct. Fixtures do not prove native-human origin or
non-interference. A new SHA/MVID/runtime receives no authority from an older one.

Do not commit game files, decompiled source, raw recordings, saves, local status,
provider output, secrets, build outputs, or deployment snapshots.

## Change Loop

1. identify the exact native and Connector seam;
2. make the smallest owning-layer change;
3. add positive and fail-closed tests;
4. run `npm run check`;
5. update current docs when behavior or evidence changes;
6. build/deploy only from an exact clean commit;
7. state Live non-claims honestly.
