# Platform Live UI

The Platform Live UI is the in-game presentation component for Environment,
Policy, Human Data, and Diagnostics. It starts hidden and toggles with `K`. It calls
typed Connector observation, Policy Runtime, and Annotator recording services;
it does not publish, resolve, or submit gameplay actions itself.

The Runtime loopback defaults to `http://127.0.0.1:15527`. Connector Snapshot
and Read opportunities, recording state, and loaded component identities remain
available when no policy artifact is running; only policy scores/modes/Receipts
are then unavailable.

## Ownership

This directory has no manifest, assembly packaging, deployment, loaded-identity
or rollback authority. `apps/game-mod` compiles this source into the repository's
one production `STS2_PLATFORM` Mod alongside Connector and Annotator source.
Its boundary tests remain portable:

```bash
npm run live-ui:check
```

See [`apps/game-mod/README.md`](../game-mod/README.md) for the only supported
build/install/cold-load/rollback lifecycle. Loaded UI identity is not Human or
policy-run evidence. Shadow, One-Step, and Auto remain unavailable until a
compatible Policy Runtime with an exact Policy Manifest and artifact is running.
