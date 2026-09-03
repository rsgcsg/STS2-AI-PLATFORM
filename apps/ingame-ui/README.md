# Platform Live UI

The Platform Live UI is the in-game presentation component for Agent Run and
Human Recorder. It is fully hidden during ordinary play; `K` opens one compact
Live Workspace and `Esc` closes it. The Workspace has exactly two peer
surfaces, `Agent Run` and `Human Recorder`; Recorder is never a floating card
or root overlay.
The Recorder tab exposes only the typed New/Pause/Resume/Close application
commands, plus a bounded Recent Actions feed and Last Action detail sourced
only from the typed Annotator application-event projection of already-owned
semantic root/lifecycle facts; missing card, target
or effect metadata is shown as unavailable. The UI
calls typed Connector observation, Policy Runtime, and Annotator recording
services; it does not publish, resolve, or submit gameplay actions itself.

The Runtime loopback defaults to `http://127.0.0.1:15527`. Connector Snapshot
and Read opportunities, recording state, and loaded component identities remain
available when no policy artifact is running; only policy scores/modes/Receipts
are then unavailable.

Workspace layout (position, size, and selected surface) is versioned
presentation-only state under the Windows local application
data directory. Persistence is fail-soft and never enters runtime evidence or
contains secrets, action operands, model weights, or raw Human data. See the
canonical [UI and interaction specification](../../docs/UI_INTERACTION_SPEC.md)
for the shared Workbench/In-Game vocabulary. The Recorder feed preserves its
scroll position during ordinary event updates and resets only for a new session
or an explicit layout reset.

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
