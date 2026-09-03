# Annotator Full-Run Coverage Inventory (2026-09-03)

This inventory is a read-only projection of the current `develop` contracts and
the exact shipped STS2 v0.111.0 (`41cef1ea`) native seams.  It is not a second
action registry or an admission authority.  Connector remains the owner of
public `BoundAction` legality and delivery; STS2 remains the owner of rules,
RNG and Commit; Annotator only correlates Human witnesses and persists
evidence.

| Family | Native owner / Commit seam | Connector binding | Annotator ingress | Current status | Exact gap |
| --- | --- | --- | --- | --- | --- |
| ordinary combat play / end turn / potion | `PlayCardAction`, `EndPlayerTurnAction`, `UsePotionAction` lifecycle; potion enqueue | complete finite combat catalog | `NCardPlay`, `NEndTurnButton`, `NPotionHolder` + lifecycle | supported and regression-covered | targeted cancel/selector canaries remain |
| generated card choice | `NChooseACardSelectionScreen.SelectHolder` / skip callback with paused parent | complete | exact choice callbacks + parent lineage | supported | skip live canary remains |
| combat hand selector | `NPlayerHand` and selected-container callbacks | complete | exact select/replace/deselect/confirm callbacks | supported | select/replace/deselect live canary remains |
| map travel | `VoteForMapCoordAction` and map owner-ready | complete | `NMapScreen.OnMapPointSelectedLocally` | supported | none at source level |
| reward / card reward | reward synchronizer tasks and typed card-reward owner | complete | reward/card-reward callbacks and completion tasks | supported | final-candidate live coverage required |
| treasure | `NTreasureRoom` lifecycle, `PickRelicAction`, terminal proceed task | complete | chest/relic/proceed callbacks | supported | final-candidate live coverage required |
| event option | `EventModel.CurrentOptions`, `EventOption.Chosen()` task; `NEventRoom.OptionButtonClicked` | complete visible option catalog | exact option callback + `EventOption.Chosen()` completion | source complete; final-candidate live canary required | exact post-room successor evidence remains a live qualification gate |
| shop room open / proceed | `NMerchantRoom.OpenInventory` / `HideScreen` native controls | complete room-control catalog | exact room callbacks | source complete; final-candidate live canary required | exact room/map successor evidence remains a live qualification gate |
| shop purchase / card removal | `MerchantEntry.OnTryPurchaseWrapper` task + purchase events | complete visible offer catalog | exact entry callback + purchase task completion | source complete; final-candidate live canary required | exact outcome/nested-selector successor evidence remains a live qualification gate |
| shop inventory close | `NMerchantInventory.Close` after exact BackButton delivery | complete visible close control | exact inventory close callback | source complete; final-candidate live canary required | exact return-to-room successor evidence remains a live qualification gate |
| rest-site option | `RestSiteSynchronizer` option task/events | complete visible option catalog | exact synchronizer callback + `Task<bool>` completion; proceed callback | source complete; final-candidate live canary required | exact post-room successor evidence remains a live qualification gate |
| event dialogue / room navigation | native presentation callbacks and room transition | observation only for dialogue | no gameplay decision authority | intentionally excluded from canonical action envelope | remains non-claim until a native decision owner is proven |
| run entry / terminal | menu/game-over adapters | observation only | no canonical Human decision seam | not in current recording profile | classify as lifecycle markers, not actions, unless contract changes |

## Decision before implementation

The current branch starts at the refreshed `origin/develop` and retains the
existing combat/map/reward/card-reward/treasure evidence path.  The Full-Run
extension is limited to the three ordinary room decision owners above.  Their
source seams now use the native catalogs and exact callback/task identities;
they remain unqualified for final Full-Run claims until the candidate is
cold-loaded and exercised.  Any family for which an exact native owner,
identity, or terminal disposition cannot be proved remains explicitly
unknown/unsupported; no timer, polling, screen inference, retry, or UI
telemetry may promote it to a canonical action.

The exact local game identity used for native inspection is:

```text
game version: v0.111.0
game commit: 41cef1ea
sts2.dll sha256: 0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9
sts2.dll mvid: 73b63ee0-6c0a-47bb-b0d1-b21f6d94222e
```

## Failed-session forensic disposition (not qualification)

The prior Windows session
`session-20260903T102650Z-50552cf165a8439397b71d7a1967f957` is rejected for
Human retest and does not qualify any candidate.  Its manifest SHA is
`70897d4ed041c92744be5ff8180f10ef94f3d84b7c3306a18e75aecf1ea99879`.
The raw coverage is 76 admitted records, 124 invalidations, 1,058 materialized
reads and zero failed reads.  The invalidation reasons are 64
`semantic_pre_frame_capture_failed`, 44 `native_task_binding_no_match`, 15
`native_task_binding_ambiguous` and one `pre_frame_capture_failed`.

The six accepted roots that started but never received a final disposition are
listed below by their stable action witness.  They are intentionally retained
as unresolved evidence rather than backfilled or transferred:

| sequence | root | native seam | bound label | final raw state |
| ---: | --- | --- | --- | --- |
| 202 | `ui-root-7c2927d5392f43b7b2428bc731de6b03` | `NRewardsScreen.OnProceedButtonPressed` | Continue from rewards | `action_started` only |
| 220 | `ui-root-4030144f9d984f30809715b6d1b9c495` | `NRewardsScreen.OnProceedButtonPressed` | Skip remaining rewards and continue | `action_started` only |
| 242 | `ui-root-b163490db86349d49ce7f48f5675bc56` | `NRewardsScreen.OnProceedButtonPressed` | Skip remaining rewards and continue | `action_started` only |
| 264 | `ui-root-2dde6c35482c467b98407435d415a68c` | `NRewardsScreen.OnProceedButtonPressed` | Continue from rewards | `action_started` only |
| 270 | `ui-root-074bf651491447fbafcc65efc318bf8a` | `NTreasureRoom.OnProceedButtonPressed` | Continue from the treasure room | `action_started` only |
| 298 | `ui-root-12283f649bd44f128e2361b8323d2ecb` | `NRewardsScreen.OnProceedButtonPressed` | Continue from rewards | `action_started` only |

The same trace contains one separately classified `transition_unknown` potion
root (sequence 86) and a final PlayerChoice parent (sequence 304) that ends at
`action_resumed`; neither is silently promoted to a terminal disposition.
Map travel in this exact raw session is represented by native
`VoteForMapCoordAction` roots and has no `membership_unknown` invalidation;
that task note is stale relative to this raw evidence.  The terminal tail was
only the polling journal entry `run_ended` followed by close; no native
`RunManager.OnEnded(bool)` marker was present.

Exact shipped decomp confirms the owning seams: event option completion is
`EventOption.Chosen()`, rest options return `RestSiteSynchronizer.ChooseLocalOption`
after `NRestSiteButton.OnRelease` disables options, reward/treasure continuation
uses `RunManager.ProceedFromTerminalRewardsScreen`, and victory/defeat converge
at `RunManager.OnEnded(bool)`.  The current repair therefore carries exact root
identity through the shared async callbacks, captures rest-site pre-frame at
the button boundary, aligns observations to the public `activate/open/cancel`
projection, and records terminal evidence only from `OnEnded`.  Polling remains
an explicitly unproved lifecycle note; it cannot publish `RunEnded` or settle a
root.  These changes are source/test evidence only until a fresh exact build,
cold load and new runtime session prove them.
