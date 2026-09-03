# Data Contract

The current recording schemas are defined by
`src/STS2HumanAnnotator.Core/CurrentContracts.cs`. Wire names ending in `-2`
are the single current format; they are not a second active product. A current
session directory contains one immutable manifest, one append-only file per
observed run, one append-only invalidation stream, a semantic boundary stream,
content-addressed Read/frame/action-space objects, a minimal run journal, and
an atomically replaced coverage summary. The current store does not create a
native-action ledger.

Each admitted `CurrentDecisionRecord` contains:

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

Historical `native-action-ledger.jsonl` evidence uses
`sts2.human-annotator/native-action-ledger-event-2`. Each exact-correlated
accepted root has one process-local action witness ID, native queue ID, frozen
read-rich predecessor pre-frame, native witness, exact-unique mapping and selected
BoundAction. Later entries contain only ordered lifecycle facts and exactly one
recorder disposition: `strict_transition_admitted` or
`strict_transition_invalidated`; decision evidence is not repeated or rewritten.

`HistoricalRecordingAuditor` verifies that an admitted ledger decision exactly
matches its historical record. `native-action-ledger-event-1/2` sidecars remain
readable only through the explicit historical reader; the current recorder
neither mutates nor audits a native ledger, and the current bundle packer
excludes it. Historical Decision V1/V2 bytes retain their original meaning;
they are not a current admission, causality or successor authority.

`semantic-boundary-trace.jsonl` is the current Human causal evidence stream.
Historical schema-1/2/3 rows are handled only by explicit archival readers. New
schema-4 rows store ordered lifecycle/disposition facts plus explicit
`human_observation_ref`, `execution_pre_ref`, `successor_ref` and boundary-state
references. Each reference resolves below the session's
`semantic-frames/sha256/` directory to one exact canonical
`CurrentDecisionFrame`; audit verifies path containment, content digest and
snapshot identity before applying the same causal validator. Roles remain
distinct even when they reference identical content.

Every STS2-accepted Human gameplay decision has one durable auditable
occurrence/disposition: canonical, honest unknown, cancelled/rejected,
unsupported, or failed closed. This does not mean every pointer click is a
decision and it does not require `canonical == clicks`. When an exact accepted
source-local mutation cannot form a root, `invalidations.jsonl` carries an
immutable `human_occurrence` object with its action family/verb, exact native
subject and operands when available, owner, paused-parent lineage, mechanism
and `failed_closed` disposition. For current generated-card select/skip rows,
audit requires that object and validates the exact callback identity, native
owner, and, for select, card and holder identities; available paused-parent
lineage must be structurally complete. These optional additive fields keep the
current schema readable by current readers, while records emitted by current
bytes are held to that stronger audit requirement. It is evidence of an
occurrence, not a mutable
admission ledger, legality engine or second canonical truth.

An execution event may additionally reference one
`sts2.human-annotator/execution-semantic-action-space-2` object below
`semantic-action-spaces/sha256/`. It preserves the exact read-only Native
Foundation semantic state/catalog captured at the native action-binding
boundary, the described native action and its exact-once membership. For a
`GameAction` this boundary is `ActionExecutor.BeforeActionExecuted`; for a
source-local callback it is the callback's pre-admission seam. Schema 2 also
records the exact Human `BoundActionId` joined to the native selection, so
public and native verbs may differ without losing identity. Audit binds the
content digest, action witness, semantic state/catalog digests and typed
Human/native action identity. Schema 1 remains readable historical evidence
under its original same-verb matching rules. This object is evidence of an
STS2-owned semantic decision, not an Annotator legality engine or Connector
delivery catalog.

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

A `native_decision_owner_ready` boundary additionally carries typed domain,
exact process-local owner witness/type and native-mechanism evidence. Audit
requires that evidence and an exact domain match; the owner signal alone is not
state, action-space or successor authority. If the synchronous Connector frame
is partial or mismatched, no proof is emitted.

`native_continuation_observed` records the exact
`GameAction.BeforePausedForPlayerChoice` parent witness. A successful
continuation is a typed parent Commit seam for opening the immediate nested
Human choice; it is not `GameAction.Finished`, a child action, or `S'`. The
parent's later ready/resume/finish callbacks retain their own lifecycle
meaning. A canonical parent row may therefore name a native terminal/direct
Commit **or** this exact PlayerChoice continuation, but still requires a
separate causal successor and no intervening Human effect.

