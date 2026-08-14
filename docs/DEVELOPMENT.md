# Development

## Prerequisites

- Node.js 20 or newer and npm;
- .NET 9 SDK;
- Git;
- a Steam installation of Slay the Spire 2 for Host tests/builds;
- Python 3.11+ only for the optional MCP adapter check.

```bash
npm run bootstrap
npm run doctor
```

Steam discovery reads installed Steam libraries. Set `STS2_GAME_DIR` only when
discovery cannot find the exact installation.

## Validation

Repository checks that do not load the game:

```bash
npm run check
git diff --check
```

Exact-game Host tests plus SDK/transport/docs checks:

```bash
npm test
```

Build a Release Host and SDK:

```bash
npm run build
```

The canonical build clears the release output, disables incremental C#
compilation and uses deterministic CI compilation with a fixed virtual source
path. Before publishing, repeat the build from an independent clean checkout
and compare the Host DLL/PDB SHA-256 and MVID. A clean Git status alone does
not prove that an incremental artifact is reproducible.

`npm run deploy` is the release-grade clean-source path. `npm run dev-deploy`
is reserved for explicitly dirty local development and records that fact; its
artifact cannot support a release or transferable support claim.

## Change Discipline

1. Read the current protocol and coverage.
2. Put the behavior in its owning layer.
3. Add the smallest test that demonstrates the contract and its fail-closed
   boundary.
4. Update the machine contract and both decoders when the wire changes.
5. Run repository and exact-game checks.
6. Treat a new DLL as unproven until installed, cold-loaded and identified.
7. Add Live evidence only to the exact artifact/runtime/game/Modset exercised.

The public contract is not generated from current C# class shape. The machine
inventory, Host records and strict SDK schemas are deliberately checked against
each other. A decoder may be stricter than an untrusted payload; it may not
silently accept a field the Host does not own.

## Repository Hygiene

Do not commit game DLLs, installed Mods, `.local/`, build output, run data,
credentials, local config, provider output or raw private logs. Public fixtures
must contain synthetic fair-player data only.

The exact-game Host test suite references the local game assembly but does not
copy it. Public CI therefore runs strategy-free SDK, protocol, boundary, docs,
CLI and Python checks; exact-game tests are a required local/release gate.
