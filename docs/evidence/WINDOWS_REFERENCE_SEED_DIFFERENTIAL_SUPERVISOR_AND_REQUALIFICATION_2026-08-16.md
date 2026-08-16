# Windows Reference Seed, Differential, Supervisor, And Requalification Closeout

Date: 2026-08-16

## Verdict

The shipped Windows Godot route remains the highest-confidence Reference Host,
but it is neither H1.0 nor Training Ready. This slice closes game-owned seed
provenance, same-artifact repeatability measurement, bounded multi-worker
supervision, a local shared-profile mutation sentinel, fail-closed update
planning, and a phase-aware shutdown containment candidate. It does not close
long soak, clean shutdown, containment qualification, cross-Host parity, Steam
Cloud isolation, real update qualification, reproducible RC dependencies, or a
high-throughput trainer.

## Exact Runtime Scope

All runtime results below use:

- STS2 Windows x64 `v0.111.0`, commit `41cef1ea`;
- executable SHA `8602c26bffd2937e3841835fd8360ef8e974624a543e05977229fd3d062be231`;
- `sts2.dll` SHA `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`;
- runtime assembly hash `222455745`, MVID
  `73b63ee0-6c0a-47bb-b0d1-b21f6d94222e`;
- Connector source `3e5c5a8b582f5d4ae07675b490d9a019bbd4602b`;
- protocol `1.0-rc.2`, DLL SHA
  `e96734970a6bd32e112fe351316bf05b56c236f5e48044d3e4f07995defd581c`;
- Connector MVID `c5bcd426-932f-41d1-a3ae-cc0c5d0e9407`;
- exact Connector-only Modset fingerprint
  `d62c684a0e0c10eda18c3c6c0068e0e6c61a8fe4bf25e4723632d65e6022466f`.

Windows remains `known_experimental`; these identities scope evidence and do
not grant support.

## Runtime Results

### Seed And Repeatability

`reference-differential-2026-08-16T06-10-02-998Z` used seed `H1D1FF01`.
Two independent runtimes (`e729...` and `762f...`) and profile generations
produced 14 canonical semantic events each with no first divergence. Both
journeys delivered 12 actions with integrity and provenance pass.

Earlier differential failures exposed four measurement defects: runtime-local
entity IDs, equivalent duplicate cards, action publication order, and
capability declaration order. Each was corrected with a regression test. No
failed comparison was relabelled a game divergence.

This proves the current canonicalizer can compare this bounded same-artifact
case. It does not prove deterministic replay or candidate-Host parity.

### Recovery And Capacity

`recovery-2026-08-16T06-14-17-975Z` used seed `H1REC0VERY01`. The injected
crash and recovered process had distinct runtime IDs and profile generations;
the recovery delivered five decisions, preserved exact identity/provenance and
released process and endpoint. Native recovered shutdown returned zero without
force, but emitted 954 diagnostics.

`capacity-2026-08-16T06-15-44-285Z` used seed `H1CAPAC1TY01`:

| Workers | Decisions/s | Average cores | Peak RSS |
|---:|---:|---:|---:|
| 1 | 0.4981 | 0.245 | 0.711 GiB |
| 2 | 0.8975 | 0.519 | 1.423 GiB |
| 4 | 1.5246 | 0.943 | 2.857 GiB |

Every worker passed provenance/integrity. The result rejects this route as the
primary realistic trainer under the current `>=1000 d/s` hypothesis.

### Supervisor And Profile Sentinel

`reference-soak-2026-08-16T06-23-30-470Z` ran two workers for two bounded
episodes. It delivered 32 decisions through four unique runtime IDs and four
unique profile generations, with no worker failure, process leak or endpoint
leak. Aggregate rate over summed episode windows was `0.8630 d/s`.

`reference-soak-2026-08-16T06-27-27-441Z` additionally compared the normal
user-data tree before and after one isolated run. Both snapshots contained
1,051 files, 78,922,880 bytes and digest `f9e58712...`; the tree was unchanged.
This run deliberately records a dirty Headless source digest (`37a05c88...`)
because it validated the sentinel before commit. The evidence is attributable,
but it is not clean-source release evidence and says nothing about remote Steam
Cloud state.

### Shutdown Diagnostic Containment

Headless now captures stderr separately before and after its runtime-bound
native shutdown request. It accepts only exact signatures in the observed
phase and under explicit count ceilings; every unknown, wrong-phase or
over-limit line rejects the worker and therefore the soak. Native shutdown must
also be requested, return code zero and avoid forced termination.

`reference-soak-2026-08-16T06-43-34-353Z` demonstrated fail-closed behavior:
both workers delivered six decisions and released their endpoints, but one
worker emitted one pre-shutdown `Invalid Task ID`; the then-current policy did
not admit it and the soak ended `soak_incomplete`.

