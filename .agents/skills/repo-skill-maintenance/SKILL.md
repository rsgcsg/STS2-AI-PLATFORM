---
name: repo-skill-maintenance
description: Explicitly govern a proposal to create, split, update, or deprecate a repository Skill; do not use for ordinary code, tests, or documentation.
---

# Repository Skill Maintenance

Use this Skill only when the user explicitly invokes `$repo-skill-maintenance`
for repository Skill governance.

## Admission first

Before authoring, decide whether the lesson belongs in a cheaper durable owner:

- defect or invariant -> code and regression test;
- deterministic drift -> check or CI;
- authority/architecture -> AGENTS, canonical doc, or ADR;
- current state -> CURRENT, PR, or dated evidence;
- component workflow -> component guide;
- repeated non-obvious workflow -> possible Skill.

Require a one-line job, positive and negative triggers, inputs, output, stop
condition, recurrence evidence, and why the alternatives above are insufficient.
Normal admission is about three independent occurrences, two repetitions of a
high-cost failure, or an unusually high-risk workflow that is already stable.
Do not admit a first ordinary occurrence.

## Author or revise

1. Read `docs/PROJECT_SYSTEM.md` and re-check current official Skill behavior
   when discovery or invocation semantics matter.
2. Use bundled `$skill-creator` for the actual authoring or revision.
3. Keep one job, a discriminating description, explicit inputs/outputs,
   non-triggers, and risk-proportionate stop gates.
4. Add positive, negative, and overlap trigger evals.
5. New meta or high-risk Skills start explicit-only when required.
6. Validate the Skill and open a separate governance PR.

Update only when workflow, trigger boundaries, authority/stop gates, or a
required interface changes, or a reproducible failure needs a regression fix.
Do not update for current SHAs, PRs, sessions, blockers, or ordinary code work.

## Output and stop

Return the admission decision, evidence, files, validation, invocation policy,
and rollback/deprecation path. Stop before creation when recurrence or authority
is unresolved; do not manufacture recurrence evidence.

## Trigger evals

- Positive: “Use `$repo-skill-maintenance` to evaluate and create the admitted
  release-audit Skill after three independent occurrences.”
- Negative: “Fix this TypeScript compile error and update its test.”
- Overlap: “Qualify this runtime artifact” routes to
  `platform-runtime-qualification`, not this Skill, unless Skill governance is
  explicitly requested.
