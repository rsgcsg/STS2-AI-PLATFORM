# Semantic Evidence Storage Baseline

## Exact predecessor runtime

The latest protected owner recording is
`session-20260828T151112Z-a559d80cd88741738f2a902427b10140`, timeline
`timeline-1b26f97548a443bba8100d6a52cc7270`. Its records bind:

- unified artifact `b5fbda1277404e277eb8871faa4baa126fb92e324dc0dc09c26f7693e9791f02 /
  1cbcff84-1a35-4f4a-a387-dfdce601f8f1`;
- Annotator source `fba874e8d7a89b7843c82aea3cd5987bb54b41e3` and Connector source
  `4de52cfd72c6bf5b0d2312538152e81c616dabfb`;
- runtime `7cfde8d4c3084f8aa868d757b7809d0a` and environment
  `575f57f4265242e72434b05ec50dc5f89c4bcdf1e45ff02da630cdcc87de2c0e`;
- STS2 `v0.111.0 / 41cef1ea`, assembly
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4 /
  57785517-0b16-42b9-8b36-bad6fb28384b`;
- sole exact Modset
  `67f7e0179cba12c2b23342b0144685f7e37ea05d6749b34e1546fa6e1db9162a`.

Independent audit passes 77 Decision V2 records with zero invalid records,
95 explicit legacy invalidations, 5,586 materialized Reads and zero Read
failures. The semantic trace has 233 accepted roots and 233 proved
dispositions with zero unknown, cancellation, abort or unresolved root. Four
`UsePotionAction` roots exact-map an enemy-target potion, a no-target potion and
two self-target potions. Target-picker cancel remains unexercised.

This is Human runtime evidence for the predecessor schema-2 artifact only. It
does not load or validate the storage/hot-path source introduced after it.

## Measured architecture cost

The streaming analyzer reads immutable sessions without rewriting them. On the
latest session, the 30,747,054-byte semantic trace contains 1,225 events but
1,939 inline H/S/S' role occurrences. Exact-frame role references project it to
11,998,502 bytes (39.0%); gzip control falls from 1,163,609 to 618,578 bytes.
The session persisted 23.97 Reads per accepted root.

The longer predecessor session
`session-20260828T032151Z-43b2f87e65484b8abccccbba71c713c8` has a
102,455,858-byte trace, 3,215 events and 5,111 inline role occurrences. Exact
normalization projects it to 40,462,367 bytes (39.5%); gzip control falls from
3,979,269 to 1,957,911 bytes. It persisted 28.63 Reads per accepted root.

These measurements establish two separate costs:

1. repeated full frames dominate durable trace bytes;
2. repeated Read capture/persistence and per-Read coverage rewrites are hot-path
   work that compression cannot remove.

They do not measure CPU, GC or rendered frame latency and therefore do not prove
that owner-perceived lag is fixed.

## Source candidate

New source writes schema-3 ordered events with explicit
`human_observation_ref`, `execution_pre_ref`, `successor_ref` and boundary-state
references into exact canonical content-addressed frames. The auditor verifies
every object path, digest and snapshot identity, reconstructs the schema-2
causal view and applies the existing no-cross-Human/disposition invariants.
Schema-1/2 sessions remain byte-readable.

The runtime also gates expensive Read-rich captures behind cheap Snapshot-only
candidate checks, captures only interaction-required semantic Reads, batches
Read coverage updates and reuses one authoritative successor capture across the
semantic and Decision V2 projections. Snapshot-only checks never establish S
or S'.

The finalized candidate was built from workspace `750315b3...` with Annotator
source `54efe38d...`, installed and cold-loaded as unified artifact
`4fa6757045b6d5c2b137e78b1e96e7163c2a5c64372a41955682257d6a6a1056 /
51c7c37b-3305-4286-b2bc-52cd5725ac76`. Runtime `7bcc19e7...` reports exact
STS2 `v0.111.0 / 41cef1ea`, environment `15177b88...` and exact Platform
Modset `2263e395...`. Rollback is
`apps/game-mod/.local/deployments/2026-08-28T16-46-50.719Z`.

This is source/test/build/install/load evidence. Because native bytes changed,
one new exact-artifact owner canary is required before any Human semantic or
owner-perceived performance claim transfers.

That gate was subsequently completed by exact schema-3 session
`session-20260829T052157Z-e549d3601e7640f997b6f475180b2dfe`. See the
[schema-3 closeout](SCHEMA3_HUMAN_DATA_LIFECYCLE_CLOSEOUT_2026-08-29.md).
Only the later stage-profiler source still requires a new artifact for runtime
latency attribution.