This stream is not corpus admission or research authority. Current audit only
promotes current schema containers; predecessor sessions retain their original
claims only through an explicit archival reader. Evidence is never transferred
or backfilled.

`native-semantic-discriminator.jsonl` is an additive read-only diagnostic
stream. For each exact-correlated root it records ordered native lifecycle and
projections of the public UI catalog and Native Foundation semantic decision.
At the execution boundary it consumes the same immutable capture preserved by
schema 4 rather than recapturing or acting as a second semantic authority.
Lifecycle rows may be `not_sampled`. A `successful_capture_delegated` detail
means the canonical semantic-boundary stream durably owns that sample; it is not
a missing capture and cannot authorize an action. Successful sampled roots must
match exactly once; cancellation and PlayCard pre-Commit abort are separate
dispositions. Player-choice commits are linked to the paused parent action.
The stream is audited by `audit-native-semantic`; it does not authorize input,
change the current Decision record, admit training rows, prove End Turn
completion by itself, or claim Full-Run semantic completeness.

The authoritative `audit` consumes this stream only for envelope/session
integrity and cross-stream accounting. Per-action diagnostic coverage or
membership findings (for example `not_applicable` native catalog coverage) are
retained in the `audit-native-semantic` report but do not invalidate an
otherwise valid semantic/canonical session. Malformed JSON, schema/sequence
errors, identity mismatches, and orphan cross-stream identities remain fatal.

`canonical-transitions.jsonl` schema 2 is the non-authorizing current canonical
projection and the sole durable canonical truth. A row is written only after
one complete semantic state, exact-once selected action in the authoritative
native action space, exact Human/native
correlation, exact native terminal/direct Commit or PlayerChoice continuation,
no intervening Human effect and one
complete causal successor are all present. It references immutable semantic
frame and typed semantic action-space objects. The action-space object records
whether it was captured before `GameAction` execution or before a source-local
native callback admission, plus the exact Human BoundAction binding. Historical
or not-yet-migrated direct UI evidence may name `public_bound_actions` only when
its exact frame has the complete typed public catalog and contains the action
exactly once. Audit
verifies hashes, identities, the unique tracker proof and action membership.
Schema-1 serialized-input rows remain readable historical evidence.

Canonical sequential training evidence has the stricter contract:

```text
S_t + complete A(S_t) -> exact A_t in A(S_t) -> causal S_(t+1)
```

H and its frozen public BoundAction prove Human choice/correlation but do not
define S. Acceptance does not define execution order. Execution S is eligible
only when its same-boundary authoritative action space contains A exactly once;
current public deliverability is not substituted for that semantic fact. A
generic later interactive Snapshot is not causal S' merely because it is
interactive. `calibrate-semantic-training` mechanically classifies semantic
candidates and joins them to the durable canonical stream. It never creates
canonical truth or promotes a historical admission or a `transition_proved`
label by terminology alone.

`SemanticActionReference` may add exact process-local witness, mapping,
BoundAction and native-mechanism metadata. Missing metadata on historical rows
retains its prior meaning. The current `game_action` and `direct_ui_commit`
mechanisms both resolve one already-published frozen BoundAction and converge on
the same tracker and disposition rules; neither is a second execution API.

`pack-session` creates `sts2.human-annotator/session-bundle-2`. A current bundle contains
the untouched raw session, independent audit, deterministic export, the exact
versioned `CollectionProfile`, a human-origin attestation, a content-identity
manifest, and a complete `checksums.sha256` inventory. The content identity binds
session, worker, campaign, profile, run IDs, raw files, export and audit. Existing
bundles are immutable: an exact retry reuses identical bytes and any changed
retry fails.

Files are append-only by Recorder behavior, not cryptographically tamper-proof.
The SHA-256 of an exported JSONL is one source identity, not the whole bundle
identity. STPD independently verifies raw/export equivalence, checksums, profile
identity and its own research admission before a bundle enters a corpus. The
current CLI accepts only the current manifest; predecessor V1 bundles require
the explicit historical packer. Preserve both raw sessions and accepted bundles
read-only.
