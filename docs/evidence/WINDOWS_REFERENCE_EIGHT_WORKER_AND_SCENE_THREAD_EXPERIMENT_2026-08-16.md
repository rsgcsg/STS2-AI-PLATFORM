# Windows 8-Worker And Scene-Thread Experiment

Date: 2026-08-16

Verdict: the current shipped Reference Host remains semantically useful but is
not an admitted eight-worker capacity baseline. Godot
`--single-threaded-scene` is rejected as a route improvement.

## Exact Scope

- Headless source before the measurement correction: `f5863d41...`;
- Headless source for the repeated default and candidate experiment:
  `a6150f5...`;
- Connector source `3e5c5a8...`, protocol `1.0-rc.2`;
- Connector DLL SHA `e9673497...`, MVID `c5bcd426...`;
- Windows x64 STS2 `v0.111.0`, commit `41cef1ea`;
- `sts2.dll` SHA `0861bfa1...`, MVID `73b63ee0...`;
- exact Connector-only Modset fingerprint `d62c684a...`.

All raw reports remain local under `.local/evidence`. These results are
experimental Windows evidence, not release support or qualification.

## Default Route

Two current-artifact eight-worker windows are relevant:

| Local report suffix | Decisions | Aggregate d/s | CPU cores | Summed peak RSS | Admission |
|---|---:|---:|---:|---:|---|
| `07-15-14-079Z` | 64 | 2.3207 | 2.469 | 5.64 GiB | rejected |
| `07-24-04-461Z` | 64 | 2.2989 | 2.482 | 5.73 GiB | rejected |

The first window exposed one worker with an unclassified RID/shader chain:
wrong/uninitialized RID, null memory, and null shader, plus one known invalid
task diagnostic. The second did not repeat that chain, but two workers each
emitted two null-texture diagnostics, exceeding the observed count bound of
one. Every worker still passed exact identity, episode provenance, semantic
journey integrity and decision-window resource measurement.

The failures are therefore Host lifecycle/diagnostic admission failures, not
Connector delivery failures and not policy failures. Intermittence is evidence
against silently expanding the signature allowlist.

## Measurement Correction

The capacity group previously treated any resource-sampler error as an
incomplete measurement but did not include shutdown containment in its status.
Source `a6150f5...` corrected both boundaries:

- sampler failures now carry monotonic timestamps;
- only failures inside the semantic decision window invalidate that window;
- all sampler failures remain in the report;
- capacity `measured` now requires measured worker windows, exact comparable
  identity, seed provenance, journey integrity and bounded containment;
- the effective Host launch configuration is recorded and compared.

This changes evidence admission only. It does not hide runtime diagnostics or
alter STS2 execution.

## Single-Threaded Scene Candidate

Godot documents `--single-threaded-scene` as disabling SceneTree sub-thread
groups. Two exact-artifact eight-worker windows tested it:

| Local report suffix | Decisions | Aggregate d/s | CPU cores | Summed peak RSS | Admission |
|---|---:|---:|---:|---:|---|
| `07-25-41-109Z` | 64 | 2.3038 | 2.409 | 5.71 GiB | measured |
| `07-27-38-482Z` | 64 | 2.3660 | 2.353 | 5.62 GiB | rejected |

The rejected window had four `Invalid Task ID` diagnostics in one worker,
exceeding the current three-occurrence bound. A same-seed canonical comparison
between one default and one candidate worker matched ten semantic events, but
that bounded comparison is not broad semantic qualification.

The candidate produced no material throughput gain and no repeatable
reliability gain. It was removed from the production CLI rather than retained
as an unproven configuration seam.

## Source Basis And Limits

Official Godot 4.5.1 source identifies the observed messages as RID ownership,
dummy renderer shader lookup, and WorkerThreadPool task-ID failures. It does
not prove the custom MegaDot fork is byte-identical or identify the STS2 caller:

- [Godot RID owner](https://github.com/godotengine/godot/blob/4.5.1-stable/core/templates/rid_owner.h)
- [Godot dummy material storage](https://github.com/godotengine/godot/blob/4.5.1-stable/servers/rendering/dummy/storage/material_storage.cpp)
- [Godot worker thread pool](https://github.com/godotengine/godot/blob/4.5.1-stable/core/object/worker_thread_pool.cpp)
- [Godot command-line options](https://docs.godotengine.org/en/4.5/tutorials/editor/command_line_tutorial.html)

The exact STS2 decompile confirms substantial async/UI/gameplay use but cannot
recover proprietary native MegaDot engine source. Root cause remains an engine
or game-native lifecycle issue until a more precise runtime trace proves
otherwise.

## Decision

- retain shipped Godot as highest-confidence semantic Reference Host;
- retain strict fail-closed lifecycle admission;
- retain 1/2/4 current-artifact capacity baselines;
- do not promote current 8-worker numbers as admitted capacity;
- reject `--single-threaded-scene` as an H* route;
- do not expand diagnostic count/signature bounds from this evidence;
- next investigate a materially different Host/runtime seam rather than tune
  an option that did not change the bottleneck.
