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

- exact-game build from clean Platform source;
- install/cold-load exact artifacts;
- targeted Connector, Host and Annotator canaries;
- publish independent component releases and a tested BOM.

Workbench, Receiver, cloud storage and wider Annotator capture are later work,
not reasons to weaken the initial component boundaries.
