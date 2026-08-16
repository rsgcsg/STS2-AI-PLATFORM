# Managed Exact Native Rewards, Treasure, And Capacity

Date: 2026-08-17 (Australia/Brisbane)

Verdict: **the rebuilt managed route is now a high-throughput, partial,
unqualified Player Environment candidate. It is not H1.0 and not Training
Ready.**

## Exact Runtime

- upstream: `wuhao21/sts2-cli@d11aa883b582dd68bd39b331f3370746b30d447e`;
- admitted patch SHA-256: `b6dc69aed741887797e7c83ca8e53c87baaab5f8214bf6a952351f74a54e5434`;
- Host artifact SHA-256: `2b8fa6c6e29a6f49ed7a5cca5f631781e1ee17f0a15e7cbee260d96d563b482e`;
- Host MVID: `9ef2d858-4f1b-49f1-b0d5-666471676fad`;
- STS2: macOS arm64 `v0.111.0`, commit `41cef1ea`, main assembly
  hash `1010476334`;
- original and runtime `sts2.dll` SHA-256:
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`;
- STS2 MVID: `57785517-0b16-42b9-8b36-bad6fb28384b`.

The reports were generated from a dirty working tree rooted at predecessor
HEAD `101e4838653f86a49349ffa17688dc555d445dd9`. The candidate patch and artifact
identities above are exact; the reports are not evidence for a later changed
adapter source without rerunning its gates.

## Correctness Changes

The old spike manually generated combat rewards and auto-selected the first
treasure relic. Those paths were not acceptable Headless authority.

The rebuilt reward path now delegates generation to `RewardsCmd`, offer and
hooks to `RewardsSet`, selection/effects/history to
`RewardsSetSynchronizer`, source-specific card choice to native
`CardReward.OnSelect`, and room exit to native map/act transitions. The Host
only replaces the absent rewards-screen input wait.

Treasure now has explicit closed-chest, relic-selection, and completion
decisions. Exact relic identity is private to the Host. Execution revalidates
the current room and relic, dispatches `PickRelicLocally`, and accepts the
native synchronizer's vote/RNG result. The narrow presentation adapter performs
only the absent UI handler's `RelicCmd.Obtain` and unawarded-relic fallback
handoff. It no longer chooses for the consumer.

## Automated And Runtime Evidence

The exact-runtime native binding gate passed all 24 checks. It includes wrong
map/card/potion/treasure identities, native map/card/potion Commit, canonical
treasure projection, Host-local room/relic operands, explicit chest open,
native treasure vote, inventory effect, and map successor. Treasure setup in
this gate is privileged scenario evidence, not an organic journey.

One fair-player canonical run group completed three in-process episodes:

- 441/441 canonical actions delivered;
- 444 state-bound Reads completed;
- three game-owned seeds matched their requests;
- all episodes terminated at `game_over` with zero unknown or failed delivery;
- organic coverage included 372 combat turns, 23 reward selections, 10 card
  reward selections, 10 reward completions, 17 map choices, three events,
  three shops, and three card selectors;
- maximum observed progress was Act 1, floor 7; treasure did not occur
  organically.

The same artifact's 1/2/4/8-worker canonical capacity window measured:

| Workers | Delivered actions | Reads | Reset-inclusive decisions/s | Lifecycle-inclusive decisions/s | Summed peak RSS |
|---:|---:|---:|---:|---:|---:|
| 1 | 400 | 403 | 236.12 | 136.40 | 131.7 MiB |
| 2 | 815 | 821 | 428.22 | 255.08 | 263.4 MiB |
| 4 | 1,626 | 1,638 | 750.25 | 457.60 | 531.6 MiB |
| 8 | 3,317 | 3,341 | 1,017.39 | 664.75 | 1,092.6 MiB |

Every worker used a distinct runtime instance, completed three reset episodes,
reached `game_over` each time, reported no action/read failure, and exited
zero. The 8-worker reset-inclusive result crosses the provisional 1,000
decision/s hypothesis. It does not qualify the route because the projection is
partial, the episodes are short losses, and semantic parity is unproved.

## Non-Claims And Next Gates

- No complete run, boss/Architect transition, organic treasure, alternative
  card reward, or broad selector coverage is claimed.
- No cross-Host semantic match or Reference transfer is claimed.
- `game_over` is lifecycle coverage, not policy quality or game completion.
- The manual managed observation/projection surface and TestMode execution
  remain critical qualification risks.
- This is not a 1M-decision reliability run, changed-build drill, external
  adapter, learning smoke, package, release, or H1.0 admission.

Next admission work is a canonical cross-Host scenario driver and differential
corpus, followed by full-run lifecycle coverage, reset/recovery soak, and
consumer/learning transfer evidence.
