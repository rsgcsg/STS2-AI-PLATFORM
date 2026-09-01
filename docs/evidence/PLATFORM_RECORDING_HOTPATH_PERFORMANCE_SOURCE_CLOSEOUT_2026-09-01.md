# Platform Recording Hot-Path Performance Source Closeout

Date: 2026-09-01
Base: `develop@2e1a5b67eef25faa897602d237a16b6698127af0`
Implementation source: `4b4e1f7e7bad7108f73863262364b8e385fc8412`
Evidence level: source, tests, exact build, installed and loaded for the predecessor
performance candidate; the Close-boundary follow-up below is source/test/exact-build
only and requires a fresh Human session for runtime qualification.

## PR #10 Close-boundary follow-up

The Recorder Close path now treats an accepted user Close as the recording
session's terminal boundary. It performs no semantic drain and no five-second
deadline wait. Any still-open pending native root is invalidated with the
explicit reason `session_closed_before_successor_boundary`; the semantic
timeline emits `transition_unknown` with `no_semantic_successor` and no
`SemanticSuccessor`. Existing root, lifecycle, Commit and invalidation evidence
remain durable, while generation reset and subscription disposal invalidate late
native completions. Clean sessions still flush and close normally, and rapid
overlap remains fail-closed.

This is a runtime behavior change, so the predecessor Human evidence does not
qualify these bytes. No controlled Recorder OFF/ON performance comparison is
claimed, and no user-visible stutter elimination claim is made.

The clean exact-game build from PR #10 source `6432b8bf5bd8226916eb5b32dac7bd23136e983e`
produced Annotator artifact `754c7cf6edc094993f740b86a92b8b8e225222786054f9d311e318d025475ad7 /
182bb6a3-2ab5-46ff-a696-8035b5984b02` and unified Game Mod artifact
`8c08d4ad48a7facdfcf22ded8d5fa4c15a33cc6b1f53f7940958a4008500d8d9 /
562eff75-18c2-4e07-9099-9ac52722ae43`. The build used game `v0.111.0 /
41cef1ea`, STS2 MVID `57785517-0b16-42b9-8b36-bad6fb28384b`, and clean
source provenance. It was not installed or Human-exercised in this follow-up.

## Proven trigger and bounded change

The exact PR #9 Human profile recorded full native witness capture in the
Recorder hot path. Read-rich capture was 127 calls (mean 21.465 ms, p95
30.387 ms); discriminator Snapshot capture was 96 calls (mean 19.472 ms, p95
27.363 ms); semantic capture was 35 calls (mean 19.779 ms, p95 21.999 ms).
The idle/status path performed only two full captures (mean 19.782 ms), so it
is not the measured dominant source. A process sample during repeated Snapshot
requests showed SHA-256 and `pread` activity. Source inspection found that
`EnvironmentIdentityRuntime.ReadGame()` rehashed the immutable loaded game
assembly on every call, including repeated Snapshot captures.

The bounded repair uses a process-local `Lazy` value for only the loaded game
assembly SHA-256 and MVID. Native state, Modset identity, surface discovery,
Reads, BoundActions, exact references, action legality and every causal capture
remain live and are recaptured at their existing observation boundaries. No
TTL/time cache, polling, queue-idle rule, later-state backfill or semantic
authority move was introduced.

The native witness capture now records per-call-site subphases (identity,
native surface/state, persistent visible state, Reads, visibility, bindings,
referents, projection, signature, exact references and controller status) into
the existing performance profile. This is diagnostic telemetry only and is not
Human evidence.

## Exact candidate

- source SHA: `4b4e1f7e7bad7108f73863262364b8e385fc8412`;
- Connector artifact: `cfa97f46ad20b9405e51f21c74acfb1262282f4be449dcecca678d2c16bf92cb / 3d529368-6dc0-47af-9595-8e01f50162a0`;
- unified Platform artifact: `111bcba1e99cf68dfa8c7bd51d0f3365341280051b988a885084329e37a8c0bd / 7187768f-40e8-4036-a5f3-35379e11d32e`;
- game: `v0.111.0 / 41cef1ea`, STS2 MVID `57785517-0b16-42b9-8b36-bad6fb28384b`;
- loaded runtime instance: `dd4e2229f7ea400b9ac2e0d7cd67352e`;
- loaded environment fingerprint: `6b7ccbf23f1c6630b8bc07978c23069b76f949fcaacf4b6fa5c78a41d32475ec`;
- exact sole-Platform Modset fingerprint: `05df53c2c25398109a947303e1d9ea207fe6c91741f822cb42b943a19bb26a1b`;
- rollback: `apps/game-mod/.local/deployments/2026-09-01T09-40-24.818Z`.

The candidate passes the repository portable and exact-game checks, clean
build, safe deployment and cold-load identity verification. A 120-request
loaded Snapshot probe measured 28.222 ms mean, 45.056 ms p95 and 51.560 ms
max; this is an automated transport/runtime probe under different game state,
not a controlled OFF/ON or Human-visible comparison. It must not be described
as an improvement.

## Required next gate

Run one exact-candidate Human canary with the same ordinary supported scenario
first with Recorder OFF and then ON (or a paired controlled run), while keeping
the capture profile active for the ON leg. Preserve the generated
`performance-profile.json` and compare OFF/ON frame/hitch behavior, full
capture counts and the new subphase table. The canary must also confirm the
existing semantic/evidence invariants: strict V2 and modern calibration remain
passing, no unresolved or close-drain timeout appears, and no duplicate or
cross-Human proof is introduced. Until that owner-attested canary exists, this
PR is exact loaded but not Human-qualified and the user-visible stutter is not
declared solved.
