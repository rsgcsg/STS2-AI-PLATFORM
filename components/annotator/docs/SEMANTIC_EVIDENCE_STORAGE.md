# Semantic Evidence Storage

This note defines the minimum durable model for native-Human semantic evidence.
It is a storage and audit boundary, not a gameplay or action-authority model.

## Required facts

For every exact accepted Human root, the recording must retain:

- exact session, run, timeline, game, Modset, runtime, Connector, Annotator and
  artifact provenance;
- the Human observation role `H`, including native witness, exact-unique
  BoundAction mapping and the player-visible frame observed during correlation;
- the exact native execution or direct Commit that consumes semantic state `S`;
- one action identity `A` and its accepted/started/paused/resumed/finished,
  cancelled or aborted lifecycle facts;
- either a causal authoritative `S'` captured before another Human effect, or
  one explicit unknown/cancel/abort disposition;
- enough immutable content and ordering information for an independent auditor
  to verify every reference and reject cross-Human proof.

`H`, `S` and `S'` remain distinct semantic roles even when two roles reference
the same immutable frame content.

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
```

The digest is over canonical uncompressed frame content. Physical compression
is optional cold-storage encoding and never changes semantic identity. Event
records preserve the role of each reference and do not embed full frames.

The writer may cache a frame by immutable Snapshot identity within one session
to avoid repeated serialization and hashing. The independent auditor resolves
each reference, verifies path containment and content digest, reconstructs the
semantic timeline, and applies the same causal invariants as the legacy trace
auditor.

## Capture boundary

Cheap Snapshot-only observation may decide that a Read-rich capture cannot yet
be a decision boundary. It never establishes `S` or `S'`. Once a candidate is
possible, one complete Player Environment capture with the interaction-specific
required Reads is evaluated on its own exact identity. Only that authoritative
capture can bind execution state or prove a successor.

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

## Current evidence boundary

The closed owner session
`session-20260828T151112Z-a559d80cd88741738f2a902427b10140`
is immutable predecessor evidence for the current repair artifact. Its existing
audit passes with 77 Decision V2 records, while the semantic trace accounts for
233 accepted roots and 233 proved dispositions. The session remains schema 2;
offline normalization can compare representations but cannot rewrite or upgrade
that raw evidence.