A local audit of 41 attributable journey reports found that exact line zero
times in 17 reports, once in 18, twice in 3 and three times in 3; no report had
more than three. The admitted ceiling is therefore exactly three, not an
unbounded wildcard. The other admitted signatures remain lifecycle-specific:
one null-texture parameter before shutdown, and at most 2,048 node-path, 16 RID
leak and one resources-in-use diagnostics after shutdown. An unhandled managed
exception is never admitted.

Godot upstream fixed two bugs that could emit the same `Invalid Task ID` text:
[PipelineHashMapRD task error spam](https://github.com/godotengine/godot/pull/104044)
and [ResourceLoader invalidated task waits](https://github.com/godotengine/godot/pull/104060).
Those primary sources support classifying the text as a plausible engine task
diagnostic. They do not independently prove the root cause in the exact MegaDot
fork, so the local exact-build bound and fail-closed behavior remain necessary.

`reference-soak-2026-08-16T06-46-23-544Z` then ran clean Headless source
`63d03ee1c4aab46672871f11c8b762bd953f3455`: two workers delivered 12
decisions through distinct runtime/profile generations, released both
endpoints and processes, and left the 1,051-file shared-profile sentinel
unchanged. Both workers emitted 988 known post-shutdown diagnostics, no unknown
or wrong-phase lines, exited zero without force, and received
`bounded_containment_candidate`. This proves the current bounded gate operated
as designed once. Its report explicitly remains `not_qualified`.

### Update Planning

`requalification-2026-08-16T06-32-06-526Z` correctly returned
`known_experimental_qualification_required` with `fail_closed` authority.
Game-independent fixtures prove assembly drift adds exact source audit and
semantic differential gates, while executable/platform drift adds Host
lifecycle qualification. No code path edits the support table or promotes a
candidate.

No second game build was available. This is an executable invalidation plan and
fixture evidence, not a real game-update drill.

## Source Findings

Exact decompilation confirms `NGame.Quit()` saves native settings/profile data,
clears text/font caches and calls `SceneTree.Quit()`. Runtime exit zero and
process/endpoint release therefore use the native path. Phase-aware containment
prevents those diagnostics from being hidden, but does not justify a clean-
shutdown claim.

The assembly also contains Mega Crit's broad `AutoSlayer`, including seed
override, native commands, UI handlers and watchdog. The same assembly
hard-codes `NGame.IsReleaseGame()` to true, making the `--autoslay` branch
unreachable in the Steam build.

An exact-fingerprint research Mod invoked the public `AutoSlayer` class after
the native main menu became visible, without patching game methods. The first
attempt (`autoslayer-upper-bound-2026-08-16T06-57-17-089Z`) started from the Mod
initializer too early and hit a `SaveManager` initialization null reference;
it failed, exited one, restored the prior Modset and changed no shared-profile
files. That failure corrected the experiment lifecycle rather than the game.

The clean-source rerun
`autoslayer-upper-bound-2026-08-16T06-58-43-714Z` used Headless
`4a0d2507a8720d26958ad59d03caa2bad21e57e1`, exact probe DLL SHA
`c570570998b9761196620b359eea24f1199884258c4034b8ba4992ff3a685a83`
and seed `H1AUTOSLAYER01`. It completed 50 room entries across three acts and
returned to the main menu in `394.5s`, then exited zero. Resource sampling
recorded `59.52` CPU seconds (`0.151` average cores) and `928,600,064` peak RSS
bytes. The single warning was the official handler reporting no room handler
for `Map`; the run still completed.

The transaction removed the Connector from the experimental Modset, loaded
only the research Mod, then restored the exact Connector-only files including
DLL SHA `e9673497...`. No STS2 process remained and the shared-profile sentinel
was unchanged. The report's `max_total_floor_observed` field is misnamed: the
source log publishes act-local floor. Current code corrects the field to
`max_act_floor_observed`; the raw historical report is preserved.

The 616 native action log entries and 50 rooms are explicitly not normalized
semantic decisions. The result says that official UI/Command automation can
complete this exact run while remaining mostly CPU-idle; it does not provide a
high-throughput Host, Connector conformance, cross-Host parity, policy quality
or qualification.

## Reproducibility Gap

The tested tree links local Connector SDK `1.0.0-rc.1`, while the public
dependency lock requires SDK `1.0.0`. `npm ls` reports this mismatch as invalid.
The development Host and SDK evidence is exact, but a clean `npm ci` does not
recreate it. H1.0 release remains blocked until one versioned Host/SDK pair is
published, pinned and rerun through the gates.

## Gate Status

- H1.0 Core: incomplete.
- Training Ready: false.
- H*: unresolved.
- Reference Host: retained.
- Primary trainer: rejected on current performance evidence.
- Managed `sts2-cli` candidate: rejected for now on exact-build bootstrap,
  patch, localization and save failures.
- Official AutoSlayer upper-bound: completed and retained only as route evidence.
- Next highest-information work: broaden Reference operational gates while
  admitting a genuinely faster candidate through normalized Connector
  conformance and differential, rather than optimizing AutoSlayer's fixed UI
  policy into a second gameplay contract.
