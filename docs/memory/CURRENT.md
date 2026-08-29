# Current Context

This file is the bounded handoff for active work. It does not replace
[`STATUS.md`](../STATUS.md), the Platform BOM, component manifests, pull
requests, or dated evidence.

## Current phase

`develop` retains the bounded Human-proved schema-2 trace baseline. PR #3 now
contains schema-3 content-addressed evidence, exact after-repair Human
performance evidence, and the canonical training calibration. Latest session
`session-20260829T084437Z-...` accounts for 933/933 trace dispositions but
performs 31,613 full captures (628.720 seconds cumulative) and yields zero
canonical `S + A(S) -> A -> S'` rows. ADR 0003 selects serialized Human input.
The candidate is implemented and cold-loaded as `b805474d... / 3ab1e10e...`;
only its owner canary remains.

## Active workstreams

- Full-Run Human Semantic Timeline and evidence representation: PR #3. Keep its
  runtime semantics and exact Human evidence on this feature branch. Existing
  schema-3 proof is trace-level only; canonical eligibility comes from
  `calibrate-semantic-training`.
- Repository System v1: the integrated governance baseline for documentation
  routing, bounded context, sparse Skills, deterministic checks, and
  supply-chain configuration. It changes no game behavior or component
  semantic version.

## Current blockers and open questions

- Input serialization is implemented for the migrated canonical families and
  has source/test/exact-build/install/load evidence. After-latency, blocked
  rapid-input behavior and canonical rows require a new exact-artifact owner
  canary.
- The S1 checkpoint named by the current Policy Manifest is unavailable on this
  Mac, so real-model Shadow, One-Step, Auto, and Agent-run evidence remain
  unexercised.
- Human/runtime gates remain owner-operated and cannot be promoted by portable
  repository checks.

## Next meaningful gates

- Keep the required `portable` source/test gate green on latest PR heads.
- Run the bounded owner canary documented in the serialized source/runtime
  closeouts. Do not add natural-observer polling or transfer predecessor Human
  evidence.

Use `npm run project:context` to start a task and
`npm run project:closeout` to surface likely documentation, evidence, contract,
version, and governance impacts before PR closeout.
