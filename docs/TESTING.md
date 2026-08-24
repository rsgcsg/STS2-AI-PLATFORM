# Testing And Evidence

Install portable dependencies and run all source checks:

```bash
npm ci
npm run check
```

Focused checks:

```bash
npm run check:identity
npm run check:boundaries
npm run check:history
npm --prefix components/connector run check
npm --prefix components/host-runtime run check
npm --prefix components/annotator run check
```

`npm run build` requires an exact local STS2 installation and builds Connector
and Annotator artifacts. A successful build is not install, load or Live
evidence.

`npm run check:exact-game` compiles the current game-bound projects after the
portable suite. Public CI runs `npm run check` and does not require proprietary
game assemblies.

Evidence levels are strictly ordered but never implied:

```text
source/test -> build -> installed -> loaded -> live_exercised
            -> human_validated -> qualified
```
