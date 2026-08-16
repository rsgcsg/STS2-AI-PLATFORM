# Windows Reference Seed, Differential, Supervisor, And Requalification Closeout

Date: 2026-08-16

## Verdict

The shipped Windows Godot route remains the highest-confidence Reference Host,
but it is neither H1.0 nor Training Ready. This slice closes game-owned seed
provenance, same-artifact repeatability measurement, bounded multi-worker
supervision, a local shared-profile mutation sentinel, and fail-closed update
planning. It does not close long soak, clean shutdown, cross-Host parity, Steam
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
process/endpoint release therefore use the native path, but repeated Godot
diagnostics prevent a clean-shutdown claim.

The assembly also contains Mega Crit's broad `AutoSlayer`, including seed
override, native commands, UI handlers and watchdog. The same assembly
hard-codes `NGame.IsReleaseGame()` to true, making the `--autoslay` branch
unreachable in the Steam build. It is a candidate exact-patch upper-bound
experiment, not a current Host, semantic interface or qualification source.

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
- Next highest-information Host experiment: an isolated, exact-fingerprint
  official AutoSlayer upper-bound spike, with no Connector authority claim.

Before that experiment can affect route selection, it must record a patch
manifest, separate Modset/artifact identity, normalized progress events,
resources, profile isolation and clean rollback. It may not enter production or
inherit Reference evidence.
