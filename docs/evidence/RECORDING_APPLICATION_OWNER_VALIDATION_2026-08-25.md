# Recording Application Owner Validation - 2026-08-25

## Exact runtime

- artifact SHA-256: `a7b11d930c0d5b2dee22ac7ce5faea7bc5db84802b5b36bfb27e8258320c9c0f`
- artifact MVID: `c3e7127a-93bf-4e29-9c05-257b5089edc6`
- Annotator source: `94ecc515b9096b16a1c73cc70844908f4a9dc773`
- runtime: `bd6b73e7c2744680848539f96b6cae6d`
- game: `v0.111.0/41cef1ea`, assembly `9cb4f1ad.../57785517...`
- Modset: `exact_platform_modset`, only `STS2_PLATFORM`

## Owner-exercised lifecycle

The owner opened the K workspace and used the Human Data controls. Session
`session-20260825T113115Z-49dd69787c904838b389d50e7f55ebd8` recorded distinct
start, run start, pause, resume, pending Close and session-closed events. Session
`session-20260825T113325Z-110f467db08e4f3aba68d036907df6da` then started in the
same process with a different timeline/store, accepted a pending Close, handled
a duplicate Close as `already_closing`, and closed. This proves the application
lifecycle and same-process session isolation for this artifact.

## Defect found

Neither session admitted a decision. Independent audit failed closed with zero
valid/invalid records because no decision file was committed. The first session
had 14 invalidations and the second had four. Accepted end turns reached a
different complete interactive successor, but final record validation still
required the predecessor `canary_exact_observer_modset` string while runtime
admission correctly used `exact_platform_modset` for the unified Mod.

That validation exception left the pending decision active. Successor Reads were
therefore persisted repeatedly until timeout: the first session reported 2,052
materialized Reads despite zero records. These Reads and both sessions are failed
diagnostic evidence, not admissible Human Evidence.

## Source repair boundary

Current source gives runtime admission and independent record validation one
shared exact-Modset predicate. It accepts only the predecessor exact observer
envelope and the unified exact Platform envelope. Unknown values remain fail
closed. A record persistence exception now produces one
`decision_persistence_unknown` invalidation, clears pending and is never retried
per frame.

The repair has automated evidence only until a new artifact is built, installed,
cold-loaded and owner-exercised. Evidence from the artifact above does not
transfer to the repaired artifact.
