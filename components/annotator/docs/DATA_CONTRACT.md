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

`native-action-ledger.jsonl` uses
`sts2.human-annotator/native-action-ledger-event-1`. Each exact-correlated
accepted root has one process-local action witness ID, native queue ID, ordered
lifecycle facts and exactly one recorder disposition:
`strict_transition_admitted` or `strict_transition_invalidated`. Admission
requires native `finished` plus the existing stable-successor gates. Cancellation
or overlap preserves decision/lifecycle accounting but produces no strict V2
transition. Missing or unknown ledger persistence makes audit fail; it is never
retried or repaired from a later frame. Sessions sealed before this additive
sidecar remain readable, so historical V2 bytes and meaning are not redefined.

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
