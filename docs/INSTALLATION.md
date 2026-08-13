# Installation, Update And Rollback

## Source Deployment

This release candidate currently supports verified source deployment.

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

## Rollback

The deploy output includes `rollback_backup`. Fully close the game, then run:

```bash
npm run rollback -- --backup "<exact backup directory>"
```

Cold-start STS2 and verify the restored process. Rollback only restores the
known Host DLL/manifest/provenance in that backup; it never rewrites game files,
saves, other Mods or consumer data.

## Binary Releases

Ordinary users should consume a tagged release artifact, not a development
branch. The first public binary release remains blocked on the standalone
artifact runtime seal documented in [Status](STATUS.md).
