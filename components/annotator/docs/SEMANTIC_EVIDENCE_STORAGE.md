# Semantic Evidence Storage

This note defines the minimum durable model for native-Human semantic evidence.
It is a storage and audit boundary, not a gameplay or action-authority model.

## Required facts

For every exact accepted Human root, the recording must retain:

- exact session, run, timeline, game, Modset, runtime, Connector, Annotator and
  artifact provenance;
- the Human observation role `H`, including native witness, exact-unique
  BoundAction mapping and the player-visible frame observed during correlation;
- the exact native execution or direct Commit and its execution-adjacent
  authoritative frame candidate;
- one action identity `A` and its accepted/started/paused/resumed/finished,
  cancelled or aborted lifecycle facts;
- either a trace-level successor candidate captured before another Human
  effect, or one explicit unknown/cancel/abort disposition;
- enough immutable content and ordering information for an independent auditor
  to verify every reference and reject cross-Human proof.

Human observation, execution boundary, and successor candidate remain distinct
roles even when two roles reference the same immutable frame content. Storage
role names do not promote a frame to canonical sequential `S` or `S'`.

## Canonical layout

New recordings use a normalized content-addressed evidence graph:

```text
semantic-boundary-trace.jsonl
  small ordered action/lifecycle/disposition events
  -> human_observation_ref
  -> execution_pre_ref
  -> successor_ref
  -> optional boundary_state_ref

semantic-frames/sha256/<prefix>/<digest>.json
  one canonical uncompressed FrozenDecisionFrame per unique content digest
  -> existing content-addressed Read payloads

canonical-transitions.jsonl
  serialized-lane S + A(S) -> A -> S' references
  -> exact Decision V2 action
  -> exact pre/successor semantic frame objects
```

The digest is over canonical uncompressed frame content. Physical compression
is optional cold-storage encoding and never changes semantic identity. Event
records preserve the role of each reference and do not embed full frames.

The writer may cache a frame by immutable Snapshot identity within one session
to avoid repeated serialization and hashing. The independent auditor resolves
each reference, verifies path containment and content digest, reconstructs the
semantic timeline, and applies the same causal invariants as the legacy trace
auditor.

The canonical stream is additive and absent from predecessor sessions. Its
auditor also binds frame content back to the matching Decision V2 record; a
valid object from another action cannot be substituted. It does not reinterpret
schema-3 `transition_proved` as canonical eligibility.

## Capture boundary

Snapshot-only observation is semantically narrower than Read-rich capture, but
it is not computationally cheap: both construct the public Snapshot and complete
BoundAction catalog. It must therefore run only for a concrete causal purpose.
Migrated canonical families do not capture a parallel execution-pre frame or
poll recovery/successor state. The next mutation edge or Close captures one
Read-rich boundary and reuses it for settlement and subsequent admission.
Operational status refresh is explicit and never authorizes a transition.
Schema-3-only families retain their bounded trace mechanism until migrated.

This removes repeated Reads and serialization while preserving fail-closed
proof. No timing, animation, queue-idle or later-state inference is introduced.

## Compatibility and projections

- Historical schema-1 and schema-2 sessions remain byte-preserved and auditable
  through legacy readers.
- The new writer does not keep producing repeated inline schema-2 frames merely
  for compatibility.
- A conversion or portable export has its own digest and provenance and never
  replaces the owner-attested raw session.
- Decision V2 may remain an offline materialized compatibility projection for
  existing verified consumers. It is not the canonical semantic store.
- STPD continues to own `ResearchTransition`, corpus admission and Parquet/Zstd
  training datasets. Platform raw evidence does not acquire research semantics.

## Rejected primary designs

- Compression-only retains repeated capture, hashing, serialization and giant
  lifecycle records, so it is only a cold-storage control.
- SQLite or a binary event database adds operational complexity without a
  measured need that immutable objects plus an append-only log cannot satisfy.
- Per-surface storage models would duplicate the causal kernel; new surfaces add
  witness adapters, not new H/S/A/S' persistence semantics.

## Operational performance profile

Current source records bounded per-stage counts and latency quantiles at Close
for separately named recovery, semantic and legacy probes, Read-rich/semantic
capture, serialization, hashing, object writes, buffered append and durable
Close flush. The profile is diagnostic output, not Human evidence; it cannot
authorize actions or alter H/S/A/S'. The analyzer derives capture call rate,
cumulative main-thread stall and append cost from that profile. It is absent
from older sessions and must not be synthesized from event timestamps.

Hot append-only streams flush bytes to the OS so immediate write failures remain
visible, but they do not fsync each lifecycle callback. Close flushes every
Decision, invalidation, RunJournal, native-ledger and semantic stream durably
before publishing Closed. Only a successfully closed session is a durable
evidence seal; interrupted sessions remain inspectable partial evidence.

## Current evidence boundary

Closed owner session
`session-20260829T052157Z-e549d3601e7640f997b6f475180b2dfe`
is exact schema-3 Human trace evidence for artifact
`4fa67570... / 51c7c37b...`.
It independently audits 188 Decision V2 records and accounts for 333 accepted
roots with 333 proved dispositions and zero unknown. The event log contains no
inline role frames; 2,724 references resolve to 947 immutable frame objects.
Persisted Reads are 5.354 per accepted root. A subsequent exact profiler session
`session-20260829T072035Z-...`, artifact `f1afebd2... / a618ef18...`, accounts
for 267/267 proved actions and shows that synchronous Player Environment capture
consumed 50.47% of recording wall time. Those repairs were later exercised by
exact owner session
`session-20260829T084437Z-cc4079776c9e417eba53a122e452cab7` on artifact
`bb37d34f... / 3587836e...`. That session accounts for 933 trace dispositions,
but mechanical canonical calibration yields zero complete
`S + A(S) -> A -> S'` rows. See the root causal performance baseline and
canonical causality decision for the full attribution and non-claims.
