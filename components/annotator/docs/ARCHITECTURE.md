# Architecture

## Ownership

```text
STS2 native UI and action queue  game truth and accepted action
STS2-Connector                  stable S, complete A(S), exact Host bindings
Human Annotator                 correlation, lifecycle/boundary observation, raw evidence
STPD                            research projection and dataset admission
```

The only production Human-action correlation path is:

```text
native `StartCardPlay` stages the exact complete Connector frame while the card
is still in the hand -> final UI Prefix selects current or same-card staged frame
-> game native validation/Commit
-> GameAction.OnEnqueued Postfix -> exact reference match and lifecycle witness
-> legacy V2 waits for a provably action-local stable interactive S'
-> append record
```

An additive semantic-boundary observer consumes the same exact accepted-action
identity without changing that path:

```text
frozen Human observation H + exact accepted action
-> game-owned started / choice pause-resume / cancelled / finished lifecycle
-> authoritative complete Player Environment capture immediately before the
   next tracked Human action executes, or at the next complete decision surface
-> proved S -> A -> S', cancelled/not-successful, or unknown
```

`SemanticBoundaryTracker` never publishes or executes actions. It does not use
the next Human decision pre-frame as the prior successor and does not treat
`GameAction.Finished` as universal business completion. The legacy V2 adapter
and the semantic trace share action identity, but neither is authority for the
other.

Acceptance sequence is evidence order, not causal execution order. The tracker
orders predecessors by observed native start. If a later accepted source-local
choice executes before an earlier queued action, the choice is settled first.
The queued precommit can consume the resulting S only when the complete
authoritative frame captured immediately before its own execution is available;
that frame replaces its earlier observation. An incomplete rebind is unknown.

The observer uses Harmony Prefix/Finalizer only to establish a thread-local UI
scope and a Postfix to observe an action already accepted by STS2. It does not
skip a method, alter an argument/result, transpile game code, or enqueue an
action. Connector-origin actions call a different native entry path and do not
enter the human UI scope.

Starting a native card play moves its holder out of the active hand before
`TryPlayCard(target)` commits. The recorder stages S at `StartCardPlay`, then
uses it only when the exact same card reference, runtime, environment,
interaction and target resolve to one frozen BoundAction within 30 seconds.
There is no latest-frame fallback: card play uses only the current exact frame
or that same-card staged frame, while end turn requires the current exact
frame. Each scope admits exactly one expected root action; game-owned actions
caused by that root are ignored rather than mislabeled as additional human
decisions. A same-type action cannot claim the root until its exact native card
and target resolve uniquely against the frozen catalog.

## Exact Mapping

The frozen Connector frame holds strong process-local references only for
referents in the frozen BoundAction catalog. A native card and target match by
`ReferenceEquals`; verb, subject, argument roles, and argument count must also
match. Native witness IDs are opaque recording evidence and cannot resolve back
to game objects.

## Authority

The observer Modset canary identifies exact provenance but does not enable
Connector mutation. Action publication still comes from current native UI
readiness; accepted human action still comes from STS2; correlation creates no
legality. An external Connector controller blocks recording.

## Lifecycle

Runtime initialization stops at `Ready`; it does not create evidence or bind a
session. `RecordingService` owns the application contract:

```text
Ready -> StartNewSession -> Recording <-> Paused -> Closing -> Closed
  ^                                                            |
  +--------------------- StartNewSession -----------------------+
```

Every session receives a new session ID, timeline, store, counters and run
sequence. Pause blocks new witness scopes while an already admitted pending
decision still settles. Close blocks new scopes and waits for that pending
decision to settle or invalidate before the RunJournal and evidence streams are
flushed and disposed. Audit/pack/verify/store/transfer remain offline Evidence
operations.

Only a complete interactive pre-frame is eligible. One non-overlapping accepted
root may remain a strict transition candidate. Every exact-correlated native
root enters a bounded ledger at `GameAction.OnEnqueued`, after STS2 assigns its
queue ID and before queue notification or execution. The observer subscribes to
the action's game-owned started, player-choice pause/resume, cancelled and
finished events without changing the action.

If another Human root is accepted before the first candidate has both finished
and reached an action-local stable successor, every action in that causal window
is explicitly accounted with its own frozen decision pre-frame and BoundAction,
but loses strict transition eligibility. The next decision pre-frame is not
evidence of the prior action's successor. Recovery is allowed only after every
tracked action is terminal and a fresh complete interactive boundary is
observed. Timeout, overlap, cancellation, runtime drift, lifecycle persistence
uncertainty, root-contract error, or mapping failure is fail-closed. Native
lifecycle, invalidated-decision facts and `semantic-boundary-trace.jsonl` are
additive sidecar evidence; `HumanDecisionRecordV2` bytes and meaning are
unchanged. The semantic trace is observation-only and audited independently.
Audit rejects a proved transition whose semantic pre does not match its complete
pre-execution boundary, or whose causal window contains another Human action
start after A begins and before S'.
Persistence failure disables that trace for the session, surfaces
`semantic_boundary_trace_unavailable` in RecordingStatus, and never retries or
invents a later boundary.

The current combat implementation uses existing typed `GameAction` lifecycle
events and `ActionExecutor.BeforeActionExecuted`. One exact-build read-only
Prefix observes `NCardPlayQueue.RemoveCardFromQueueForCancellation(PlayCardAction)`
because the pile-missing execution branch returns with native state `Finished`
without spending resources or running `OnPlay`; this is classified as
`not_a_successful_action`. No scheduler, argument, result or gameplay behavior
is changed.

The application event stream is typed, process-local and bounded. A consumer
queries current status, then requests events after sequence N. A gap means the
consumer must query status again. Application events are operational state, not
Human evidence and not action authority.
