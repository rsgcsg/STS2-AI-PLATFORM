# PR25 Act execution audit and repair

G5 cross-layer Connector state completeness / Annotator execution evidence.
Base develop: `cd9a0fbb0c85577a13513abe715f91d794ac86eb`.
Audited PR head: `ba05e9ba8da4ef2cbe8500dc4e7a4afa6bc42b36`.
STPD remains external and unchanged (`67566db562b95f57468290b97659c9f2e3862fe1`).

## Exact historical evidence

Closed session `session-20260910T125725Z-795e7862240a4061838a872be4b2468e`
uses Annotator `759d39008431623a522a3b1e0b671c570d2176af`, Connector
`2272b499b94372e8d40a3450c78a37a77d629e96`, loaded artifact SHA256
`fecf56ad996a68ca7af675ef6babb103845ca6618eb2a385c6be38380f6e0d86`,
MVID `0d8456f6-d4e7-4455-b814-3395ae53cb0a`. The preserved runtime log agrees.
Raw session hashes and audit outputs are retained locally; no raw evidence is rewritten.

Official audit passes: 107 legacy valid / 0 invalid, 33 invalidations.
Semantic calibration: 356 accepted, 350 proved/canonical, one state/action-space
unresolved and five successor-unresolved. Trace has 356 distinct Started and
Finished, no duplicate lifecycle. Accepted kinds: 295 roots, 44 native selectors,
17 nested selectors. Canonical kinds: 290 roots, 44 native selectors, 16 nested
selectors. Every canonical nested selector has a canonical parent and matching
causal root; referenced state/action-space objects exist. Native-origin selectors
retain exact GenericHookGameAction / HookPlayerChoiceContext provenance without
inventing a Human parent. Three potion uses are canonical; no potion discard was
exercised. This run supplies hand selectors, not a new combat-pile canary.

All six unresolved:

| Count | Reason | Family / occurrence |
| --- | --- | --- |
| 1 | unrecorded_human_effect_before_successor | EndTurn #129 |
| 2 | unrecorded_human_effect_before_successor | PlayCard #248, #254 |
| 1 | semantic_state_incomplete_before_next_action | reward selector #189 |
| 1 | native_commit_not_observed | act-ready #190 (execution S also incomplete) |
| 1 | session_closed_before_successor_boundary | terminal EndTurn #356 |

All 33 invalidations: 15 ReadyToBeginEnemyTurnAction and 14 MoveToMapCoordAction
internal type-mismatch diagnostics; three PlayCard pre-frame capture failures;
one act-ready native synchronous binding no-match. The three failed cards retain
exact native operands and fence pending transitions. All three were phase Play,
InCardPlay=true, with a settling public input surface at both card-start and
submission. No complete H catalog is inferred from a nearby frame.

Native diagnostic: 244 successful, 229 exact, 15 unknown (14 map votes, one
PickRelicAction). This diagnostic remains failed, distinct from the canonical
projection. Journal: no fresh native start, one native resume, one native end,
one act owner-ready and one ActEntered. This is a resumed run, not a complete
new-run qualification.

## First incorrect facts and repair

1. Annotator reward Proceed registered the historical short kind
   `VoteToMoveToNextActAction.ExecuteAction`, whereas the native Commit callback
   supplies the provider's complete seam ending in `OnPlayerReady`. Exact root
   matching correctly refused it. Registration now uses the same owning constant;
   no matching rule is weakened.
2. Connector RewardClaimSurfaceReader made complete state conditional on enabled
   controls. Empty exact rewards plus a bound, disabled Proceed is a completely
   observable settling state. Completeness now follows exact owner/presentation
   binding; readiness and mutation revalidation stay separate. Missing bindings,
   linked rewards and absent player/button still fail closed.
3. The queued UI subscription correctly stopped reusing H as execution S, but
   dropped its typed native selection too. It now retains only process-local
   selection operands and semantic native type, then recaptures the native
   catalog synchronously at actual execution. Changed owners/operands fail exact
   membership. It never reuses the admission catalog or fabricates a GameAction.

## Validation and non-claims

Focused regressions cover the shared act seam, execution recapture wiring, exact
empty reward state and mismatched presentation. Existing ledger tests preserve
wrong-root/kind/owner fail-closed behavior. Root portable gates, exact-game build,
Host tests and final-head CI/runtime receipts are recorded with the candidate.
Lead reviews the source and evidence; no separate reviewer is claimed.

This repair does not make historical rows valid retroactively. Rapid-card H
capture, native terminal successor state, diagnostic map/relic membership and
unseen family coverage remain open. No all-valid Full Run, non-interference,
training eligibility or STPD admission is claimed. STPD must consume versioned
canonical decisions, retain root/parent lineage and native origin, and filter
unknown/failed occurrences; policy and dataset eligibility remain external.

Next bounded Human canary: cross an Act after selecting the last card reward;
exercise rapid card switching; discard potion during combat and at rest;
exercise Human-parent and native-origin selectors; finish a run. Record before
starting a fresh run when evaluating full-run boundaries. Do not require every
adjacent S-prime to equal S: exact asynchronous native effects and nested
continuations are explicit causes, not invented Human actions.

Rollback uses the owning Game Mod deployment snapshot for the previously loaded
artifact. Build/install/load do not promote this candidate to Human qualification.

## Late bounded owner canary and offline audit repair

During handoff the owner opened and closed a short session on the repaired native
candidate (`session-20260910T211207Z-245793054228456d96739fa5c542a3b9`).
It has six accepted, five canonical, one successor unresolved at session close,
one internal MoveToMapCoord diagnostic, one fresh native start and no native end.
It does not exercise an Act transition or establish all-valid Full Run.

All five canonical rows intentionally omitted incompatible legacy projection.
The audit nevertheless unconditionally required a legacy decision file. The
current auditor now permits its absence only after canonical/trace validation
and exact one-to-one journal accounting: every canonical ID must occur once in
both canonical-recorded and legacy-projection-omitted events. Missing promised
legacy files, missing omission events, empty canonical files and tampered payloads
remain fail-closed. The regression covers both ordinary legacy and canonical-only
sessions, missing files/omission journals and action-space tampering.

The game may remain in the owner's live combat after recording closes. Do not
terminate that run merely to promote the later offline-auditor source identity;
report source/test/build and actual loaded identity separately.
