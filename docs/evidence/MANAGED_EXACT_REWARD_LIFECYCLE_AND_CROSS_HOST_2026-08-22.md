# Managed Exact Reward Lifecycle and Cross-Host Closure

Date: 2026-08-22

Verdict: the exact Candidate closes the observed full-potion reward loop,
reaches ordinary `game_over` boundaries in the fixed ten-seed corpus, and
matches the shipped Reference for one same-seed reward-to-map-to-combat
window. This is lifecycle and named-window evidence, not broad semantic or
H1.0 qualification.

## Exact identities

- game: macOS arm64 STS2 `v0.111.0` / `41cef1ea`, main assembly hash
  `1010476334`, `sts2.dll` SHA `9cb4f1a...`, MVID `57785517...`;
- Reference: Connector source `99f09a7...`, artifact `99d5df9...`, MVID
  `e7b2be84...`, protocol `1.0.0`;
- Candidate: upstream `d11aa883...`, patch `d136d4b...`, artifact
  `126ae0c...`, MVID `3fbeec1b...`;
- semantic target: `sts2-v0.111.0-player-visible-zhs-v1`.

## Closed defects

1. A full potion belt did not prevent publication of a potion reward. Native
   selection correctly remained pending, so the deterministic consumer
   repeated a delivered input forever. Publication and execute-time
   revalidation now both reject that claim until one exact owned potion is
   discarded through the game-owned discard action.
2. A fresh candidate checkout could not reproduce a ledger containing a new
   source file because `git apply` left it outside the audited diff. Preparation
   now uses intent-to-add and a fresh clone passes source/build identity audit.
3. Reward labels for gold and card rewards were English Host strings while the
   Reference used native zhs `Reward.Description`. Candidate labels now use the
   same game-owned localization source; potion/relic details use native dynamic
   descriptions.
4. `Map.GetPointsInRow()` omits the special `StartingMapPoint` after room
   entry although the native map UI still renders it as traveled. The exact
   read now includes that game-owned point, preserving current-position type,
   visible topology and canonical referent ordering.
5. Cross-Host scenario discovery was asymmetric and had an implicit eight-step
   preamble. Discovery and compared action windows are now separate, bounded,
   and both Hosts follow the same finite Player Environment policy.

## Runtime evidence

- exact regression seed `H1LIFECYCLECURRE007` reached `game_over` after 352
  delivered actions and 645 Reads. Its reward sequence discarded Explosive
  Ampoule, claimed Energy Potion, and proceeded instead of looping at 600;
- ten exact seeds `H1LIFECYCLECURRE001` through `...010` all reached
  `game_over`: 2,129/2,129 actions delivered, 3,944 Reads completed, zero
  unknown or runtime failure;
- all 26 native identity, refusal, Commit, localization and treasure gates
  passed;
- same-seed CrossHost scenario `first-reward-prefix-v1` matched four canonical
  events on both Hosts: `reward_claim`, `map_navigation`, and two
  `combat_turn` decisions.

Local reports:

- `.local/evidence/managed-player-environment-2026-08-21T17-30-45-058Z/report.json`;
- `.local/evidence/managed-native-binding-gates-2026-08-21T17-30-34-418Z/report.json`;
- `.local/evidence/managed-cross-host-2026-08-21T17-30-06-349Z/report.json`;
- `.local/evidence/managed-prepare-2026-08-21T17-28-47-864Z/report.json`.

## Evidence boundary

The ten-run corpus exercises combat, reward/card reward, map, event, card
selection, shop, rest and treasure. It does not prove every source or selector,
victory, randomized CrossHost parity, information closure, 1M+ reliability,
external Python consumption, learning, learner contention, or Candidate to
Reference policy transfer. Snapshot completeness remains partial when the
Candidate cannot yet project canonical persistent run identity and all stable
hover/modifier facts.
