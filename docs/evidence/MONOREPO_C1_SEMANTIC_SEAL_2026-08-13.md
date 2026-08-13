# Player Environment C1 Semantic-Seal Closeout

Date: 2026-08-13

## Scope And Evidence

The pass began from `human_equivalent_connector@1833d8fde70b05fd50d2ac4cbb14325ef968e2f0`
with a clean worktree aligned to its upstream. The semantic-seal source is the
commit containing this closeout; the standalone extraction records its exact
SHA separately.

Directly verified in this pass:

- current C#, TypeScript, Python, machine contract, current docs and CI;
- 87 exact-game Host tests against local `v0.110.1/db5d3552` assemblies;
- 68 Re tests, typecheck and production build;
- contract, boundary, current-truth, Markdown, CLI, identity and Python checks;
- direct metadata audit of local `sts2.dll` SHA-256
  `7c446efabf80614c429b5088e87101423aa5bb4c04fc3e73393261f6e6d404fd`
  and MVID `c0f649b8-8d57-4a9c-8b07-21aece97dca0`.

The locally available latest run is only
`run-20260811124838-4br6z3`: one decision on an older `1.0-preview.5`
artifact with incomplete Agent source identity. It is historical diagnostic
coverage, not C1 evidence. The 289-decision
`run-20260812150244-h6fbo1` is a repository-recorded predecessor claim; its raw
run is not present on this device and was not independently recomputed here.

## Defect Classification

| Finding | Owner | Resolution |
|---|---|---|
| completed-event and treasure handoff reported unsupported | C lifecycle classification | already fixed by `1833d8f`; 87 Host tests verify bounded positive/negative cases, new Live evidence still required |
| two independently built Read catalogs | C contract ownership | fixed; Snapshot visibility and advertised Reads consume one `PlayerVisibilityCatalog` result |
| current internals and public diagnostics retained `Inspection` vocabulary | C naming/contract | fixed as Read; protocol advanced to `1.0-rc.2` |
| tooltip subtype closure was only a documentation claim | C information boundary | fixed for the exact audited game assembly; both concrete `IHoverTip` types are projected and subtype drift fails an exact-game test |
| retired V2/V3 empty ownership trees failed the boundary check | repository hygiene | removed; boundary check passes |
| offline baseline report parsed live connector settings | A evidence tooling | fixed; offline run reading now depends only on the data directory |
| local `.env.local` contains retired settings | operator environment | not committed or rewritten; live commands still reject them explicitly |
| latest local run has one step and incomplete Agent identity | evidence quality | not a C defect; excluded from qualification |

No provider or strategy failure in the local evidence was reclassified as a C
defect. No declared unsupported interaction was made executable.

## Information Closure

Stable facts, bounded player-reachable list/grid contents, typed tooltip/card
preview semantics, and the four state-bound Reads form the C1 information
contract. Gesture replay is not required to expose information already
obtainable through a supported hover or scroll path. Current focus state and
generic unknown hover/scroll containers remain explicit partial/unsupported
scope. Hidden RNG, true draw order and future content remain excluded.

## Ownership Verdict

The production path is one-way:

```text
LiveHost visible facts/owner
-> NativeUi private binding and native input
-> PlayerEnvironment Snapshot/Read/BoundAction/Receipt
-> REST or optional MCP
-> consumer projection
```

Game/Host legality, operands and effects were not moved into Re. Reads remain
non-authorizing. Complete finite BoundActions, stale rejection, execute-time
revalidation, one controller, idempotency and unknown-no-retry remain intact.

## Version And Migration Boundary

- Player Environment protocol: `1.0-rc.2`.
- Host release candidate version: `1.0.0-rc.1`.
- `STS2_MCP` remains the compatibility implementation/mod ID for this major
  line; product and repository naming use STS2 Connector / Player Environment.
- Old loaded evidence remains predecessor evidence only.
- Standalone extraction must produce a new source SHA, artifact SHA/MVID and
  runtime identity before any freeze or release verdict.

Verdict: **C1 semantic-seal candidate; ready for resp