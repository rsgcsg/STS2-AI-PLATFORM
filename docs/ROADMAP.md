# Roadmap

## Consolidation Baseline

- import all three histories without squashing;
- remove sibling source/build-output coupling;
- establish independent component identity and one portable check entry;
- publish a source-candidate Platform BOM.

## Consumer Cutover

- package the Connector SDK from Platform;
- make STPD consume a pinned public package instead of sibling source;
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
