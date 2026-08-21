# Managed Host Performance Route Selection

Date: `2026-08-21`

Verdict: **Managed Exact v2 is the preferred H1.0 route, but only as a narrow
allocation, projection, and lifecycle implementation improvement. Performance
alone does not admit it.** The shipped runtime remains the Reference Host.

## Exact Scope

All final benchmark jobs were run serially from clean Headless source; only
the workers inside a named capacity job ran concurrently. The current
confirmation source was `2e03445c79c094990e2fb38ab735a74e363ec0fa` on an
Apple M4 with 4 performance and 6 efficiency cores and 16 GiB RAM. Its parent
`9f7ffddb4d4450089378b14a097c7086077addc6` contains the benchmark code; the
delta is documentation only. macOS did not provide a supported
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

The confirmation window also used shipped Reference Connector source
`99f09a771a66436eb47a27cf351570185a4641a1`, artifact SHA-256
`99d5df96b000dca362dea751664fa5f175839acd352678bf3eb606b6c078aef0`, MVID
`e7b2be84-f9a2-4906-b24f-30a28d25d80d`, and protocol `1.0.0`. That exact
artifact was loaded by each Reference process; installed or built identity was
not substituted for runtime identity.

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

Three fresh five-episode windows used independent seed prefixes. The
un-serialized `D_engine` loop delivered 775, 1,257 and 809 decisions:

| Metric | Result |
|---|---:|
| hot-loop `D_engine` | `345.88 d/s` mean (`334.22`-`356.51`) |
| reset-inclusive `D_engine` | `295.76 d/s` mean |
| CPU-normalized `D_engine` | `485.01 d/CPU-s` mean |
| native Commit, settling and Host lifecycle | `2.53 ms/decision` |
| decision detection and raw projection | `0.36 ms/decision` |
| managed allocation | `710,159 bytes/decision` mean |
| reset | `92.83 ms/episode` mean |

Serializing one same-seed raw-decision window cost `0.085 ms/decision` and
about `8.5 KiB/decision`. Its hot-loop rate was lower, but the native stage
also moved, so the whole throughput delta cannot be assigned to JSON. JSON is
not the single-environment ceiling.

The three fresh `D_qual` windows measured `183.25 d/s` mean
(`155.41`-`210.31`). The paired `D_train` profile measured `208.87 d/s` mean
(`179.24`-`243.27`), a `14.0%` uplift. Its native/transport stage remained
`3.88 ms/action`; training projection cost only `0.062 ms/snapshot`, versus
`0.257 ms/snapshot` under qualification. Absolute rates differ from the
earlier windows because trajectories differ; neither set supersedes the other
as a workload-independent constant.

`D_train` action latency averaged `1.10 ms` p50, `9.11 ms` p95 and `55.51 ms` p99.
The source contains bounded one-millisecond pumps around native
`ActionExecutor`, reward, operation, cleanup and run-location transitions.
The stable p99 is consistent with those lifecycle seams and allocation/GC, but
this experiment did not isolate one exact cause for every 55 ms sample.

## Reversible Ablations

The current same-seed ablation used `H1ABLATECURR`; small differences remain
noise-sensitive:

| Change | Throughput effect | Decision |
|---|---:|---|
| resource sampler off | `+0.1%` | keep sampler for qualification; noise-level cost |
| crypto IDs to sequence IDs | about `+1.7%` | keep sequence IDs in training profile only |
| eager Reads off | about `+3.2%` | keep lazy Reads in training; do not remove Read capability |
| canonical evidence off | about `+3.8%` | keep off in training, on in qualification |
| quiet diagnostics | `-0.3%` | no performance claim; keep only to bound logs |
| every-step SDK validation off | about `+1.7%` | validate at boundaries in training, every step in qualification |
| raw JSON serialization shadow | `0.085 ms/decision` serialization cost | no transport rewrite justified by speed alone |
| independent Node supervisor per worker | slower or equal, much more RSS | reject as default topology |

No wrapper ablation produced a large speedup. The highest-value remaining
Managed Exact work is allocation reduction and exact lifecycle measurement,
not a new wire or a sharded Node architecture.

## Machine Capacity

The final shared-supervisor training profile used five episodes per worker and
the same exact artifact:

