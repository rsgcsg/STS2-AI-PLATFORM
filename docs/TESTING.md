# Testing And Evidence

The root suite is the portable source and package gate. It does not require
proprietary STS2 files and does not prove installation, loading, mutation, a
journey, or qualification.

```bash
npm ci
npm run check
```

Repository-system checks and routing can also be run directly:

```bash
npm run project:context
npm run project:check
npm run project:closeout
```

`project:check` is part of the root portable gate. `project:closeout` reports
path-based review signals and never rewrites semantic truth.

Focused checks:

```bash
npm run check:identity
npm run check:bom
npm run check:boundaries
npm run check:history
npm --prefix components/connector run check
npm --prefix components/host-runtime run check
npm --prefix components/annotator run test
npm --prefix components/evidence run check
npm --prefix apps/workbench run test
```

The component checks have separate meanings:

- Connector check: contract, native source, SDK, and Connector-local tests;
- BOM check: component source identities, component versions, public package
  pins, runtime artifact identities and explicit non-claims agree;
- Host Runtime check: lifecycle, package, Python consumer, and Host tests;
- Annotator `test`: portable recorder and workstation-tool tests;
- Annotator `check`: the portable tests plus exact native compilation against
  the locally installed game and current Connector artifact.
- Evidence check: Python package build, V1/V2 typed verification, immutable
  store/transfer/receiver and failure paths;
- Workbench test: read-only service aggregation, JSON status and HTML entry.

`npm run build` and `npm run check:exact-game` require the exact local STS2
installation. They build or compile game-bound artifacts; a successful build is
not install, load, Live, native-human mutation, or qualification evidence.

Public CI runs the complete portable gate on Ubuntu and a targeted Windows
portability gate for root identity, Host packaging/runtime adapters, Annotator
.NET and evidence file sharing, canonical Evidence bytes, and game-Mod
lifecycle tests. Neither job requires proprietary game assemblies. Exact-game
checks are local and must record the game, platform, Modset, source, artifact,
and runtime identities from the current manifests and reports.

Root Host wrappers retain an explicit nested-npm `--` boundary so profile,
endpoint, and experimental-evidence arguments reach the owning Host CLI on
Windows and POSIX shells.

The Host profile-template test also pins its captured file inventory. This
prevents Node-version-specific recursive-copy filter behavior from admitting
runtime-only Windows `logs` or `sentry` files into a reusable profile template.
Workspace package tests use Node's standard automatic discovery rather than
depending on shell-expanded `*.test.mjs` globs, which Windows npm does not
expand on the supported Node 20 baseline.

## Evidence Ladder

Evidence levels are ordered but never implied:

```text
source -> test -> build -> package -> installed -> loaded
       -> Live mutation -> journey -> human_validated -> qualified
```

Predecessor reports and fixtures can test mechanics or migration assumptions,
but cannot qualify a different Platform artifact. Local `.local/` evidence is
not documentation and must not be committed.
