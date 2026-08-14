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

Windows, Linux, macOS x86_64, additional Mods, and any later game build are
currently `pending exact-runtime evidence`, not implicitly supported.
