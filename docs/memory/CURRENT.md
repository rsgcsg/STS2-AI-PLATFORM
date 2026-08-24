# Current Context

Phase: **Platform source/package candidate; runtime seal pending.**

`STS2-AI-PLATFORM` is the only forward-development source authority for the
Platform components. The root BOM and component release manifests are the
machine-readable authorities for component revisions, package identities,
protocol, compatibility, and non-claims. This file is a handoff, not a second
identity registry.

## Current Work

- Keep portable source, boundary, history, package, and component checks green.
- Publish and cold-check the current Host Runtime package, then update the BOM
  and STPD pin to the exact released composition.
- Build and verify the exact current candidate, then record install, cold-load,
  non-human runtime gates, and owner-only native UI validation separately.

## Evidence Boundary

Source, test, build, package, installed, loaded, Live, journey, and qualified
are distinct evidence levels. A current source or package candidate does not
inherit runtime authority from predecessor repositories, old releases, or old
reports. Historical evidence remains useful for comparison and rollback only.
