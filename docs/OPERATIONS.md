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
executable and `--headless`; it refuses an ambiguous/reused PID.

## Probes

```bash
npm run probe:shipped -- --shared-profile
npm run probe:menu-control -- --shared-profile
npm run probe:journey -- --shared-profile
```

- shipped: boot, loaded identity, headless display, interactive Snapshot;
- menu-control: delivery, duplicate idempotency, stale refusal, successor;
- journey: representative menu/non-combat/combat surfaces and advertised Reads.

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
