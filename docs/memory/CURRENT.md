# Current Context

This file is the bounded handoff for active work. It does not replace
[`STATUS.md`](../STATUS.md), the Platform BOM, component manifests, pull
requests, or dated evidence.

## Current phase

PR #11 has merged into `develop` as
`a898b38e1cd116bc7bda4642e5f2ec529faf1e85`, closing the explicitly bounded
source/test/build/install/load/Human causal-foundation qualification. PR #12 has
also merged, advancing `develop` to
`59aefecb1ae047d120966a02c5372adff6a0ba67` and reconciling the BOM current
component commit provenance that changed when PR #11 was squash-integrated.
The post-merge CI for `59aefecb...` passed.

The active repository work is CI/provenance hardening from that exact
`develop` base. It does not change runtime bytes or transfer Human evidence.
The objectives are:

- make Linux and Windows run the same complete portable root gate;
- keep the ruleset-required `portable` status as an aggregate that requires
  both OS lanes;
- stop duplicate topic push + pull-request runs and cancel stale CI;
- codify the hosted-CI versus exact-game/runtime qualification boundary;
- protect GitHub Action pinning and read-only/full-history checkout;
- make the merge-method consequence of commit-based component
  `source_revision` explicit and regression-tested.

The current provenance contract means component-changing PRs must use a normal
merge commit. Squash/rebase rewrites path-scoped commit provenance after final
PR-head CI even when component tree/digest content is unchanged. Docs/governance
changes that touch no component source may still be squashed. Repository
rules/settings are a separate enforcement plane from the committed workflow.

## Qualified PR #11 causal foundation

The active recorder has one current causal/evidence path. `SemanticBoundaryTracker`
is the sole current Human causal/successor authority; canonical transition is
the sole durable training-transition truth. Historical V1/V2/native-ledger
readers remain archival only. NativeSemanticDiscriminator remains diagnostic:
coverage/membership unknown cannot veto healthy authoritative evidence, while
structural/identity/cross-stream corruption remains fatal.

Exact bounded Human candidate retained by PR #11:

- Human session: `session-20260902T120248Z-71a3cf217c3b4fbea8d8c81053e95cb7`
- unified Game Mod artifact: `085a70f3cbf436bbe20784f8519494b2bfd8e26371977c2e6bc3e270e426e647`
- artifact MVID: `d08ee098-e9f1-417e-a03f-d9986ef61cc4`
- loaded runtime: `b24fc95928dd4f97aeea8f1071b6ecaa`
- environment: `e34d7ed2777ea169195d060d79f44c18b0d2c1d9d81852ba8b31beec42da16a5`
- exact Modset: `exact_platform_modset`
- Modset fingerprint: `f80770c0eb87c49b54bb3871976610bf9cbf8d0b63258e989e9049393007bdc1`
- STS2: `v0.111.0 / 41cef1ea`
- `sts2.dll` SHA-256: `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`
- `sts2.dll` MVID: `57785517-0b16-42b9-8b36-bad6fb28384b`

The final Human audit reported:

- current recorder audit: `185` admitted, `0` invalid records, `35` explicit invalidations;
- semantic trace: `705` accepted, `678` proved, `26` unknown, `1` cancelled;
- durable canonical: `627`, all transition IDs unique;
- `state_action_space_unresolved = 0`;
- no rapid-admission `semantic_causal_overlap`;
- PlayerChoice pause/resume `11/11`, no same-parent self-settle;
- no duplicate canonical, cross-session identity contamination, false success,
  fabricated successor, or later-frame backfill;
- clean `session_closed`, no `recording_close_drain_timeout`.

The 26 unknowns are honest dispositions: 10 refuse to cross a later Human
effect and 16 terminate at Recorder Close before a successor boundary
completes. The exact closeout is
[`PR11_HUMAN_QUALIFICATION_CLOSEOUT_2026-09-02.md`](../evidence/PR11_HUMAN_QUALIFICATION_CLOSEOUT_2026-09-02.md).

## Remaining non-claims / next Platform work

PR #11 is **not** exhaustive Full-Run qualification. The next semantic coverage
work remains domain expansion and Live exercise, not another causal-foundation
rewrite. In particular:

- room-internal Shop, Event and Rest Human decisions are not yet implemented as
  shared native decisions/Human witnesses;
- generated skip, hand-selector select/replace/deselect, potion target-picker
  cancel, run entry and exhaustive terminal paths still need their own Live
  evidence where applicable;
- business outcome correctness, STPD model/training quality and controlled
  Recorder OFF/ON performance improvement are not claimed by PR #11.

The observed Human-session Close flush of about `13.96 ms` is recorded only as
a fact, not as performance qualification.

Use `npm run project:context` to start a task and `npm run project:closeout`
before PR closeout. For test/evidence boundaries and the current merge method
rule, use [`TESTING.md`](../TESTING.md) and
[`DEVELOPMENT_WORKFLOW.md`](../DEVELOPMENT_WORKFLOW.md).
