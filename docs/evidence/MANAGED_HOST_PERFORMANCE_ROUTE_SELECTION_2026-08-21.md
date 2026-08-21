# Managed Host Performance Route Selection

Date: `2026-08-21`

Verdict: **Managed Exact v2 is the preferred H1.0 route, but only as a narrow
allocation, projection, and lifecycle implementation improvement. Performance
alone does not admit it.** The shipped runtime remains the Reference Host.

## Exact Scope

All final benchmark jobs were run serially from clean Headless source; only
the workers inside a named capacity job ran concurrently. The source was
`9f7ffddb4d4450089378b14a097c7086077addc6` on an Apple M4 with 4 performance
and 6 efficiency cores and 16 GiB RAM. macOS did not provide a supported
physical-core affinity control, so this report distinguishes one environment,
CPU-normalized work, and machine aggregate; it does not claim a pinned P-core
or E-core result.

The measured candidate was:

| Identity | Value |
|---|---|
| STS2 | `v0.111.0` / `41cef1ea` / runtime hash `1010476334` |
| exact `sts2.dll` SHA-256 | `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4` |
| exact `sts2.dll` MVID | `57785517-0b16-42b9-8b36-bad6fb28384b` |
| managed upstream | `d11aa883b582dd68bd39b331f3370746b30d447e` |
| patch SHA-256 | `708c51c5282a78ab107b4c1066e508c7ce5d31be477e836892da494dc9c11cc8` |
| Host artifact SHA-256 | `34aa29f79148b4d732034cc8e89a201c21a7a66b1c921cb142b5762ddb8169ad` |
| Host artifact MVID | `ff6c7349-05b3-4c1c-b1e7-4664bcd0c931` |

Raw reports remain local under `.local/evidence/`. They are ignored because
they contain machine paths and runtime logs. This closeout records only
reviewed aggregates and non-claims.

## Measurement Planes

- `D_engine`: exact STS2 rules, RNG, effects and Commit plus managed decision
  detection. It excludes Player Environment projection, SDK validation,
  per-step JSON, Node policy, Reads and evidence.
- `D_train`: the partial fair-player Player Environment path with sequence
  identities, no eager Reads, no canonical evidence and no every-step SDK
  validation. It is representative training overhead, not qualification.
- `D_qual`: the same partial path with cryptographic identities, eager Reads,
  canonical evidence, every-step SDK validation and resource sampling.

The engine and Player Environment harnesses do not yet produce an identical
trajectory for identical seed text. Their absolute throughputs therefore
cannot be subtracted as a paired semantic differential. Stage timings and
same-harness ablations are the supported comparisons.

## Single Environment And Stage Cost

Three five-episode windows used independent seed prefixes. The un-serialized
`D_engine` loop delivered 841, 760 and 757 decisions:

| Metric | Result |
|---|---:|
| hot-loop `D_engine` | `297.66 d/s` mean (`250.56`-`330.87`) |
| reset-inclusive `D_engine` | `241.77 d/s` mean |
| CPU-normalized `D_engine` | `394.45 d/CPU-s` mean |
| native Commit, settling and Host lifecycle | `3.10 ms/decision` |
| decision detection and raw projection | `0.31 ms/decision` |
| managed allocation | `1,004,588 bytes/decision` |
| reset | `120.49 ms/episode` mean |
| GC per 1,000 decisions | `120.69` gen0, `3.82` gen1, `0.44` gen2 |

Serializing the raw decision to JSON added only `0.069 ms/decision`; the same
three trajectories retained `297.78 d/s` hot-loop throughput. JSON is not the
single-environment ceiling.

The three `D_qual` windows delivered 1,215, 734 and 1,148 decisions at `213.25
d/s` mean (`178.44`-`261.40`) and `218.42 d/CPU-s`. The paired `D_train`
profile delivered the same trajectories at `234.25 d/s` mean
(`187.49`-`295.25`) and `279.29 d/CPU-s`. `D_train` was `9.8%` faster than
`D_qual`, but its native/transport stage remained `3.87 ms/action` and its
child allocation remained `972,536 bytes/decision`.

