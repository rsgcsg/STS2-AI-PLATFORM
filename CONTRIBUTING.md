# Contributing

Read `AGENTS.md`, the current architecture and the owning component guide before
editing. Keep changes inside the narrowest owning component and state public
contract, component identity, artifact and runtime-evidence impact explicitly.

```bash
npm ci
npm run check
git diff --check
```

Game-bound changes additionally require `npm run check:exact-game` and the
relevant exact-runtime gate. Do not include proprietary game files, generated
artifacts, raw recordings, local evidence, credentials or model weights.
