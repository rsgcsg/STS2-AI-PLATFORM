# Platform Live UI

The Platform Live UI is the in-game presentation component for Environment,
Policy, Human Data, and Diagnostics. It is fully hidden during ordinary play;
`K` opens the one Live Workspace and `Esc` closes it. There is no compact HUD
or root-level Recorder surface while the Workspace is closed. The Recorder tool region is mounted on that same Workspace surface,
never as a second legacy shell, and exposes only the typed New/Pause/Resume/
Close application commands. The Recorder also contains a bounded Recent
Actions feed and Last Action detail sourced only from the typed canonical
recording event projection; missing card, target or effect metadata is shown
as unavailable. The UI
calls typed Connector observation, Policy Runtime, and Annotator recording
services; it does not publish, resolve, or submit gameplay actions itself.

The Runtime loopback defaults to `http://127.0.0.1:15527`. Connector Snapshot
and Read opportunities, recording state, and loaded component identities remain
available when no policy artifact is running; only policy scores/modes/Receipts
are then unavailable.

Workspace and Recorder layout (position, size, collapse state, and selected
page) are versioned presentation-only state under the Windows local application
data directory. Persistence is fail-soft and never enters runtime evidence or
contains secrets, action operands, model weights, or raw Human data. See the
canonical [UI and interaction specification](../../docs/UI_INTERACTION_SPEC.md)
for the shared Workbench/In-Game vocabulary.

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
