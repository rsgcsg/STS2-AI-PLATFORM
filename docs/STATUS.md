# Current Status

Phase: **coherent Platform source/package candidate; runtime seal pending**.

## Implemented

- Connector, Host Runtime, and Human Annotator histories are preserved under
  `components/` without squashing the imported ancestry.
- Root dependency direction, evidence terminology, and component identity are
  explicit and checked by the portable suite.
- Annotator compiles against the exact Connector output produced by the Platform
  build; it does not create a second Connector build.
- The Host Runtime package and Connector SDK are represented by the current
  public-package entries in `platform-bom.json`; source may be ahead of the
  last BOM only during an explicit release closeout.
- Host Runtime setup is pinned to the immutable Platform Connector release,
  including archive checksum and native source/SHA/MVID/protocol identity.
- STPD consumes public packages and does not require a predecessor sibling
  checkout for its package path.
- Root and component portable checks pass at the current source revision.

## Current Evidence

The current candidate's exact-game and package identities are recorded in
`platform-bom.json` and the component release manifests. The current source
tree is the authority for what can be built; those files are the authority for
published candidate identity. Predecessor loaded/Live evidence does not
transfer.

## Non-claims

- The current Platform-built Connector and Annotator artifacts are not proven
  installed or loaded by this source/package candidate record.
- No current Platform artifact has current-identity Live, human-validation, or
  qualification evidence.
- The Platform BOM is a source/package candidate, not a runtime-sealed release.
- A source change ahead of the current public Host package/BOM remains a
  package non-claim until that release closeout is published and cold-checked.
- Workbench, generic evidence receiver, and a Platform-wide Python SDK are not
  current Platform claims; the Host component's Python consumer is component
  scope.
