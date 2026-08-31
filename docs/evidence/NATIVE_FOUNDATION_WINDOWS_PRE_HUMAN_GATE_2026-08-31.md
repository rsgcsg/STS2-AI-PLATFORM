# Native Foundation Windows Pre-Human Gate - 2026-08-31

## Evidence Level

This report records fresh Windows source checks, build, deployment, rollback,
cold-load, bounded visible Connector checks, shipped-headless H0, and a
main-menu-only visible/headless semantic comparison. It does not claim Human
gameplay evidence. PR #3 and the earlier macOS Native Foundation candidate
remain historical exact evidence and do not transfer to these Windows bytes.

The branch continued from remote handoff
`0831eefd2a5884586bc199320d3ed91878a33c1f`. Native Foundation, Connector,
Annotator, and unified-Mod implementation source remains anchored at
`a3bcd373e156fb354a6b4947b72c15236457c4b0`. Windows boundary fixes were made
at their owning path/process/package/install layers; no Windows branch was
added to semantic state, `A(S)`, BoundAction, Receipt, successor, Human
evidence, serialization, or canonical-transition semantics.

## Exact Windows Baseline

- platform/architecture: `win32 / x64`;
- STS2: `v0.111.0`, commit `41cef1ea`, release assembly hash `222455745`;
- shipped executable SHA-256:
  `8602c26bffd2937e3841835fd8360ef8e974624a543e05977229fd3d062be231`;
- `sts2.dll` SHA-256:
  `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`;
- `sts2.dll` MVID: `73b63ee0-6c0a-47bb-b0d1-b21f6d94222e`;
- exact installation, profile, Mods, settings, and log paths were resolved by
  repository-owned Windows discovery. Machine-specific absolute paths are not
  committed.

## Windows Artifact And Deployment

- clean build workspace:
  `c667122ee35853f6a4a315871cb4e70383363c8f`;
- artifact SHA-256:
  `a681f8b1b516376a26823114ca42d2dca4c2981c2930e5770872777c3e3bc3a9`;
- artifact MVID: `7c42c4c3-02fb-46c9-ac90-dfb1cf516fdd`;
- installed identity source:
  `a3bcd373e156fb354a6b4947b72c15236457c4b0`;
- build, doctor, deploy, installed/loaded equality: pass;
- production Modset: ordered `loaded_mod_ids = ["STS2_PLATFORM"]`;
- Windows native Mod settings transaction: backup, enable only Platform while
  preserving other entries disabled, exact rollback restore, and redeploy all
  pass;
- final rollback archive:
  `apps/game-mod/.local/deployments/2026-08-30T14-44-51.829Z`.

The rollback drill first restored the previous installed artifact, then the
extended drill restored the exact predecessor native settings bytes as well.
The Windows candidate was redeployed and cold-loaded after both drills. No
predecessor production DLL remains in the shipped Mods directory; existing
Workshop entries remain preserved and disabled.

## Visible Runtime Gate

- host kind: `live_ui`;
- Connector protocol: `1.0.0`;
- Connector runtime:
  `7a1942b652da47d29baf6852f427f924`;
- environment fingerprint:
  `a52b5cc5f903bd1583af6463f098123226a1abf6fd74ca0b2a99b1ef3cd24889`;
- Modset status/fingerprint:
  `exact_platform_modset / e5693d19c7571c1a30a07c2bca584eeced6b64e675bc5fb37acbb1638a1cb86c`;
- Connector ready and execution available: pass;
- complete interactive main-menu Snapshot: pass;
- BoundActions: complete, one current menu action;
- Reads: none advertised and none completed;
- second-controller conflict: HTTP 409;
- deliberately stale request: `not_delivered / stale_snapshot`;
- duplicate request ID: exact same Receipt;
- delivered mutations during this gate: zero.

The initial cold-load found a Windows deployment-layer defect: the production
Mod was disabled while preserved Workshop Mods were enabled in native settings.
The game-mod deployment transaction now owns the exact settings mutation and
rollback. Regression tests prove other entries are preserved, only Platform is
enabled for production load, and rollback restores the original bytes.

## Shipped Headless Gate

The repository root Host wrapper now preserves forwarded profile/build flags on
npm 11; its regression test prevents a second argument parser from silently
dropping exact-profile authority.

The first public Windows-portability run then reproduced a Node 20 filesystem
adapter defect that local Node 24 did not expose: recursive-copy filtering
admitted runtime-only `logs` and `sentry` files to a reusable profile template.
Host source `8543e562aaee880fd7e0c4e41887ce94fbb2bd84` replaces that callback-dependent
filter with an explicit contained file walk and pins the exact captured
inventory. This changes no game artifact or gameplay/evidence semantics.

The same Node 20 Windows run exposed shell-only `*.test.mjs` package scripts
after Host, Annotator, and Evidence had passed. Game Mod, Live UI, and Workbench
now use Node's standard test discovery; a root regression test keeps those
entrypoints portable. This is package/test-harness authority only and likewise
does not change the installed game artifact.

- H0 verdict: pass;
- host kind: `headless`;
- runtime: `49f34fbfbbbc429393be52ce66625d65`;
- artifact SHA/MVID: exact Windows candidate above;
- environment/Modset: exact values above;
- final Snapshot: interactive `main_menu`;
- shutdown: requested through Host HTTP authority, process exit 0, not forced;
- local report:
  `components/host-runtime/.local/evidence/shipped-h0-2026-08-30T14-50-02-025Z/report.json`.

The visible and headless main-menu Snapshots canonicalize to the same digest:

```text
eaf8516dc290509ca8b2a33f098b0d6582842c9be2635accd4c217c2d3dd58e4
```

This is a main-menu-only semantic invariance result. It does not prove Combat,
PlayerChoice, cross-domain, performance, determinism, or Full-Run parity.

## Recorder And Architecture Boundary

Recorder portable lifecycle tests pass. Cold-load initializes the typed
RecordingService at Ready with no open session. New Session, Pause, Resume,
pending-safe Close, and owner-visible page behavior remain part of the bounded
Human runtime gate because the production UI intentionally exposes no second
automation or mutation authority.

The architecture remains:

- Native Foundation owns Combat semantic truth and PlayerChoice lineage;
- Surface/UI owns presentation and input deliverability, not legality;
- Connector alone projects and delivers BoundActions;
- Annotator consumes semantic/lifecycle facts read-only and owns evidence
  correlation, not action authority;
- Host Runtime owns process/profile/exact-runtime lifecycle;
- `Receipt.Successor` is an immediate post-delivery observation, not causal
  `S'`;
- ADR 0004 remains `RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`; no new evidence
  justifies reopening that route.

## Remaining Exact Human Gate

The exact Windows artifact still requires one owner-operated recording that
covers:

1. ordinary card, targeted card, a natural rapid accepted sequence if
   available, one potion, and End Turn;
2. a real typed/generated PlayerChoice with parent pause-choice-resume lineage,
   such as Survivor/discard;
3. `lethal -> Reward -> CardReward -> Map` domain transitions;
4. Recorder New Session, Pause, Resume, and pending-safe Close.

Pass requires normal gameplay to remain unblocked; exact native membership and
lineage to remain coherent; every accepted action to receive one disposition;
the domain owner to transition without creating action authority; and the
closed session to pass the repository native-semantic audits. Any gameplay
block, false root, missing/duplicate disposition, fabricated catalog, incorrect
domain owner, recorder lifecycle failure, or audit failure is fail-closed and
must be assigned to its owning layer before another canary.
