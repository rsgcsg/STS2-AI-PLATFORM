# Measurement Contract

## Normalized Semantic Decision

The unit used for Host comparison is:

```text
stable interactive Snapshot
-> select one current finite BoundAction
-> submit the exact snapshot-bound action
-> receive a delivery Receipt
-> observe the next stable Snapshot or terminal state
```

One HTTP request, poll, rendered frame, simulator microstep, or animation frame
is not a semantic decision. Host throughput claims must count the unit above.

Each recorded decision separates policy selection, submit-to-Receipt,
Receipt-to-stable-successor, and total stable-Snapshot-to-successor time.
Reports summarize observed samples without inventing interpolated samples.
Resource sampler errors are retained with monotonic timestamps. Only errors
inside the semantic decision window invalidate its resource measurement;
startup/teardown sampler races remain visible but do not erase sufficient
in-window samples. Capacity admission separately requires measured worker
windows, exact identity/provenance, journey integrity, and bounded shutdown
containment.

## Durable Trace

Journey events are appended to rotating JSONL files and flushed after every
record by default. A crash may truncate only the final in-flight record; it
must not erase the preceding trajectory. Raw traces remain local because they
may contain profile or save information.

Every executed test decision records the exact state/action identifiers,
selected action, Receipt attribution, canonical fair-player decision, stable
successor, and timing chain.

The canonical form removes runtime-local identifiers and timestamps while
retaining visible content and referent/action relationships. It is a comparison
artifact only. It never creates legality, execution authority, or an action.
Equivalent duplicate visible entities are compared as a multiset when no
player-visible fact distinguishes them; execution still uses each runtime's
exact private binding.

## Integrity Versus Coverage

Journey integrity and surface coverage are independent results:

- **integrity** checks unknown delivery, advertised Read failure, missing stable
  successor, and unsafe termination;
- **coverage** reports which named interactions and action classes were
  exercised against an explicit target.

Missing a particular event in a finite run is not automatically a Host
integrity failure. Visiting many surfaces does not excuse an unknown delivery
or missing successor.

## Promotion Use

This contract admits measurements; it does not define a performance winner.
The partial managed candidate now exceeds the old `>=1000` aggregate route
hypothesis, but that result is neither semantic admission nor real learner
throughput. Promotion also requires semantic differential, reset and recovery
evidence, resource efficiency, and a representative training workload.

## Current Reference Baselines

On Windows x64 STS2 `v0.111.0` / `41cef1ea`, current development Connector
source `3e5c5a8...`, DLL SHA `e9673497...`, MVID `c5bcd426...`, and seed
`H1CAPAC1TY01`, the isolated shipped Host measured:

| Workers | Aggregate decisions/s | Average CPU cores | Summed peak RSS |
|---:|---:|---:|---:|
| 1 | 0.4981 | 0.245 | 0.711 GiB |
| 2 | 0.8975 | 0.519 | 1.423 GiB |
| 4 | 1.5246 | 0.943 | 2.857 GiB |

All 1/2/4 workers passed seed provenance, journey integrity and lifecycle
admission for their bounded windows. Two current-artifact default 8-worker
windows delivered all semantic decisions at `2.3207` and `2.2989` decisions/s,
but failed lifecycle admission on intermittent Godot diagnostics. An official
`--single-threaded-scene` candidate measured `2.3038` and `2.3660` decisions/s,
passing containment once and failing once; it was rejected. A predecessor
Connector artifact reached `2.9085` decisions/s at eight workers with `5.696
GiB` summed peak RSS. Artifact/configuration-specific windows cannot be merged,
but all independently reject shipped Godot as the current primary trainer.

A two-worker, two-episode supervisor smoke on the current artifact delivered 32
decisions across four unique runtime instances and profile generations at
`0.8630` aggregate decisions/s over summed episode windows. It proved bounded
lifecycle repetition, not a long-soak throughput result.

## Current Managed Candidate Baseline

The exact managed candidate patch `708c51c...`, Host artifact `34aa29f...` / MVID
`ff6c7349...`, and clean Headless source `2e03445...` measured three distinct
planes on Apple M4. The benchmark implementation is its parent `9f7ffdd...`;
the source delta is documentation only:

| Plane | Meaning | Mean result |
|---|---|---:|
| `D_engine` | exact game-owned loop, no Player Environment/SDK/JSON/Node/Reads/evidence | `345.88 d/s` hot; `295.76 d/s` reset-inclusive |
| `D_train` | partial fair-player projection with training-cost settings | `208.87 d/s` single environment |
| `D_qual` | strict IDs, Reads, SDK validation, evidence and sampling | `183.25 d/s` single environment |

The shared-supervisor `D_train` profile scaled from `248.54 d/s` at one worker
to `1,686.88` at eight, `1,920.06` at ten, and a machine plateau of `2,460.62`
at 24 environments. The 24-worker window measured `10.24` aggregate CPU cores
and increased p95 latency to `15.68 ms`. A supervisor per worker did not improve
throughput and materially increased Node RSS.

The single-environment engine loop spent `2.53 ms/decision` in native Commit,
settling and Host lifecycle, `0.36 ms` in decision detection/raw projection,
and allocated about `0.71 MB/decision`. Raw JSON serialization cost about
`0.085 ms/decision`. The current bottleneck is native lifecycle plus allocation
and GC, not SDK, JSON or one Node supervisor.

These are clean-source performance and method-selection results for a partial,
unqualified projection. A fresh CrossHost run delivered the same 12 action
labels in both Hosts but failed canonical parity at a playable-card referent
representation boundary. The results are not physical-core affinity, real
learner throughput, H1.0 or Training Ready. See the
[performance route closeout](evidence/MANAGED_HOST_PERFORMANCE_ROUTE_SELECTION_2026-08-21.md).

The current performance stop rule is workload-based: once the Host is at most
about 20% of end-to-end training time and doubling Host speed improves total
wall time by at most about 10%, further Host optimization stops being P0.
