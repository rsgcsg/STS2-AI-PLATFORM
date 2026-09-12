# Closed-session delivery

The Evidence `delivery_cli` is an opt-in developer background service. It
observes successful Recorder Close receipts, never game frames or speculative
successor timing. Recorder, native causal source and the one production Mod are
unchanged. Close does not imply a complete native run or research admission.

## Fixed collection tool

Build once from an exact clean Platform commit:

```bash
npm --prefix components/annotator run publish:collection-tool -- --output /absolute/new-tool-release
```

Publication runs the portable .NET Annotator Tool build (no game files), copies
its complete dependency output and the Platform BOM, and writes
`collection-tool.json`. `release_id` hashes canonical identity plus the exact
file inventory. The developer combination pins that ID independently; trusting
an arbitrary adjacent manifest is not authentication. The release is not a
signature or an assertion that the recorded game used the tool's source.
Recording identity remains the raw manifest; packing-tool identity is separate.

The installed release verifies every file before execution and no longer asks
Git about a developer's working tree. The legacy developer `pack-session` CLI
retains its clean-source check. Neither route weakens current causal audit,
close-seal, canonical, lineage or bundle verification. The .NET runtime is still
required on developer hosts; this is not a self-contained consumer installer.

## Configuration and entry point

Install the exact Evidence package and provide a private JSON config:

```json
{
  "schema": "sts2.evidence/delivery-config-1",
  "recordings_root": "/persistent/recordings",
  "outbox_root": "/persistent/delivery/campaign-a",
  "tool_directory": "/persistent/tools/pinned-release",
  "tool_release_id": "<64 lowercase hexadecimal characters>",
  "worker_id": "developer-a",
  "campaign_id": "campaign-a",
  "human_origin_attested": true,
  "hub_url": "https://hub.example.invalid",
  "allowed_upload_hosts": ["storage.example.invalid"],
  "poll_seconds": 5
}
```

`human_origin_attested` must reflect the actual operator's explicit consent and
attestation for this fixed campaign. It is never inferred from a successful
audit. The Hub token is read only from `STPD_HUB_TOKEN`, not the config or logs.
The token is a transport-edge credential, not STPD research policy.

```bash
python -m sts2_platform_evidence.delivery_cli doctor --config /absolute/delivery.json
python -m sts2_platform_evidence.delivery_cli run --config /absolute/delivery.json
python -m sts2_platform_evidence.delivery_cli status --config /absolute/delivery.json
```

`run --once` performs one discovery cycle and at most ten eligible attempts.
Continuous mode defaults to five seconds between cycles. SIGTERM/SIGINT stops
after the current attempt. HTTP timeouts bound socket inactivity, not total
wall-clock upload duration; a supervising workbench can terminate its own child
and restart from durable state. The collection subprocess has a 600-second
hard timeout. No end-to-end network latency SLO is claimed. The project entry point can manage this
process; it does not need to keep the game alive. An OS lifetime lock prevents
a second background worker, including when its previous parent was killed; it
does not guess ownership from a PID. Local SQLite serializes one
writer with a process-owned transaction, so no expired wall-clock lease can
overlap two delivery attempts. Read-only status uses WAL. A restarted process
rediscovers closed sessions and reuses identical published bytes.

One outbox binds worker/campaign/attestation/tool identity. To change any of
those, create a new outbox and retain the original; do not mutate old receipts.
The outbox must be outside the raw recording root. Nothing deletes local raw
evidence or completed bundles automatically.

## Durable states and incidents

`pending` includes locally enrolled, packed and network-retry work. `verified`
and `quarantined` require matching terminal server receipts. `incident` records
a local seal/source/tool/bundle/protocol failure; it is not automatically retried
under a new interpretation. Status reports attempts and last error independently
from decision dispositions. A network retry re-delivers evidence, never an
unknown gameplay action.

The receiver's explicit `verification_pending` response keeps the outbox `pending`
with no error. Its `content_id` identifies the locally verified bundle; only a
matching terminal receipt can make it `verified` or `quarantined`. Normal receipt
polls still count as attempts and use the same bounded backoff. A real socket
timeout remains a `TimeoutError` diagnostic. Older logs that used `TimeoutError`
for both cases are ambiguous and remain unchanged; they cannot establish a
network failure without additional evidence.

