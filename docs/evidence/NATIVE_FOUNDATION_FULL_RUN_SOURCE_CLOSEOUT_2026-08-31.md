# Native Foundation Full-Run Source Closeout

Date: 2026-08-31
Branch: `refactor/platform/native-foundation-full-run-mainline`
Original stacked base: `refactor/platform/native-foundation@79191a1e8c93d3e1a9cbd7632972fc7d6cbad39f`
Implementation commit: `47dacf93d49d2f8cb1a1d8409557bee9884e0ecf`

Closeout note: PR #3 and PR #5 are now integrated in `develop` at
`c751952fb2730f198e3adadbebce5aff9cf63c98`. The continuation was rebased onto
that commit; its patch-equivalent Map/Reward/CardReward implementation is
`129d73e...`. Artifact and runtime identities below remain predecessor evidence
for the original pre-rebase source and do not qualify the restacked bytes.

## Scope

This continuation migrates Map, Reward and CardReward from an owner-only
discriminator to typed Native Foundation decision adapters. PR #5's exact
Windows Human evidence is now durable on `develop`, but it is bound to its own
artifact and is not transferred here.

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

## Mac Automated Runtime Evidence

The exact clean artifact above was safely installed and cold-loaded on macOS
against the same exact STS2 identity. The deploy created rollback snapshot
`apps/game-mod/.local/deployments/2026-08-30T22-32-44.334Z` before replacing
the predecessor installation.

- `verify-loaded`: PASS.
- Connector runtime instance: `2c94849ea18a453a990e402e886287b1`.
- Environment fingerprint: `8db5a2aff09d059664d57f1b7ef03770dd6dcfe27ba315e8c795448b58c48377`.
- Modset: exact sole `STS2_PLATFORM`, fingerprint
  `2f4b276f9c4ebcffb665f680ab1d7cf0ba969a2ff8ccbf414104bd4fee29a710`.
- Connector protocol: `1.0.0`; execution was available at the main menu.
- Annotator runtime: Ready/no-session, as expected for a non-Human canary.
- Startup log: unified Mod initialization completed without Platform,
  Connector, Annotator, Native Foundation or Harmony errors. Existing
  non-fatal profile-content warnings were unrelated to this artifact.

The process was stopped after verification. No gameplay action was submitted.

## Evidence Boundary

At this closeout the continuation artifact has source, deterministic test,
exact clean-build, safe-install and bounded cold-load evidence. It has not been
exercised on a Map/Reward/CardReward decision or used for Human recording. The
loaded main-menu readiness envelope does not prove the new decision adapters.
PR #5 Windows T3, predecessor schema-3 Human evidence and earlier macOS runtime
evidence do not transfer. Ritsu remains
`RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`.

Next non-Human architecture batch is the Treasure lifecycle discriminator;
T3 gameplay evidence for this artifact remains a separate gate.
