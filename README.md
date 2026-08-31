# STS2 AI Platform

STS2 AI Platform is the shared, model-neutral environment foundation for
programs that use the real Slay the Spire 2 runtime. It brings the runtime,
fair-player automation, native-human evidence, evidence logistics, and
operator surfaces into one workspace without merging their authorities.

Platform is not a strategy, model, reward function, training system, research
projection, or second game-rules engine. STPD and other research projects are
independent consumers of versioned Platform contracts. STS2 remains the owner
of rules, RNG, effects, native legality, and Commit.

## How the pieces fit

```text
STS2 game truth
  -> Native Foundation: shared game-side semantic decisions and lifecycle
  -> Host Runtime: process lifecycle, isolation, recovery, exact identity
  -> Connector: fair-player Snapshot, Read, finite BoundAction, Receipt, successor
  -> external strategy and research consumers

Native human play
  -> Human Annotator: witness correlation and immutable recording evidence
  -> Platform Evidence: typed verification, store, transfer, receiver receipts
  -> external evidence consumers

External policy adapter
  -> Policy Runtime: model-neutral mode/controller/delivery lifecycle
  -> Connector-owned finite actions

Workbench and Platform Live UI
  -> typed status and bounded application commands, never domain authority
```

| Path | Responsibility |
|---|---|
| `components/native-foundation` | Shared STS2 semantic decisions, lifecycle, and owner lineage |
| `components/connector` | Fair-player Player Environment and native action binding |
| `components/host-runtime` | Runtime discovery, isolation, lifecycle, recovery, and qualification support |
| `components/annotator` | Native-human witness recording, audit, and immutable session bundles |
| `components/evidence` | Typed artifact verification and immutable evidence logistics |
| `components/policy-runtime` | Model-neutral policy and controller lifecycle over Connector contracts |
| `apps/workbench` | Typed operational status and bounded Policy Runtime commands |
| `apps/ingame-ui` | In-game status and application controls |
| `apps/game-mod` | The one production STS2 Mod build, deploy, load, and rollback path |

The detailed dependency graph and ownership matrix live in
[Architecture](docs/ARCHITECTURE.md) and [Components](docs/COMPONENTS.md).

## Start here

- New to the project: follow the [New Engineer Guide](docs/NEW_ENGINEER_GUIDE.md).
- Making a change: read [AGENTS.md](AGENTS.md), then the
  [development workflow](docs/DEVELOPMENT_WORKFLOW.md) and the owning
  component's guide.
- Starting a Codex task: run `npm run project:context` or select a component,
  for example `npm run project:context -- --component connector`.
- Checking current claims: read [Status](docs/STATUS.md) and the bounded
  [current context](docs/memory/CURRENT.md).
- Debugging runtime behavior: use the Host Runtime or Game Mod guide, then the
  exact evidence named by Status.
- Reviewing proof: read [Testing and Evidence](docs/TESTING.md), then load only
  the relevant dated evidence report.

The [Document Map](docs/DOCUMENT_MAP.md) is the short routing index. Historical
evidence remains discoverable without being part of the default reading path.

## Portable quick start

The root workspace requires Node.js 20 or newer. Component checks also discover
the required .NET and Python toolchains.

```bash
npm ci
npm run doctor
npm run check
```

These commands prove portable source/test facts only. They do not prove an
exact build, package, installation, loaded Mod, live mutation, native-human
origin, journey, or qualification. Evidence levels never transfer to another
artifact merely because source was merged or rebuilt.

## Development boundary

Normal work starts from current `origin/develop`, uses one short-lived topic
branch and worktree, and targets `develop` through a pull request. `main` is the
governed release/hotfix landing line. Never commit proprietary game files,
decompiled source, raw human data, `.local/`, credentials, model weights, or
installed artifacts.

See [Project System](docs/PROJECT_SYSTEM.md) for documentation, style, Skill,
and anti-drift governance; [Contributing](CONTRIBUTING.md) for the concise
contributor contract; and [Security](SECURITY.md) for reporting concerns.
