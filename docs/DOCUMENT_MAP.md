# Document Map

Read current Platform truth in this order:

1. [Status](STATUS.md)
2. [Architecture](ARCHITECTURE.md)
3. [Components](COMPONENTS.md)
4. [Testing](TESTING.md)
5. [Versioning](VERSIONING.md)
6. [Roadmap](ROADMAP.md)
7. [Current handoff](memory/CURRENT.md)
8. [Policy Runtime](POLICY_RUNTIME.md)
9. [Platform Live UI](LIVE_UI.md)
10. [Semantic boundary source closeout](evidence/SEMANTIC_BOUNDARY_SOURCE_CLOSEOUT_2026-08-26.md)
11. [Rapid-input ledger source closeout](evidence/RAPID_INPUT_LEDGER_SOURCE_CLOSEOUT_2026-08-26.md)
12. [V2 Read-Rich Combat closeout](evidence/HUMAN_EVIDENCE_V2_READ_RICH_COMBAT_CLOSEOUT_2026-08-25.md)
13. [V1 runtime-seal predecessor evidence](evidence/RUNTIME_SEAL_CANDIDATE_2026-08-24.md)

Then read the component entry points:

13. [Connector map](../components/connector/docs/DOCUMENT_MAP.md)
14. [Host Runtime map](../components/host-runtime/docs/DOCUMENT_MAP.md)
15. [Annotator map](../components/annotator/docs/DOCUMENT_MAP.md)
16. [Evidence package](../components/evidence/README.md)
17. [Workbench](../apps/workbench/README.md)
18. [Platform Game Mod operations](../apps/game-mod/README.md)
19. [In-game Live UI boundary](../apps/ingame-ui/README.md)

The Connector contract and release identity are authoritative in the Connector
component manifest and the root `platform-bom.json`. Component documentation is
authoritative for implementation and operations, but must not create a second
version or identity registry. Dated evidence proves only the exact
source/artifact/runtime named by that evidence.
