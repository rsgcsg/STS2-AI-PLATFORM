# ADR 0004: Native Foundation And Ritsu Route

- Status: accepted and exact Windows Human sealed
- Date: 2026-08-30
- Scope: game-side Platform semantic/lifecycle infrastructure

## Context

Combat semantic enumeration existed independently in Connector publication and
the Annotator discriminator. Lifecycle subscriptions also belonged directly to
Annotator. This duplicated STS2 truth and made presentation staging look like a
possible semantic authority even though exact Human evidence showed otherwise.

RitsuLib was evaluated as a possible shared low-level dependency. The exact
audited refs were upstream main/release `v0.5.18` at
`f224961a9392e010335da092240b90ee8235317f` and development at
`c466809004f8ecd801956fea2bc3fef83a5d7ad5`. It is MIT-licensed and provides a
capable Mod framework, Harmony patch infrastructure, compatibility diagnostics,
content and UI utilities. Its compatibility table includes exact game API
`0.111.0`.

The runtime audit also found a broad registered patch surface and a debug
compatibility mode whose fallbacks are enabled by default. A real Ritsu runtime
A/B would therefore need the exact package, patch diagnostics, and strict
fallback configuration fingerprinted as a distinct Modset. Those controls were
not silently assumed or loaded for this decision.

Neither audited line provides the Platform's required generic exact
`GameAction` execution-order timeline, finished/cancel/abort disposition,
PlayerChoice parent lineage, or causal next-decision contract. STS2 v0.111.0
already exposes the exact lifecycle events and `ActionExecutor` owner used by
this bounded slice.

Subsequent PR #7 retrofit and PR #8 Ritsu-first counterfactual work reached the
same bounded result. Zero whole integration categories and zero sampled
Treasure touchpoints were removed; Combat, PlayerChoice, Treasure, and Shop
still retained Platform-owned semantic facts and exact lifecycle/lineage. The
durable measurements are preserved in the
[final decision packet](../evidence/RITSU_ROUTE_FINAL_DECISION_2026-08-31.md),
without merging the research implementations into production.

## Decision

`RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`

Create a small Native Foundation compiled into the unified Platform Mod. It
adapts STS2-owned semantic state, native validators, exact action lifecycle,
and owner lineage. Connector and Annotator consume it; neither may reconstruct
the same semantic catalog or lifecycle independently.

RitsuLib remains a source/API comparison reference. Platform adds no Ritsu
package, Mod dependency, bootstrap, patch manager, or runtime compatibility
surface. If a future domain needs a seam that Ritsu actually supplies better,
that proposal requires a new bounded ADR and exact build/runtime evidence.

The exact Windows Native Foundation artifact then passed the bounded Human
Combat/PlayerChoice, cross-domain owner-handoff, and Recorder lifecycle gate;
see the [Windows Human closeout](../evidence/NATIVE_FOUNDATION_WINDOWS_HUMAN_CLOSEOUT_2026-08-31.md).

## Consequences

- Connector remains the sole fair-player/public action and delivery authority.
- Annotator remains read-only and owns evidence, not legality.
- Host Runtime remains process infrastructure and does not absorb game truth.
- Presentation readiness may filter a semantic catalog but cannot create it.
- Exact operands remain process-local.
- Public protocol 1.0.0 stays compatible; receipt successor semantics are
  clarified rather than redefined.
- Ritsu runtime A/B was not run because source/API audit found no substitute
  implementation to compare for the required seam. This is an explicit
  non-claim, not evidence of Ritsu runtime incompatibility.
- Reopening the dependency decision requires a stable Ritsu release to replace
  a critical Platform seam across at least two real production domains, delete
  meaningful Direct integration rather than wrap it, pass the same
  Platform-owned conformance/fail-closed contracts, and justify a strict-config
  exact runtime A/B.

## Rejected

- Keep duplicate combat legality in Connector and Annotator.
- Make UI staging the semantic decision authority.
- Add Ritsu only to standardize patch/bootstrap plumbing.
- Move native legality into Host Runtime.
- Create a broad universal selector or transaction framework before native
  mechanisms demonstrate real commonality.
