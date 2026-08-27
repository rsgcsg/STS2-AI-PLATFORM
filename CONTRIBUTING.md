# Contributing

Read `AGENTS.md`, the current architecture and the owning component guide before
editing. Keep changes inside the narrowest owning component and state public
contract, component identity, artifact and runtime-evidence impact explicitly.

Read the [development workflow](docs/DEVELOPMENT_WORKFLOW.md). Ordinary changes
start from current `origin/develop` on a short-lived topic branch and enter
`develop` through a pull request. `main` is the release landing line; never
direct-push `main` or `develop`. One human or agent owns one writable
branch/worktree.

```bash
npm ci
npm run check
git diff --check
```

Game-bound changes additionally require `npm run check:exact-game` and the
relevant exact-runtime gate. Do not include proprietary game files, generated
artifacts, raw recordings, local evidence, credentials or model weights.

Cross-repository work uses a separate STPD PR pinned to an exact Platform
release or explicitly non-stable candidate. STPD is a research consumer of this
model-neutral foundation; Platform does not absorb its model semantics.
