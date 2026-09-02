# Current Context

This is the bounded active-work handoff. It does not replace
[`STATUS.md`](../STATUS.md), `platform-bom.json`, component manifests, PRs, or
dated evidence.

## Integration truth

- `develop@59aefecb1ae047d120966a02c5372adff6a0ba67`
- PR #11 merged as `a898b38e1cd116bc7bda4642e5f2ec529faf1e85` and closed its bounded causal-foundation Human qualification.
- PR #12 merged as `59aefecb...` and repaired current BOM commit provenance after the PR #11 squash; its post-merge CI passed.
- PR #11 exact Human/runtime proof remains in
  [`PR11_HUMAN_QUALIFICATION_CLOSEOUT_2026-09-02.md`](../evidence/PR11_HUMAN_QUALIFICATION_CLOSEOUT_2026-09-02.md). Do not copy that evidence to different bytes/runtime.

The active recorder still has one current causal path: `SemanticBoundaryTracker`
is the sole Human causal/successor authority and canonical transition is the
sole durable training-transition truth. NativeSemanticDiscriminator membership
coverage is diagnostic-only; structural/identity/cross-stream corruption is
fatal.

## Active work: CI / provenance hardening

Current topic branch starts from exact `develop@59aefecb...` and changes no
component runtime/product source, artifact, or Human evidence.

Goals:

- run the same complete portable root gate on Linux and Windows;
- keep ruleset-required `portable` as an aggregate that requires both OS lanes;
- stop redundant topic push + PR runs and cancel stale CI;
- codify hosted source/test CI versus local exact-game/runtime/Human gates;
- guard Action SHA pinning and read-only full-history checkout;
- regression-test component provenance under normal merge versus squash.

Current component `source_revision` is path-scoped commit provenance. Normal
merge preserves the topic component revision; squash/rebase rewrites it even
when component tree/digests are unchanged. Until that schema changes,
component-source PRs use a normal merge commit. Docs/governance-only PRs may be
squashed. Repository settings/rulesets are a separate enforcement plane.

## Remaining Platform non-claims

PR #11 was not exhaustive Full-Run qualification. Shop/Event/Rest internal
Human decisions, generated skip, hand-selector variants, potion target-picker
cancel, run entry, and exhaustive terminal paths still need their own Live
coverage where applicable. Business outcome correctness, STPD model/training
quality, and controlled Recorder OFF/ON performance improvement also remain
unclaimed.

Use `npm run project:context` to start work and `npm run project:closeout`
before PR closeout. See [`TESTING.md`](../TESTING.md) and
[`DEVELOPMENT_WORKFLOW.md`](../DEVELOPMENT_WORKFLOW.md) for current gates.
