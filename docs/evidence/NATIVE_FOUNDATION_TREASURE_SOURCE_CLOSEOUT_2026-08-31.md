# Native Foundation Treasure Source Closeout

Date: 2026-08-31  
Branch: `refactor/platform/native-foundation-full-run-mainline`  
Stacked base: `refactor/platform/native-foundation@79191a1e8c93d3e1a9cbd7632972fc7d6cbad39f`  
Implementation commit: `85cb0a59a0edc9432d76e55d5925a8a3264a4d05`

## Scope

This continuation migrates Treasure semantic publication from visible-node
stage reconstruction to one typed Native Foundation decision provider. PR #5
remains frozen at its pending exact Windows Human T3 gate; no evidence is
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

## Evidence Boundary

This report initially records source and deterministic test evidence only. A
dirty-worktree development build is not evidence. Final clean build, safe
install, cold-load, exact identity and Human open/select/skip/proceed exercise
must be appended only after those exact gates pass. Earlier continuation and
PR #5 runtime/Human evidence do not transfer.
