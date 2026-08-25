# Platform Live UI

The Platform has one in-game UI, not separate Connector, Annotator and Policy
GUIs. The DLL-only Mod starts hidden and toggles with `F10`.

| Page | Source |
|---|---|
| Overview | combined typed status and exact identity |
| Environment | Connector Snapshot/capabilities plus in-process Annotator and Live UI identity |
| Policy | Policy Runtime status, scores, selected action, Receipt |
| Human Data | Annotator recording application service |
| Diagnostics | Reads, invalidations, errors and transports |

The UI has no Player Environment submit client. Connector Read opportunities
remain visible even when no policy process is running; materialized policy Reads
are clearly distinguished. Policy controls call the loopback Runtime on the
canonical default port `15527`; recording controls call the Annotator
application service. Runtime and Annotator remain the owning layers.

Build/deploy/rollback are source- and artifact-bound. The Mod prints its own
SHA, MVID and source identity from inside the loaded assembly; verification
compares that record with installed provenance. `installed`, `loaded`, Human
recording evidence and Agent-run evidence remain separate claims.

See `apps/ingame-ui/README.md` for commands. The current artifact has passed
exact install/cold-load identity verification; F10 page navigation and Human
recording controls still require owner interaction. Existing Human V2 evidence
binds the predecessor loaded Annotator and does not qualify this new artifact.
