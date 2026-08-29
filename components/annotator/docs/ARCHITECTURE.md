# Architecture

## Ownership

```text
STS2 native UI and action queue  game truth and accepted action
STS2-Connector                  fair-player state, complete A(S), exact Host bindings
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

An additive semantic timeline consumes the same exact accepted-action identity
without changing that path:

```text
frozen Human observation H (observation evidence only) + exact accepted action
-> game-owned started / choice pause-resume / cancelled / finished lifecycle
-> exact execution captures an adjacent state boundary
-> state-complete execution handoff or complete interactive observation
-> trace disposition, followed by independent canonical training calibration
```

`SemanticBoundaryTracker` never publishes or executes actions. Human H is not
silently promoted to semantic S. The current tracker can establish a
state-causal trace boundary while the UI catalog has not republished; state,
Read and catalog completeness are recorded independently. Such a boundary is
not canonical sequential S without complete same-state A(S) and exact action
membership. An arbitrary settling poll cannot prove a causal training boundary.
`GameAction.Finished` is lifecycle evidence, not universal completion. The
legacy V2 adapter and semantic timeline share action identity, but neither is
authority for the other.

Acceptance sequence is evidence order, not causal execution order. Every action
binds S only at its real execution boundary, so a later accepted source-local
choice executing first naturally consumes the then-current S. The same complete
state captured before the following queued action both closes the prior causal
edge and becomes that action's S. There is no separate rebind exception. An
incomplete state capture remains unknown and cannot be repaired from a later
frame.

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
Ready -> StartNewSession -> Recording <-> Paused -> Closing/Draining -> Closed
  ^                                                            |
  +--------------------- StartNewSession -----------------------+
```

Every session receives a new session ID, timeline, store, counters and run
sequence. Pause blocks new witness scopes while already admitted work settles.
Close blocks new scopes, drains native lifecycle and semantic-boundary work, and
only then flushes and disposes the RunJournal/evidence streams. A five-second
drain limit may classify a still-unproved semantic edge unknown; elapsed time
never proves S'. Audit/pack/verify/store/transfer remain offline Evidence
operations.

The frame loop is not observation authority. It performs no Player Environment
capture while an idle recording has no concrete work. Exact lifecycle callbacks
capture execution state, unresolved semantic actions request bounded successor
observation, and the legacy ledger requests a 50 ms recovery probe only while it
has recovery debt. Session/run lifecycle changes request one operational status
refresh; status does not continuously rebuild game truth. These requests share
one capture when concurrent and never prove a boundary by elapsed time.

Evidence streams append and flush to the OS on the native callback path so write
errors remain immediate, but they do not fsync each lifecycle fact. Close is the
durability boundary: it blocks new witnesses, drains causal work, writes the
derived coverage summary, then `Flush(true)` seals every stream before Closed is
published. An interrupted session is partial inspectable evidence, not a durable
Human evidence seal.

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
unchanged. Schema-3 events reference exact content-addressed frozen frames by
H/S/S' role, eliminating repeated inline frames without normalizing away
snapshot or Read provenance. The semantic trace is observation-only and audited
independently; historical schema-1/2 rows remain readable.
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

Full-Run expansion does not add a surface switch to the causal tracker. Map
selection enters the same lifecycle path through STS2's
`VoteForMapCoordAction`. Reward claim/proceed and card-reward selection have no
equivalent root `GameAction`, so narrow source-local Prefix/Postfix observers
record their exact native UI delivery while Connector still supplies the frozen
complete BoundAction. These observers neither await business reward completion
nor create legality. Semantic state Reads are selected by interaction kind and
remain information completeness only; they cannot publish or authorize an
action.

## Canonical Sequential Collection Decision

Exact runtime calibration shows that native UI staging removes PlayCard,
UsePotion and EndTurn affordances before execution, while generic interactive
polling does not prove causal S'. The natural observer therefore remains useful
for accounting and future action-chain research but is not canonical one-step
training authority. ADR 0003 selects serialized Human input as the preferred
future one-step collection architecture. Implementation is not authorized: no
input behavior, feature flag, patch, build, install or canary has been added.

The application event stream is typed, process-local and bounded. A consumer
queries current status, then requests events after sequence N. A gap means the
consumer must query status again. Application events are operational state, not
Human evidence and not action authority.
