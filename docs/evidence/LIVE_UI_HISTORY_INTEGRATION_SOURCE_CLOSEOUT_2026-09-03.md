# Live UI historical-line integration source closeout — 2026-09-03

> Historical baseline note: the runtime/UI evidence in this report belongs to
> the predecessor integration candidate. PR #15's subsequent UI convergence
> candidate at source `58e56cd6b8a0f29c46398898b9d68756fa537dfb` removes the
> six-page dashboard and collapse state, and its new bytes require fresh exact
> build/install/load evidence and a new owner Human UI canary. No Human or
> artifact qualification from this report transfers.

## Scope and exact topology

This report records the read-only archaeology and selective integration of the
historical `ui-testing` line, plus the bounded owner-operated Human UI canary
performed on the exact cold-loaded candidate.

- refreshed `origin/develop`: `6667b24828af41b38a6ad2f66c3d160397a25049`
- preserved `origin/ui-testing`: `99bf05c982bf604d8f7296724e09e66c539d249c`
- merge base: `2e1a5b67eef25faa897602d237a16b6698127af0`
- divergence commit: `refactor: close pre-Full-Run platform hardening (#9)`,
  committed `2026-09-01T19:28:30+10:00`
- exact ahead/behind at preflight: five develop-only commits and 29
  ui-testing-only commits
- open pull requests targeting `develop`: none
- overlapping active remote topic branches: none
- active ruleset: pull request, resolved conversations, strict `portable`, no
  deletion or non-fast-forward update; this component-source PR additionally
  requires a normal merge by the repository provenance contract

`git merge-tree` showed conflicts in Live UI, Annotator application/runtime
sources, mutable status, and BOM. A direct merge was therefore rejected before
production edits.

## Verdict

`SELECTIVE_PORT_FROM_CURRENT_DEVELOP`

The current-compatible presentation commits were replayed with `-x` from the
exact refreshed develop base. Historical BOM-only commits and mutable status
claims were excluded. The old action-feed product intent was reimplemented
against the post-PR #11 owner: `SemanticBoundaryTracker` remains the only Human
causal/successor authority, while the typed application stream receives a
read-only projection only after the owning semantic event is persisted.

## Complete historical disposition

| Historical commit | Original purpose / paths | Current dependency | Disposition |
|---|---|---|---|
| `7cdc178` | Workspace presentation, UI docs/tests, old BOM/status | Current Godot UI and typed services | `KEEP_BY_CHERRY_PICK` for UI/docs/tests; old BOM/status excluded |
| `6bde5be` | Interaction spec and old BOM | Current presentation vocabulary | `KEEP_BY_CHERRY_PICK` for spec; old BOM excluded |
| `8ca6d01` | Title-bar drag input | Presentation only | `KEEP_BY_CHERRY_PICK` |
| `31eec65` | Title-bar repair BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `da7ef85` | Single Workspace ownership | Current presentation boundary | `KEEP_BY_CHERRY_PICK`; mutable status excluded |
| `2811261` | Unified Workspace BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `ab3acc1` | Drag routing through one surface | Presentation only | `KEEP_BY_CHERRY_PICK` |
| `41c8b2d` | Workspace surface BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `c19713b` | Selected feedback while unavailable | Presentation only | `KEEP_BY_CHERRY_PICK` |
| `e379507` | Style BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `28c90bb` | Old develop-alignment BOM identity | Superseded topology | `DROP_STALE_STATE_OR_IDENTITY` |
| `1fe7adc` | Action Feed plus pre-PR11 Annotator event fields | Superseded causal/runtime implementation | UI intent `REDESIGN_FOR_CURRENT_CONTRACT`; old Annotator patch dropped |
| `85c0e7e` | Action Feed BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `21d006b` | Session-local feed reset | Current event/session projection | `KEEP_BY_CHERRY_PICK` |
| `52819c2` | Feed isolation BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `f9af5fc` | Remove compact HUD/second presentation owner | Current presentation boundary | `KEEP_BY_CHERRY_PICK`; mutable status excluded |
| `859fccf` | Sole Workspace BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `96ec257` | Recorder as compact peer tab | Current typed recording controls | `KEEP_BY_CHERRY_PICK`; current PR11 status vocabulary retained |
| `387a31a` | Recorder-tab BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `6595b13` | Bounded viewport and tab collapse | Presentation only | `KEEP_BY_CHERRY_PICK` |
| `57c38e8` | Bounded Workspace BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `dbb4526` | Preserve feed scroll across status updates | Presentation only | `KEEP_BY_CHERRY_PICK` |
| `b5cdcde` | Feed-scroll BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `881f7a4` | Lifecycle aggregation and fixtures | Current event projection required | `PORT_SEMANTICALLY`; UI aggregation/tests retained and rebound to `RootPending` |
| `536fb94` | Lifecycle-feed BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `d555753` | Shell-neutral .NET fixture entrypoint | Current Windows/Linux CI | `KEEP_BY_CHERRY_PICK` |
| `6406660` | Portable-test BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |
| `af001a7` | Require canonical root identity for aggregation | Current semantic `RecordId` | `KEEP_BY_CHERRY_PICK` |
| `99bf05c` | Exact-root BOM identity | Historical identity | `DROP_STALE_STATE_OR_IDENTITY` |

