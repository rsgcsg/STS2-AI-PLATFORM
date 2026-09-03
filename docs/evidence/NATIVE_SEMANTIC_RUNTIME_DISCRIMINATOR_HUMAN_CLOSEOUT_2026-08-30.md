# Native Semantic Runtime Discriminator Human Closeout - 2026-08-30

## Exact evidence

Owner-operated session
`session-20260830T064823Z-ed1d683fe0b44e1db312c7489cda7fba`, timeline
`timeline-bc4ee13a1bdd400bbec356e5a0abdbdc`, is bound to:

- STS2 `v0.111.0 / 41cef1ea`, assembly
  `9cb4f1ad... / 57785517-0b16-42b9-8b36-bad6fb28384b`;
- native Connector and Annotator source `05d9e8e...`;
- unified artifact `d3b59bed... / 04acd691...`;
- runtime `f015b026...`, environment `190234e4...`;
- sole-Platform Modset `968a30c3...`.

The process was exited after the recording closed. The closed manifest and
Decision V2 payload independently retain the exact identities above; no later
loaded-status claim was substituted for them.

## Results

The runtime discriminator reports:

| Fact | Count |
|---|---:|
| accepted native roots | 41 |
| successful roots | 41 |
| exact-once membership in native `A_sem(S)` | 41 |
| unknown / cancelled / aborted | 0 / 0 / 0 |
| PlayCard / EndTurn / UsePotion | 30 / 10 / 1 |
| player-choice pause / resume | 2 / 2 |
| adjacent execution handoff candidates | 40 |
| overlapping acceptance | 0 |

All 41 first-execution captures were complete. The current UI frame was not an
execution authority for 34 roots; for another seven roots it was interactive
and catalog-complete but did not contain the native action that executed. In
contrast, the action was exact-once in STS2-owned logical `A_sem(S)` for every
root. This is direct runtime counter-evidence to treating the Human UI
affordance catalog as the sequential semantic action set.

Decision V2 audit passes 40 valid and zero invalid records. The native ledger
contains one disposition per accepted root: 40 strict transitions admitted and
the final action explicitly invalidated because Close could not prove a
successor. It is accounting, not silent loss. The additive discriminator
records all 41 successful native roots, including that final root.

The original audit failed with 41
`semantic_trace_missing_accepted_native_action` findings because it required
ordinary native-ledger roots to appear in a schema-2 tracker that owned only
direct-commit/semantic-only roots. Audit-only source `193861a...` now validates
the discriminator envelope and analyzer, joins by exact action witness, rejects
orphan discriminator roots, and preserves the old failure when neither stream
accounts a root. The same closed session then passes. This source change does
not change or inherit the loaded native artifact.

Evidence hashes are fixed in `platform-bom.json`; raw local session data is not
committed.

## Engineering verdict

`FEASIBLE_FULL_RUN_NATIVE_SEMANTIC_RECORDER_EXISTS`

The minimum supported architecture is:

1. Human UI observation `H` and `A(UI)` remain player-presentation evidence;
2. accepted exact native identity records provenance but does not bind `S`;
3. first real execution captures compact STS2 logical `S_sem` and native
   `A_sem(S)`; the root must be exact-once in that set;
4. native lifecycle owns started, pause/resume, cancellation, abort and finish;
5. a next first-execution boundary is a causal handoff candidate for prior
   `S'` and next `S` only when no intervening Human effect or terminal failure
   invalidates it;
6. source-local direct commits and player-choice parent lineage use explicit
   native adapters, while unknown UI remains playable and evidence fails closed.

No global UI serialization, timing heuristic, page allowlist, shadow legality
engine or behavior-changing patch is required by this bounded evidence. Exact
native state/action adapters must still be added for non-combat Full-Run
mechanisms.

## Non-claims

- The canary used rapid consecutive roots but did not produce overlapping
  accepted roots, execution reorder, cancellation or pre-Commit abort.
- Forty matching execution digests are handoff candidates, not proof of final
  business outcomes or all terminal successors.
- The canary does not prove full-run surface completeness, generated skip,
  exhaustive potion/selector behavior, long-soak reliability or training
  admission.
- Owner Human origin is attested, not machine-proven.

