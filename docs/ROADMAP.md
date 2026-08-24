# Roadmap

## Consolidation Baseline

- Complete: import all three histories without squashing.
- Complete: remove sibling source/build-output coupling.
- Complete: establish independent component identity and one portable check entry.
- Complete: publish a source/package-candidate Platform BOM.

## Consumer Cutover

- Complete: package the Connector SDK and Host Runtime from Platform.
- Complete: make STPD consume pinned public packages instead of sibling source.
- move generic bundle/profile verification only after parity tests;
- retain ResearchTransition, corpus policy, splits, B0, serialization, models and
  training in STPD.

## Runtime Seal

- Complete: exact-game, deterministic Connector and Annotator builds from clean
  component source.
- Complete: immutable public Connector/SDK/Host releases and anonymous cold
  download verification.
- Complete: install, rollback snapshots, exact Connector H0/H1/H2, and exact
  Annotator loaded-identity/Modset gates.
- Complete: one normal native-human combat recording with the current exact
  Annotator artifact, followed by independent audit and deterministic export.
- Complete: promote the exact composition to Platform BOM `0.1.0-rc.3` after
  the human gate; this remains a runtime-seal candidate, not qualification.

Workbench, Receiver, cloud storage and wider Annotator capture are later work,
not reasons to weaken the initial component boundaries.