`D_train` action latency was `1.44 ms` p50, `7.25 ms` p95 and `55.54 ms` p99.
The source contains bounded one-millisecond pumps around native
`ActionExecutor`, reward, operation, cleanup and run-location transitions.
The stable p99 is consistent with those lifecycle seams and allocation/GC, but
this experiment did not isolate one exact cause for every 55 ms sample.

## Reversible Ablations

Each percentage is a paired mean over the same three seed trajectories:

| Change | Throughput effect | Decision |
|---|---:|---|
| resource sampler off | `-0.1%` | keep sampler for qualification; noise-level cost |
| crypto IDs to sequence IDs | `+1.1%` | keep sequence IDs in training profile only |
| eager Reads off | `+2.7%` | keep lazy Reads in training; do not remove Read capability |
| canonical evidence off | `+3.9%` | keep off in training, on in qualification |
| quiet diagnostics | `-0.3%` | no performance claim; keep only to bound logs |
| every-step SDK validation off | `+1.7%` | validate at boundaries in training, every step in qualification |
| raw JSON serialization shadow | about `0%` hot loop | no transport rewrite justified by speed alone |
| independent Node supervisor per worker | slower or equal, much more RSS | reject as default topology |

No wrapper ablation produced a large speedup. The highest-value remaining
Managed Exact work is allocation reduction and exact lifecycle measurement,
not a new wire or a sharded Node architecture.

## Machine Capacity

The final shared-supervisor training profile used five episodes per worker and
the same exact artifact:

| Workers | Aggregate d/s | d/CPU-s | Measured CPU cores | .NET final RSS | Node final RSS |
|---:|---:|---:|---:|---:|---:|
| 1 | 256.32 | 377.22 | 0.68 | 145 MiB | 131 MiB |
| 2 | 506.93 | 404.74 | 1.25 | 296 MiB | 145 MiB |
| 4 | 992.41 | 391.96 | 2.53 | 581 MiB | 145 MiB |
| 6 | 1,390.72 | 350.27 | 3.97 | 887 MiB | 143 MiB |
| 8 | 1,676.64 | 302.24 | 5.55 | 1,193 MiB | 194 MiB |
| 10 | 1,865.33 | 270.51 | 6.90 | 1,083 MiB | 157 MiB |
| 12 | 2,249.88 | 283.78 | 7.93 | 1,795 MiB | 313 MiB |
| 16 | 2,353.70 | 251.88 | 9.34 | 2,404 MiB | 259 MiB |
| 20 | 2,401.21 | 243.69 | 9.85 | 2,531 MiB | 500 MiB |
| 24 | 2,451.00 | 238.40 | 10.28 | 2,722 MiB | 360 MiB |

RSS columns are summed final samples, not comparable peak-memory claims; their
non-monotonic values must not be read as improved density.

At 24 workers p50/p95/p99 action latency rose to `3.72/16.56/62.11 ms`.
The machine is saturated near 20-24 environments. One shared Node supervisor
is not a serialization bottleneck: a supervisor per worker reached only
`1,842.51 d/s` at 10 workers and consumed about `1.21 GiB` of Node RSS versus
`157 MiB` for the shared topology.

`2,451 d/s` is Host-exclusive capacity, not a training envelope. Reserving
cores and memory for a learner makes the measured 6-8 worker range, about
`1,390-1,677 d/s`, the honest planning envelope until a real learner benchmark
exists.

## 5k And 10k

- This M4 cannot reach `5k` or `10k d/s` with the measured current method.
  They require `2.04x` and `4.08x` the observed machine plateau.
- The measured `394 d/CPU-s` engine result gives an ideal ten-core arithmetic
  ceiling near `3.9k d/s`; lifecycle waits, mixed P/E cores and contention make
  that an upper bound, not a Managed v2 forecast.
- A narrow Managed Exact v2 may plausibly reach roughly `2.8k-3.5k d/s` on
  this machine if allocation and lifecycle overhead are reduced. This is an
  engineering estimate, not runtime evidence; `5k` is not promised.
- `5k` appears plausible on roughly 16-24 strong physical cores and `10k` on
  roughly 32-48 strong cores or multiple machines, assuming comparable
  workload and scaling. Neither high-core result has been measured.
