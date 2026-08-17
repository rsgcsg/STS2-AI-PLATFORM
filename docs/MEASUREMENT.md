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
The current `>=1000` aggregate decisions/s trainer target is a route hypothesis,
not a release fact. Promotion also requires semantic differential, reset and
recovery evidence, resource efficiency, and representative training workload.

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

The exact managed candidate patch `b6dc69a...` and Host artifact `2b8fa6c...`
measured the same canonical decision unit through a partial strict Connector
SDK projection on clean source `b573fed...`:

| Workers | Reset-inclusive decisions/s | Lifecycle-inclusive decisions/s | Summed peak RSS |
|---:|---:|---:|---:|
| 1 | 239.69 | 158.75 | 132.2 MiB |
| 2 | 398.12 | 265.66 | 271.3 MiB |
| 4 | 720.96 | 500.70 | 540.8 MiB |
| 8 | 956.33 | 694.51 | 1,063.2 MiB |

Each worker completed three short episodes and exited zero. A predecessor
same-artifact window measured `1,017.39`, but its adapter worktree was dirty.
The clean-source result does not cross the provisional threshold, so `>=1000`
is not repeatably established. The projection is incomplete and unqualified;
both windows are route-priority signals, not semantic or release admission.

The current performance stop rule is workload-based: once the Host is at most
about 20% of end-to-end training time and doubling Host speed improves total
wall time by at most about 10%, further Host optimization stops being P0.
