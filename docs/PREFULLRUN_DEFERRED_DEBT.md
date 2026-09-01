# Pre-Full-Run Deferred Debt

This register is the explicit handoff from the bounded 2026-09-01 hardening
pass. An item may move into a later PR only when its trigger is observed. None
authorizes weakening STS2 authority, causal evidence, exact identity, or
fail-closed behavior.

| Item | Owner | Current evidence and reason to defer | Trigger | Latest point |
|---|---|---|---|---|
| Ordinary-combat discriminator compatibility capture | Annotator + Native Foundation | Exact PR #6 session has 104 full discriminator captures at distinct execution boundaries (93 before execution, 11 resume). They support legacy strict-V2 accounting; Map already delegates when the canonical boundary owns the sample. No duplicate same-boundary capture was proved. | A modern ordinary-combat replacement proves equivalent strict accounting without the full compatibility capture, or a same-boundary duplicate is demonstrated by exact trace identity. | Before final Full-Run delivery if the compatibility path is still active. |
| Snapshot hot-path cost | Connector | Exact PR #6 session: read-rich capture count 136, mean 21.481 ms, p95 28.440 ms; discriminator capture count 104, mean 20.028 ms, p95 24.887 ms; semantic capture count 33, mean 22.438 ms, p95 36.440 ms. Projection is 1.079 ms mean. Power conditions were not controlled, so this is a baseline, not a regression claim. | Repeated controlled-power profile crosses the agreed frame budget or an owner-attested canary reproduces noticeable recording-on stalls. | Before performance qualification/final delivery; review after each new Full-Run domain batch. |
| RecorderRuntime decomposition | Annotator | The runtime remains highly concentrated, but the current evidence codec removes the highest-risk durable mapping duplication without changing lifecycle semantics. A mechanical split would invalidate tested bytes for code-shape benefit alone. | A new domain requires another independent lifecycle/evidence state machine or a change cannot be tested without broad RecorderRuntime setup. | Before the second post-PR6 domain batch. |
| Native UI patch organization | Game Mod + Connector | Typed surface composition now has one registration point, while STS2-specific Harmony publishers remain intentionally typed. No shared gameplay state machine is justified. | A new domain needs duplicate patch lifecycle/identity code rather than only a typed publisher/adapter. | During the first Shop/Event/Rest batch that triggers it. |
| Session-local evidence storage | Annotator + Evidence | Latest session has 334 files and about 10.5 MiB allocated on disk. It already deduplicates repeated read blobs within the session (409 references, 100 blobs). Serialization/hash work is materially smaller than Snapshot capture. Cross-session CAS or a new format lacks current runtime need. | Long-run evidence volume breaches an agreed retention/transfer budget or exact profiling shows storage work on the gameplay-critical path. | Before long-soak or corpus-scale qualification, not before the next bounded domain slice. |
| Legacy/modern accounting presentation | Annotator Audit | The modern semantic timeline is sole modern authority. The discriminator and strict-V2 ledger remain non-authorizing compatibility evidence, but older docs and reports can make that distinction hard to read. | A report consumer conflates compatibility evidence with modern canonical promotion, or ordinary combat migrates fully to the modern timeline. | With the next audit/report schema revision. |

The exact performance source is Human session
`session-20260901T061040Z-561a204be0bc422da5809e1ec5c148aa` on artifact
`2382b3dd01be009731fdfa02a5f936986487163042a7b4614cc931c3bf6a4f8e`,
MVID `b1a7d1f1-6f38-4501-a1ef-9a642d40df53`, runtime
`a00b1852fcd44c8b9c489233c78301c0`. It does not transfer Human or performance
qualification to later bytes.
