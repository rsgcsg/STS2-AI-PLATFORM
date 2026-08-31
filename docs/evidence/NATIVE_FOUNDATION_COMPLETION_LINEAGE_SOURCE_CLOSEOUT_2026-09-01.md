# Native Foundation Completion Lineage Source Closeout - 2026-09-01

## Evidence Boundary

This report closes the source, deterministic-test, exact-build and exact-load
portion of the Map, Reward and Treasure native completion-lineage repair. It
does not claim Human qualification for the new artifact. A new Human canary is
required because the source and artifact identity changed.

Implementation source commit: `61be43df7fe0acf73dd8f634d2f2b9527b85973a`.
Provenance/BOM commit: `32c75184128d72c65a5165dea4e14ddb7e4ee7a7`.
Branch: `refactor/platform/native-foundation-full-run-mainline`.

The latest predecessor Human session used to classify the failures was
`session-20260831T135855Z-f647f7127cea48f3825c2a33a0520d1a`, timeline
`timeline-a7298fef194c4e15b80780f570678162`. It loaded the predecessor Game Mod
artifact `8ecdb2dfca07c2bd323d16a754d25f500d8c048489a9b374c86314ad89055716`
with MVID `d0340c68-9323-4f98-80fd-cb7f78d2bd00`. Its modern events proved
completion behavior but did not carry an exact related Human-root identity;
therefore those events are diagnostic predecessor evidence only and do not
qualify this repair.

## Failure Classification Used For The Repair

The predecessor session recorded:

| Domain | Predecessor invalidations | Classification |
|---|---:|---|
| Map | 34 | 30 exact native-reference misses; 4 serialized-evidence overlaps |
| Reward claim | 50 | 41 exact native-reference misses; 9 serialized-evidence overlaps |
| Treasure | 9 | 3 chest and 3 relic and 3 proceed roots remained fail-closed |
| Reward proceed | 18 modern proofs | predecessor source/runtime evidence only |
| CardReward select | 12 modern proofs | predecessor source/runtime evidence only |

The misses were not repaired by relaxing completeness, actionability, or
required Reads. The source fix aligns the Annotator witness with the same
public/native referents used by the Connector catalog:

- Map observes the native `MapPoint` (`NMapPoint.Point`), not the UI wrapper.
- Reward claim observes the native `Reward`, not `NRewardButton`.
- Treasure chest and room-proceed public roots use the public null subject;
  relic selection uses `activate`; skip uses `skip`; room ownership remains a
  private native constraint rather than a public action operand.

These changes make the old exact-reference misses matchable when the current
decision frame is genuinely authoritative. They do not backfill a frame after
the decision boundary or turn an incomplete Treasure frame into a success.

## Completion Lineage

Direct UI callbacks account for the Human root and freeze its semantic pre
state. Native completion callbacks then carry an identity-bearing signal:

```text
session + generation + Human root
  -> exact native family/kind/task/owner/operand/lineage
  -> game-thread transport
  -> post-commit semantic capture
  -> proved successor or explicit unknown
```

`NativePostCommitCompletionLedger` indexes registrations by the exact
`ActionWitnessId`. Session/generation, family, kind, task identity and any
native owner/operand/lineage identities are retained in the completion
evidence. A missing root identity, mismatch, duplicate, reset, stale
generation, failed task, or unmatched completion cannot consume another
registration and is quarantined or recorded unknown.

The queue drained by `OnProcessFrame` is only a game-thread transport seam. It
does not provide semantic authority. There is no count, FIFO, current-waiting
root, timer, animation, queue-idle, or polling fallback in the modern proof
path. Legacy V2 successor material remains historical compatibility evidence
and cannot qualify this lane.

## Native Seams

| Domain/action | Pre-state owner | Causal completion | Successor capture |
|---|---|---|---|
| Map travel | `RunState.Map` / exact `MapPoint` | `VoteForMapCoordAction` lifecycle | exact `GameAction` terminal boundary |
| Reward claim | exact `RewardsSet` / `Reward` | `RewardsSetSynchronizer.SelectLocalReward` successful task | identity-matched post-commit frame |
| Reward proceed | exact reward screen/proceed state | `RunManager.ProceedFromTerminalRewardsScreen` task | identity-matched post-commit frame |
| CardReward select | exact native card option | `CardReward.OnSelect` successful task | identity-matched post-commit frame |
| Treasure open | exact `TreasureRoom` lifecycle | `OneOffSynchronizer.DoLocalTreasureRoomRewards` task | identity-matched post-commit frame |
| Treasure relic/skip | exact relic choice owner | `PickRelicAction` lifecycle | exact `GameAction` terminal boundary |
| Treasure proceed | completed exact room state | `RunManager.ProceedFromTerminalRewardsScreen` task | identity-matched post-commit frame |

Reward proceed and CardReward completion paths retain the existing native
completion mechanism and receive identity checks; no protocol or gameplay
authority changed.

## Automated Exact Build

The clean exact-game build used the following identities:

- STS2 `v0.111.0 / 41cef1ea`;
- `sts2.dll` SHA-256
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`;
- `sts2.dll` MVID `57785517-0b16-42b9-8b36-bad6fb28384b`;
- Connector `6215718eafb3bc16042d4b94bb0bf6acafaadc7c4aa09ce2dd35010bf4c9aeb9`
  / `cb205b8e-95b0-483f-877d-b2747f402e68`;
- Annotator `d915c0e14fc1989c97a778a8d1652450c69cf8028a019191916c4b32d23b9ddd`
  / `47130865-befc-405e-9b2d-379730c9a2bf`;
- unified Game Mod
  `f9f616dafe6aa4f7733c81caad3a79ddbe771adbb7b9e46bd79c310138d8efc2`
  / `2b2de33c-9f01-41f4-906e-c73cf7d283a2`;
- build workspace `ed2e0b514500d6d1d19cb123f338ad0d704489d8`;
- game Mod compiled source revision `9f89d5e42575e0716b9df981f3bab5ffffe1e0f6`;
- compiled Platform source digest
  `33c521a04a8788b58053e469daf9f024851200db6b7b75b94284ac992ffdf7c0`.

`npm run check:bom` and `npm run check:exact-game` pass on this source. The
artifact was then safely installed and cold-loaded with the sole
`STS2_PLATFORM` Modset in runtime `e64e508d7f8e420abb2f9c723670e832`,
environment `8002d9e30693e4ad46575310b8885cc280a949a141e49b9b5bfb4d14fe1f5730`.
Loaded identity matched the artifact exactly. This remains load evidence only;
the artifact has not been Human-exercised.

## Non-Claims And Next Gate

No new performance claim is made. The predecessor profile remains the only
available runtime profile; SnapshotBuilder was not reworked and lifecycle-only
capture remains intended to stay at zero.

The next gate is one fresh exact-runtime Human canary using only
`STS2_PLATFORM`, covering Map travel, Reward claim/proceed, CardReward select,
Treasure open/relic select/skip/proceed, and rapid/overlap input. The audit
must show each modern successful completion carries the exact root ID and
native identity, while stale, failed, cancelled, incomplete, or unmatched
signals remain unknown without cross-root proof. This is a new canary, not a
transfer of the predecessor session.

Current status: `source_test_exact_build_human_pending`.
