# ADR-0006: Decision occurrences within causal roots

Status: Accepted

Date: 2026-09-10

## Problem and exact grounding

A native PlayCard or reward operation can await several Human selector inputs.
Treating each input as a new external effect cuts off its own parent; retaining
only the final selection loses the intermediate select, deselect and preview
states. CausalRoot and DecisionOccurrence therefore cannot share an identity.
This amends ADR-0005's root accounting for exact nested input ownership.

## Decision

Keep one semantic trace, tracker and canonical transition stream. Attach a
versioned decision identity to each accepted semantic action and its canonical
projection. A root decision names its causal root. A nested decision names the
same causal root, its exact opening parent decision, selector surface/family,
and an opaque native owner witness. Siblings remain siblings; evidence sequence
expresses their order. No synthetic native GameAction or queue ID is created.

At an exact native selector input callback, freeze Connector's complete visible
state and catalog before mutation. Resolve the observed operation against that
same frame's private owner/operand bindings. The exact input-owned boundary can
settle the parent's decision without finishing its enclosing native operation.
Each accepted select, deselect, preview, cancel or confirm has its own decision.
An in-place successor requires a complete frame still owned by that selector;
a terminal input waits for an independently witnessed next decision boundary.
Missing state, catalog, lineage or successor stays explicitly unresolved.

## Owning fact and authority

STS2 owns native input dispatch, choice results and lifecycle. Connector owns
visible state and complete finite BoundActions; its process-local correlation
query grants no delivery authority. Annotator owns Human origin, exact lineage,
causal accounting and immutable persistence. STPD owns downstream projection,
policy, legality filtering for its agent and research admission.

Native automatic deselection or completion is not Human input. Exact automatic
hand mutation scopes suppress Human deselect admission. A terminal selector
result is labeled Human only when enclosed by an exact observed input callback.

## Alternatives considered

Final-selection-only continuation loses real decisions. Synthetic GameActions
invent native roots. Polling, ambient current-root correlation and later-frame
backfill cannot establish exact ownership. A second selector log would duplicate
canonical authority and is rejected.

## Evidence and falsification

Portable tracker/projection tests exercise parent handoff, separate select and
deselect transitions, unresolved boundaries and lineage corruption. Exact-game
compilation and cold load check shipped callback compatibility. Only a bounded
Human canary can qualify actual selector records and their durable pile Reads;
source tests and predecessor sessions do not qualify the new semantics.

## Consequences and tradeoffs

Recording performs synchronous captures at real inputs. Completeness can fail
closed and terminal decisions can remain unresolved. This preserves evidence
truth without blocking gameplay or reconstructing legality.

## Compatibility and migration

The current semantic wire format retains its existing outer schema. The optional
`decision` extension was introduced at schema version 1. The native-origin entry
extension below advances new manifests to `decision_schema_version=2`; audit
requires the matching extension on every trace event. Historical version-1
manifests and manifests without the marker remain readable with their original
semantics. Their continuation-only rows cannot be promoted into new
independent decisions. Root native owner is nullable when unavailable; a subject
or recorder-generated ID must never impersonate a native owner.

## Rollback

Restore the prior exact production package with the Game Mod rollback tooling.
Do not rewrite existing sessions or claim evidence transfers across artifacts.

## Non-goals

No STPD changes, consumer-created native operands, forward-only policy, automatic
retry, hidden information, Human gameplay or Full-Run qualification claim.

## Native-origin selector entry extension (2026-09-10)

Decision identity version 2 adds `native_selector` for real Human selector
input whose exact native causal owner is an already-started GenericHookGameAction
and has no admitted Human parent. STS2's HookPlayerChoiceContext creates this
action and awaits ExecutionStartedTask before the CardSelectCmd selector factory.
The factory carries the exact action reference through its logical async scope;
no current-action, most-recent EndTurn or timestamp association is permitted.

The decision has no ParentDecisionId. Its CausalRootId names the actual native
hook action, separately from its own Human input witness. Typed `native_origin`
retains native action witness/type, choice context type and factory mechanism.
Trace audit cross-checks the input witness's native-origin and selector-owner
IDs and requires direct UI commit with no invented native queue ID. A bound
Human parent continues to use `nested_selector`; an unowned ordinary GameAction
or a factory with no exact owner remains fail-closed. The synthetic Human parent
alternative would fabricate a Human decision and is rejected.

All inputs of the same native-owned selector remain independent sibling entries
sharing their actual native cause; evidence sequence retains order. Each has its
own complete visible state/catalog, chosen action and proved successor or
explicit unresolved disposition. No Human continuation is appended to an absent
Human parent. Terminal native completion without a Human callback is not an
input. Existing current tracker/canonical/persistence boundaries remain owners.

New manifests require decision schema 2 on every trace event; historical schema
1 remains validated with its original semantics. Consumers must explicitly
support the new version. UI labels native-origin selectors and counts parentless
entries under roots/entries; this counter is not a count of unique native causes.
No historical input is backfilled. A fresh hook-selector Human canary is required.
