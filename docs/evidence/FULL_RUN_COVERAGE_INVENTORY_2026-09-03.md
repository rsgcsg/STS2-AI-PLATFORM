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
