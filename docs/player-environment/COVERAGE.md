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
The no-owner frame between a delivered standard-run entry and the mounted run
is a bounded `settling` lifecycle, not a transient unsupported Surface. A real
unknown owner remains fail-closed. The exact event-room model also settles while
its native room node mounts; this does not authorize an event option before the
real visible controls exist.

## Information

Implemented:

- persistent run/player summary;
- tagged current interaction content;
- visible referents and directly observed state;
- `run_deck`, `combat_piles`, `shop_catalog` and `surface_card` reads;
- default-off native-page evidence for run deck, combat draw/discard/exhaust
  piles and shop catalog.

The exact `v0.111.0/41cef1ea` assembly exposes only `HoverTip` and
`CardHoverTip` as concrete `IHoverTip` implementations; both are typed and an
exact-game test rejects subtype drift. Supported bounded lists/grids project
their complete player-reachable logical collections without requiring scroll
gestures.

Partial/unsupported: current keyboard/controller focus, generic hover/scroll
gestures, future unknown tooltip subtypes and native pages outside the fixed
profile.
See the repository [Player Environment Information Closure](../INFORMATION_CLOSURE.md).

## Evidence

The repaired public source has exact-game Host tests and portable SDK, package,
contract, boundary, CLI, Python and documentation checks. It has no current
loaded or Live evidence until a clean commit is built, installed and cold-loaded.

The targeted runtime notes for local-only sources `b050c46...` and `f104e16...`
remain useful predecessor diagnostics, but those Git objects are not fetchable
and cannot qualify the repaired source. Monorepo journeys are predecessor-only.

## Unsupported

There is no arbitrary click/reflection, visual computer-use fallback, Headless
process lifecycle or profile isolation, Training authority, hidden-state
projection or arbitrary-version/Mod compatibility claim. Retired V2/V3
protocols are not fallbacks.
