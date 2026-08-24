# Windows Current-Source Runtime Admission — 2026-08-22

## Verdict

The exact clean `main` source at
`c9d7af518a7c84b81ab0db9e2eea5cf0cce13cbe` was built twice in the primary
Windows checkout, deployed while the game was closed, cold-loaded through the
shipped Godot headless route, and exercised through H0, the H1 control probe,
and one fresh-profile bounded H2 journey.

This is a named Windows x64 candidate admission. It does not extend the macOS
stable or STPD operational seals, does not qualify formal H1.0, and does not
grant general Windows support.

## Exact Identity

- source revision:
  `c9d7af518a7c84b81ab0db9e2eea5cf0cce13cbe`;
- Player Environment source digest:
  `430c90109a521a1ef199bec0f16e7e82d30d1c1e4e686ab94bbafea6e7151183`;
- Host DLL SHA-256:
  `2050ae23610fd2c719efa319eefea4837e5c4aebcfdc6c2502bebe0a6f6aeaa3`;
- Host MVID: `64066c98-c97d-4c82-a01f-6c9a902ec974`;
- protocol: `1.0.0`;
- game: Windows x64 `v0.111.0/41cef1ea`, runtime main-assembly hash
  `222455745`;
- game `sts2.dll` SHA-256:
  `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`;
- Modset: exact Connector-only;
- authority: explicit exact process-local game/source/artifact canary.

Two consecutive clean builds in the primary checkout produced the same DLL
SHA-256 and MVID above. The full source check passed 130 Host tests, 7 SDK
tests, and all contract, boundary, compatibility, CLI, release-tool, package,
documentation, and Python checks.

## Cold-Load And Control Evidence

The deployed artifact cold-loaded with its exact source, artifact, MVID, game,
and Modset identity. The H0 report is retained locally at:

`STS2-headless/.local/evidence/shipped-h0-2026-08-22T08-21-56-255Z/report.json`

The independent menu-control probe passed H0 and its H1 control checks,
including delivered mutation, duplicate-request idempotency, stale refusal,
and immediate successor observation. Its report is retained at:

`STS2-headless/.local/evidence/shipped-h0-2026-08-22T08-22-46-176Z/report.json`

Both processes used native Host shutdown, exited with code zero, and required
no forced termination.

## Bounded Journey Evidence

The first bounded journey deliberately remains retained at:

`STS2-headless/.local/evidence/bounded-journey-2026-08-22T08-23-29-768Z/report.json`

It reused a progressed profile and ended `h2_incomplete`: 12 actions were
delivered with zero unknown deliveries, read failures, or successor failures,
but the episode provenance was incomplete and the run began beyond the menu
and map coverage preconditions. The failure was not relabelled or discarded.

A newly instantiated isolated profile then produced:

`STS2-headless/.local/evidence/bounded-journey-2026-08-22T08-24-32-921Z/report.json`

That report records `h2_pass`, `integrity_pass`, and `coverage_reached` for
seed `STPDC0NNECT0R01`: 18 delivered actions, exact seed provenance, main-menu,
run-entry, map, non-combat, and combat coverage, 13 combat deliveries, zero
unknown deliveries, zero read failures, zero successor failures, and native
exit code zero without forced termination.

The report also retains known phase-scoped Godot diagnostics. They were not
used to widen the verdict.

## Restoration

After the candidate evidence was collected, the pre-deployment Host DLL was
restored from the exact rollback bundle. Its SHA-256 is
`c1877f1af1b311904b0d536fdfc08cd5c425281f4cc93eed2ff11729380c7586`.
The game was not running during restoration. The previously existing stale
installed-provenance sidecar remains visible to `doctor`; restoration is a disk
identity fact, not a loaded-runtime claim.

## Non-Claims

- no formal H1.0, broad CrossHost parity, or general Windows support;
- no arbitrary-version, arbitrary-Modset, full-run, long-soak, or capacity
  qualification;
- no claim that a delivery Receipt proves downstream business completion;
- no claim that the restored Host DLL has been cold-loaded in this evidence
  pass.
