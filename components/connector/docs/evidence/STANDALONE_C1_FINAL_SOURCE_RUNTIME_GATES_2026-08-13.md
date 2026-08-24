# Standalone C1 Final-Source Runtime Gates

> Historical evidence note: the named local source object is unavailable from
> both public repositories and retained local object databases. This record is
> predecessor diagnostic evidence only and does not qualify the repaired public
> source.

Date: 2026-08-13

Verdict: release candidate; targeted gates passed; freeze/release blocked on
ordinary journey and runtime rollback exercise

## Exact Identity

- Host source: `f104e16b6585599e6acf5481c255fa74ea1d221e` (clean)
- Player Environment source digest:
  `16d41a85c06e872129250967d79fbdf03124c8c1ebbd8d017ee19646bec2e310`
- protocol: `1.0-rc.2`
- built/installed/loaded DLL SHA-256:
  `8540a7ff54b26a1d34e9565636815272a0ad7a13b75cca4439a2d09d611a3157`
- built/installed/loaded MVID: `a4b5cdac-3b9b-444a-8def-9d7a2f58f4a4`
- runtime instance: `91ebf7a4c1e9415d874c6985cc3cfe2c`
- game: `v0.110.1/db5d3552`, main assembly hash `-959015736`
- Modset: `exact_player_environment_only`; only `STS2_MCP` loaded
- rollback backup created at deploy time:
  `.local/deployments/2026-08-13T07-00-30-061Z`

`verify-loaded-artifact --wait` required exact source, protocol, SHA, MVID,
runtime, game and Modset agreement. Installation bytes alone were not counted
as loaded evidence.

## Deterministic Evidence

- 93/93 exact-game Host tests;
- 7/7 TypeScript SDK tests, strict typecheck and production build;
- contract, boundary, package, CLI, Python and active Markdown checks;
- Release Host build with zero warnings and errors;
- SDK production and full dependency audits reported zero vulnerabilities;
- clean-source deploy recorded source digest, SHA, MVID and rollback backup.

## Live Gates On This Runtime

- one current `main_menu` Continue BoundAction was delivered through the native
  path;
- duplicate submit and Receipt poll for the same `request_id` returned the same
  terminal Receipt and did not deliver twice;
- a second controller was rejected with HTTP 409 while the first lease was
  held;
- transition to the saved combat exposed 15 sampled `settling` observations,
  all non-interactive, then recovered to one interactive combat owner with six
  BoundActions;
- reusing the pre-transition Snapshot/BoundAction produced structured
  `not_delivered/stale_snapshot`; polling that request returned the same
  Receipt;
- current `run_deck` and `combat_piles` Reads succeeded; a stale Snapshot token
  was rejected with HTTP 409;
- `run_deck` was complete with 14 cards;
- `combat_piles` was complete for player-visible unordered contents, with
  `missing=[]` and `hidden_by_policy=[draw_pile_true_order]`;
- optional `native_pages.v1` opened/read/returned the run-deck page, exposed 14
  current referents, rejected a false runtime binding with HTTP 409, restored
  the exact pre-owner and created no mutation authority or ledger entry.

## Runtime-Found Defect And Repair

The first clean standalone candidate `5373c5aa5c88339547e11b3567fcde1ed62e8259`
incorrectly reported hidden draw-pile order as a missing visible fact, making
`combat_piles` partial while leaving `hidden_by_policy` empty. Mutation was
stopped, the owning Read projection was repaired, and a contract test was added.
The `5373c5a` runtime is defect-discovery evidence only; it does not qualify
`f104e16`.

## Predecessor Evidence

Standalone `b050c46ffa8dc3317e66c261175f92ac8e7d3cb4` exercised a
Scroll Box card bundle, linked detail, native page, settling, stale action,
controller and idempotency gates. That evidence remains valid only for its own
artifact/runtime. Card-bundle reversible selection was not independently
repeated on `f104e16`.

## Remaining Release Gates

- an ordinary completed same-artifact Journey on the exact `f104e16` artifact;
- an actual close/restore/cold-load/verify rollback exercise, followed by
  restoration of the release candidate;
- public GitHub source/tag, binary artifact and SDK package publication.

No old MVID, predecessor Journey or fixture is assigned to the current Host.
Delivery Receipts are not described as STS2 business completion.
