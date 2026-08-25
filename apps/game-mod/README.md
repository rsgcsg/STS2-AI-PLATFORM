# STS2 Platform Game Mod

This is the only production game-side package installed by STS2 AI Platform.
One `STS2_PLATFORM` manifest loads one assembly containing three separately
owned components:

1. Connector observes player-visible state and owns Reads/BoundActions.
2. Human Annotator witnesses native human input and writes immutable raw data.
3. Platform Live UI presents typed status and recording/runtime controls.

The explicit unified initializer starts them in that order. Packaging does not
merge their authority: the UI cannot submit gameplay actions, the Annotator
cannot authorize them, and the Connector cannot write Human evidence.

## Lifecycle

Fully close STS2 before build/deploy/rollback:

```bash
npm run game-mod:build
npm run game-mod:doctor
npm run game-mod:deploy
npm run game-mod:launch
npm run game-mod:verify-loaded
```

Deployment backs up the existing unified artifact, the predecessor three-Mod
files, and component configuration before replacement. It removes the three
predecessor manifests rather than retaining a silent fallback. Restore with:

```bash
npm run game-mod:rollback
```

After cold load, press `K` to open/close the five-page Platform UI; `Escape`
also closes it. Verification requires one exact `STS2_PLATFORM` Modset, one
common loaded SHA/MVID for Connector/Annotator/UI, component-specific embedded
source provenance, a ready UI node, and Connector execution availability.

The UI is composed from built-in Godot nodes and driven by SceneTree signals.
Do not replace it with a custom `Node` callback unless the package explicitly
adds and validates Godot's C# source-generator toolchain; standard single-DLL
Mod builds do not generate those callbacks.

`installed`, `loaded`, `K-visible`, Human action evidence and Policy evidence
are separate claims.
