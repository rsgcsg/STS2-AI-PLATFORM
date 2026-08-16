# Installation, Update And Rollback

## Release Installation

Ordinary users install the tagged Host archive, not a branch or source build.
Download these assets from the same GitHub release:

- `STS2-Connector-<version>-host.tar.gz`
- `checksums.sha256`

Verify the archive against `checksums.sha256`, extract it, fully close STS2,
then run from the extracted directory:

```bash
node tools/install-release.mjs
```

Use `--game-dir "<path>"` only when Steam discovery cannot find the game. The
installer copies only `payload/STS2_MCP.dll` and
`payload/STS2_MCP.json`, records a timestamped backup outside the game, and
prints its exact rollback path. It does not start the game or claim the Host is
loaded.

Cold-start STS2 through Steam, then verify source revision, protocol, artifact
SHA, MVID and runtime identity against the release payload:

```bash
node tools/verify-release.mjs
```

The release is supported only when the matching runtime-seal asset exists on
the GitHub release and the verifier reports `ok: true`.

## Release Rollback

Fully close STS2 and use the exact backup printed by the installer:

```bash
node tools/install-release.mjs --rollback "<backup directory>"
```

Cold-start the game after rollback. The tool restores only the prior Connector
DLL and manifest; it never changes the Steam game, saves, other Mods or Agent
data.

## Source Deployment

The stable release also supports verified source deployment for contributors.

1. Install prerequisites and run `npm run bootstrap`.
2. Run `npm run doctor` and review the discovered game directory.
3. Fully close Slay the Spire 2.
4. Run `npm run deploy`.
5. Cold-start the game through Steam.
6. Run `npm run verify:loaded`.

`deploy` runs tests, builds the Host, records source digest/revision and
artifact SHA/MVID, backs up the prior installed artifact, installs the new DLL
and manifest, then verifies disk bytes. It does not claim the process loaded
the artifact.

The installed names `STS2_MCP.dll`, `STS2_MCP.json` and `STS2_MCP.conf` are the
stable major-1 Mod implementation identity. They do not mean MCP is required.

For supervised multi-process experiments, the Host accepts a process-local
`STS2_CONNECTOR_PORT` environment variable. It takes precedence over the shared
config file and must be an integer from 1 through 65535; an invalid value stops
the Connector listener instead of silently falling back. The supervisor must
still prove that each endpoint was clear before launch and bind the endpoint to
the loaded runtime instance in its evidence. This transport setting grants no
gameplay authority.

A Host supervisor may also inject the process-local
`STS2_CONNECTOR_HOST_CONTROL_TOKEN` used by the default-disabled Host shutdown
and provenance routes. The value must be exactly 64 lowercase hexadecimal
characters.
It must never be written to logs, capabilities, evidence, shared configuration,
the SDK or MCP. The route additionally requires the current runtime instance
ID and is not part of the Player Environment gameplay contract.

A Headless supervisor may additionally set `STS2_CONNECTOR_RUN_SEED` for one
process. It must canonicalize to 1-64 ASCII letters or digits. Connector applies
it only on the headless standard-run Embark path, after exact owner/control
revalidation and immediately before the native click. It does not become a
BoundAction operand or fair-player observation. The protected provenance route
is the only Connector endpoint that reports configured and game-observed seed
agreement to the Host supervisor.

## Development Deployment

For a deliberate local experiment with uncommitted Host changes:

```bash
npm run dev-deploy
```

The recorded provenance remains `dirty`. Never treat such an install as a
release, supported runtime or transferable evidence.

## Loaded Verification

```bash
npm run verify:loaded
```

Verification requires source/build/installed/loaded protocol, SHA-256, MVID and
source revision agreement. It also reports runtime, exact game and Modset
identity. A matching loaded artifact is still not an ordinary Live journey.

## Source Rollback

The deploy output includes `rollback_backup`. Fully close the game, then run:

```bash
npm run rollback -- --backup "<exact backup directory>"
```

Cold-start STS2 and verify the restored process. Rollback only restores the
known Host DLL/manifest/provenance in that backup; it never rewrites game files,
saves, other Mods or consumer data.

Source rollback uses repository-local deployment backups. Release and source
backups are intentionally separate and must not be interchanged.
