# Native Foundation Full-Run Source Closeout

Date: 2026-08-31  
Branch: `refactor/platform/native-foundation-full-run-mainline`  
Stacked base: `refactor/platform/native-foundation@79191a1e8c93d3e1a9cbd7632972fc7d6cbad39f`  
Implementation commit: `47dacf93d49d2f8cb1a1d8409557bee9884e0ecf`

## Scope

This continuation migrates Map, Reward and CardReward from an owner-only
discriminator to typed Native Foundation decision adapters. PR #5 remains the
frozen proof line; its exact Windows Human T3 gate remains pending and is not
transferred here.

## Exact Native Basis

The implementation was checked against shipped macOS STS2
`v0.111.0 / 41cef1ea`, assembly SHA-256 `9cb4f1ad...`, MVID
`57785517-0b16-42b9-8b36-bad6fb28384b`.

- Map destinations come from `RunState.Map`, `CurrentMapPoint`,
  `VisitedMapCoords`, starting/boss points and
  `MapTravel.GetTravelablePointsFrom`.
- Reward membership and proceed policy come from the exact `RewardsSet` passed
  to `NRewardsScreen.ShowScreen`, `Reward.SuccessfullySelected`, potion slots,
  `DisallowSkipping`, `AllRewardsSuccessfullySelected`, and
  `Hook.ShouldProceedToNextMapPoint`.
- CardReward membership comes from the exact option/alternative arrays passed
  to `NCardRewardSelectionScreen.ShowScreen` and `RefreshOptions`. Shipped code
  creates alternative buttons in that same array order and commits the captured
  index back into `_extraOptions`.

No decompiled proprietary source is stored in the repository.

## Authority And Cutover

```text
STS2 owner and native membership
-> typed Native Foundation decision
-> Connector visible/deliverable intersection
-> exact Host-local binding and execute-time recapture
-> native delivery

same typed decision
-> Annotator process-local semantic witness
```

The exact owner arguments are observed by three explicit read-only Harmony
Postfix seams in the unified Mod composition root. They cannot suppress,
replace or invoke gameplay. Weak owner registries cannot keep screens alive.
Unknown owner, duplicate binding or native/presentation contradiction fails
closed for Connector publication; it does not block unrelated Human gameplay.

Removed or demoted semantic authority:

- `NMapPoint.State` no longer creates map reachability;
- `NRewardButton` no longer creates reward membership;
- card holders and alternative buttons no longer create CardReward membership;
- UI controls remain presentation/input deliverability and exact delivery
  bindings only.

The Player Environment `1.0.0` wire, controller, stale rejection, request
idempotency and unknown-no-retry semantics are unchanged. `Receipt.Successor`
is still an immediate post-delivery observation, not causal `S'`.

## Automated Evidence

- Connector Host: 156/156 tests pass.
- Connector TypeScript SDK: 7/7 tests pass.
- Connector CLI, docs, contract and boundary checks pass.
- Unified game-Mod boundary tests: 32/32 pass.
- Platform dependency boundary checks pass.
- Root exact-game orchestration now builds the Connector Release artifact
  before the dependent standalone Annotator check. A regression test fixes
  that order; the prior clean-order failure was a missing build prerequisite,
  not a semantic or runtime failure.
- Root portable suite and root exact-game suite pass.
- Exact clean unified build: PASS with no warnings.
- Build artifact SHA-256: `3e3ebc3cbb7b3e19c2e6bfe2412a9b215aca86666dffcc8c028ba7686a9fa89e`.
- Build artifact MVID: `53568805-b90a-4165-84ba-098f1c05fc6c`.

## Evidence Boundary

At this closeout the continuation artifact has source, deterministic test and
exact clean-build evidence. It has not been installed, loaded, exercised on a
Map/Reward/CardReward decision, or used for Human recording. PR #5 Windows T3,
predecessor schema-3 Human evidence and earlier macOS runtime evidence do not
transfer. Ritsu remains
`RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`.

Next non-Human architecture batch is the Treasure lifecycle discriminator;
runtime evidence for this artifact remains a separate gate.
