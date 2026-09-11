# PR25 native-origin selector and Human canary candidate

G5 change on PR25, input head `186ef791799017e5e1c299344acb3eb1555a4e7f`,
base develop `cd9a0fbb0c85577a13513abe715f91d794ac86eb`. Exact current source,
CI and local runtime receipts override this report. No gameplay or STPD edits.

## First incorrect fact and design correction

Historical Human hand-selector failures following Tools of the Trade carried
an exact GenericHookGameAction but no admitted Human parent. The previous
recorder made existence of a Human parent a prerequisite for every selector
input. That conflated native causal origin with Human decision ancestry.

Exact STS2 v0.111.0 HookPlayerChoiceContext creates a GenericHookGameAction,
sets its choice context, enqueues it and awaits ExecutionStartedTask before
pausing for player choice. CardSelectCmd.FromHand awaits that choice-begun seam
before invoking NPlayerHand.SelectCards. The recorder's existing async factory
scope carries that exact action reference. Native-origin entry is admitted only
for this real already-started hook action, with the actual selector owner and a
subsequent exact native Human input callback. Other missing/unbound owners stay
fail-closed; neither recent EndTurn nor current executor action supplies lineage.

Decision identity v2 adds `native_selector` plus typed native-origin evidence.
CausalRootId names the actual hook GameAction; ActionWitnessId/DecisionId name
the distinct Human input. ParentDecisionId is null because no Human opened it.
Existing Human-parent selectors continue as nested_selector. The native input
witness independently repeats origin and selector-owner IDs; audit checks these,
direct UI mechanism, absent fabricated queue ID and immutable metadata. The
same tracker, state/catalog capture, canonical and append paths remain owners.
Each input has its own outcome; no continuation is appended to an absent parent.
Automatic terminal native completion is not Human input.

New manifests require decision schema 2, while historical schema 1 is still
validated. No raw data migration or backfill. Source revision and schema version
must be pinned by any external consumer. STPD remains unchanged at
`67566db562b95f57468290b97659c9f2e3862fe1`; its future Platform adapter must handle
both true Human-parent lineage and native-origin entries explicitly.

## UI and incomplete pre-frame evidence

UI displays `Selector / native origin`, native action/context, and
`Parent decision: none (native origin)`. Root/entry totals count parentless Human
decisions rather than unique native causal roots. Selectors with Human parents
retain their exact parent label. Canonical, unresolved/cancelled, invalidations
and legacy counts remain distinct.

The two prior PlayCard pre-frame failures cannot justify copying a nearby frame.
Their failure diagnostics now state whether a staged frame was absent/ineligible,
for a different exact card, expired, or unmappable; they also retain current
snapshot status, interaction, catalog completeness and count. Eligibility and
native gameplay are unchanged. This prepares the shortest targeted canary for
the first incorrect capture fact without manufacturing a successful transition.

## Validation and handoff

Positive/negative tests exercise native-origin identity round-trip, absent or
mismatched origin/owner, forged Human parent, schema-1 misuse, immutable lifecycle
metadata, native input witness mismatch, canonical projection and UI display.
Production source assertions require an exact started hook action and prohibit
ambient/current/latest-action lookup. Existing Human-parent cases remain tested.
Full root and exact-game gates, schema-1 historical session audit, exact clean
build/install/load and latest-head CI are separately recorded in the local
qualification receipt and PR25 body. Tests/build/load do not prove real Human
coverage, new origin-path use, non-interference or Full-Run admission.

The intended canary begins at the main menu with an active empty Human Recorder
session. The Human owns new-run selection and every gameplay action. Check:

1. New-run start and natural end; saved resume must have a different marker.
2. Hook-owned hand selector, plus a normal PlayCard selector: each input's state,
   catalog, chosen card, lineage and outcome; no invented Human parent.
3. Event card removal select/cancel/reselect/confirm and boss rewards→next Act.
4. Draw/discard selector with independent catalog and durable pile/card evidence.
5. Bounded turn-boundary card timing and the new pre-frame failure diagnostics.

Success means all genuine Human inputs are accounted for in the appropriate
format and every successful causal transition has a valid canonical record.
Native cancellation, unknown delivery, incomplete state and session-close tails
retain honest dispositions. No unsupported family is silently labeled valid.
This candidate is prepared for Human testing, not a promise of exhaustive game
coverage. Rollback uses the exact backup produced by owning Game Mod deployment;
PR25 remains Draft and is neither merged nor marked ready.


## Runtime UI follow-up

Native cold load reached the main menu, but repeated short K taps did not open
the panel. The old ProcessFrame key-state polling can miss a press and release
between frames. K and Escape now use built-in Godot Shortcut/Button events;
the global K host is transparent, mouse-ignoring and non-focusable, while
Escape belongs to the visible Close button. This preserves the hidden Workspace
and requires no unsupported custom Godot input override or gameplay listener.
Exact final package and runtime receipts supersede the initial canary build.
