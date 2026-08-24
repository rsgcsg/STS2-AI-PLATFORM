# Windows Reference Capacity, Recovery And Managed Admission

Date: 2026-08-16

Verdict: shipped Godot is the current Reference Host, not a primary trainer.
H1.0 Core remains incomplete and Training Ready is false.

## Exact Runtime

- Windows x64 STS2 `v0.111.0`, commit `41cef1ea`;
- executable SHA `8602c26bffd2937e3841835fd8360ef8e974624a543e05977229fd3d062be231`;
- `sts2.dll` SHA `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`;
- runtime assembly hash `222455745`;
- exact decompile generated 3,538 C# files for local source audit only.

This tuple remains `known_experimental`. No proprietary source or binary is
committed.

## Capacity

Connector source `b9df6c1...` and its exact Modset measured 1/2/4/8 workers at
`0.4966`, `0.9483`, `1.7388`, and `2.9085` aggregate normalized semantic
decisions/s. Eight workers averaged `2.710` CPU cores and `5.696 GiB` summed
peak RSS. Every bounded worker window passed semantic integrity.

The result is artifact-specific. It rejects shipped Godot as the current
primary trainer and does not qualify another backend.

## Reset And Recovery

Template `vanilla-clean` has payload SHA
`c44a5bb775e650c88e4150dd0a73fe530b6a522df70c1508023505204677b863`.
It contains only reviewed native settings/profile/progress material and excludes
runtime telemetry.

On Connector source `08a59904533c777be71e93bd030ea3bca88cbc82`, protocol
`1.0-rc.2`, DLL SHA
`97727e82b820274df987de4cb8748a6d6ad7b9eaf08d309b16f5afbb3b01c18f`,
and MVID `7a6992a7-acfb-4690-96f2-5e1e20f7559d`, one current drill proved:

- fault after one delivered normalized decision;
- a different template generation for restart;
- a different recovered runtime instance;
- unchanged exact game, Connector and Modset identity;
- three delivered recovery decisions with integrity pass;
- no remaining game process and released Connector endpoint.

Status was `recovery_operational_pass_shutdown_diagnostics_observed`.

## Shutdown Finding

The runtime-bound Host route invoked STS2 `NGame.Quit()`, returned HTTP 200,
exited with code 0, required no forced fallback, and released the process. A
zero-action main-menu control still emitted about 1090 Godot renderer/node
teardown errors. This disproves the earlier hypothesis that only an in-run quit
caused the diagnostics. Operational recovery and clean shutdown are separate
gates.

## Managed Candidate

The pinned `wuhao21/sts2-cli` revision was `d11aa883...` and targeted older
`v0.106.1` content. A local, ignored exact-build spike repaired only current API
and early-bootstrap compile/runtime blockers. It reached a decision state but
reported missing Godot profile support, unsupported CoreCLR task patches,
localization failure and save failure. Its `RunSimulator` is about 3,916 lines
and the source contains extensive reflection, patch and manual mutation seams.

Therefore this revision is rejected as the primary trainer. The spike is not
committed, not parity evidence, and not a rejection of every possible managed
Host.

## Evidence Limits

- Local raw reports and logs remain under `.local/evidence`.
- Capacity evidence belongs to `b9df6c1...`; recovery evidence belongs to
  `08a5990...`; neither transfers across artifacts.
- No seed/replay differential, long soak, 1M reset, learning smoke, policy
  transfer, Windows support or general release is claimed.
- The current RC Host/SDK is not yet a reproducibly published dependency for a
  clean Headless clone.
