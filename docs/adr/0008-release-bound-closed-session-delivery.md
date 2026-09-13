# ADR-0008: Release-bound closed-session delivery

Status: Accepted

Date: 2026-09-12

## Problem and exact grounding

Developer `pack-session` requires the current Annotator checkout to be clean,
even though a closed recording has an independent loaded runtime identity.
There is no durable Close-to-upload handoff when the game exits. Removing the
Git check would allow arbitrary current tooling to be presented as a fixed
producer; adding upload to the game thread would join unrelated authorities.

## Decision

Publish a portable collection-tool release from exact clean source, including
the .NET Tool/dependencies and Platform BOM. Consumers pin its content identity
outside the release directory and verify every byte before invoking it. The
existing development pack command retains its original contract.

Evidence owns an opt-in external persistent outbox. It reconciles successful
Close seals, records immutable raw inventories and fixed campaign attestation,
calls the release-bound producer audit/packer, independently verifies bundle3,
and delivers the immutable archive through a bounded HTTP edge adapter. Only a
matching terminal receiver receipt finishes delivery. A malformed/unsealed
recording is retained as incident evidence, never promoted by a timer.

## Owning fact and authority

Recorder alone owns Close and causal facts. Annotator Tool owns its exact causal
audit and pack format. Evidence owns immutable transport and receipts. The
external Hub owns operational authorization and STPD owns research admission;
neither gains native gameplay authority. One STS2 Mod remains unchanged.

## Alternatives considered

- Removing the dirty-source gate: rejected; source identity becomes unbound.
- Packing/uploading in Recorder or after a fixed delay: rejected; game-thread
  work and time would replace independent close evidence.
- Inferring Human origin from the bundle audit: rejected; only owner attestation
  authorizes that statement.
- Sharing outbox leases across machines: deferred; one local SQLite transaction
  serializes one writer, and cloud content identity handles repeated delivery.

## Evidence and falsification

Portable regressions exercise tampered tool dependencies, exact/missing seals,
source mutation, valid failed-only bundles, offline restart, crash after remote
receive, duplicate/concurrent attempts and HTTP host/credential boundaries.
Real storage, GPU and a newly observed Human Close-to-cloud journey require the
coordinated project's external gates and are not inferred from these tests.

## Consequences and tradeoffs

The service may reconcile closed files periodically; that is transport discovery
and cannot supply game state or successor proof. SQLite uses one writer and WAL
readers, bounding local concurrency without stale leases. Source bytes are
checked at enrollment and delivery rather than continuously rehashed during
gameplay. Archive bytes and upload identity persist across retries. Developer
hosts still require the supported .NET runtime; this is not a consumer installer.

## Compatibility and migration

Evidence adds public delivery APIs at 0.1.0-rc.3. Current bundle3 bytes and all
native/compatibility/archival readers retain their meanings. Automatic discovery
requires close capability 1; older sessions keep their explicit manual route.
The HTTP adapter's coordinated Hub v1 namespace is an edge wire contract, not a
Platform dependency on STPD research code.

## Rollback

Stop the external delivery process and retain its private outbox, raw sessions,
bundles and receipts. Continue manual version-pinned audit/transfer. No game Mod
rollback is needed for this source change. Tool or campaign changes use a new
outbox instead of rewriting previous provenance.

## Non-goals

No native gameplay changes, legality reconstruction, causal backfill, research
projection, automatic training admission, GPU provisioning or Human origin
proof. No automatic raw incident upload, local deletion or credential storage.
