# Compatibility

Compatibility is exact and fail-closed. Matching a display version alone is
insufficient.

## Current Authority

The current exact-game audit, Connector host identity, protocol, Modset policy,
and package identity are recorded in the root `platform-bom.json` and the
Connector `release-manifest.json`. The Host Runtime package and runtime
verifier must consume those identities; this document does not duplicate
version, SHA, or MVID values.

A source/package candidate is not an installed or loaded runtime. A runtime
claim must name:

- game executable and main assembly identity;
- platform and architecture;
- Host/Managed source and artifact identity;
- Connector source, artifact, protocol, and native Mod identity;
- Modset and profile mode;
- process/load evidence produced by the current verifier.

## Fail-Closed Rules

Normal startup refuses any mismatch in game bytes, platform, assembly, Host
artifact, Connector artifact/protocol, Modset, profile mode, or information
policy. Unknown builds may be investigated only with an explicitly experimental
command. A successful fixture, package check, or probe does not update support.

The native implementation ID `STS2_MCP` is a stable Mod compatibility identity,
not the name of the Platform repository or a second gameplay authority.

## Evidence Boundary

Build, package, install, boot, loaded, mutation, journey, differential,
performance, and qualification are separate evidence levels. No old runtime,
artifact, release, or predecessor report inherits authority after any identity
change.

Current support is limited to the exact tuples named by the current manifests.
Later game builds, other platforms, additional Mods, and changed information
policy remain pending exact runtime evidence.
