# Current Context

Phase: **runtime-seal candidate; owner native-human validation pending.**

`STS2-AI-PLATFORM` is the only forward-development source authority for the
Platform components. The root BOM and component release manifests are the
machine-readable authorities for component revisions, package identities,
protocol, compatibility, and non-claims. This file is a handoff, not a second
identity registry.

## Current Work

- Keep portable source, BOM, boundary, history, package and component checks
  green.
- Preserve Connector `1.2.0-rc.5`, Host Runtime `1.1.0-rc.7`, Annotator
  `0.2.0-rc.2` and STPD `f75fb140...` as separate exact identities.
- The game is cold-loaded with the exact current Connector + Annotator Modset.
  The only open current-artifact gate is one owner-operated ordinary-combat
  session, followed by audit; no external Connector controller may run during
  that session.

## Evidence Boundary

Source, test, build, package, installed, loaded, Live, journey, and qualified
are distinct evidence levels. A current source or package candidate does not
inherit runtime authority from predecessor repositories, old releases, or old
reports. Historical evidence remains useful for comparison and rollback only.
