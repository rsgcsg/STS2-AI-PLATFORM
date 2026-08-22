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
latest complete authoritative Connector frame -> UI Prefix selects the current
or same-interaction frozen frame -> game native validation/Commit
-> RequestEnqueue Postfix -> exact reference match -> wait for stable S'
-> append record
```

The observer uses Harmony Prefix/Finalizer only to establish a thread-local UI
scope and a Postfix to observe an action already accepted by STS2. It does not
skip a method, alter an argument/result, transpile game code, or enqueue an
action. Connector-origin actions call a different native entry path and do not
enter the human UI scope.

Card targeting can make the UI temporarily settling before `TryPlayCard` runs.
The recorder therefore retains the latest complete frame and may reuse it only
when runtime, environment, and interaction identity still match. Each scope
admits exactly one expected root action; game-owned actions caused by that root
are ignored rather than mislabeled as additional human decisions.

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

Only a complete interactive pre-frame is eligible. One accepted root action may
be pending at a time. Native actions caused by that root are outside the human
decision boundary and are ignored. A different complete interactive snapshot in
the same runtime and environment is recorded as S'. Timeout, overlap, runtime
drift, root-contract error, or mapping failure is appended to
`invalidations.jsonl`.
