# Data Contract

Schema version 1 is defined by
`src/STS2HumanAnnotator.Core/Contracts.cs`. A session directory contains one
immutable manifest, one append-only file per observed run, one append-only
invalidation stream, and an atomically replaced coverage summary.

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

Files are append-only by Recorder behavior, not cryptographically tamper-proof.
The SHA-256 of an exported JSONL is the downstream source identity. Preserve raw
session directories read-only when reproducibility matters.
