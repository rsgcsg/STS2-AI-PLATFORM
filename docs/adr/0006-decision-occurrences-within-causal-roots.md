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
`decision` extension has schema version 1; new runtime manifests declare
`decision_schema_version=1` and audit requires the extension on every trace
event. Historical manifests without this marker remain readable with their
original semantics. Their continuation-only rows cannot be promoted into new
independent decisions. Root native owner is nullable when unavailable; a subject
or recorder-generated ID must never impersonate a native owner.

## Rollback

Restore the prior exact production package with the Game Mod rollback tooling.
Do not rewrite existing sessions or claim evidence transfers across artifacts.

## Non-goals

No STPD changes, consumer-created native operands, forward-only policy, automatic
retry, hidden information, Human gameplay or Full-Run qualification claim.
