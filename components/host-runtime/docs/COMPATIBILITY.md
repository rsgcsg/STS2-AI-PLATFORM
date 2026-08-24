# Compatibility

Compatibility is exact and fail-closed. Matching the display version alone is
insufficient.

## STPD v0.1 Operational Patch Tuple

| Field | Exact value |
|---|---|
| Platform | macOS arm64 |
| STS2 | `v0.111.0`, commit `41cef1ea` |
| Executable SHA-256 | `ec8c10831dbb424c45859907f5ef6a7711f7a6e9a02f386ad13922ba8a7fcbe7` |
| `sts2.dll` SHA-256 | `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4` |
| `sts2.dll` MVID | `57785517-0b16-42b9-8b36-bad6fb28384b` |
| Runtime assembly hash | `1010476334` |
| `GodotSharp.dll` SHA-256 | `0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289` |
| Managed upstream | `d11aa883b582dd68bd39b331f3370746b30d447e` |
| Managed patch | `8ced088bffbfeaa378d4580ca2c254c41cd89ae58b5b1e9b6ee1a764f8eef87e` |
| Managed Host | SHA `8dc622b0c003dc632a753e0bb524a82690290930dfe9573a4c71cb8234a6d8a6`, MVID `7228541c-d4f4-4033-9ff5-30f4c9997e98` |
| Connector Reference Host | `v1.1.0-rc.1`, source `e0651024117d22bdeb95142766917103d87c0185` |
| Connector artifact | SHA `c1877f1af1b311904b0d536fdfc08cd5c425281f4cc93eed2ff11729380c7586`, MVID `64765ea1-29fe-4475-9b7d-3b0d65955825` |
| Player Environment | protocol `1.0.0`, SDK `1.0.0` |

The Managed Host does not load the Connector Mod; it independently implements
the same Connector-owned contract and keeps exact native operands in-process.
The shipped Reference route cold-loads the exact Connector artifact with only
`STS2_MCP` in the Modset. The RC requires its explicit exact process-local
canary and is not universal release support.

`release_info.json` declares main-assembly hash `1172974615`; the selected
arm64 assembly reports runtime hash `1010476334`. Both are recorded, while
the selected bytes and runtime hash are admission authority.

## Fail-Closed Changes

Any mismatch in platform, executable, game assembly, GodotSharp, Managed
upstream/patch/artifact, Connector source/artifact/protocol, or Modset refuses
normal baseline admission. `npm run doctor`, the Managed audit, Connector
capabilities and the runtime seal expose the specific identity.

Unknown builds may be investigated only through explicitly experimental
commands. A successful fixture or probe does not update the operational tuple
above. Linux, macOS x86_64, additional Mods and later game builds remain
pending exact runtime evidence.

## Windows x64 Managed Candidate Tuple

This second tuple is admitted only to the experimental Managed route and has
separate provenance. It is not a supported operational tuple and inherits no
macOS qualification.

| Field | Exact candidate value |
|---|---|
| Platform | Windows x64 |
| STS2 | `v0.111.0`, commit `41cef1ea` |
| Executable SHA-256 | `8602c26bffd2937e3841835fd8360ef8e974624a543e05977229fd3d062be231` |
| `sts2.dll` SHA-256 | `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9` |
| Runtime assembly hash | `222455745` |
| `GodotSharp.dll` SHA-256 | `0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289` |
| Managed upstream | `d11aa883b582dd68bd39b331f3370746b30d447e` |
| Managed patch | `8ced088bffbfeaa378d4580ca2c254c41cd89ae58b5b1e9b6ee1a764f8eef87e` |
| Managed Host | SHA `0d8c916365f0a64a0ed5cfc706186811e33708c841fef82e1f73c6a33dcfcc4d`, MVID `387bc1a3-5a3f-4f76-babb-698a770cdb8b` |

Candidate admission means exact clone/patch/build/audit reproducibility only.
Player Environment, journey, recovery, soak and Cross-Host results must name
this tuple and cannot promote it into the macOS table.

## Requalification Scope

- game/assembly drift: source impact audit, build, native gates, Reference
  comparison and lifecycle smoke;
- Managed patch/artifact drift: full candidate audit plus affected semantic,
  reset/recovery and consumer gates;
- Connector artifact/protocol drift: Connector checks, install/cold-load,
  settling/action/Read/Receipt gates and a shipped Reference journey;
- orchestration-only drift: deterministic checks plus affected reset,
  identity, timeout and worker smoke.

No old runtime, artifact or release inherits authority after a changed identity.
