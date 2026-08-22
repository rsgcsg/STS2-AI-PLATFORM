# Contributing

Open an issue before broadening the supported native-action family. A proposed
family must identify the exact shipped UI entry point, accepted game-owned
semantic action, native object operands, corresponding Connector BoundAction,
stable successor boundary, and zero/ambiguous failure behavior.

Keep commits cohesive and run:

```bash
npm run check
git diff --check
```

Public CI intentionally does not compile against proprietary STS2 files. Exact
game builds and Live evidence are separate local gates documented in
[`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).
