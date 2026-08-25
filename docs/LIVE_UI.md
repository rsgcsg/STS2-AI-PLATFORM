# Platform Live UI

The Platform has one in-game UI, not separate Connector, Annotator and Policy
GUIs. The UI starts hidden inside the unified `STS2_PLATFORM` Mod and toggles
with the uncommon letter key `K`; `Escape` closes it.

| Page | Source |
|---|---|
| Overview | combined typed status and exact identity |
| Environment | Connector Snapshot/capabilities plus in-process Annotator and Live UI identity |
| Policy | Policy Runtime status, scores, selected action, Receipt |
| Human Data | RecordingService lifecycle, counters, health and closeout |
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

Build/deploy/rollback are source- and artifact-bound in `apps/game-mod`. One
manifest and one DLL replace the former Connector/Annotator/Live-UI manifests.
The common assembly prints its SHA/MVID plus component-specific source identity;
verification compares those records with installed provenance. `installed`,
`loaded`, automated input, owner-visible UI, Human recording evidence and
Agent-run evidence remain separate claims.

See `apps/game-mod/README.md` for commands and `apps/ingame-ui/README.md` for the
presentation boundary. The predecessor three-Mod artifact passed exact
install/cold-load identity verification, but its F10 input was not observed.
The final unified `K` artifact `a7b11d93... / c3e7127a...` is cold-loaded and
reports panel readiness. Its non-human toggle canary was `not_observed`, so K
visibility and recording controls remain explicit owner gates. No Human/policy
evidence is inferred from load identity, and predecessor Human evidence does not
qualify this artifact.