| Workers | Aggregate d/s | d/CPU-s | Measured CPU cores | .NET final RSS | Node final RSS |
|---:|---:|---:|---:|---:|---:|
| 1 | 248.54 | 341.74 | 0.73 | 147 MiB | 128 MiB |
| 2 | 510.13 | 393.57 | 1.30 | 295 MiB | 150 MiB |
| 4 | 982.92 | 379.42 | 2.59 | 589 MiB | 156 MiB |
| 6 | 1,372.98 | 340.07 | 4.04 | 885 MiB | 184 MiB |
| 8 | 1,686.88 | 289.54 | 5.83 | 1,190 MiB | 192 MiB |
| 10 | 1,920.06 | 263.95 | 7.27 | 1,485 MiB | 208 MiB |
| 12 | 2,220.06 | 282.42 | 7.86 | 1,788 MiB | 392 MiB |
| 16 | 2,360.57 | 250.74 | 9.41 | 2,409 MiB | 468 MiB |
| 20 | 2,429.97 | 243.18 | 9.99 | 2,389 MiB | 418 MiB |
| 24 | 2,460.62 | 240.36 | 10.24 | 3,013 MiB | 524 MiB |

RSS columns are summed final samples, not comparable peak-memory claims; their
non-monotonic values must not be read as improved density.

At 24 workers p50/p95/p99 action latency rose to `3.67/15.68/62.74 ms`.
The machine is saturated near 20-24 environments. One shared Node supervisor
is not a serialization bottleneck: a supervisor per worker reached only
`1,835.42 d/s` at 10 workers, `4.4%` below the same-seed shared result, while
also multiplying Node memory.

`2,461 d/s` is Host-exclusive capacity, not a training envelope. Reserving
cores and memory for a learner makes the measured 6-8 worker range, about
`1,373-1,687 d/s`, the honest planning envelope until a real learner benchmark
exists.

A second seed family peaked materially lower even with the same artifact and
profile. Throughput is policy/trajectory/workload dependent; capacity curves
must not be joined across seed families or presented as a universal Host rate.

## Reference And Differential Check

The shipped Reference Host measured `0.482`, `0.976`, and `1.868 d/s` at one,
two, and four workers. All three bounded groups passed identity, provenance,
delivery, successor and process-containment integrity. One worker used about
`0.36` CPU core and `1.04 GiB` summed peak RSS; four workers used about one CPU
core and `2.32 GiB` summed peak RSS. The low CPU utilization confirms that
frame/lifecycle waiting, not compute saturation, defines this route.

A fresh same-seed CrossHost run selected and delivered the same 12 action
labels in both Hosts, with no unknown delivery. The comparator still rejected
semantic parity at event 1: the managed playable-card referent exposed
`definition_id` and `hand_index`, while Reference canonicalization retained
only player-visible identity/name/targets. This is a projection/conformance
contract mismatch, not measured gameplay divergence, and parity remains
unproven. Removing fields merely to make the comparator green is not an
acceptable fix without first deciding the canonical duplicate-card semantics.

## 5k And 10k

- This M4 cannot reach `5k` or `10k d/s` with the measured current method.
  They require `2.03x` and `4.06x` the observed machine plateau.
- The measured `485 d/CPU-s` engine result gives an ideal ten-core arithmetic
  ceiling near `4.85k d/s`; lifecycle waits, mixed P/E cores and contention make
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
| Shipped Reference | complete shipped game, SceneTree and Connector | `0.48 d/s` at 1 worker; `1.87 d/s` at 4 | existing | truth and transfer authority; not bulk trainer |
| Managed Exact current | exact assembly owns rules/RNG/effects/Commit; narrow absent-UI seams | `346 d/s` hot single env; `2.46k d/s` machine plateau | existing experimental candidate | preferred semantic base, but partial and unqualified |
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
cross-Host semantic parity currently fails at a representation boundary;
broad interaction coverage, 1M+ reliability, a Python consumer, real learning,
Reference evaluation and changed-build requalification are absent.

No Q values, rewards, tensors, masks or STPD-specific commands belong in
Headless or Connector. A Python training adapter should derive those from the
Player Environment. The CrossHost mismatch opens one generic canonical
projection question, but it does not establish a wire, legality, execution or
gameplay defect; Connector source was therefore not changed in this study.

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
