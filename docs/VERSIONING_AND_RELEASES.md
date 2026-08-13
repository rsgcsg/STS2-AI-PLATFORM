# Versioning And Releases

Four identities are intentionally separate:

1. **Connector release** (`1.0.0-rc.1`): source/product packaging.
2. **Player Environment protocol** (`1.0-rc.2`): wire compatibility.
3. **Capabilities**: loaded features, exact game/Modset and observation/input
   availability.
4. **Artifact identity**: DLL SHA-256, MVID, source revision and runtime
   instance.

A release version does not grant support to every game build or Modset. A
protocol-compatible consumer still checks capabilities and exact environment.
SpireAgent and other consumers version independently; they declare a supported
protocol range and a versioned SDK dependency rather than sharing a branch or
release number.

## Compatibility

- Patch releases preserve the stable protocol or advance only compatible
  optional capabilities.
- A protocol shape/meaning change advances the protocol and both Host/SDK
  conformance fixtures.
- A new game assembly or Modset starts unqualified and fails closed until its
  exact compatibility evidence exists.
- The Mod implementation ID `STS2_MCP` remains stable through major 1 so update
  and rollback find one installation.

## Release Gate

1. clean source and passing deterministic checks;
2. exact-game Host tests and Release build;
3. SDK package dry-run and MCP import check;
4. install with rollback backup;
5. cold-load and exact loaded identity;
6. targeted Read/stale/controller/idempotency/unknown/native-page gates;
7. an ordinary same-artifact Live journey;
8. evidence record tied to source, SHA, MVID, runtime, game and Modset;
9. tag, release artifact and SDK package publication.

Build, install or predecessor Live evidence cannot skip a later gate.
