# Pre-Full-Run Deferred Debt

This register is the explicit handoff from the bounded 2026-09-01 hardening
pass. An item may move into a later PR only when its trigger is observed. None
authorizes weakening STS2 authority, causal evidence, exact identity, or
fail-closed behavior.

| Item | Owner | Current evidence and reason to defer | Trigger | Latest point |
|---|---|---|---|---|
| Ordinary-combat discriminator compatibility capture | Annotator + Native Foundation | Exact PR #6 session has 104 full discriminator captures at distinct execution boundaries (93 before execution, 11 resume). They support legacy strict-V2 accounting; Map already delegates when the canonical boundary owns the sample. No duplicate same-boundary capture was proved. | A modern ordinary-combat replacement proves equivalent strict accounting without the full compatibility capture, or a same-boundary duplicate is demonstrated by exact trace identity. | Before final Full-Run delivery if the compatibility path is still active. |
| Snapshot hot-path cost | Connector | PR #9 Human profile: read-rich capture count 127, mean 21.465 ms, p95 30.387 ms; discriminator Snapshot count 96, mean 19.472 ms, p95 27.363 ms; semantic capture count 35, mean 19.779 ms, p95 21.999 ms. Idle/status full capture occurred only twice (mean 19.782 ms). A process sample during repeated Snapshot requests showed main-thread SHA-256/pread work; source inspection confirmed repeated game-assembly hashing in `ReadGame()`. The performance branch caches only the loaded game assembly SHA/MVID and adds capture subphase telemetry. Power and gameplay conditions were not controlled, so neither the old nor new numbers claim a Recorder-on improvement. | Controlled same-scenario Recorder OFF/ON profile on the new exact candidate crosses the agreed frame budget or an owner-attested canary reproduces noticeable recording-on stalls. | Human canary before calling this debt solved; review after each new Full-Run domain batch. |
| RecorderRuntime decomposition | Annotator | The runtime remains highly concentrated, but the current evidence codec removes the highest-risk durable mapping duplication without changing lifecycle semantics. A mechanical split would invalidate tested bytes for code-shape benefit alone. | A new domain requires another independent lifecycle/evidence state machine or a change cannot be tested without broad RecorderRuntime setup. | Before the second post-PR6 domain batch. |
| Native UI patch organization | Game Mod + Connector | Typed surface composition now has one registration point, while STS2-specific Harmony publishers remain intentionally typed. No shared gameplay state machine is justified. | A new domain needs duplicate patch lifecycle/identity code rather than only a typed publisher/adapter. | During the first Shop/Event/Rest batch that triggers it. |
| Session-local evidence storage | Annotator + Evidence | Latest session has 334 files and about 10.5 MiB allocated on disk. It already deduplicates repeated read blobs within the session (409 references, 100 blobs). Serialization/hash work is materially smaller than Snapshot capture. Cross-session CAS or a new format lacks current runtime need. | Long-run evidence volume breaches an agreed retention/transfer budget or exact profiling shows storage work on the gameplay-critical path. | Before long-soak or corpus-scale qualification, not before the next bounded domain slice. |
| Legacy/modern accounting presentation | Annotator Audit | The modern semantic timeline is sole modern authority. The discriminator and strict-V2 ledger remain non-authorizing compatibility evidence, but older docs and reports can make that distinction hard to read. | A report consumer conflates compatibility evidence with modern canonical promotion, or ordinary combat migrates fully to the modern timeline. | With the next audit/report schema revision. |

The exact performance source is Human session
`session-20260901T085236Z-b17e8578c5ff4b7db6bfa598b59493d6` on artifact
`734098f8458e7369b4e1eb6013b7516fa0c5dc126621aad11109196dd3a8bf2f`, MVID
`889b7a2e-2eaf-47e5-9383-7ddc406eb9b7`, runtime
`97943a5ec5164da389d244a279bb4ab7`. It does not transfer Human or
performance qualification to later bytes.

Operating policy: after every newly completed Full-Run domain batch, rerun the
recording profile. If controlled Recorder-on performance exceeds the agreed
frame budget, or a Human canary again reports obvious recording-on stalls,
reopen performance work before accumulating more domain scope. No numeric
frame budget has been set in the current repository; until the owner sets one,
use the measured Recorder OFF/ON delta together with Human hitch evidence.
