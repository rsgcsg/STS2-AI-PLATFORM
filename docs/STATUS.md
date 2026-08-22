# Current Status

General stable release: `v1.0.1`

STPD operational baseline prerelease: `v1.1.0-rc.1`

Player Environment protocol/SDK: `1.0.0/1.0.0`

Verdict: **`v1.1.0-rc.1` is runtime-sealed for the exact STPD operational
baseline; it is not universal support or formal H1.0.**

## STPD Baseline Identity

- tag/source: `v1.1.0-rc.1/e0651024117d22bdeb95142766917103d87c0185`;
- source digest: `430c90109a521a1ef199bec0f16e7e82d30d1c1e4e686ab94bbafea6e7151183`;
- Host DLL: `c1877f1af1b311904b0d536fdfc08cd5c425281f4cc93eed2ff11729380c7586`;
- Host MVID: `64765ea1-29fe-4475-9b7d-3b0d65955825`;
- exact game: macOS arm64 `v0.111.0/41cef1ea`, assembly
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`;
- Modset: exact Connector-only;
- authority: explicit exact process-local canary.

The clean source passes 130 Host and 7 SDK tests plus contract, boundary,
compatibility, CLI, release-tool, package, docs and Python checks. The release
build reproduces the DLL/MVID above; it was installed and cold-loaded.

Exact runtime evidence proves that partially mounted card rewards remain
`settling` and publish no mutation authority even when Skip is enabled. The
first stable successor publishes all three selectable cards plus Skip, and an
independent observation reproduces the complete catalog. Two
Candidate-trained-policy shipped-Reference episodes reached terminal with 390
deliveries, exact seed provenance and zero unknown; one exact terminal outcome
matched. This is named operational evidence, not broad parity.

## Implemented Contract

- one fair-player Observe/Read/Interact path;
- visible facts and Referents independent of action publication;
- complete finite BoundActions with Host-local native operands;
- execute-time owner/target/legality revalidation;
- exact controller, stale rejection and request idempotency;
- `delivered/not_delivered/unknown` Receipt plus immediate successor;
- state-bound non-authorizing Reads;
- settling Snapshots with empty mutation authority;
- standalone build/deploy/verify/rollback and strategy-free SDK.

## General Stable Lineage

`v1.0.1` remains the general-support release. Its source/artifact and the
`v1.0.0` C1 runtime seal remain immutable predecessor authority. The STPD RC
does not silently broaden that support table; it requires two exact,
process-local canary opt-ins.

## Explicit Non-Claims

- formal H1.0 or broad CrossHost semantic equivalence;
- arbitrary versions, platforms, Modsets, cards/relics/events or native pages;
- long soak, changed-build campaign, training/search/strategy;
- hidden-state, coordinate/reflection mutation or visual computer use;
- business completion inferred from a delivery Receipt;
- transient VFX/SFX/history information closure.

See [Coverage](player-environment/COVERAGE.md),
[Support](SUPPORT.md), and the
[STPD operational runtime seal](evidence/STPD_OPERATIONAL_RUNTIME_SEAL_2026-08-22.md).
