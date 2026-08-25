# Architecture

## Ownership

```text
STS2 native UI and action queue  game truth and accepted action
STS2-Connector                  stable S, complete A(S), exact Host bindings
Human Annotator                 correlation, successor capture, raw evidence
STPD                            research projection and dataset admission
```

The only production correlation path is:

```text
native `StartCardPlay` stages the exact complete Connector frame while the card
is still in the hand -> final UI Prefix selects current or same-card staged frame
-> game native validation/Commit
-> RequestEnqueue Postfix -> exact reference match -> wait for stable S'
-> append record
```

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

Only a complete interactive pre-frame is eligible. One accepted root action may
be pending at a time. Native actions caused by that root are outside the human
decision boundary and are ignored. A different complete interactive snapshot in
the same runtime and environment is recorded as S'. Timeout, overlap, runtime
drift, root-contract error, or mapping failure is appended to
`invalidations.jsonl`.

The application event stream is typed, process-local and bounded. A consumer
queries current status, then requests events after sequence N. A gap means the
consumer must query status again. Application events are operational state, not
Human evidence and not action authority.
