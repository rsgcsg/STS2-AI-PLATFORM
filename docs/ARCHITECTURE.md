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
UI Prefix -> freeze Connector frame -> game native validation/Commit
-> RequestEnqueue Postfix -> exact reference match -> wait for stable S'
-> append record
```

The observer uses Harmony Prefix/Finalizer only to establish a thread-local UI
scope and a Postfix to observe an action already accepted by STS2. It does not
skip a method, alter an argument/result, transpile game code, or enqueue an
action. Connector-origin actions call a different native entry path and do not
enter the human UI scope.

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

Only a complete interactive pre-frame is eligible. One accepted action may be
pending at a time. A different complete interactive snapshot in the same runtime
and environment is recorded as S'. Timeout, overlap, runtime drift, unsupported
action type, or mapping failure is appended to `invalidations.jsonl`.
