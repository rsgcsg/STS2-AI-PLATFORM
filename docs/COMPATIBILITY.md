# Compatibility

Compatibility is exact and fail-closed. Matching a display version alone is
not enough.

## Supported Runtime

| Field | Exact value |
|---|---|
| Platform | macOS arm64 |
| STS2 | `v0.111.0`, commit `41cef1ea` |
| Executable SHA-256 | `ec8c10831dbb424c45859907f5ef6a7711f7a6e9a02f386ad13922ba8a7fcbe7` |
| `sts2.dll` SHA-256 | `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4` |
| Runtime assembly hash | `1010476334` |
| `GodotSharp.dll` SHA-256 | `0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289` |
| Connector Host | `1.0.1`, source `154a5cdf435626f2cc84fe0412e9f9e1adbd637b` |
| Connector DLL | SHA `81f9447a122761887078a714b6c71d6a20f611bd829e8353bdb30d96f411187f`, MVID `967fd749-e3f5-4339-9db4-b19964049d5b` |
| Player Environment | protocol `1.0.0`, SDK `1.0.0` |
| Modset | exact Connector-only Modset |

`release_info.json` carries a declared main-assembly hash (`1172974615`) that
differs from the selected arm64 assembly's runtime hash. Both are recorded;
the selected assembly bytes and runtime hash are compatibility authority.

## Version Changes

`npm run doctor` records platform, architecture, release metadata, executable,
`sts2.dll`, runtime assembly hash, GodotSharp, active processes, and an exact
compatibility verdict.

Normal `npm start` refuses an unknown tuple. Probes also refuse it unless the
maintainer explicitly adds `--experimental-build`. Experimental output may be
used to audit a new version, but does not become support until source/owner
audit, automated checks, exact H0/H1/H2 runtime gates, and a reviewed support
table update are complete.

`npm run drill:update` records the current disk tuple and emits the exact gates
required by the nearest known tuple. Any mismatch keeps authority fail closed.
Assembly/version drift requires a new exact source inventory and impact review;
platform/executable/Godot drift also requires Host lifecycle qualification.
The planner never edits this support table or promotes a build.

Windows, Linux, macOS x86_64, additional Mods, and any later game build are
currently `pending exact-runtime evidence`, not implicitly supported.

## Windows Experimental Candidate

The development branch recognizes, but does not support, this exact tuple:

| Field | Exact value |
|---|---|
| Platform | Windows x64 |
| STS2 | `v0.111.0`, commit `41cef1ea` |
| Executable SHA-256 | `8602c26bffd2937e3841835fd8360ef8e974624a543e05977229fd3d062be231` |
| `sts2.dll` SHA-256 | `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9` |
| Runtime assembly hash | `222455745` |
| `GodotSharp.dll` SHA-256 | `0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289` |

`doctor` reports this tuple as `known_experimental`. Local RC Connector
artifacts, Modsets and runtime instances are always recorded separately. No
artifact or Modset inherits support from this disk identity.

The current Windows development runtime evidence uses Connector source
`3e5c5a8b582f5d4ae07675b490d9a019bbd4602b`, protocol `1.0-rc.2`, DLL SHA
`e96734970a6bd32e112fe351316bf05b56c236f5e48044d3e4f07995defd581c`,
and MVID `c5bcd426-932f-41d1-a3ae-cc0c5d0e9407`. This identifies evidence;
it does not add Windows support.
