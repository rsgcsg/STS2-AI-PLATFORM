# Current Context

This file is the bounded handoff for active work. It does not replace
[`STATUS.md`](../STATUS.md), the Platform BOM, component manifests, pull
requests, or dated evidence.

## Current phase

`develop` retains the bounded Human-proved schema-2 baseline. Full-Run work
continues in PR #3, where schema-3 content-addressed frames, role-reference
events, Snapshot-gated Read capture, batched Read persistence and successor
reuse are now bounded Human-proved on artifact `4fa67570... / 51c7c37b...`,
runtime `7bcc19e7...`. Current unbuilt source adds operational stage timing;
those profiler results remain pending a new exact artifact.

## Active workstreams

- Full-Run Human Semantic Timeline and evidence representation: PR #3. Keep its
  runtime semantics and exact Human evidence on this feature branch. Schema 3
  is bounded Human-proved by session `session-20260829T052157Z-...` with
  333/333 proved actions and zero unknown.
- Repository System v1: the integrated governance baseline for documentation
  routing, bounded context, sparse Skills, deterministic checks, and
  supply-chain configuration. It changes no game behavior or component
  semantic version.

## Current blockers and open questions

- Latest owner session `session-20260829T052157Z-...` is audited immutable
  schema-3 Human evidence: 333 accepted/333 proved roots, 947 exact frames and
  5.354 persisted Reads/action. It does not contain the new stage profiler.
- The S1 checkpoint named by the current Policy Manifest is unavailable on this
  Mac, so real-model Shadow, One-Step, Auto, and Agent-run evidence remain
  unexercised.
- Human/runtime gates remain owner-operated and cannot be promoted by portable
  repository checks.

## Next meaningful gates

- Keep the required `portable` source/test gate green on latest PR heads.
- Build and cold-load the stage-profiler candidate, then run one short exact
  Human canary to attribute snapshot/Read/serialization/persistence latency.
  Tamper rejection remains an automated audit gate rather than a Human action.

Use `npm run project:context` to start a task and
`npm run project:closeout` to surface likely documentation, evidence, contract,
version, and governance impacts before PR closeout.
