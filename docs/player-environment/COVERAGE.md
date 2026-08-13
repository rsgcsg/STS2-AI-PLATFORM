# Player Environment Coverage

## Interaction

LiveHost readers cover menu/run entry, combat cards/potions/end turn, map,
event, rewards/card rewards, shop, rest, treasure, generated choices, combat
hand/piles, upgrade/removal/transform/enchant/bundle selectors and game over.

Visible state is explicitly projected before finite action projection; native
owner/slot/control IDs do not enter public Surface facts. Referents are derived
only from those facts. A complete bound-action
catalog expresses exact public subject/argument combinations while native
objects remain Host-local. Unknown owner or target fails closed. Unknown
business provenance alone does not suppress an exact visible native control.

## Information

Implemented:

- persistent run/player summary;
- tagged current interaction content;
- visible referents and directly observed state;
- `run_deck`, `combat_piles`, `shop_catalog` and `surface_card` reads;
- default-off native-page evidence for run deck, combat draw/discard/exhaust
  piles and shop catalog.

The exact `v0.110.1/db5d3552` assembly exposes only `HoverTip` and
`CardHoverTip` as concrete `IHoverTip` implementations; both are typed and an
exact-game test rejects subtype drift. Supported bounded lists/grids project
their complete player-reachable logical collections without requiring scroll
gestures.

Partial/unsupported: current keyboard/controller focus, generic hover/scroll
gestures, future unknown tooltip subtypes and native pages outside the fixed
profile.
See the repository [Player Environment Information Closure](../INFORMATION_CLOSURE.md).

## Evidence

Standalone commit `b050c46ffa8dc3317e66c261175f92ac8e7d3cb4` has exact
loaded and targeted Live evidence for Read current/stale behavior, the fixed
run-deck native page, linked card detail, card-bundle reversible selection,
single-controller refusal, request idempotency, stale-action refusal and
bounded room-to-combat settling. See the
[dated closeout](../evidence/STANDALONE_C1_TARGETED_RUNTIME_GATES_2026-08-13.md).

That evidence does not transfer to a later DLL and does not include an ordinary
complete same-artifact journey. Monorepo journeys remain predecessor evidence
for their exact old artifacts.

Current Host source `f104e16b6585599e6acf5481c255fa74ea1d221e` has its own
clean build/install/load identity and targeted Live gates, including corrected
combat-pile hidden-information completeness. Its exact identity and remaining
Journey/rollback gates are recorded in the
[final-source closeout](../evidence/STANDALONE_C1_FINAL_SOURCE_RUNTIME_GATES_2026-08-13.md).

## Unsupported

There is no arbitrary click/reflection, visual computer-use fallback, Headless
Host, Training authority, hidden-state projection or arbitrary-version/Mod
compatibility claim.