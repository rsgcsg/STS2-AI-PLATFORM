# Platform Live UI

The in-game **Platform** button opens two surfaces: **Human Recorder** and
**Agent Run**. There is no `K` shortcut. Close returns to the small launcher;
`Esc` closes the normal workspace. Drag the header to move the workspace and
use its corner to resize the normal view.

Both surfaces have a **Minimize** control and a separate, genuinely smaller
440 × 174 view. The arrow restores the normal workspace and its previous size.
The compact Recorder shows only **已录入 / 真实失败 / 最新 3 条**. Its totals come
from Annotator's session counters, not the retained UI list:

- 已录入 is the durable canonical decision count.
- 真实失败 is Annotator's versioned in-scope failure disposition total. Unknown
  older counters display `—`; normal cancellation, abort, diagnostic
  invalidation and unsupported/non-decision dispositions do not inflate it.
- The latest three rows omit native diagnostics and unsupported/non-decisions.
  They retain the owning decision status; pending and cancelled are distinct.

The normal Recorder contains lifecycle controls, session totals, a paginated
retained decision list and details revealed by selecting a row. Exact parent
and causal-root lineage, selector metadata, action and state identities remain
available in those details. Missing metadata stays unavailable. Canonical
recording does not itself establish Full-Run qualification or research admission.

Agent Run uses only typed Policy Runtime controls. Its compact view keeps mode,
controller, policy, last selected action, Receipt and **Return to Human** visible.
It never resolves or submits a gameplay action directly. The Runtime loopback
defaults to `http://127.0.0.1:15527`; modes and model status require a compatible
Policy Runtime with an exact Policy Manifest and artifact.

## Retention and display cost

The closed workspace performs no status polling. An open Recorder queries only
the typed Annotator status/event service once per second; it does not request a
Connector Snapshot or action catalog. Agent Run requests its typed live status
only while visible. Loaded assembly identity is immutable and cached once.
The UI retains at most 512 action rows; ordered event sequence is the replay
cursor. A reconnect gap refreshes the available batch and labels partial
history. Authoritative session totals are independent of this retention.
Minimized mode does not rebuild the hidden normal decision widgets.

Layout version 5 stores position, normal size, compact mode and selected surface
in local application data. This presentation-only state is fail-soft; it
contains no Human evidence, native operands, model weights or secrets. Old UI
layout files are not migrated because no evidence or consumer contract depends
on their geometry. See the canonical
[UI and interaction specification](../../docs/UI_INTERACTION_SPEC.md).

## Ownership and validation

This component consumes typed Connector, Policy Runtime and Annotator services.
It owns neither gameplay/recording authority nor packaging, deployment, loaded
identity or rollback. `apps/game-mod` compiles it into the single production
`STS2_PLATFORM` Mod. Portable boundary and event-projection tests run with:

```bash
npm run live-ui:check
```

Use [`apps/game-mod/README.md`](../game-mod/README.md) for the supported exact
build/install/cold-load/rollback path. Source tests and compilation do not prove
rendering, Human origin, policy operation or Full-Run qualification. Final UI
canary checks launcher, both compact views, drag/restore and Recorder counters
against the exact candidate's session audit.