No Full-Run semantics, Connector legality/delivery, policy semantics, STPD, or
historical evidence identity was ported.

## Current ownership result

The predecessor UI was one hidden-by-default `CanvasLayer` presentation owner.
`K` opened one bounded Workspace; Recorder was the default peer tab. Layout
persistence, drag/resize/reset, tab-body collapse, scrolling, toasts, and
click-through were presentation state. Recorder buttons called
`RecordingApplicationService`; policy buttons called Policy Runtime application
commands. The UI had no Player Environment submit path. The current PR #15
convergence keeps the same authority boundary but presents exactly two peer
surfaces, Agent Run and Human Recorder, with no body-collapse product state.

For the Action Feed, `RecordingActionProjection` copies display fields from the
already-bound `RecordedBoundAction`. `RootPending` is emitted only after the
current semantic evidence batch containing `ActionAccepted` is appended.
Terminal and recorded updates use the same semantic `RecordId`. The projection
does not call `Accept`, settle a boundary, append a decision, or change a
disposition; a gap causes a status refresh and an explicitly incomplete feed.

## Automated and exact-runtime evidence

- `npm ci`: pass, zero vulnerabilities
- `npm --prefix apps/ingame-ui run check`: 17/17 pass
- `npm --prefix components/annotator run test`: Core 136/136 plus 21 Node
  checks pass
- `npm run check`: pass, including BOM 8/8 and Game Mod 53/53
- `npm run check:exact-game`: pass against STS2 `v0.111.0` / `41cef1ea`
- built/installed/loaded artifact SHA-256:
  `04fcb6e71a1a077578f76c18910df8e06a1e8c89198bef144cf98d78310cb4dc`
- artifact MVID: `40fd1f1f-0ca4-49ee-975a-2ae1fb91c705`
- process: `37284`
- loaded Modset: only `STS2_PLATFORM`
- Connector protocol: `1.0.0`; runtime instance:
  `9a30638bc08e4009890015008850ecfd`
- environment fingerprint:
  `8bd511f1b8a6fa2612590cbf187d5670424b382eff9d0a9f867829f43f5521bb`
- Modset fingerprint:
  `65242d5d824e76fa6efcea4e9b047d862b7186a06e2786d50bf4b1ca91a062a8`
- rollback:
  `apps/game-mod/.local/deployments/2026-09-02T16-32-21.278Z`

The exact loaded source revisions are Annotator and Live UI `b36790cabc85814f33a35e22dc604591c4e995ed`.
The current BOM binds their current component identities while retaining old
loaded/Human evidence as historical, non-transferred facts.

## Bounded Human UI canary

The owner attested PASS on 2026-09-03 for the requested shortest meaningful UI
canary on the exact candidate above. The exercised scope covered hidden-default
and `K`/Escape/Close visibility, the sole bounded Workspace, Recorder as a peer
tab, drag/resize/reset/tab-body toggle and scrolling, gameplay click-through,
Recorder controls, and the read-only Action Feed lifecycle presentation. No
historical `ui-testing` Human result was transferred.

The corresponding current-format session is
`session-20260902T164749Z-e9823523f1234dca8b5038495d4ff46e`. Its runtime status
binds PID `37284`, runtime instance `9a30638bc08e4009890015008850ecfd`, and the
artifact SHA/MVID above; it ended `recording_closed`. Repository-native audit
passes with nine valid records, zero invalid records, and zero invalidations.
That audit corroborates format/integrity only: Human origin remains
owner-attested, and the bounded UI canary does not qualify unexercised action
families or semantic correctness. The game was safely closed after the canary,
so a later `verify-loaded` correctly reported no running STS2 process; the
pre-interaction cold-load identity remains the load evidence.

## Remaining non-claims

- the bounded Human UI canary is not exhaustive gameplay, semantic, or durable
  evidence qualification
- repository-native session audit does not independently prove Human origin or
  non-interference
- no Shop/Event/Rest/Full-Run expansion is claimed
- no policy or STPD qualification is claimed
- `origin/ui-testing` remains untouched and may be considered for archival only
  after this integration is normally merged and separately reviewed
