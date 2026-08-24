# Development

## Public Checks

Public CI can run without proprietary game files:

```bash
dotnet test tests/STS2HumanAnnotator.Core.Tests/STS2HumanAnnotator.Core.Tests.csproj -c Release
node tools/check-boundary.mjs
node tools/check-docs.mjs
```

STS2 initializes one assembly from each Mod manifest and does not resolve the
adjacent Core project DLL. The Mod project therefore links the same Core source
files into its one runtime assembly; tests and the offline Tool compile the Core
project normally. There is one source authority and no runtime loader shim.

The Mod compiles against the exact Connector artifact built first by the root
workflow. It does not use a Connector `ProjectReference`, because that would
silently create a second artifact outside the recorded build identity.

## Exact-Game Check

With a local exact STS2 installation, from the Platform root:

```bash
npm run check:exact-game
```

To add a native action family, identify its shipped UI entry method and the
game-owned accepted semantic action from the exact assembly, then add exact
operand mapping, fail-closed tests, invalidation behavior, and current docs.
Never infer action identity from labels, position, post-state, or business source.
