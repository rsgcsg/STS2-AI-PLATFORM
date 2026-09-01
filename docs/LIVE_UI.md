# Platform Live UI

The Platform has one in-game UI, not separate Connector, Annotator and Policy
GUIs. The UI starts hidden inside the unified `STS2_PLATFORM` Mod and toggles
with the uncommon letter key `K`; `Escape` closes it.

| Page | Source |
|---|---|
| Overview | combined typed status and exact identity |
| Environment | Connector Snapshot/capabilities plus in-process Annotator and Live UI identity |
| Policy | Policy Runtime status, scores, selected action, Receipt |
| Human Data | RecordingService lifecycle, per-family scope/results, health and closeout |
| Diagnostics | Reads, invalidations, errors and transports |

The UI has no Player Environment submit client. Connector Read opportunities
remain visible even when no policy process is running; materialized policy Reads
are clearly distinguished. Policy controls call the loopback Runtime on the
canonical default port `15527`; recording controls call the Annotator
application service. Runtime and Annotator remain the owning layers.

Recording starts only after **New Session**. The same process can Pause/Resume,
Close safely, and create another isolated session. Views obtain a current
RecordingStatus and may follow typed events from a sequence number; a reported
event gap requires another status query. These controls never enter the native
human witness path and cannot create a HumanDecision by themselves.
After a lifecycle command, the UI presents the authoritative post-command
status rather than leaving an intermediate command result on screen. Human Data
separately lists actions that were recorded, supported actions that failed
closed and were not recorded, supported actions not observed in this session,
and actions declared outside the active CaptureProfile.

Build/deploy/rollback are source- and artifact-bound in `apps/game-mod`. One
manifest and one DLL replace the former Connector/Annotator/Live-UI manifests.
The common assembly prints its SHA/MVID plus component-specific source identity;
verification compares those records with installed provenance. `installed`,
`loaded`, automated input, owner-visible UI, Human recording evidence and
Agent-run evidence remain separate claims.

The product shell keeps a small click-through HUD visible during ordinary play;
the full Workspace opens with `K` and closes with `Escape` or its Close button.
Only one presentation state is active at a time: opening the Workspace hides
the compact HUD, and the Recorder tool region is mounted on that same
Workspace surface rather than as a legacy root overlay. Workspace and Recorder
cards support bounded drag, resize, collapse, and Reset layout. The Recorder
offers typed New Session, Pause, Resume, and Close controls. A bounded toast
stack reports accepted/rejected commands and transport recovery, deduplicates
repeated state changes, expires automatically, and can be dismissed. Layout is
versioned local presentation state with fail-soft defaults; it is never written
to evidence or sent to the Policy Runtime. The root overlay is click-through
outside interactive controls, so gameplay input remains owned by STS2.

The Recorder's Recent Actions list and Last Action detail are a read-only
projection of the Annotator `RecordingApplicationService` event stream. They
show canonical lifecycle (`Observed`, `Recorded`, `Invalidated`) plus any
already-owned bound-action subject/argument IDs and labels. Effect text or
other metadata absent from canonical evidence is explicitly `unavailable`; the
UI never infers it from pointer input, coordinates, timing, or a later frame,
and a visible feed row is not itself recording proof.

The shared status vocabulary and interaction rules are defined in the canonical
[UI and interaction specification](UI_INTERACTION_SPEC.md).

See `apps/game-mod/README.md` for commands and `apps/ingame-ui/README.md` for the
presentation boundary. The predecessor three-Mod artifact passed exact
install/cold-load identity verification, but its F10 input was not observed.
The unified `K` artifact `d3b25e62... / ee78d9a1...` has six audited end-turn
records on its exact runtime. Its first Close completed internally while its
command label remained at `Closing`. The repaired artifact
`06f62285... / 17981f40...` is now cold-loaded and presents authoritative
post-command status plus typed scope results. Two owner sessions prove card-play
recording and both immediate and pending-safe Close behavior. Follow-up source
renames the failure view to `native-accepted but failed closed` and excludes
native-rejected attempts. That source is cold-loaded as
`887630f4... / 14761ed4...` in runtime `bcf2b3f1...`, but the latest owner
session now independently audits 19/19 records: 10 play and nine end-turn, with
78 Reads and no Read failures. Sixteen native-accepted card actions are shown
separately as failed closed rather than records; one owner-observed cancelled
attempt changed neither category. First-click Close completed in 5.153 ms. The
cancelled attempt's absence is owner-attested rather than machine-attributable.
No policy evidence is inferred from this Human session, and prior evidence does
not qualify later source.
