---
name: platform-runtime-qualification
description: Qualify an exact Platform source/package/install/load/probe/runtime candidate while preserving evidence levels; do not use for ordinary source fixes or portable-only checks.
---

# Platform Runtime Qualification

Use this Skill for an explicit build/package/install/load/probe/runtime
qualification request involving an exact Platform candidate. Ordinary coding,
compilation fixes, and portable tests do not need it.

## Inputs

Identify the owning component, source SHA/path-scoped identity, contract and BOM
pins, target game/runtime identity, requested evidence level, allowed runtime
actions, rollback, and required Human gates. Missing exact identity or authority
fails closed.

## Workflow

1. Read root/local AGENTS, `docs/TESTING.md`, `docs/VERSIONING.md`, Status, and
   only the exact component/runbook/evidence relevant to the requested gate.
2. Verify the source and portable tests before packaging. Do not infer a clean
   source from an installed artifact.
3. Produce and hash the exact package/artifact; keep component identity,
   contract identity, workspace provenance, artifact SHA/MVID, and runtime
   identity distinct.
4. Use only the owning Host/Game Mod lifecycle for install, cold load, probes,
   and rollback. Preserve profile isolation and exact Modset admission.
5. Record each reached level independently: source, test, build, package,
   installed, loaded, live exercise, journey, Human, qualification.
6. On uncertain delivery or post-submission transport failure, classify
   `unknown`, taint as required, and never retry automatically.
7. Run independent verification appropriate to the gate and report non-claims.

Do not promote predecessor evidence, treat compilation as load, invent gameplay
legality, or absorb external policy/research semantics.

## Output and stop

Return exact identities, commands, results by evidence level, rollback, blockers,
and non-claims. Stop for a required Human/runtime action, unavailable exact game
or credential, authority ambiguity, unsafe install target, or unauthorized
irreversible action. A failed level leaves later levels unclaimed.

## Trigger evals

- Positive: “Build, install, cold-load, probe, and qualify this exact Game Mod
  candidate with rollback evidence.”
- Negative: “Fix a C# compile error and run portable tests.”
- Overlap: A native-human capture/audit request routes to
  `platform-human-evidence`; this Skill applies only to its distinct exact
  runtime/package gates.
