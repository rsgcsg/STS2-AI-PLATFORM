# Operations

## Install

```bash
npm ci
npm run doctor
npm run setup
```

`setup` installs only Connector Host `1.0.1` from its tagged GitHub Release and
verifies archive SHA-256 `cdb33f2e...`. If an older/different Mod is installed,
the Connector release installer creates a timestamped rollback directory. The
result is also stored locally in `.local/last-connector-install.json`.

Installation is not loading. Fully exit and restart the game process before
using a newly installed artifact.

## Start And Stop

Shared-profile operation can alter the active Steam profile. Accept that risk
explicitly:

```bash
npm start -- --shared-profile
```

The development-only isolated Windows workflow is deliberately staged so the
game, not this repository, creates the save schema:

```bash
node tools/headless.mjs reset-profile --isolated-profile h1-train
npm run bootstrap:profile -- --isolated-profile h1-train
node tools/headless.mjs enable-profile-mods --isolated-profile h1-train --settings-schema 8 --accept-ea-disclaimer
npm start -- --isolated-profile h1-train --experimental-build
```

Bootstrap records exact game/profile evidence and requires a native positive
settings schema plus a Steam-disabled runtime log. The following consent step
fails closed on schema drift and atomically backs up `settings.save`; it does
not pre-answer tutorial or gameplay choices.

`start` runs in the foreground. It prints readiness only after exact-build,
headless-host, Connector, Modset, execution, and interactive-snapshot gates
pass. Runtime records and logs are local under `.local/runtime/`.

From another terminal:

```bash
npm run status
npm run stop
```

`stop` signals only the PID whose command still matches the recorded exact
executable and `--headless`; it refuses an ambiguous/reused PID. Current
development Hosts first request the secret, runtime-bound native shutdown route
and record whether a forced fallback was needed. The secret is process-local and
must never be written to evidence.

## Probes

```bash
npm run probe:shipped -- --shared-profile
npm run probe:menu-control -- --shared-profile
npm run probe:journey -- --shared-profile
npm run bench:capacity -- --template vanilla-clean --workers 1,2,4,8
npm run probe:recovery -- --template vanilla-clean --experimental-build
npm run probe:recovery -- --template vanilla-clean --fault-mode process_hang --experimental-build
npm run probe:differential -- --template vanilla-clean --seed H1D1FF01 --experimental-build
npm run soak:reference -- --template vanilla-clean --workers 2 --episodes 2 --actions 8 --experimental-build
npm run drill:update
```

- shipped: boot, loaded identity, headless display, interactive Snapshot;
- menu-control: delivery, duplicate idempotency, stale refusal, successor;
- journey: representative menu/non-combat/combat surfaces and advertised Reads.
- capacity: concurrent isolated profiles, process-local endpoints, normalized
  decisions, CPU/RSS and exact identity consistency;
- recovery: injected process crash, new profile generation, distinct recovered
  runtime, exact identity, process/endpoint release and shutdown diagnostics;
- hang recovery: suspend the exact child PID only after a stable successor and
  requested seed provenance, prove the process remains while its endpoint
  times out, then replace the process and profile generation. This Windows
  fault primitive is experimental and creates no gameplay authority.
- differential: two independent same-seed runtime/profile generations,
  canonical Snapshot/Read/selected-action comparison and first divergence;
- soak: repeated bounded workers, unique runtimes/generations, endpoint/process
  release and unchanged normal user-data tree;
- update drill: exact identity admission and a fail-closed requalification plan.

`drill:update` intentionally exits `8` for the current Windows experimental
tuple and for any unknown tuple. A generated list of gates is not proof those
gates passed.

Capture a template only from a reviewed native isolated profile, then instantiate
workers from it:

```bash
node tools/headless.mjs capture-profile-template --isolated-profile h1-train --template vanilla-clean
node tools/headless.mjs instantiate-profile-template --template vanilla-clean --isolated-profile worker-01
```

Template capture excludes runtime logs and telemetry. Instantiation re-hashes
the payload and refuses a different exact game identity before deleting the
selected worker namespace.

For an unknown build, only maintainers collecting non-support evidence should
append `--experimental-build`. Normal start still refuses that build.

## Rollback Connector

Use the exact `rollback_backup` returned by setup:

```bash
npm run rollback -- --backup "<backup directory>"
```

Rollback requires all STS2 processes to be closed. Cold-start afterward and
verify loaded identity before making a runtime claim.

## Troubleshooting

- `Unsupported STS2 runtime`: inspect `npm run doctor`; do not edit hashes to
  force admission.
- `existing STS2 process`: fully exit game/Steam-launched instances, then retry.
- `endpoint already owned`: use `npm run status`; never attach to an ambiguous
  process.
- `Connector endpoint did not become ready`: check local session logs and exact
  Connector installation, then rerun setup if necessary.
- `profile acknowledgement required`: choose one explicit mode; the isolated
  route is experimental and the shared route requires `--shared-profile`.
- `unknown` delivery: stop the consumer and inspect evidence. Never retry the
  mutation.
- `recovery_operational_pass_shutdown_diagnostics_observed`: reset/restart and
  cleanup gates passed, but native shipped-headless teardown was not clean. Do
  not relabel this as clean shutdown or broad soak qualification.
- `shutdown_containment_rejected`: at least one native shutdown, exit-code,
  forced-termination, diagnostic signature, lifecycle phase or count gate did
  not match. Stop and inspect the exact report; do not broaden the signature or
  count merely to make the run pass.
- `bounded_containment_candidate`: the run used native shutdown, exited zero
  without force and contained only exact phase/count-bounded diagnostics. It is
  still `not_qualified` until the long-soak gate is satisfied.
- `npm ls` reports an invalid local Connector SDK: the development RC SDK was
  linked over the public dependency. This is acceptable only for explicitly
  attributed local evidence; run no release from that tree.
