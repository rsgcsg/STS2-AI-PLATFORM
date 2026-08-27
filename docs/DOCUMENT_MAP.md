# Document Map

Read current Platform truth in this order:

1. [Status](STATUS.md)
2. [Architecture](ARCHITECTURE.md)
3. [Components](COMPONENTS.md)
4. [Testing](TESTING.md)
5. [Versioning](VERSIONING.md)
6. [Development workflow](DEVELOPMENT_WORKFLOW.md)
7. [Roadmap](ROADMAP.md)
8. [Current handoff](memory/CURRENT.md)
9. [Policy Runtime](POLICY_RUNTIME.md)
10. [Platform Live UI](LIVE_UI.md)
11. [Semantic timeline schema-2 owner closeout](evidence/SEMANTIC_TIMELINE_OWNER_CLOSEOUT_2026-08-27.md)
12. [Semantic timeline source closeout](evidence/SEMANTIC_TIMELINE_SOURCE_CLOSEOUT_2026-08-27.md)
13. [Semantic boundary owner canary](evidence/SEMANTIC_BOUNDARY_OWNER_CANARY_2026-08-27.md)
14. [Semantic boundary source closeout](evidence/SEMANTIC_BOUNDARY_SOURCE_CLOSEOUT_2026-08-26.md)
15. [Rapid-input ledger source closeout](evidence/RAPID_INPUT_LEDGER_SOURCE_CLOSEOUT_2026-08-26.md)
16. [V2 Read-Rich Combat closeout](evidence/HUMAN_EVIDENCE_V2_READ_RICH_COMBAT_CLOSEOUT_2026-08-25.md)
17. [V1 runtime-seal predecessor evidence](evidence/RUNTIME_SEAL_CANDIDATE_2026-08-24.md)

Then read the component entry points:

18. [Connector map](../components/connector/docs/DOCUMENT_MAP.md)
19. [Host Runtime map](../components/host-runtime/docs/DOCUMENT_MAP.md)
20. [Annotator map](../components/annotator/docs/DOCUMENT_MAP.md)
21. [Evidence package](../components/evidence/README.md)
22. [Workbench](../apps/workbench/README.md)
23. [Platform Game Mod operations](../apps/game-mod/README.md)
24. [In-game Live UI boundary](../apps/ingame-ui/README.md)

The Connector contract and release identity are authoritative in the Connector
component manifest and the root `platform-bom.json`. Component documentation is
authoritative for implementation and operations, but must not create a second
version or identity registry. Dated evidence proves only the exact
source/artifact/runtime named by that evidence.
