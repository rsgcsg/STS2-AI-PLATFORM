# Current Status

Phase: **coherent Platform source baseline under migration**.

## Implemented

- Complete non-squashed Git histories of Connector, Host Runtime and Human
  Annotator are imported under `components/`.
- The original source revisions remain unchanged ancestors and are fixed in a
  machine-readable migration manifest.
- Root dependency direction, evidence terminology and component identity are
  explicit.
- Annotator consumes Host Runtime and Connector through in-repository component
  paths rather than sibling checkout names or sibling build outputs.
- Host Runtime consumes the strategy-free Connector client through a declared
  package dependency.
- Root portable checks compose all existing component checks.

## Current Evidence

The predecessor repositories passed their portable checks at the exact
migration refs before import. Platform source checks must pass again after all
path and identity changes; predecessor loaded/Live evidence does not transfer.

## Non-claims

- No Platform-built Connector or Annotator artifact is installed or loaded.
- No Platform artifact has Live, human-validation or qualification evidence.
- The initial Platform BOM is a source candidate, not a supported release.
- Workbench, generic evidence receiver and Python SDK are not implemented.
