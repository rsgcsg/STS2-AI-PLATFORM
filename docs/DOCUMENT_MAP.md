# Document Map

Read current Platform truth in this order:

1. [Status](STATUS.md)
2. [Architecture](ARCHITECTURE.md)
3. [Components](COMPONENTS.md)
4. [Testing](TESTING.md)
5. [Versioning](VERSIONING.md)
6. [Roadmap](ROADMAP.md)
7. [Current handoff](memory/CURRENT.md)
8. [V1 runtime-seal predecessor evidence](evidence/RUNTIME_SEAL_CANDIDATE_2026-08-24.md)

Then read the component entry points:

9. [Connector map](../components/connector/docs/DOCUMENT_MAP.md)
10. [Host Runtime map](../components/host-runtime/docs/DOCUMENT_MAP.md)
11. [Annotator map](../components/annotator/docs/DOCUMENT_MAP.md)
12. [Evidence package](../components/evidence/README.md)
13. [Workbench](../apps/workbench/README.md)

The Connector contract and release identity are authoritative in the Connector
component manifest and the root `platform-bom.json`. Component documentation is
authoritative for implementation and operations, but must not create a second
version or identity registry. Dated evidence proves only the exact
source/artifact/runtime named by that evidence.
