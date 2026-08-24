# Component Versioning

The monorepo does not impose lockstep component versions.

- Connector, Host Runtime, Annotator, evidence tools and future Workbench have
  independent semantic versions.
- Component tags use `<component>/v<version>`.
- `platform-bom/v<version>` names a tested compatible composition.
- Player Environment protocol, SDK version, component version and exact artifact
  identity are distinct.

The component identity report records the workspace commit, the most recent
commit that changed each component path, the component Git tree, and source and
contract digests. A change outside a component cannot change its path-scoped
identities. Runtime evidence binds artifact bytes and does not transfer across
a rebuild.
