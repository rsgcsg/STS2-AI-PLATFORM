# Full-Run Semantic Coverage

> This matrix tracks Human/native accounting and trace-level boundary coverage.
> It is not a canonical one-step training matrix. ADR 0003 and the latest
> calibration show that schema-3 `transition_proved` does not by itself prove
> complete same-state A(S), exact A membership and causal S'. The former global
> serialized-input candidate is no longer active after it blocked ordinary UI.
> Current work tests a read-only native semantic sequential lane without
> expanding polling or weakening gameplay.

This is the current coverage authority for the additive Human Semantic Timeline.
It does not expand `CurrentDecisionRecord`, corpus admission, or Connector
action authority.

## Causal Model

```text
Human observation H
-> exact current BoundAction correlation
-> STS2-owned GameAction lifecycle or exact source-local UI Commit
-> execution-adjacent state boundary
-> trace-level successor candidate
```

The ordinary-combat schema-2 owner closeout is the trace regression oracle. New
surfaces use the same `SemanticBoundaryTracker`; no surface may invent legality,
infer completion from time/animation/queue idle, or fill an earlier successor
from a later Human effect.

## Current Matrix

| Slice | Native witness | Source/test | Current-artifact Live |
|---|---|---:|---:|
| ordinary combat play / End Turn | `GameAction.OnEnqueued` + typed lifecycle + `ActionExecutor.BeforeActionExecuted` | complete | schema-3 canary: 214 `PlayCard`, 48 `EndTurn` roots proved |
| generated-card select | exact selection callback + direct UI delivery | complete | schema-3 canary: three selects proved |
| generated-card skip | exact skip callback + direct UI delivery | complete | not exercised |
| Combat hand selector select / replace / deselect / confirm | exact hand/container callbacks + direct UI delivery | complete | schema-3 canary: eight confirms; select/replace/deselect not proved |
| potion use / target / cancel | Human `NPotionHolder.UsePotion` arm -> exact `PotionModel.EnqueueManualUse` commit -> `UsePotionAction` lifecycle; target-picker cancel never enqueues | complete | schema-3 canary: three uses proved; cancel remains unexercised |
| lethal combat -> reward | existing combat lifecycle + reward Player Environment boundary | complete | repair canary PASS |
| reward claim | `NRewardButton.OnRelease` -> exact bound Task completion, or exact `NCardRewardSelectionScreen.ShowScreen` owner creation for a nested CardReward | source/test complete; Commit remains distinct from successor proof | final PR #6 session: 10 canonical |
| reward proceed | `NRewardsScreen.OnProceedButtonPressed` -> `RunManager.ProceedFromTerminalRewardsScreen` completion | source/test complete; direct UI return is not a successor proof | final PR #6 session: 4 canonical |
| card reward select | `NCardRewardSelectionScreen.SelectCard` -> enclosing `CardReward.OnSelect` completion | source/test complete; option callback alone is not a successor proof | final PR #6 session: 4 canonical |
| map travel | `NMapScreen.OnMapPointSelectedLocally` -> `VoteForMapCoordAction` lifecycle | source/test complete | final PR #6 session: 7/7 canonical; five typed owner-ready successors complete |
| treasure open / relic select / skip / proceed | exact room/synchronizer owner; `PickRelicAction`, normal-reward task and terminal-proceed task seams | source/test complete; visual `OnRelease` is not gameplay authority | predecessor repair sessions exercised the seam; the final exact PR #6 session did not exercise Treasure |
| event option | `EventRoom.LocalMutableEvent.CurrentOptions` + `EventOption.Chosen` task | complete visible option catalog | `NEventRoom.OptionButtonClicked` + exact option completion ledger | source complete; final candidate live canary required |
| shop room open / proceed | `NMerchantRoom.OpenInventory` / `HideScreen` native controls | complete room-control catalog | exact room callbacks | source complete; final candidate live canary required |
| shop purchase / card removal | `MerchantEntry.IsStocked/EnoughGold` + `OnTryPurchaseWrapper` task | complete visible offer catalog | `MerchantEntry.OnTryPurchaseWrapper` + exact entry completion ledger | source complete; final candidate live canary required |
| shop inventory close | `NMerchantInventory.Close` after exact BackButton delivery | complete visible close control | exact inventory close callback | source complete; final candidate live canary required |
| rest-site option / proceed | `RestSiteSynchronizer.ChooseLocalOption` task and `NRestSiteRoom` proceed | complete visible option catalog | exact synchronizer and proceed callbacks | source complete; final candidate live canary required |
| run entry / game terminal | native `RunManager.Launch()` start and `RunManager.OnEnded(bool)` terminal seams; lifecycle-only | source/test complete for native markers; no Human action authority | fresh candidate lifecycle canary and exhaustive Full Run still required |

