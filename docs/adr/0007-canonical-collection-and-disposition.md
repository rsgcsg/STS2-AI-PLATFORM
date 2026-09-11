# ADR-0007: Canonical collection and explicit recording disposition

Status: Accepted

Date: 2026-09-11

## Problem and exact grounding

Default bundle-2 exported only compatibility decisions, omitting canonical
native-input decisions unsupported by that projection. Diagnostic invalidations
and cancellations could look like recording failure, while persistence failures
could leave an apparent zero. A staged card observation used elapsed time even
though native CardPlay creation/cleanup already provides an exact owner.

## Decision and owning fact

Keep ADR-0005/0006's sole tracker and canonical authority. Default export/bundle-3
uses canonical transitions plus complete raw session and hashed producer audit.
Annotator owns dispositions and close completion; UI consumes them. Evidence
independently validates transport and typed links, but does not authorize Human
origin, legality, Full-Run qualification or research admission.

Current status-4 exposes canonical, cancelled, aborted and exact distinct real
failure counts. Unavailable accounting is explicit. Invalidation metadata marks
capture/persistence failures and their witness IDs; internal diagnostics and
unsupported input remain separate. Current manifests advertise disposition-1
and close-1. A close receipt is written only after all evidence streams flush;
a final journal entry alone cannot certify completion.

Bind staged H to the exact native CardPlay returned by its factory within the
StartCardPlay invocation, and forget it at native Cleanup. A session/environment
generation invalidates stale owners. No wall-clock expiry or global latest-card
slot determines ownership. H remains distinct from execution S + A(S); native
Commit remains distinct from successor, and proof never crosses Human effect.

## Alternatives considered

Deleting record-2/bundle-2 now would break concrete STPD consumers. Continuing
to use it as the default would silently truncate current evidence. Retaining it
as explicit compatibility is bounded and honest. A UI reason-code classifier
or new recording ledger would create another authority; rejected. Increasing
the staged timeout would preserve an arbitrary correctness boundary; rejected.

## Evidence and falsification

Regressions cover canonical-only/failure-only bundles, exact frame/catalog/Read
links, malformed lineage, checksum rewrites, cancellation/diagnostic separation,
I/O failure and close receipts, and exact native-owner lifetime. Actual immutable
predecessor bytes are packed in C# and verified in Python. This verifies tooling,
not the new runtime. Exact-game/build/load and final Human gate remain distinct.

## Consequences, compatibility and migration

Current writers emit trace-4, canonical-3 and execution-action-space-3 only.
Historical readers and fixture tests keep their schema meanings. The unused
historical production writer moves to tests. STPD must explicitly adopt a
canonical-3 importer for complete Full-Run research projection; Platform imports
no STPD policy. Live UI removes K and uses an explicit launcher with compact
Recorder and Policy views. Data remains local until operator-authorized transfer.

## Rollback and non-goals

Rollback restores the prior exact package. New sessions retain their advertised
schema requirements and immutable bytes; older tools must not pretend to validate
unsupported capabilities. No automatic upload, gameplay retries, second legality
engine, scientific admission, exhaustive all-valid guarantee, power-loss recovery
or retrospective evidence upgrade is introduced. See the collection runbook for
versioned incidents and post-merge repair.