Discovery requires `close_schema_version=1` and matching
`session-close-receipt.json` (`session-close-1`). An absent seal remains
`unsealed` in discovery counts; it is never silently promoted by elapsed time.
Malformed seals become incidents. Legacy and unsealed sessions require explicit
manual archival/diagnostic handling. Full source inventories are captured at
enrollment and checked before and after packing. Post-seal byte changes stop
delivery. Zero-canonical failed-only sessions remain eligible for valid bundle
transport, with their exact failure evidence intact.

SQLite, archives, metadata and receipts belong in a private persistent user
directory. They contain gameplay provenance and may contain local diagnostic
paths; they must not enter Git or public logs. Re-auditing an incident with new
tooling uses a new outbox and produces additional evidence, not a rewritten
original bundle. Automatic incident uploading of corrupt/unsealed raw data is
not provided by the verified-bundle route.

## HTTP receiver protocol

The edge adapter uses the coordinated Hub v1 wire namespace (`stpd/*`); the
protocol carries evidence logistics, not legality, dataset eligibility or
training policy:

1. `POST /v1/uploads`: `schema=stpd/upload-intent-v1`, complete
   `transfer_manifest` (`sts2.evidence/directory-transfer-1`), `archive_sha256`,
   `archive_bytes`, and optional `delivery_metadata` (tool and source provenance).
2. The response supplies `upload_id`, `upload_method=PUT`, `upload_url` and
   `upload_headers`, or the existing terminal receipt for identical content.
3. PUT a persistent deterministic tar.gz containing exactly the manifest's
   files. The archive hash is transport identity, not bundle content ID. No Hub
   bearer is sent to storage. Only explicitly configured HTTPS hosts are allowed;
   loopback HTTP requires the explicit test-only option. Redirects are rejected.
4. `POST /v1/uploads/{id}/complete` starts/queries independent verification.
5. `GET /v1/uploads/{id}` recovers after restart. `verification_pending` is not
   success. A terminal receipt contains `schema=stpd/receive-receipt-v1`,
   `receipt_id`, `status=verified|quarantined`, `content_id`,
   `manifest_sha256`, and findings. Both identities must match the local transfer.

A receiver `transfer_failed` status stops automatic attempts and requires explicit
operator recovery; it is neither a verified nor quarantined semantic receipt.

The server must key retries by content ID and transfer-manifest hash, retain
verified immutable objects, reject content collisions, and independently verify
staged bytes before receipt. Expired presigned URLs are refreshed by repeating
the same intent, reusing the exact archive. Upload, structural verification,
Human origin, Full-Run qualification and STPD admission remain separate facts.

## Qualification

Portable tests cover release tamper/extra dependency detection, changed source,
wrong/missing seals, failed-only evidence, offline restart, crash after receive,
concurrent writers, receiver identity mismatch, pending HTTP recovery, exact
archive membership and credential/host boundaries. They do not establish a
real cloud account, real R2/Hub deployment, GPU work, or a new Human delivery
canary. Those belong to the coordinated project's exact runtime gates.

## Terminal preflight and campaign isolation

`doctor` returns the versioned `delivery-doctor-1` report and blocks on invalid config,
missing credential, unusable collection-tool bytes/runtime, conflicting outbox identity or
invalid local paths. It executes the pinned tool's read-only identity command to prove the
actual .NET load. It never sends network requests, initializes/enrolls an outbox or changes
its logical records. SQLite read-only access may create WAL/SHM reader-coordination files;
it does not ignore an existing WAL. `status` likewise does not initialize a missing outbox.

Inspect the reported discovered session count before starting delivery. All sealed sessions
under the selected root are eligible for discovery: use an initially empty dedicated campaign
recording root and separate new outbox for first Human upload. Do not infer consent from
timestamps or assume only the next Close will upload. Configure the Mod's `recording_root`
through its supported config/environment before cold load. Deployment preserves valid
operator recording/status paths and rejects malformed existing configuration. Unset process
environment overrides when expecting the config file to apply.

The fixed tool release is built once by the operator; the exact Evidence package is supplied
by the consuming workbench lock. A local `publish:collection-tool` output is not a hosted
release or self-contained player installer. Distribution instructions are coordinated in
the consumer's versioned terminal handoff; Platform remains the owner of preflight and
recording/tool contracts. A PASS here is local readiness, not Human attestation or cloud IAM.
