# PR25 v4 Human audit and popup precedence repair

G5 Connector/Annotator follow-up from PR head
`14f0db332da70a31336915cd5d93d5fa52f9e5ab`, targeting
`develop@cd9a0fbb0c85577a13513abe715f91d794ac86eb`.
Final source, BOM, CI and runtime receipts are separate exact-level evidence.
STPD remains unchanged at `67566db562b95f57468290b97659c9f2e3862fe1`.

## Exact tested candidate and audit

The closed session created at 2026-09-10T12:05:47Z used capture profile
`human-full-run-read-rich-v4`, Connector/Annotator source `7a255157e8b679c2b80bcbc39322aeec414471fb`,
DLL SHA256 `52bdbe7d456ae1b090a962fa6f35db1083259dab3504c766161960488bbff21f`,
MVID `6ce89069-f597-4fa9-9530-37146d04aceb`, runtime
`4c203de582f94a80a61d8701fc7516f3`. Raw evidence and input hashes remain local.
The owner's testing statement supplies Human attestation; machine checks alone
cannot establish Human origin or absence of all unseen input.

- 321 accepted decisions: 276 roots and 45 nested selectors.
- 311 proved and durable canonical: 266 roots and all 45 children; no proved-to-canonical gap.
- All 45 children have canonical parents and identical causal-root lineage.
  Families: 31 hand, 7 reward, 6 rest and 1 event selector. No pile or
  native-origin selector sample in this run.
- 10 unresolved: 9 `unrecorded_human_effect_before_successor`
  (6 EndTurn, 2 map, 1 PlayCard), 1 terminal EndTurn
  `session_closed_before_successor_boundary`.
- 45 explicit invalidations: 18 ReadyToBeginEnemyTurn and 16 MoveToMapCoord
  internal mismatch diagnostics, 10 PlayCard pre-frame failures, 1 potion
  discard pre-frame mapping failure. The 11 failed captures lose real Human
  decisions. The 34 internal diagnostics are not additional Human decisions.
- Official legacy audit: 108 valid, 0 invalid, 45 invalidations; 203 canonical
  entries omitted by that older projection. This is not 100% decision coverage.
- Native diagnostic: 228 accepted/successful, 211 exact memberships and
  17 unknown (16 map votes, 1 relic). Diagnostic exit 1 is retained, not hidden.
- Journal: one native fresh start, one native end with victory false. An earlier
  observed-in-progress marker did not suppress the true Launch witness.

The previous causal barrier works: failed captures invalidate pending predecessors
instead of silently assigning their effects to those predecessors. In particular,
map #141 is unresolved when BLOCK_POTION is discarded. The same-runtime log
contains two discards: BLOCK_POTION at the rest site failed capture; ATTACK_POTION
in combat is canonical #157. Targeted FIRE_POTION use is canonical #312.
There is no claim that all possible silent omissions have been excluded.

## First incorrect fact and owning repair

`PlayerEnvironment/Observation/SnapshotBuilder.cs` selected specialized native
surfaces, including NativeRestSite, before calling LiveObservationReader. This
bypassed the latter's potion-popup ownership precedence. The rest site therefore
published room actions while the actual Human callback belonged to the popup;
exact matching returned zero. Combat did not take that bypass and succeeded.
This is a confirmed Connector input-owner composition defect, affecting one
captured discard and its predecessor map transition in the audited session.

The specialized readers now run lazily inside the shared resolver after exact
identity/multiplayer checks. Modal ownership preempts both paths; the potion popup
preempts specialized room/selector adapters. Ambiguous popup or specialized-reader
failure fails closed without falling back to an underlying room. No new legality,
public verb, native operand, or research policy is added. Regression tests cover
popup-over-room, modal precedence, ambiguous-popup failure and absent-popup routing.

Annotator retains the failed staged card's eligibility reasons and exact native
readiness flags (phase, disabled, hand mode, active drag, peeking, turn membership)
for the later accepted-failure diagnostic. It no longer collapses failed staging
into an unexplained absent frame. A failed staged frame is never eligible for
admission. Semantic mapping failures also report actual interaction and catalog
size. These are diagnostic repairs, not a claim to fix the ten PlayCard failures.

## Remaining blockers and Human canary

1. Rapid PlayCard inputs: ten pre-frames were settling with zero actions;
   historical evidence does not identify the first false native readiness fact.
   Keep fail-closed; use the new exact-seam diagnostics before changing authority.
2. Final EndTurn: native run-end is recorded, but no complete terminal semantic
   successor is proved. Do not upgrade OnEnded or a later menu to S-prime.
3. Popup composition repair needs fresh Human validation on its own bytes.
4. Untested pile/native-origin paths and native map/relic diagnostic unknowns
   remain scoped non-claims; structural canonical is not research admission.

The next bounded Human canary should exercise: (1) discard at rest and combat,
(2) discard with an open reward/selector surface when the game permits it,
(3) fast consecutive cards and first card after enemy turn, (4) targeted potion
use plus nested selection, and (5) natural run end. Do not ask the Human to slow
down to conceal a capture gap. Leave Recorder ready; no agent gameplay.

Full-Run all-valid qualification is not reached. Source/test/build/install/load
and latest-head CI are recorded independently in the PR and local receipt.
Historical evidence is unchanged; no STPD edits, merge or mark-ready.