Current source keeps the gameplay-safe observer path and adds an independent
process-local discriminator for ordinary combat Play/End Turn/Potion. At native
first execution it records both `A(UI)` and `S_sem + A_sem(S)` from logical hand,
current potion slots, combat phase and STS2 validators when the canonical
boundary does not already own that execution sample; otherwise it records an
explicit lifecycle-only delegation. Generated choice remains linked to its
paused parent lifecycle rather than becoming a replacement root.
Exact owner session `session-20260830T064823Z-...` exercises 30 PlayCard, ten
EndTurn and one potion root plus two player-choice pause/resume pairs. All 41
successful roots have complete first-execution captures and exact-once native
semantic membership. The UI frame is non-authoritative for 34 roots and a
complete UI catalog omits the executing root seven times. This is bounded Human
support for the native semantic lane, not proof of true overlapping acceptance,
cancel/abort, final successor semantics or non-combat Full Run. See the
[Human closeout](evidence/NATIVE_SEMANTIC_RUNTIME_DISCRIMINATOR_HUMAN_CLOSEOUT_2026-08-30.md).

The stacked Native Foundation candidate moves that bounded combat catalog and
exact lifecycle observation into one neutral game-side owner consumed by both
Connector and Annotator. Stacked continuation adds typed native decision
providers for Map, Reward, CardReward and Treasure: game-owned route/reward/
option/room membership now precedes presentation intersection, and execute-time
binding re-captures the same provider. The current Annotator continuation also
uses exact native completion seams for these four families:
`VoteForMapCoordAction`/`PickRelicAction` lifecycle, reward synchronizer and
card-reward completion tasks, and the terminal proceed task. These callbacks
are queued and captured on the game frame, never treated as an immediate UI
return or a polling successor. Treasure additionally separates the room-owned
`closed/opening/relic_choice/resolving/completed` lifecycle from its visible
chest/holder/proceed controls. The matrix above retains predecessor Live claims
only where explicitly named; no predecessor Human evidence transfers to the
current continuation artifact.

The merged 2026-09-01 causal-evidence implementation replaces the earlier
overlapping modern/legacy accounting assumptions. Its semantic timeline is the
sole modern authority; exact native binding proves Commit, and Commit waits for
a separate owner-ready or next-root execution boundary before `S'`. Async Task
identity survives UI scope exit, and shared completion methods no longer
hard-code a family. Source, cross-layer tests, exact build, cold-load and the
bounded final Human qualification pass. The final exact session proves 25/25
modern rows, including Map 7/7 and typed owner-ready 5/5, without unresolved
transitions or Close drain timeout. Predecessor sessions remain classification
evidence only. See the
[completion-lineage source closeout](evidence/NATIVE_FOUNDATION_COMPLETION_LINEAGE_SOURCE_CLOSEOUT_2026-09-01.md).

The Full-Run room extension adds a read-only Native Foundation catalog for
event options, merchant room/inventory controls and rest-site options. Each Human witness
still resolves against the same frozen public `BoundAction` frame. Event and
merchant tasks carry the exact option/entry identity through asynchronous
completion; rest-site selection uses the synchronizer's `Task<bool>` and the
proceed control remains a direct UI decision whose successor must be observed
at the next authoritative boundary. These seams establish source-level
accounting without transferring any predecessor Human evidence; targeted and
continuous qualification on the final artifact are still required.

The failed Windows session
`session-20260903T102650Z-50552cf165a8439397b71d7a1967f957` is forensic evidence
only: six accepted roots stopped at `action_started`, while Event/Rest/Shop
callbacks produced pre-frame or exact-task binding failures. The trace also
ended with a polling-only `run_ended` journal entry, so terminal proof was
unknown. Current source now uses exact public action shapes and root-bound
async completion, captures Rest-site input before native option disabling, and
observes `RunManager.OnEnded(bool)` as the sole native terminal marker. No old
session or polling observation qualifies the new bytes.

