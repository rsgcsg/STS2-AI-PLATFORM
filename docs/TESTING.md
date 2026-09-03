# Testing And Evidence

The root suite is the portable source and package gate. It does not require
proprietary STS2 files and does not prove installation, loading, mutation, a
journey, or qualification.

```bash
npm ci
npm run check
```

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

Public CI runs `npm run check` and does not require proprietary game assemblies.
Exact-game checks are local and must record the game, platform, Modset, source,
artifact, and runtime identities from the current manifests and reports.

## Evidence Ladder

Evidence levels are ordered but never implied:

```text
source -> test -> build -> package -> installed -> loaded
       -> Live mutation -> journey -> human_validated -> qualified
```

Predecessor reports and fixtures can test mechanics or migration assumptions,
but cannot qualify a different Platform artifact. Local `.local/` evidence is
not documentation and must not be committed.
