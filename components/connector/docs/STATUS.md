# Current Status

Platform migration note: this document retains exact predecessor release and
runtime evidence. Current Platform Connector `1.2.0-rc.5` is a source candidate
with no inherited build, loaded, Live or qualification claim. Root
`../../../docs/STATUS.md` is authoritative for the consolidated source state.

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

## Windows Current-Source Candidate

The exact clean `main` source
`c9d7af518a7c84b81ab0db9e2eea5cf0cce13cbe` also has a separate named Windows
x64 candidate admission. Two primary-checkout builds reproduced Host DLL
`2050ae23610fd2c719efa319eefea4837e5c4aebcfdc6c2502bebe0a6f6aeaa3`
and MVID `64066c98-c97d-4c82-a01f-6c9a902ec974`. The exact artifact cold-loaded,
passed H0 and H1 control checks, and reached a fresh-profile bounded H2 coverage
verdict with 18 deliveries and zero unknown, read, successor, or provenance
failures. A prior reused-profile `h2_incomplete` result is retained as a real
precondition failure.

This evidence is candidate-only. The prior installed Host DLL was restored
after collection; no loaded-runtime claim is made for that restored disk state.

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

## Unreleased Observer Candidate

Source `9a929cc...` and observer source `bc9c568...` were built, installed and
cold-loaded together. One native-human ordinary-combat run produced 60 complete
records: 51 card plays, including 30 targeted plays, and 9 end turns. Every
record mapped exact-unique, reached an interactive successor, exported, and
passed strict STPD import/B0. The same run exposed 22 accepted native inputs
that the old Host missed because it treated any non-empty action queue as
settling. That is a Connector completeness defect, not authority refusal or
Recorder rejection.

Current source aligns combat publication with the shipped hand/control gates,
excludes card holders already moved to the native play queue, and keeps exact
execute-time revalidation. Automated tests pass, but this changed source is not
covered by the predecessor run: build, install, cold-load and a new rapid-input
native-human run remain pending exact-runtime evidence. See the
[queue-authority audit](evidence/NATIVE_HUMAN_RECORDER_QUEUE_AUTHORITY_AUDIT_2026-08-23.md).

See [Coverage](player-environment/COVERAGE.md),
[Support](SUPPORT.md), and the
[STPD operational runtime seal](evidence/STPD_OPERATIONAL_RUNTIME_SEAL_2026-08-22.md),
plus the
[Windows current-source runtime admission](evidence/WINDOWS_CURRENT_SOURCE_RUNTIME_ADMISSION_2026-08-22.md).
