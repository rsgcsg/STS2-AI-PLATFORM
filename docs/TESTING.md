# Testing And Evidence

The root suite is the portable source and package gate. It does not require
proprietary STS2 files and does not prove installation, loading, mutation, a
journey, Human evidence, or qualification.

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

## Hosted CI contract

GitHub-hosted CI is intentionally a **source/test portability gate**, not an
exact-game or runtime qualification environment.

The workflow has three relevant jobs:

1. `linux-portability` runs the complete root `npm run check` on Ubuntu and, for
   pull requests, dependency review;
2. `windows-portability` runs the same complete root `npm run check` on
   Windows, rather than maintaining a hand-picked Windows subset that can drift
   behind the monorepo;
3. `portable` is the repository-ruleset status. It runs only after both OS lanes
   finish and fails unless both succeeded.

The ruleset can therefore continue to require the stable `portable` context
while Windows is still a real merge gate. `portable` is not a Linux alias.

CI triggers on every pull request and on pushes to integration/release lines:
`develop`, `main`, `release/**`, and `hotfix/**`. Topic-branch pushes do not also
run a second push workflow when the pull-request workflow already covers them.
Concurrency cancels stale runs for the same PR/ref.

All third-party GitHub Actions are pinned by full commit SHA, checkout fetches
full Git history because identity/history checks require it, and checkout does
not persist write credentials.

`npm run check:ci` is part of the root suite and guards these properties. It
also rejects adding exact-game deploy/load commands to public hosted CI. This is
an evidence boundary, not a convenience restriction: hosted runners do not own
the exact local STS2 installation, admitted Modset, installed artifact, or Human
operator needed to make such claims honestly.

## Focused portable checks

```bash
npm run check:ci
npm run check:identity
npm run check:bom
npm run check:boundaries
npm run check:history
npm --prefix components/connector run check
npm --prefix components/host-runtime run check
npm --prefix components/annotator run test
npm --prefix components/evidence run check
npm --prefix components/policy-runtime run check
npm --prefix apps/workbench run test
npm --prefix apps/ingame-ui run check
npm --prefix apps/game-mod run check
```

The checks have separate meanings:

- CI contract: workflow topology, trigger/concurrency policy, cross-OS aggregate
  gate, action pinning, and hosted/exact-game boundary;
- component identity: path-scoped Git provenance plus component tree, source
  digest, contract digest, version, and clean-worktree reporting;
- BOM: component source identities, versions, public package pins, retained
  runtime/artifact evidence, and explicit non-claims agree;
- boundary: component dependency direction, active predecessor references,
  local-path leakage, source completeness, and admitted workspace graph;
- migration history: imported predecessor histories still have their exact
  original tree/parent relationship; this is archival integrity, not runtime
  qualification;
- Connector check: public contract/SDK/package/docs/CLI/release tooling and
  portable Connector-local checks;
- Host Runtime check: lifecycle, package, Python consumer, and Host tests;
- Annotator `test`: portable recorder and workstation-tool tests;
- Annotator `check`: portable tests plus exact native compilation against the
  locally installed game and current Connector artifact;
- Evidence check: Python typed verification, immutable store/transfer/receiver
  and failure paths;
- Policy Runtime check: typecheck, tests, and package build;
- Workbench/Live UI/Game Mod portable checks: presentation/service/lifecycle
  source tests that do not claim exact game loading.

## Local exact-game and runtime gates

`npm run build` and `npm run check:exact-game` require the exact local STS2
installation. They build or compile game-bound artifacts; a successful build is
not install, load, Live mutation, Human evidence, or qualification.

Use the smallest evidence ladder required by the change:

| Change class | Minimum additional evidence beyond `npm run check` |
| --- | --- |
| docs / governance / pure portable tooling | normally none beyond `project:closeout` and `git diff --check` |
| game-bound C# / native seam / unified Mod source | `npm run check:exact-game` plus a clean exact build and source/artifact identity |
| install / lifecycle / runtime packaging | exact build -> install -> cold load -> `verify:loaded` / owning runtime checks -> rollback readiness |
| Human recorder / causal semantics | exact runtime identity plus the bounded Human canary/audit required by the owning evidence contract |
| release | advertised build/package/install/load/runtime gates plus rollback and release evidence; Human/scientific gates only when claimed |

Do not promote a lower row into a higher one. A green GitHub workflow proves
source/test only.

## Merge provenance and component identity

Current component `source_revision` is path-scoped Git commit provenance derived
from `git log -1 -- <component path>`. Component tree and source/contract
digests are separate semantic/content identities.

That distinction has one important Git consequence:

- a normal merge commit preserves the topic commit as the path-scoped component
  source revision;
- squash or rebase integration rewrites that commit provenance even when the
  resulting component tree and content digest are byte-for-byte identical.

The component-identity regression suite proves both cases. Therefore, while the
current BOM/runtime provenance schema carries commit-based component
`source_revision`, **PRs that change any component source path must use a normal
merge commit**. Do not use GitHub `Squash and merge` or `Rebase and merge` for
those PRs. A docs/governance-only PR that changes no component source may still
be squashed.

If the repository later replaces commit provenance with a different stable
identity contract, change the tests, BOM contract, workflow guidance, and merge
policy together. Do not merely weaken `check:bom` after a squash-induced drift.

## Portability notes

Root Host wrappers retain an explicit nested-npm `--` boundary so profile,
endpoint, and experimental-evidence arguments reach the owning Host CLI on
Windows and POSIX shells.

The Host profile-template test pins its captured file inventory. This prevents
Node-version-specific recursive-copy filter behavior from admitting runtime-only
Windows `logs` or `sentry` files into a reusable profile template. Workspace
package tests use Node's standard automatic discovery rather than depending on
shell-expanded `*.test.mjs` globs, which Windows npm does not expand on the
supported Node 20 baseline.

## Evidence Ladder

Evidence levels are ordered but never implied:

```text
source -> test -> build -> package -> installed -> loaded
       -> Live mutation -> journey -> human_validated -> qualified
```

Predecessor reports and fixtures can test mechanics or migration assumptions,
but cannot qualify a different Platform artifact. Local `.local/` evidence is
not documentation and must not be committed.
