# Architecture

## Product Boundary

STS2 Connector turns the real game's current fair-player state and native UI
inputs into a constrained Player Environment. STS2 remains authoritative for
rules, RNG, objects, effects and Commit paths.

```text
STS2
-> LiveHost observation
-> NativeUi binding/execution
-> PlayerEnvironment contract
-> loopback REST
-> optional SDK or MCP transport
-> consumer
```

The Connector is not a strategy engine, simulator, Headless process manager,
UI automation system, or source-code interpreter. The same embedded Host can
report `live_ui` or `headless` according to the actual Godot display driver;
launching that process, save/profile policy and lifecycle belong to a Host
launcher such as STS2-headless.

## Ownership

| Layer | Owns | Must not own |
|---|---|---|
| `LiveHost` | one current input owner; stable fair-player facts | public authority; consumer strategy |
| `NativeUi` | exact native objects, controls, operands, rediscovery and native input | wire identities; guessed business rules |
| `PlayerEnvironment` | Snapshot, Interaction, Referent, Read, BoundAction, Receipt, stale semantics | hidden state; second legality/effect model |
| `Authority` | artifact/runtime/environment identity, one controller, request idempotency | UI legality or outcomes |
| REST/MCP | bytes and endpoint translation | semantics or authority |
| optional Host control | process-local, runtime-bound graceful shutdown requested by a supervisor | Player Environment actions, gameplay authority or remote lifecycle control |
| TypeScript SDK | strict decoding, HTTP and controller session mechanics | strategy, normalization, legality or retries of unknown delivery |
| consumer | strategy, prompts, search, memory and progress interpretation | native operands or mutation authority |

## Canonical Contract

`contracts/player-environment-contract.json` is the machine-readable protocol
inventory. C# records implement the wire. TypeScript schemas independently
reject malformed responses. `npm run check:contract` keeps protocol version,
routes, schemas, verbs and hard-shell invariants aligned. Neither the SDK nor a
consumer may extend authority by accepting more than the Host publishes.

The default-disabled `/api/host-control/shutdown` route is outside this
canonical contract. A Host supervisor may enable it for one process with a
256-bit environment token, then request shutdown bound to the loaded runtime
instance. The Host invokes STS2's native `NGame.Quit()` save/cache/exit path.
The token is not published through capabilities, SDK, MCP, Snapshot, Read or
BoundAction.

## Observe, Read, Interact

**Observe** returns stable current facts, the current Interaction, Referents,
advertised Reads, completeness, and a finite BoundAction projection.

**Read** retrieves stable normal-player information that need not be repeated
in every hot Snapshot. A Read is advertised, Snapshot-bound, runtime-bound,
read-only and non-authorizing.

**Interact** submits one opaque BoundAction already present in the current
complete projection. The public action names Referents; exact native operands
remain Host-local. The Host re-observes and revalidates before native delivery.

Only a non-empty complete BoundAction projection is `interactive`. Truncated,
unknown-owner, stale, unsupported, identity-incomplete or controller-conflicted
states have no mutation authority.

## Delivery Lifecycle

`request_id` is idempotent within the runtime. The first attempt produces one
action-local Receipt; a duplicate returns that Receipt and cannot deliver the
input twice.

- `delivered`: the game-owned input path accepted delivery.
- `not_delivered`: no mutation was delivered; the reason determines whether a
  newly observed decision may proceed.
- `unknown`: delivery may have happened; automatic retry is forbidden.

Receipt delivery is deliberately narrower than business completion. Consumers
use the immediate successor and later observation for progress without
recreating game rules.

## Information Boundary

The information policy is fair-player only. Stable visible facts and
player-openable details are projected; hidden RNG, true draw order, unrevealed
future content and private game state are excluded. Unknown tooltip/subtype or
owner drift fails the affected surface closed. See
[Information Closure](INFORMATION_CLOSURE.md).

The default-off `native_pages.v1` profile may open/read/return fixed native
pages as operator evidence. It reserves input while active and creates no
BoundAction or controller authority.

## Identity And Compatibility

Capabilities expose exact Host artifact SHA-256/MVID/source revision, runtime
instance, game identity, main assembly identity and Modset. Observation and
mutation compatibility are explicit. A new game binary, Modset or Host artifact
does not inherit old Live evidence automatically.

The installed Mod implementation ID and DLL remain `STS2_MCP` for upgrade and
rollback compatibility. Current source naming is `STS2Connector`; MCP is only
an optional transport.

## Extension Rules

A new supported interaction needs visible extraction, exact private binding,
finite projection, execute-time revalidation, tests and exact-runtime evidence.
A data-only content instance composed entirely from an existing native UI shape
should require no consumer strategy or protocol fork. An unknown UI shape or
unknown semantics may still be observed, but it receives no guessed action.

No extension may introduce index/coordinate mutation, arbitrary method calls,
consumer operands, silent fallback, a second controller, hidden information, or
automatic retry after `unknown`.
