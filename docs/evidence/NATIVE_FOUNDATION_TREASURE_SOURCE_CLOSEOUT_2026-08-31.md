# Native Foundation Treasure Source Closeout

Date: 2026-08-31
Branch: `refactor/platform/native-foundation-full-run-mainline`
Original stacked base: `refactor/platform/native-foundation@79191a1e8c93d3e1a9cbd7632972fc7d6cbad39f`
Implementation commit: `85cb0a59a0edc9432d76e55d5925a8a3264a4d05`

Closeout note: PR #3 and PR #5 are now integrated in `develop` at
`c751952fb2730f198e3adadbebce5aff9cf63c98`. The continuation was rebased onto
that commit; its patch-equivalent Treasure implementation is `9f89d5e...`.
Artifact and runtime identities below remain predecessor evidence for the
original pre-rebase source and do not qualify the restacked bytes.

## Scope

This continuation migrates Treasure semantic publication from visible-node
stage reconstruction to one typed Native Foundation decision provider. PR #5's
exact Windows Human evidence is now durable on `develop`; no evidence is
transferred between the two artifacts.

## Exact Native Basis

The source audit is bound to shipped macOS STS2 `v0.111.0 / 41cef1ea`, assembly
SHA-256 `9cb4f1ad...`, MVID
`57785517-0b16-42b9-8b36-bad6fb28384b`.

- `NTreasureRoom.Create(TreasureRoom, IRunState)` supplies the exact room/run
  owner pair.
- `NTreasureRoom.OpenChest` owns reward generation and coordinates the native
  `TreasureRoomRelicSynchronizer`.
- `CurrentRelics`, `GetPlayerVote`, `PickRelicLocally` and
  `SkipRelicLocally` own exact collection membership and local choice state.
- the shipped single-player contract is explicit; unknown multiplayer or owner
  state fails closed rather than guessing.

No decompiled proprietary source is stored in the repository.

## Authority And Cutover

`NativeTreasureDecisionProvider` exposes process-local stages `closed`,
`opening`, `relic_choice`, `resolving` and `completed`, with exact
`open/select/skip/proceed` candidates. Two bounded read-only Postfixes register
the exact owner and chest callback observation. They cannot suppress, replace
or invoke gameplay.

Connector intersects that catalog with the current chest, relic holder and
proceed controls, re-captures native membership at execution, and alone owns
public BoundAction identity and delivery. Public room/relic referents now bind
the semantic `TreasureRoom`/`RelicModel`; UI nodes remain Host-local operands.
Annotator consumes the same provider read-only. Unknown owners remain playable
and simply yield no Connector candidate or strict Human semantic claim.

Player Environment `1.0.0`, stale rejection, request idempotency,
unknown-no-retry and controller ownership are unchanged. `Receipt.Successor`
remains an immediate post-delivery observation, not a causal `S'`.

## Automated Evidence

- Connector Host: 165/165 tests pass.
- Connector TypeScript SDK: 7/7 tests pass.
- Connector CLI, docs, contract and boundary checks pass.
- Unified game-Mod tests: 33/33 pass.
- exact owner/stage/membership and presentation-separation tests pass.

The final clean build also closes a provenance defect discovered during this
batch: compiled game-Mod identity now includes every composition `.cs` input,
including `NativeFoundationOwnerPatches.cs`, rather than only the initializer,
project and manifest. The portable regression suite contains 34 game-Mod tests.

- exact clean artifact SHA-256:
  `3bc44ddb3c339353a5afcb1acb079e58edad019c4959ffaa10369da783ac3c1b`;
- MVID: `708ecfab-b370-4575-83d0-39c1700bc8b6`;
- compiled Platform source revision: `85cb0a59...`;
- compiled Platform source digest: `831b63e2...`;
- build: PASS with no warnings.

## Mac Automated Runtime Evidence

The exact clean artifact above was safely installed after creating rollback
snapshot `apps/game-mod/.local/deployments/2026-08-31T03-07-08.478Z`, then
cold-loaded against the same exact STS2 identity.

- `verify-loaded`: PASS;
- Connector runtime: `955e5b0232ff47eda43068e41e13ec99`;
- environment fingerprint:
  `722a4149cef7bdb74cbe9a1c547c614be139d316320128b2124e643a5ea0d89e`;
- sole `STS2_PLATFORM` Modset fingerprint:
  `b6b669df28947a5fc16e42f00dc4f48cc0cddc9d1bca3cffb34ccd6593530f8d`;
- Connector protocol `1.0.0`, execution available at main menu;
- Annotator Ready/no-session as expected;
- startup completed without Platform, Native Foundation, Connector, Annotator
  or Harmony errors.

The process was stopped after verification. No gameplay action was submitted.

## Evidence Boundary

This report records source, deterministic test, exact clean-build, safe-install
and bounded main-menu cold-load evidence. It does not prove that Treasure was
entered or that open/select/skip/proceed was observed or delivered. Human and
gameplay runtime remain T3 pending. Earlier continuation and PR #5
runtime/Human evidence do not transfer.
