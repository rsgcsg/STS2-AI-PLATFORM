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

`human-session-bundle-1` remains readable. `human-session-bundle-2` additionally
verifies its embedded capture profile, RunJournal, state-bound Read evidence,
required materialized Reads, content-addressed Read blobs, and exact content
identity. Verification does not prove Human origin; the bundle carries an
explicit owner attestation with `machine_verifiable=false`.

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
admitted object.
