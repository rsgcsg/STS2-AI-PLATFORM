# Session Bundles

`SessionBundlePacker` creates the current wire contract
`sts2.human-annotator/session-bundle-2`, the durable boundary between Human
collection and STPD. The Annotator owns raw native-human witness evidence;
STPD owns registry, admission, corpus construction, split and training
eligibility. The old bundle-1 reader/packer is historical-only.

```text
Current CaptureProfile + human native session
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

The current bundle contains:

```text
raw/                         untouched recording evidence
audit/audit-report.json      independent audit result
export/decisions.jsonl       deterministic admitted-record export
profile/capture-profile.json
session-bundle-manifest.json content identity and explicit attestation
checksums.sha256             complete file inventory
```

The attestation says that the owner asserts human native-UI operation. It is
deliberately marked `machine_verifiable: false`; checksums and audit cannot prove
who operated the game. Raw data and bundles remain local/private and must not be
committed.

An exact pack retry is idempotent. A changed destination is rejected rather than
overwritten. STPD verifies the bundle again and never trusts a filename or loose
manifest claim. The current packer accepts only `recording-manifest-2`; it
excludes the archival `native-action-ledger.jsonl` sidecar from current bundle
contents and identity. Historical bundles remain available through the explicit
archival reader without adding a second current recording authority.
