# Standalone C1 Targeted Runtime Gates

> Historical evidence note: the named local source object is unavailable from
> both public repositories and retained local object databases. This record is
> predecessor diagnostic evidence only and does not qualify the repaired public
> source.

Date: 2026-08-13

Status: intermediate standalone artifact evidence; not a release seal

## Exact Identity

- source: `b050c46ffa8dc3317e66c261175f92ac8e7d3cb4` (clean)
- Player Environment source digest:
  `a8aaf5c5a19db814c76bbd2f7443f5109005514070193b6294220400a1ce374c`
- protocol: `1.0-rc.2`
- DLL SHA-256:
  `6dd0febd16f2a4e7eac94606ec5ea5339cddca5de0bec6ad16423364b0229618`
- Module MVID: `9e9d891f-eaf3-4d93-97fa-1aed1523ee8b`
- runtime instance: `ba5a654c4fbc48eb8f2abae81b714254`
- game: `v0.110.1/db5d3552`, main assembly hash `-959015736`
- Modset: `exact_player_environment_only`; only `STS2_MCP` loaded
- build, installed and loaded SHA/MVID/source/protocol: exact match

The loaded process was verified through capabilities and
`verify-loaded-artifact`; disk installation alone was not counted as loaded
evidence.

## Directly Exercised

The following gates were exercised against that exact runtime through the
Player Environment REST contract:

- current `run_deck` Read returned the complete 11-card deck;
- a fabricated stale Read token was rejected with HTTP 409;
- optional `native_pages.v1` run-deck open/read/return restored the exact input
  owner, rejected a false runtime binding, and created no mutation authority;
- one Scroll Box card bundle exposed six state-bound `surface_card` Reads;
- current linked detail succeeded and the prior-snapshot linked detail was
  rejected as stale;
- card-bundle preview, cancel back to choosing, reselect, preview and native
  confirm all succeeded; the run deck changed from 11 to 14 cards;
- the same mutation `request_id` returned the same Receipt on duplicate submit
  and poll without a second delivery;
- a second controller was rejected with HTTP 409;
- a deliberately stale BoundAction returned structured `not_delivered` with
  `stale_snapshot`; polling the same request returned the same Receipt;
- event-to-map and map-to-combat transitions exposed bounded `settling` with no
  actions and recovered to an interactive combat snapshot with six actions.

The local game log independently records the selected cards
`ARMAMENTS`, `MOLTEN_FIST`, and `JUGGLING`, the map vote and creation of the
first combat room. Raw logs and ad hoc request output are local evidence and
were not committed.

## Defects Closed Before This Artifact

- the Host now identifies its stable Mod ID and fails closed for a non-exact
  Modset;
- structured 409 Receipts survive the TypeScript client boundary;
- card-bundle preview binds the exact selected native bundle;
- card-bundle cards advertise linked `surface_card` Reads;
- known room mount transitions use bounded settling instead of premature
  unsupported;
- Host configuration resolves the stable `STS2_MCP.conf` filename.

## Limits And Non-Claims

- This was a targeted partial journey, not an ordinary completed run.
- It does not qualify any source, build, MVID or runtime created after
  `b050c46`.
- It does not prove arbitrary game versions, Modsets or unsupported UI shapes.
- A delivery Receipt was not treated as business completion.
- The final standalone source still needs its own build/install/cold-load
  identity, affected runtime gates and ordinary journey before freeze/release.
