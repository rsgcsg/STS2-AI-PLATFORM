# Platform Development Workflow

This document governs changes to the Platform Foundation repository. It is the
canonical Git and collaboration workflow for humans and agents.

## Project position

```text
STS2 AI Platform
├── Platform Foundation (this repository)
│   ├── Host Runtime and exact identity
│   ├── Connector / Player Environment
│   ├── Human Annotator and Evidence
│   ├── model-neutral Policy Runtime
│   └── Workbench, Live UI and unified Mod
└── Research Projects
    └── STPD (independent research repository and consumer)
```

Platform is the shared, model-neutral foundation. STPD is an independently
versioned research project built on Platform public contracts; it is not a peer
platform. Platform must not depend on STPD checkpoints, rewards, training,
research projections or policy semantics. The repositories are not submodules
and do not share branches.

## Governance migration

The public `main` at
`5604050ef0e0f55f13bf2fdb720e5c215d774fd5` is preserved as
`baseline/pre-governance-platform-20260827`. It is a green historical
integration snapshot, not a stable-release claim. No history was rewritten.

`develop` was bootstrapped from that exact commit. Until the first governed
release completes its release gates, `main` remains the frozen pre-governance
baseline and all normal work targets `develop`. The first governed release is:

```text
develop -> release/<version> -> release gates -> main -> version tag
```

Only after that merge does `main` acquire stable-only meaning.

## Branches

- `main`: stable release landing line; accepts only `release/*` and urgent
  `hotfix/*` pull requests.
- `develop`: the single long-lived integration line and normal PR target.
- `feature|fix|refactor|evidence|experiment|docs|chore/<scope>/<name>`:
  short-lived topic branches from current `develop`.
- `release/<version>`: release stabilization, identity, BOM, documentation and
  blocker fixes only.
- `hotfix/<name>`: urgent stable-line fix from `main`; merge the same fix back
  into `develop` and any active release branch.

Scopes normally name an owning component: `native-foundation`, `connector`, `host-runtime`,
`annotator`, `evidence`, `policy-runtime`, `game-mod`, `live-ui`, `workbench` or
`platform`. Do not create permanent component develop branches.

## Work and pull requests

Before editing:

1. fetch and prune `origin`;
2. inspect `origin/develop`, recent commits and open PRs;
3. record the base SHA and check for overlapping files/contracts;
4. create one topic branch and, for concurrent agents, one worktree;
5. keep one primary responsibility per PR.

Normal topic PRs target `develop`; release and hotfix PRs follow the branch
rules above. Prefer squash merge for topic PRs. Preserve an explicit release
boundary when merging a release branch. Refactors and behavior changes should
be separate when either is substantial.

Every PR records repository, base branch/SHA, workstream, owner, scope,
non-goals, affected contract, cross-repository dependencies, exact identities,
evidence level, rollback and remaining non-claims. CI green proves source/test
only. It never promotes build, installed, loaded, runtime, Human or
qualification evidence.

## Multi-human and multi-agent work

One writer owns one topic branch/worktree. A handoff records repository,
branch, base SHA, HEAD, task/non-goals, changed files, checks, pending work and
risks. Never overwrite a changed `develop`, BOM, manifest or contract merely to
make a stale branch pass. Rebase or merge only after understanding the new
facts.

Active workstreams belong in pull requests and the bounded
[Current Context](memory/CURRENT.md), not in this durable workflow.

## Cross-repository dependencies

STPD consumes a released Platform package or an explicitly non-stable candidate
pinned by exact source SHA, artifact/package digest, protocol and manifest.
Floating `Platform/develop` is forbidden. A cross-repository change uses two
independent PRs:

```text
Platform contract/candidate PR and exact identity
  -> STPD manifest/config pin PR
  -> STPD adapter/admission/research verification
```

Platform release does not depend on STPD model quality. STPD qualification may
depend on one exact Platform release/candidate.

## Evidence and release gates

The owning component determines checks. At minimum:

```bash
npm ci
npm run check
npm run project:closeout
git diff --check
```

Game-bound changes also run `npm run check:exact-game`, an exact build and the
required exact-runtime gates. Raw sessions, local artifacts, installed files,
game binaries, decompiled source, secrets and model weights stay outside Git.
Reviewed evidence documents contain exact identity, aggregate results,
reproducible commands, rollback and non-claims.

Release branches may change only release blockers, versions, packages,
manifests, BOM, release notes and exact closeout evidence. A release reaches
`main` only when its advertised scope is internally consistent and rollback is
available. Component version, protocol, SDK/package version, Platform BOM and
artifact identity remain separate.

## GitHub enforcement

Both `main` and `develop` should require pull requests, the `portable` status
check and resolved conversations, and should block deletion and force pushes.
Approval requirements may be raised as the reviewer pool grows; an owner must
not use administrator bypass as the normal workflow. Repository settings and
their actual enforcement state are audited separately from this document.

This workflow combines short-lived branches and small self-contained changes
with an explicit integration line and release stabilization because Platform
runtime evidence often matures after source tests. It intentionally does not
copy heavyweight GitFlow. See [GitHub PR standardization](https://docs.github.com/en/pull-requests/reference/managing-and-standardizing-pull-requests),
[short-lived feature branches](https://trunkbaseddevelopment.com/short-lived-feature-branches/)
and [Google's small-change guidance](https://google.github.io/eng-practices/review/developer/small-cls.html).
