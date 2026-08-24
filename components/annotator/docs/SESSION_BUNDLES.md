# Session Bundles

`HumanSessionBundle` is the durable boundary between human collection and STPD.
The Annotator owns raw native-human witness evidence; STPD owns registry,
admission, corpus construction, split and training eligibility.

```text
CollectionProfile + human native session
  -> audit
  -> pack-session with explicit worker/campaign attestation
  -> immutable checksummed bundle
  -> storage transfer
  -> STPD register/build/inspect
```

The profile pins exact game, Connector, Annotator, Player Environment protocol,
Modset, platform, record schema and supported action families. Any drift fails
closed. A profile permits collection only for its declared envelope; it does not
qualify a new artifact or action family.

The bundle contains:

```text
raw/                         untouched recording evidence
audit/audit-report.json      independent audit result
export/decisions.jsonl       deterministic admitted-record export
profile/collection-profile.json
session-bundle-manifest.json content identity and explicit attestation
checksums.sha256             complete file inventory
```

The attestation says that the owner asserts human native-UI operation. It is
deliberately marked `machine_verifiable: false`; checksums and audit cannot prove
who operated the game. Raw data and bundles remain local/private and must not be
committed.

An exact pack retry is idempotent. A changed destination is rejected rather than
overwritten. STPD verifies the bundle again and never trusts a filename or loose
manifest claim.