The successor repair adds the first production typed owner-ready publisher.
Exact STS2 call order rejects `RoomEntered`, `CombatBegan` and visual
`TransitionToActiveCombat` as too early. The player-side combat turn is emitted
only after semantic `Play`, executor unpause, synchronizer `PlayPhase`, and the
exact end-turn input-owner callback; Annotator then requires one complete,
domain-matching Connector frame. This closes the source-level Map-to-Combat
terminal-recording gap without changing next-root execution handoff or using
polling, delay, UI stability or later-state backfill. The final PR #6 runtime
passed its exact shortest Human canary; any later runtime bytes require their
own build/load and may not inherit that qualification.

Semantic state Read requirements are interaction-specific: combat requires
`run_deck` and `combat_piles`, shop requires `run_deck` and `shop_catalog`, and
the current ordinary non-combat surfaces require `run_deck`. A failed Read makes
that boundary partial; it never changes action publication or native execution.

## Repair Canary Gate

The first batch canary on artifact `fe3e3a82... / b1284288...` is not a PASS.
It exposed one direct-UI boundary defect and one parent-lifecycle pruning defect
that disabled semantic tracing while native accounting continued. Strengthened
audit now rejects that session with 546 missing accepted-root findings. See the
[batch evidence](evidence/FULL_RUN_BATCH1_OWNER_CANARY_2026-08-28.md).

The repair artifact exercised the bounded cross-surface path:

```text
lethal combat -> reward claim -> card reward select or skip/proceed -> map node
```

The later closed owner session
`session-20260828T032151Z-43b2f87e65484b8abccccbba71c713c8`, timeline
`timeline-31295a4049034cd6bd45d3b7d8fe8304`, passes overall schema-2 accounting
with 627 accepted actions, 625 proved, one cancelled before start, one
cancelled after start and correctly unknown, and zero unresolved. Independent
audit passes 219 valid Decision V2 records, 0 invalid records and 287 explicit
legacy invalidations. It exercised a long combat/reward/map path and one
enemy-targeted potion, but exposed three self-target potion mapping failures
and one accepted self-target use that lacked explicit accounting.

The canary proves repaired canonical direct-UI binding and parent-lifecycle
retention. It does not prove hand select/replace/deselect, generated skip,
potion use, room-internal event/shop/rest/treasure actions, run entry,
exhaustive Full Run, semantic-free performance, game outcome success or
qualification.

The later repair-artifact session
`session-20260828T151112Z-a559d80cd88741738f2a902427b10140`
passes 233 accepted/233 proved semantic dispositions and exercises four exact
potion mappings across enemy-target, no-target and self-target operands. It is
the regression and size baseline for the normalized schema-3 source, not Live
evidence for that new source. See the
[storage baseline](evidence/SEMANTIC_EVIDENCE_STORAGE_BASELINE_2026-08-29.md).

Source `fba874e8...` repairs the potion witness without changing Connector
authority: it matches STS2's owner normalization only after the original null
operand misses the frozen catalog, still requires exact-unique binding, and
defers arm/mapping failures until native acceptance. Its clean unified artifact
`b5fbda12... / 1cbcff84...` produced the later 233/233 owner session above.
That session proves the repair artifact's exact potion accounting but not
target-picker cancel or the subsequent schema-3 storage source. Batching cut
the predecessor's narrow boundary-to-start median from about 8 ms to 4 ms;
offline call-count and size analysis now proves repeated Read persistence and
inline frame storage are architectural duplication, but does not yet prove the
owner-perceived lag is fixed.

See the [potion canary](evidence/FULL_RUN_POTION_OWNER_CANARY_2026-08-28.md).

The older `c8775e1... / 8d2f7d2a... / 3043f4f4...` batch repair remains
predecessor evidence in the dated batch report; it is not the current source or
artifact identity.

## Schema-3 Human Gate

Closed session `session-20260829T052157Z-e549d3601e7640f997b6f475180b2dfe`
on exact artifact `4fa67570... / 51c7c37b...` passes 333 accepted/333 proved
with zero unknown, cancel, abort or unresolved action. It covers the current
combat/reward/map mechanisms listed above and proves exact role-reference
resolution with no inline event frames. This closes the generic schema-3 Human
gate. It does not close generated skip, target-picker cancel, hand
select/replace/deselect, room-internal actions or run entry. Current stage
profiling source is newer than this artifact and requires its own short canary
only for latency attribution, not to transfer schema-3 semantic proof.
