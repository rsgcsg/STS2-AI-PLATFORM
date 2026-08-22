# Compatibility

Compatibility is exact and fail-closed. Matching the display version alone is
insufficient.

## STPD v0 Operational Tuple

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
| Managed patch | `ed9248b76d3e7b7b793250d0e5fe16753c654faf5d267756df50b5d1890341c2` |
| Managed Host | SHA `a884b1048b5334f6b008dbfc543789cf7662f35fd7f33e01acb1b83f313b3d66`, MVID `5b6adbd6-1e21-4148-a6f1-a6510a534838` |
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
commands. A successful fixture or probe does not update this table. Windows,
Linux, macOS x86_64, additional Mods and later game builds remain pending exact
runtime evidence.

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