- Independent Host processes make a three-node M4-class `5k` and five-node
  `10k` cluster plausible before learner contention. Cluster orchestration and
  usable-sample throughput remain unmeasured.

## Route Map

| Route | Native ownership | Evidence-backed speed | Engineering to credible prototype | H1.0 assessment |
|---|---|---|---|---|
| Shipped Reference | complete shipped game, SceneTree and Connector | `0.50 d/s` at 1 worker; about `2.3 d/s` at 8 | existing | truth and transfer authority; not bulk trainer |
| Managed Exact current | exact assembly owns rules/RNG/effects/Commit; narrow absent-UI seams | `298 d/s` hot single env; `2.45k d/s` machine plateau | existing experimental candidate | preferred semantic base, but partial and unqualified |
| Managed Exact v2 | same game ownership; in-runtime projection, allocation and lifecycle improvements | estimated `2.8k-3.5k d/s` on this M4 | about 1-2 weeks implementation, then 4-8+ weeks qualification | best route to H1.0 if changes remain narrow and differential-tested |
| Hybrid | exact game still owns gameplay, but Host persistently reconstructs or short-circuits lifecycle | no measured prototype; `2x-10x` is only a hypothesis | several weeks to months | higher parity/update risk; use only after a measured Managed bottleneck justifies it |
| Snapshotable/virtualized Host | native state is captured or virtualized for reset/branch; ownership depends on design | unmeasured; 5k/10k may be plausible | roughly 2-6 months | valuable for search/reset, but isolation and state completeness reopen major gates |
| Independent simulator | simulator owns gameplay rules/RNG/rewards | unmeasured here; 10k+ is plausible in principle | roughly 6-18+ months | different method with highest transfer and maintenance risk; not fastest path to Reference-qualified H1.0 |

The engineering times are estimates, not commitments. Managed Exact ends while
the exact game assembly still owns rules, RNG, effects and Commit and changes
remain projection, allocation, transport or orchestration. Persistent Host
reconstruction of task/UI lifecycle is Hybrid. Reimplementing card, relic,
power, monster, reward or RNG gameplay rules is Simulator.

## H1.0 And STPD v0

Managed Exact current has the speed and process isolation shape needed for an
STPD v0 collector: stable combat state, finite actions, exact execution,
successor, seed provenance, reset and multi-worker collection are implemented
in the experimental path. It is not yet an admitted training backend because
cross-Host semantic parity, broad interaction coverage, 1M+ reliability, a
Python consumer, real learning, Reference evaluation and changed-build
requalification are absent.

No Q values, rewards, tensors, masks or STPD-specific commands belong in
Headless or Connector. A Python training adapter should derive those from the
Player Environment. No generic Connector defect was established by this
performance work, so Connector source was not changed.

## Keep, Reject, And Reopen

Keep:

- shipped Reference as truth and transfer gate;
- exact managed assembly and game-owned Commit boundary;
- shared Node supervisor;
- distinct training and qualification profiles;
- stage, parent/child CPU, allocation, GC, RSS and latency instrumentation;
- lazy Reads and boundary validation in training, with strict qualification
  retained separately.

Reject or stop:

- sharded Node supervisors as the default topology;
- JSON/transport rewrite as a P0 performance project;
- using `>=1000 d/s` or a fast benchmark as H1.0 admission;
- promising 5k/10k from linear extrapolation;
- calling lifecycle reconstruction Managed Exact after the method boundary has
  been crossed.

Reopen the route choice only with evidence of a Managed parity blocker, a
measured allocation/lifecycle prototype, real learner resource contention, or
a workload proving that the current `1.4k-1.7k d/s` training envelope is
insufficient.

## Non-Claims

- No pinned physical P-core/E-core result was obtained.
- `D_engine` is not a canonical Player Environment decision and cannot be used
  as qualification throughput.
- The partial managed projection is not cross-Host qualified or complete.
- No Hybrid, snapshotable Host, simulator, high-core machine, cluster, Python
  learner, learning curve, 1M soak or Reference transfer was measured.
- The route estimates do not transfer to another game build or artifact.
