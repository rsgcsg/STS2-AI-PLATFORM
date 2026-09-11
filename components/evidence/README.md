# Platform Evidence

This component verifies typed immutable artifacts and moves their bytes without
owning gameplay, Human action, or research semantics.

```text
producer bundle
  -> typed verifier
  -> checksummed transfer
  -> quarantine or atomic promotion
  -> immutable local object
  -> transfer receipt
```

`human-session-bundle-3` is the current canonical-first collection format. Its
export preserves `canonical-transitions.jsonl`, while the complete raw session
retains compatibility rows, unresolved decisions and failed Human occurrences.
The verifier binds the producer causal audit by hash and independently checks
canonical/trace joins, lineage, exact action/catalog/frame/Read references and
closed-session identity. Zero canonical rows do not prevent transfer of valid
failure evidence; verification does not certify a zero-failure Full Run.

`human-session-bundle-1` remains readable. `human-session-bundle-2` additionally
verifies its embedded capture profile, RunJournal, state-bound Read evidence,
required materialized Reads, content-addressed Read blobs, and exact content
identity. Verification does not prove Human origin; the bundle carries an
explicit owner attestation with `machine_verifiable=false`.

V2/V3 capture-profile identity is the SHA-256 of the producer's compact,
schema-ordered JSON. Bundle content identity remains canonical sorted JSON.
These are intentionally distinct so independent consumers verify the exact
producer contract without changing V1 profile semantics. Current Read requirements
are keyed by phase, kind and optional interaction kind; a shop-only Read does
not become mandatory in combat. Existing V1/V2 research consumers require an
explicit V3 adapter update. Platform supplies verified evidence and no training
projection or eligibility policy.

## Check

```bash
npm run check
```

## Verify

```bash
npm run evidence -- verify-human-bundle /path/to/session-bundle
```

## Transfer And Receive

```bash
npm run evidence -- transfer-manifest /path/to/session-bundle \
  --content-id <bundle_content_id> \
  --artifact-type human-session-bundle \
  --output /path/to/transfer-manifest.json

npm run evidence -- receive /path/to/session-bundle /path/to/transfer-manifest.json \
  --root /path/to/evidence-store \
  --verify-type human-session-bundle \
  --receipt /path/to/transfer-receipt.json
```

The receiver validates bytes, then runs the typed verifier inside staging before
promotion. A failed or partial artifact is quarantined and never becomes an
admitted object. Each receive attempt also publishes a non-authorizing
`store-status.json` containing its last receipt so the read-only Workbench can
show operational state without reimplementing verification.
