# Document Map

Read current Platform truth in this order:

1. [Status](STATUS.md)
2. [Architecture](ARCHITECTURE.md)
3. [Components](COMPONENTS.md)
4. [Testing](TESTING.md)
5. [Versioning](VERSIONING.md)
6. [Roadmap](ROADMAP.md)
7. [Current handoff](memory/CURRENT.md)

Then read the component entry points:

8. [Host Runtime map](../components/host-runtime/docs/DOCUMENT_MAP.md)
9. [Annotator map](../components/annotator/docs/DOCUMENT_MAP.md)

The Connector contract and release identity are authoritative in the Connector
component manifest and the root `platform-bom.json`. Component documentation is
authoritative for implementation and operations, but must not create a second
version or identity registry. Dated evidence proves only the exact
source/artifact/runtime named by that evidence.
