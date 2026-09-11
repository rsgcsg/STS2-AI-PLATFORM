# Operations

Use the single production `STS2_PLATFORM` package. Fully close STS2 before
build/deploy/rollback; cold-load and run root `npm run verify:loaded`. Click the
visible Platform launcher, select Human Recorder and start a session. For an
uninterrupted Full-Run gate, start before a fresh native run and do not pause,
reload or resume until natural terminal. Close Recorder and wait for completed
close before audit and bundle. UI Close only hides the workspace; Recorder
session controls own recording lifecycle.

Current `pack-session` creates canonical-first bundle 3. Compatibility bundle 2
is an explicit separate command. See [session bundles](SESSION_BUNDLES.md) and
[root collection / incident response](../../../docs/ANNOTATOR_COLLECTION.md)
for exact commands, local storage, authorized handoff and future repair.

## Triage

Inspect the exact runtime status, immutable semantic trace, invalidations,
canonical stream, journal and native log together. Accepted in-scope unresolved
or failed-closed decisions are real failures; internal diagnostics, presentation
cancellation, native cancellation and unsupported/non-decisions are distinct.
Unavailable accounting or failed durable close must not become a green zero.
Missing H, owner, execution catalog or successor stays fail-closed. No name,
coordinate, later-frame or current-root recovery is permitted. Preserve evidence
before changing candidate; never edit a record or add a close receipt by hand.

## Historical tooling

Predecessor macOS install-provenance and Connector-only launch commands remain
available for their exact archived profiles. They are not the production unified
package route, and do not upgrade predecessor recording formats or Human claims.
Use the owning Host/Game Mod lifecycle guidance for current platform support.
The `native-action-ledger` is archival only. Policy Runtime controller ownership
and Human recording remain separate; no model operation is Human evidence.
