# Data Contract

Decision V1/V2 schemas are defined by
`src/STS2HumanAnnotator.Core/Contracts.cs` and `V2Contracts.cs`. A session directory contains one
immutable manifest, one append-only file per observed run, one append-only
invalidation stream, an additive native-action lifecycle ledger, a minimal run
journal, content-addressed Read blobs, and an atomically replaced coverage
summary.

Each admitted `HumanDecisionRecord` contains:

- exact environment and artifact identity;
- the full frozen pre Snapshot and catalog digest/count;
- native UI origin, accepted action type, and opaque native witness IDs;
- exact-unique reference-mapping evidence;
- the selected public BoundAction copied from the frozen catalog;
- a different complete interactive successor Snapshot;
- explicit eligibility gates and non-claims.

`audit` independently recomputes and verifies nested Snapshot identity, catalog
digest/count, chosen-action uniqueness, runtime continuity, sequence monotonicity,
and exact identities. `export` refuses a failed audit and concatenates run files
in deterministic order.

`native-action-ledger.jsonl` now uses
`sts2.human-annotator/native-action-ledger-event-2`. Each exact-correlated
accepted root has one process-local action witness ID, native queue ID, frozen
Decision V2 pre-frame, native witness, exact-unique mapping and selected
BoundAction. Later entries contain only ordered lifecycle facts and exactly one
recorder disposition: `strict_transition_admitted` or
`strict_transition_invalidated`; decision evidence is not repeated or rewritten.

Admission requires native `finished` plus the existing stable-successor gates.
Audit verifies that an admitted ledger decision exactly matches its Decision V2
record. Cancellation or overlap preserves decision/lifecycle accounting but
produces no strict V2 transition. Missing or unknown ledger persistence makes
audit fail; it is never retried or repaired from a later frame. Historical
`native-action-ledger-event-1` sidecars remain readable, but do not retroactively
gain frozen decision payloads. Decision V1/V2 bytes and meaning are unchanged.

`semantic-boundary-trace.jsonl` is an additive semantic evidence sidecar.
Historical schema-1/2 rows remain readable with their original meaning. New
schema-3 rows store ordered lifecycle/disposition facts plus explicit
`human_observation_ref`, `execution_pre_ref`, `successor_ref` and boundary-state
references. Each reference resolves below the session's
`semantic-frames/sha256/` directory to one exact canonical
`FrozenDecisionFrameV2`; audit verifies path containment, content digest and
snapshot identity before applying the same causal validator. Roles remain
distinct even when they reference identical content.

The timeline stores Human observation H separately from execution-adjacent
state evidence and records exact action identity,
accepted/started/choice/cancelled/finished facts, state/Read/catalog
completeness, boundary captures, and exactly one semantic disposition:
`transition_proved`, `transition_unknown`, cancelled before/after start, or
aborted before native Commit. A trace-level proved transition requires either a
complete interactive decision boundary or a state-complete capture
synchronously before the next tracked Human effect. The latter does not require
a republished action catalog, so it is not by itself canonical one-step
training eligibility. Native acceptance order is not assumed to equal execution
order; every trace-level pre-state must equal its own exact pre-execution boundary. Audit
rejects a mismatched execution pre-state or another Human action effect between
that action's start and successor boundary. A queued action cancelled before
`started` is retained as Human/native evidence
but is not a successful A. A cancellation after start remains unknown. Audit
rejects contradictory lifecycle/disposition combinations and duplicate
dispositions.

This sidecar is not `HumanDecisionRecordV2`, corpus admission, or durable
training authority. Predecessor sessions without it remain valid, and existing
V1/V2 bytes are never reinterpreted.

Canonical sequential training evidence has the stricter contract:

```text
S_t + complete A(S_t) -> exact A_t in A(S_t) -> causal S_(t+1)
```

H proves Human origin/correlation but does not define S. Acceptance does not
define execution order. An execution-adjacent state is not trainable S unless
the complete same-state action catalog exists and contains A exactly once. A
generic later interactive Snapshot is not causal S' merely because it is
interactive. `calibrate-semantic-training` mechanically enforces these rules,
joins historical action facts from immutable sidecars, and never promotes a
legacy V2 admission or schema-3 `transition_proved` label by terminology alone.

`SemanticActionReference` may add exact process-local witness, mapping,
BoundAction and native-mechanism metadata. Missing metadata on historical rows
retains its prior meaning. The current `game_action` and `direct_ui_commit`
mechanisms both resolve one already-published frozen BoundAction and converge on
the same tracker and disposition rules; neither is a second execution API.

`pack-session` creates `sts2.human-annotator/session-bundle-1`. A bundle contains
the untouched raw session, independent audit, deterministic export, the exact
versioned `CollectionProfile`, a human-origin attestation, a content-identity
manifest, and a complete `checksums.sha256` inventory. The content identity binds
session, worker, campaign, profile, run IDs, raw files, export and audit. Existing
bundles are immutable: an exact retry reuses identical bytes and any changed
retry fails.

Files are append-only by Recorder behavior, not cryptographically tamper-proof.
The SHA-256 of an exported JSONL is one source identity, not the whole bundle
identity. STPD independently verifies raw/export equivalence, checksums, profile
identity and strict admission before a bundle enters a corpus. Preserve both raw
sessions and accepted bundles read-only.
