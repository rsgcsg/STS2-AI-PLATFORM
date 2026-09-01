# Architecture

## Ownership

```text
STS2 native UI and action queue  game truth and accepted action
Native Foundation               typed read-only semantic S, A_sem(S), lifecycle facts
STS2-Connector                  fair-player projection, A_public, exact Host bindings/delivery
Human Annotator                 correlation, causal boundary observation, raw evidence
STPD                            research projection and dataset admission
```

The current production Human-action path is:

```text
Human UI scope captures H and exact public BoundAction binding
-> STS2 accepts one exact native root
-> ActionExecutor.BeforeActionExecuted captures execution S and typed A_sem(S)
-> typed native lifecycle proves Commit
-> SemanticBoundaryTracker proves a separate causal successor boundary
-> non-authorizing projections write Decision V2 and canonical evidence
```

`SemanticBoundaryTracker` never publishes or executes actions. Human H is not
silently promoted to semantic S. It is the sole current authority for Human-root
causal order and successor settlement, not for semantic legality. At the exact
execution boundary the same process-local capture supplies a fair-player state
frame and a typed read-only Native Foundation observation of `A_sem(S)`.
Connector's public catalog may already be empty because input is settling; that
does not erase the independently observed semantic action space and does not
make the frame publicly interactive. An arbitrary settling poll cannot prove a
causal successor. `GameAction.Finished` is lifecycle/Commit evidence, not
universal `S'`.

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
`TryPlayCard(target)` commits. The recorder stages H at `StartCardPlay`, then
uses it only when the exact same card reference, runtime, environment,
interaction and target resolve to one frozen public BoundAction within 30
seconds. There is no latest-frame fallback. This Human-time binding proves the
chosen public action and correlation; it is not execution S or execution
legality. At first native execution the typed Native Foundation provider
independently describes the same native operand and requires exact-once
membership in `A_sem(S)`. Each scope admits exactly one expected root action;
game-owned actions caused by that root are ignored rather than mislabeled as
additional Human decisions.

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
sequence. Pause blocks new witness scopes while already admitted work settles.
Close blocks new scopes, immediately disposes any still-unproved final root as
`session_closed_before_successor_boundary`, then durably flushes and disposes
the RunJournal/evidence streams. Close does not wait for a semantic drain and
never captures or promotes a replacement `S'`. Audit/pack/verify/store/transfer
remain offline Evidence operations.

The frame loop is not observation authority. It performs no Player Environment
capture while an idle recording has no explicit status or Close work. Canonical
families capture one complete boundary at the mutation edge; that same frame
settles the predecessor and becomes the next action's pre-frame. Native
lifecycle callbacks carry identity and terminal facts without rebuilding a
Snapshot. The remaining schema-3-only Full-Run adapters may request a bounded
boundary until they migrate; they cannot authorize canonical rows.

Evidence streams append and flush to the OS on the native callback path so write
errors remain immediate, but they do not fsync each lifecycle fact. Close is the
durability boundary: it blocks new witnesses, drains causal work, writes the
derived coverage summary, then `Flush(true)` seals every stream before Closed is
published. An interrupted session is partial inspectable evidence, not a durable
Human evidence seal.

Every exact-correlated native root enters `SemanticBoundaryTracker` at
`GameAction.OnEnqueued`, after STS2 assigns its queue ID and before queue
notification or execution. The observer subscribes to the action's game-owned
started, PlayerChoice pause/resume, cancelled and finished events without
changing the action. A later root may be accepted and tracked without blocking
Human input. Its exact pre-execution frame may settle only the immediately
preceding committed root and simultaneously becomes that later root's own S;
proof never crosses the later Human effect.

Cancellation, runtime drift, lifecycle persistence uncertainty, root-contract
error, mapping failure, incomplete semantic action space or missing successor
is fail-closed. Schema-4 events reference exact content-addressed H/S/S' frames
and, when present, one exact execution semantic action-space object. Historical
schema-1/2/3 rows and native ledgers remain readable but are not current mutable
admission or causal authorities. Persistence failure disables the modern trace
for the session, surfaces `semantic_boundary_trace_unavailable`, and never
retries or invents a later boundary.
Audit rejects a proved transition whose semantic pre does not match its complete
pre-execution boundary, or whose causal window contains another Human action
start after A begins and before S'.

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

Exact runtime evidence shows that native UI staging removes PlayCard,
UsePotion and EndTurn from the public catalog before execution even though the
STS2-owned semantic decision still contains them. Current canonical evidence
therefore binds execution S to a content-addressed typed `A_sem(S)` observation
for those families. Direct UI domains may use their exact complete public
execution catalog when that catalog itself is the typed delivery surface.
Decision V2 separately preserves the frozen Human-time public frame for durable
compatibility. The projector only joins already-proved facts and cannot settle
the root or manufacture action-space membership. ADR 0003 remains a withdrawn
serialized-input candidate, not current authority.

The application event stream is typed, process-local and bounded. A consumer
queries current status, then requests events after sequence N. A gap means the
consumer must query status again. Application events are operational state, not
Human evidence and not action authority.
