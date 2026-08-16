# Development

## Start Here

Read [Architecture](ARCHITECTURE.md), [Compatibility](COMPATIBILITY.md), and
[Evidence](EVIDENCE.md), then run:

```bash
npm ci
npm run check
npm run doctor
```

Public CI is game-independent and must not require proprietary files. Exact
game tests are local runtime evidence and must record source, game, Connector,
Modset, process, profile, and verdict identities separately.

## Ownership

- `src/game-installation.mjs`: Steam discovery and disk identity;
- `src/compatibility.mjs`: exact normal-start support tuple;
- `src/headless-host.mjs`: long-lived lifecycle and process ownership;
- `src/project-identity.mjs`: Headless revision and deterministic source digest;
- `src/runtime-probe.mjs`: H0/H1 harness;
- `src/journey-probe.mjs`: bounded H2 test consumer;
- `src/profile-template.mjs`: exact-runtime template capture and generation reset;
- `src/episode-provenance.mjs`: requested/canonical/actual game-owned seed gate;
- `src/filesystem-sentinel.mjs`: privacy-bounded local profile tree mutation gate;
- `src/capacity-benchmark.mjs`: concurrent normalized-decision/resource runs;
- `src/recovery-drill.mjs`: fault, restart, identity and cleanup verdicts;
- `src/semantic-differential.mjs`: same-artifact canonical comparison and first divergence;
- `src/json-line-process.mjs`: single-flight managed candidate transport and diagnostics;
- `src/managed-candidate.mjs`: exact upstream preparation, audit and raw experimental probes;
- `src/managed-player-environment.mjs`: strict partial canonical projection,
  Host-local bindings, stale/idempotency ledger and unknown-no-retry shell;
- `src/managed-player-environment-probe.mjs`: strategy-free measurement harness
  plus a small deterministic test consumer;
- `src/managed-native-binding-gates.mjs`: privileged exact-object negative and
  delivery gates, never fair-player journey evidence;
- `src/soak-supervisor.mjs`: repeated bounded worker supervision;
- `src/requalification.mjs`: exact identity drift and required-gate planning;
- `src/runtime-diagnostics.mjs`: diagnostics separate from semantic integrity;
- `src/process-faults.mjs`: bounded OS process suspend/resume primitives used
  only by Host lifecycle fault injection;
- `src/connector-release.mjs`: pinned release setup/rollback integration;
- `tools/headless.mjs`: CLI transport only.
- `tools/managed-exact.mjs`: managed experiment CLI only.

Do not put gameplay strategy, native operands, card/event rules, reward shaping,
or simulator behavior in these modules. Connector remains the sole gameplay
contract and execution authority.

## Change Rules

- Add game-independent unit tests for parsers, gates, verdicts, and lifecycle
  safety.
- Bind any runtime claim to an exact artifact and game tuple.
- A new game version starts as explicit experimental evidence and remains
  unsupported until reviewed gates pass.
- Any stub, patch, reflection seam, or scheduler change must name the exact
  blocker, changed behavior, drift risk, differential test, and removal rule.
- Never commit `.local`, saves, logs, game files, credentials, or decompiled
  sources.
- A local Connector RC or replaced SDK must be recorded as development evidence.
  Never release from a lock file that resolves to different protocol bytes than
  the runtime-tested SDK.
- Same-artifact repeatability is a canonicalizer baseline. Cross-Host parity
  requires an independently identified candidate and may not inherit it.
