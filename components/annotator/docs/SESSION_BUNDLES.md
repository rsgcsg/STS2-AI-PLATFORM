# Session Bundles

The current `pack-session` command creates `sts2.human-annotator/session-bundle-3`.
Its export is the exact durable canonical stream, including native-input and
first-class selector decisions. Compatibility Decision records are retained in
raw evidence when produced; their count is not the number of Human decisions.

```text
Closed exact session
  -> current independent causal audit
  -> pack-session with explicit worker/campaign Human attestation
  -> immutable bundle and complete checksum inventory
  -> Platform Evidence typed verification / store / transfer / receive
  -> external consumer's explicit schema adapter and research admission
```

The current bundle contains:

```text
raw/                              unchanged session evidence
export/canonical-transitions.jsonl byte-identical raw canonical stream
profile/capture-profile.json       exact capture profile
audit/audit-report.json           independently run producer causal audit
session-bundle-manifest.json       content identity, canonical_count, run_ids, attestation
checksums.sha256                   complete file inventory
```

The content identity binds all raw bytes, canonical export, profile, Human
attestation and the complete audit report hash. Run IDs include journal-observed
runs even when they have no canonical rows. Packing requires exactly one final
`session_closed` journal event and a structurally passing current audit. It does
**not** require zero unresolved decisions or zero real failures: failed-only
sessions with no canonical rows remain useful, transportable defect evidence.
No success row is invented to make such a session exportable.

`export` now writes canonical bytes and refuses to overwrite either an existing
export or its source session. An identical pack retry reuses identical bundle
bytes; a changed destination is rejected. The retired `native-action-ledger.jsonl`
is excluded from current bundles, retaining its archival meaning separately.

The explicit `export-compatibility` and `pack-session-compatibility` commands
retain Decision-record-2 / bundle-2 for existing consumers. Published Python
bundle-1 and bundle-2 verifiers retain their original typed contracts. In the
independent STPD repository, the current Human importer still reads
Decision-record-1/2 and `export/decisions.jsonl`; its corpus registry remains V1.
STPD needs a version-pinned canonical/schema-3 adapter before it can consume the
Full-Run stream. Platform does not implement that research projection or grant
training eligibility. Never rename a bundle-3 manifest to bundle-2.

Platform Evidence's V3 reader independently verifies envelope identity, exact
raw/export equivalence, Read/frame/catalog content addresses, decision lineage,
canonical-to-proved-trace joins and native/public action bindings. The hashed
producer audit remains the causal validator; portable verification does not
replay STS2, invent missing `S'`, prove Human origin, or qualify an uninterrupted
Full Run. The Human attestation is explicitly `machine_verifiable: false`.

## Distributed collection and defect reports

Retain closed raw sessions and verified bundles read-only, keyed by content ID,
with exact source/game/Mod SHA/MVID/runtime identity. Collection campaigns should
pin the package and capture profile, and store typed verification findings plus
producer audit separately from research admission. A failed canonical decision
must remain failed in the original bundle even after a later source repair.

For a defect, share the bundle content ID, failing decision/native occurrence ID,
reason and exact runtime identity, with the smallest owner-approved private raw
bundle needed to reproduce it. Include the independent verification report and
relevant native log locally; raw gameplay, saves and logs must not enter Git or
public PR text. No background upload or telemetry is implied. An operator
explicitly transfers evidence through the Evidence store/receiver workflow.

A repair receives a new exact candidate, a faithful regression, new build/load
identity and a bounded Human reproduction. Re-auditing historical bytes may add
a new versioned report; it cannot rewrite old evidence, promote an unknown to
proved, or transfer Human qualification to the repaired artifact.
