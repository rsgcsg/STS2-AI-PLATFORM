# Current Status

Phase: **coherent Platform source/package candidate; runtime seal pending**.

## Implemented

- Complete non-squashed Git histories of Connector, Host Runtime and Human
  Annotator are imported under `components/`.
- The original source revisions remain unchanged ancestors and are fixed in a
  machine-readable migration manifest.
- Root dependency direction, evidence terminology and component identity are
  explicit.
- Annotator compiles against the exact Connector output produced by the Platform
  build; it does not create a second Connector build.
- Host Runtime consumes the immutable public Connector SDK package rather than
  sibling source.
- Connector SDK `1.1.0-rc.1` and Host Runtime `1.1.0-rc.2` are public prerelease
  assets with checksums and successful external cold-install smokes. Host Runtime
  `1.1.0-rc.1` is explicitly superseded after its package smoke found a root import.
- STPD source `05a2ce04e6d0dbcae721fd32b1b377500ca2b9e4` consumes both public packages and
  no longer requires any predecessor sibling checkout.
- Root and component portable checks pass, including identity, dependency-boundary,
  history-preservation and standalone-package checks.

## Current Evidence

Platform exact-game build evidence is bound to macOS arm64 STS2
`v0.111.0/41cef1ea`; public package download/install evidence is recorded in the
release checksums and BOM. Predecessor loaded/Live evidence does not transfer.

## Non-claims

- No Platform-built Connector or Annotator artifact is installed or loaded.
- No Platform artifact has Live, human-validation or qualification evidence.
- The Platform BOM is a source/package candidate, not a runtime-sealed release.
- Workbench, generic evidence receiver and Python SDK are not implemented.
