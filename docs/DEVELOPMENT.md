# Development

## Public Checks

Public CI can run without proprietary game files:

```bash
dotnet test tests/STS2HumanAnnotator.Core.Tests/STS2HumanAnnotator.Core.Tests.csproj -c Release
node tools/check-boundary.mjs
node tools/check-docs.mjs
```

## Exact-Game Check

With a local exact STS2 installation and sibling Connector build:

```bash
cd ../STS2-Connector && npm run build
cd ../STS2-human-Annotator && npm run check
```

To add a native action family, identify its shipped UI entry method and the
game-owned accepted semantic action from the exact assembly, then add exact
operand mapping, fail-closed tests, invalidation behavior, and current docs.
Never infer action identity from labels, position, post-state, or business source.
