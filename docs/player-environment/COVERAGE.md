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
real visible controls exist. Card rewards likewise remain settling while their
visible card holders are mounting but not yet clickable; an already-enabled
Skip control cannot prematurely turn that partial action set into authority.

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

Source `e065102...` has 130 exact-game Host tests plus portable SDK, package,
contract, boundary, CLI, Python and documentation checks. Its reproducible
`c1877f1a.../64765ea1...` artifact was built, installed and cold-loaded.
Exact runtime evidence proves card-reward incomplete-catalog settling with no
authority and the subsequent complete four-action catalog. Two shipped
Reference terminal journeys exercised the same artifact with zero unknown.
This is named operational coverage, not exhaustive interaction qualification.

## Unsupported

There is no arbitrary click/reflection, visual computer-use fallback, Headless
process lifecycle or profile isolation, Training authority, hidden-state
projection or arbitrary-version/Mod compatibility claim. Retired V2/V3
protocols are not fallbacks.
