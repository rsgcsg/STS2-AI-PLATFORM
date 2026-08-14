# Contributing

Start with the [document map](docs/DOCUMENT_MAP.md), then run:

```bash
npm ci
npm run check
npm run doctor
```

Open an issue before introducing a managed Host, game patch, stub, reflection
seam, or new public protocol. Small launcher, lifecycle, compatibility, test,
and documentation fixes can go directly to a focused pull request.

Public CI must pass without Slay the Spire 2. Runtime evidence must name the
exact game, platform, executable/assembly bytes, Connector, Modset, profile,
and probe source. Never commit game DLLs/PCKs/assets, patched binaries,
decompiled source, saves, logs, `.local/`, credentials, or user-specific paths.

The project accepts evidence-backed changes, not claims based only on a fixture,
simulator, source presence, build, or “it launched on my machine.”
